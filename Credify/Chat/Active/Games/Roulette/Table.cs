using System.Collections.Concurrent;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Chat.Active.Games.Roulette.Models;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;
using Credify.Chat.Active.Games.Roulette.Utilities;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Games.Live;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Roulette;

/// <summary>
/// Roulette table using HandleChatAsync pattern for input collection.
/// Players bet via chat messages during the betting phase.
/// </summary>
public class Table(
    CredifyConfiguration config,
    TranslationsRoot translations,
    PersistenceService persistenceService,
    GamePlayerCommunication communication,
    IGameOutputHandler<Player> output)
    : BaseContinuousGame<Player>(persistenceService, config, communication), IWebObservableGame<RouletteSnapshot>
{
    private RouletteGameState _gameState = RouletteGameState.WaitingForPlayers;
    private List<Player> _roundPlayers = [];
    private readonly StakeValidator _stakeValidator = new(persistenceService, 10);
    private CancellationTokenSource? _bettingTimeoutToken;

    // ── webfront observation state ──
    private DateTimeOffset? _bettingEndsAt;
    private SpinResult? _lastSpin;
    private readonly List<SpinResult> _history = [];
    private const int HistoryLength = 14;

    /// <inheritdoc />
    public event Action? StateChanged;

    private void RaiseStateChanged() => StateChanged?.Invoke();

    // ── debug helper (see CredifyDebugLog) ──
    private string RoundNames() => string.Join(", ", _roundPlayers.Select(p =>
        $"{p.Client.CleanedName}[{p.InputState} bets={p.Bets.Count}]"));

    protected override int GetMinimumPlayers() => 1;
    protected override TimeSpan GetDelayBetweenRounds() => TimeSpan.Zero;

    #region Game Loop

    protected override async Task ExecuteGameRoundAsync(CancellationToken token)
    {
        // Snapshot current players for this round
        _roundPlayers = Players.Values.ToList();
        foreach (var player in _roundPlayers)
        {
            player.ResetForNewRound();
        }

        CredifyDebugLog.Log("Roulette", $"ROUND start | players({_roundPlayers.Count})=[{RoundNames()}]");
        RaiseStateChanged();

        // Phase 1: Collect bets via chat
        await CollectBetsAsync(token);

        // Remove players who didn't complete betting
        RemoveIncompleteBets();
        CredifyDebugLog.Log("Roulette", $"BETTING closed | remaining=[{RoundNames()}]");

        if (_roundPlayers.Count == 0)
        {
            CredifyDebugLog.Log("Roulette", "ROUND aborted (no players completed betting) -> WaitingForPlayers");
            _gameState = RouletteGameState.WaitingForPlayers;
            RaiseStateChanged();
            return;
        }

        // Phase 2: Spin wheel
        _gameState = RouletteGameState.SpinningWheel;
        _bettingEndsAt = null;
        RaiseStateChanged();
        await SpinWheelMessage(token);
        var spinResult = SpinWheel();
        CredifyDebugLog.Log("Roulette", $"SPIN -> {RouletteConstants.ToDisplayString(spinResult.Number)} {spinResult.Colour}{(spinResult.IsEven ? " even" : "")}");

        // record the result for the web (history strip + landed number) before resolving
        _lastSpin = spinResult;
        _history.Insert(0, spinResult);
        if (_history.Count > HistoryLength)
        {
            _history.RemoveRange(HistoryLength, _history.Count - HistoryLength);
        }

        // Phase 3: Resolve bets
        _gameState = RouletteGameState.ResolvingBets;
        RaiseStateChanged();
        await HandleResult(spinResult);
        RaiseStateChanged();

        // Cleanup
        foreach (var player in _roundPlayers)
        {
            player.ClearBets();
            ICredifyEventService.RaiseEvent(ObjectiveType.Roulette, player.Client);
        }

        await RemoveBrokePlayers();
        CredifyDebugLog.Log("Roulette", $"ROUND end -> WaitingForPlayers | seats({Players.Count})=[{string.Join(", ", Players.Values.Select(p => p.Client.CleanedName))}]");
        _gameState = RouletteGameState.WaitingForPlayers;
        RaiseStateChanged();
    }

    private async Task CollectBetsAsync(CancellationToken token)
    {
        _gameState = RouletteGameState.CollectingBets;
        CredifyDebugLog.Log("Roulette", $"STATE -> CollectingBets | window={(Config.Roulette.TimeoutForPlayerAction * 3).TotalSeconds:0}s players=[{RoundNames()}]");

        // Prompt all players for their bet (single-line syntax taught up front).
        foreach (var player in _roundPlayers)
        {
            var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
            await output.TellPlayerAsync(player,
            [
                translations.Roulette.HowMuchToBet.FormatExt(credits.ToString("N0")),
                translations.Roulette.BetSyntaxHint,
                translations.Roulette.BetSyntaxExtras
            ]);
        }

        // Wait for all players to complete betting or timeout
        _bettingTimeoutToken?.Dispose();
        _bettingTimeoutToken = new CancellationTokenSource();

        var bettingWindow = Config.Roulette.TimeoutForPlayerAction * 3; // 3x timeout for full bet flow
        _bettingEndsAt = DateTimeOffset.UtcNow + bettingWindow;
        RaiseStateChanged();

        // Warn still-betting players ~10s before the window closes (no visible clock in chat).
        var warnAfter = bettingWindow - TimeSpan.FromSeconds(10);
        if (warnAfter > TimeSpan.Zero)
        {
            SharedLibraryCore.Utilities.ExecuteAfterDelay(warnAfter, async warnToken =>
            {
                if (warnToken.IsCancellationRequested) return;
                var pending = _roundPlayers
                    .Where(p => p.InputState is not (PlayerInputState.Complete or PlayerInputState.TimedOut))
                    .ToList();
                if (pending.Count > 0) await output.TellPlayersAsync(pending, [translations.Roulette.TimeWarning]);
            }, _bettingTimeoutToken.Token);
        }

        try
        {
            using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(token, _bettingTimeoutToken.Token);
            await Task.Delay(bettingWindow, linkedToken.Token);
        }
        catch (OperationCanceledException)
        {
            // Expected when all players complete or game is cancelled
        }
    }

    #endregion

    #region Chat Input Handling

    /// <summary>
    /// Handles chat messages from players during betting phases.
    /// </summary>
    public override async Task HandleChatAsync(EFClient client, string message)
    {
        if (!Players.TryGetValue(client, out var player)) return;
        if (player.InputState == PlayerInputState.Complete || 
            player.InputState == PlayerInputState.TimedOut) return;

        // Only accept input during betting phases
        if (_gameState != RouletteGameState.CollectingBets &&
            _gameState != RouletteGameState.AwaitingBetCategory &&
            _gameState != RouletteGameState.AwaitingBetDetails)
        {
            return;
        }

        await ExecuteUnderChatLockAsync(async () =>
        {
            switch (player.InputState)
            {
                case PlayerInputState.WaitingForStake:
                    await HandleStakeInputAsync(player, message);
                    break;
                case PlayerInputState.WaitingForCategory:
                    await HandleCategoryInputAsync(player, message);
                    break;
                case PlayerInputState.WaitingForDetails:
                    await HandleDetailsInputAsync(player, message);
                    break;
            }

            // Check if all players completed
            CheckAllPlayersCompleted();
        });

        RaiseStateChanged();
    }

    private async Task HandleStakeInputAsync(Player player, string message)
    {
        var trimmed = message.Trim();

        // Show the bet legend on demand (so the up-front prompt can stay short).
        if (trimmed.Equals("bets", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("help", StringComparison.OrdinalIgnoreCase) || trimmed == "?")
        {
            await output.TellPlayerAsync(player,
            [
                translations.Roulette.BetLegendOutside,
                translations.Roulette.BetLegendDozensColumns,
                translations.Roulette.BetLegendInside,
                translations.Roulette.BetLegendFormat
            ]);
            return;
        }

        // Repeat the previous bet verbatim.
        if (trimmed.Equals("same", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("again", StringComparison.OrdinalIgnoreCase))
        {
            if (player.LastBetInput is null)
            {
                await output.TellPlayerAsync(player, [translations.Roulette.NoPreviousBet]);
                return;
            }
            trimmed = player.LastBetInput;
        }

        var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
        var tokens = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var stakeParser = new RouletteStakeParser(_stakeValidator, credits, translations.Roulette, Config);
        var stakeResult = stakeParser.Parse(tokens[0]);
        if (!stakeResult.IsValid)
        {
            await output.TellPlayerAsync(player, [stakeResult.ErrorMessage ?? translations.Roulette.InvalidBetInput]);
            return;
        }

        // Stake-only -> fall back to the guided category/details flow.
        if (tokens.Length == 1)
        {
            player.PendingStake = stakeResult.Result;
            player.InputState = PlayerInputState.WaitingForCategory;
            _gameState = RouletteGameState.AwaitingBetCategory;

            await output.TellPlayerAsync(player, [
                translations.Roulette.InnerOrOutsideBet,
                translations.Roulette.InnerOrOutsideBetAcceptableInputs
            ]);
            return;
        }

        // Single-line bet: "<stake> <bet...>".
        var stake = (int)stakeResult.Result;
        var betInput = string.Join(' ', tokens.Skip(1));
        if (await TryPlaceBetAsync(player, stake, betInput))
        {
            player.LastBetInput = $"{stake} {betInput}";
            return;
        }

        await output.TellPlayerAsync(player,
            [translations.Roulette.InvalidBetType, translations.Roulette.InvalidBetHint]);
    }

    /// <summary>
    /// Attempts to build and place a bet from a raw bet token. Tries the outside-bet
    /// keywords first (single word), then inside-bet numbers (1-6). Returns false if
    /// neither parser accepts the input (caller reports the error).
    /// </summary>
    /// <summary>Parse a raw bet token into a bet without any side effects (outside keywords, then inside numbers).</summary>
    private BaseBet? ParseBet(int stake, string betInput)
    {
        var outside = new RouletteOutsideBetParser(stake, translations.Roulette).Parse(betInput);
        if (outside is { IsValid: true, Result: not null })
        {
            return outside.Result;
        }

        var inside = new RouletteInsideBetParser(stake, translations.Roulette).Parse(betInput);
        return inside is { IsValid: true, Result: not null } ? inside.Result : null;
    }

    private async Task<bool> TryPlaceBetAsync(Player player, int stake, string betInput)
    {
        var bet = ParseBet(stake, betInput);
        if (bet is null) return false;

        player.AddBet(bet, betInput.Trim());
        player.LastResult = "Pending";
        await PersistenceService.RemoveCreditsAsync(player.Client, bet.Stake);
        player.InputState = PlayerInputState.Complete;
        await output.TellPlayerAsync(player, [translations.Roulette.BetAccepted]);
        return true;
    }

    private async Task HandleCategoryInputAsync(Player player, string message)
    {
        var parser = new RouletteBetCategoryParser(translations.Roulette);
        var result = parser.Parse(message);

        if (!result.IsValid)
        {
            await output.TellPlayerAsync(player, [result.ErrorMessage ?? translations.Roulette.InvalidBetCategory]);
            return;
        }

        player.SelectedCategory = result.Result;
        player.InputState = PlayerInputState.WaitingForDetails;
        _gameState = RouletteGameState.AwaitingBetDetails;

        // Prompt for bet details based on category
        if (result.Result == BetCategory.Inside)
        {
            await output.TellPlayerAsync(player, [
                translations.Roulette.InsideBetSelected,
                translations.Roulette.InsidePickNumbers,
                translations.Roulette.InsideBetOptions
            ]);
        }
        else
        {
            await output.TellPlayerAsync(player, [
                translations.Roulette.OutsideBetSelected,
                translations.Roulette.OutsideSelectBet,
                translations.Roulette.OutsideBetOptions
            ]);
        }
    }

    private async Task HandleDetailsInputAsync(Player player, string message)
    {
        if (player.PendingStake is null || player.SelectedCategory is null) return;

        BaseBet? bet = null;
        var stake = (int)player.PendingStake.Value;

        if (player.SelectedCategory == BetCategory.Inside)
        {
            var parser = new RouletteInsideBetParser(stake, translations.Roulette);
            var result = parser.Parse(message);
            if (!result.IsValid)
            {
                await output.TellPlayerAsync(player, [result.ErrorMessage ?? translations.Roulette.InvalidBetType]);
                return;
            }
            bet = result.Result;
        }
        else
        {
            var parser = new RouletteOutsideBetParser(stake, translations.Roulette);
            var result = parser.Parse(message);
            if (!result.IsValid)
            {
                await output.TellPlayerAsync(player, [result.ErrorMessage ?? translations.Roulette.InvalidBetType]);
                return;
            }
            bet = result.Result;
        }

        if (bet is null) return;

        // Finalize bet
        player.AddBet(bet, message.Trim());
        player.LastResult = "Pending";
        await PersistenceService.RemoveCreditsAsync(player.Client, bet.Stake);
        player.InputState = PlayerInputState.Complete;
        player.LastBetInput = $"{stake} {message.Trim()}"; // enables "same" next round

        await output.TellPlayerAsync(player, [translations.Roulette.BetAccepted]);
    }

    private void CheckAllPlayersCompleted()
    {
        var allComplete = _roundPlayers.All(p => 
            p.InputState == PlayerInputState.Complete || 
            p.InputState == PlayerInputState.TimedOut);

        if (allComplete)
        {
            _bettingTimeoutToken?.Cancel();
        }
    }

    #endregion

    #region Game Logic

    private void RemoveIncompleteBets()
    {
        var incomplete = _roundPlayers.Where(p => p.InputState != PlayerInputState.Complete).ToList();

        foreach (var player in incomplete)
        {
            output.Tell(player, translations.Roulette.BetTimeout);
            PlayerLeave(player.Client);
        }

        _roundPlayers = _roundPlayers.Where(p => p.InputState == PlayerInputState.Complete).ToList();
    }

    private async Task RemoveBrokePlayers()
    {
        List<Player> playersToRemove = [];
        foreach (var player in _roundPlayers)
        {
            var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
            if (credits >= 10) continue;
            output.Tell(player, translations.Roulette.Broke);
            playersToRemove.Add(player);
        }

        foreach (var player in playersToRemove)
        {
            PlayerLeave(player.Client);
        }
    }

    private async Task SpinWheelMessage(CancellationToken token)
    {
        var message = translations.Roulette.SpinningWheel;
        await output.TellPlayersAsync(_roundPlayers, [$"{message}..."]);
        await Task.Delay(1_000, token);
        await output.TellPlayersAsync(_roundPlayers, [$"{message}.."]);
        await Task.Delay(1_500, token);
        await output.TellPlayersAsync(_roundPlayers, [$"{message}."]);
        await Task.Delay(2_000, token);
    }

    private async Task HandleResult(SpinResult spinResult)
    {
        foreach (var player in _roundPlayers)
        {
            if (!player.HasBet) continue;

            long totalStaked = 0;
            long totalWinnings = 0; // gross payout from the winning bets (stake was already debited on placement)

            foreach (var placed in player.Bets)
            {
                var bet = placed.Bet;
                totalStaked += bet.Stake;

                if (!bet.HasWon(spinResult))
                {
                    output.Tell(player, translations.Roulette.Lost.FormatExt(bet.Stake.ToString("N0")));
                    continue;
                }

                totalWinnings += bet.Payout;
                output.Tell(player, translations.Roulette.Won.FormatExt((bet.Payout - bet.Stake).ToString("N0")));

                if (Config.Roulette.AnnounceMaxPayoutWinners && bet is StraightUpBet straightUpBet)
                {
                    await output.BroadcastToAllServersAsync(player,
                    [
                        translations.Roulette.LongPrefix(translations.Roulette.HouseWin.FormatExt(player.Client.CleanedName,
                            (bet.Payout - bet.Stake).ToString("N0"), straightUpBet.Number))
                    ]);
                }
            }

            // round net = winnings credited back minus everything staked; the web "Won" signal (and its coin
            // sound) keys off a genuine positive net, so winning one bet but netting a loss never reads as a win.
            player.LastNet = totalWinnings - totalStaked;
            player.LastResult = player.LastNet > 0 ? "Won" : "Lost";
            CredifyDebugLog.Log("Roulette",
                $"RESULT {player.Client.CleanedName}: {player.LastResult} staked={totalStaked:N0} winnings={totalWinnings:N0} net={player.LastNet:N0} (bets={player.Bets.Count})");

            if (totalWinnings > 0)
            {
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Client, totalWinnings);
                await PersistenceService.AddCreditsAsync(player.Client, totalWinnings);
            }
        }

        var colourString = spinResult.Colour switch
        {
            Colour.Black => "(Color::White)",
            Colour.Red => "(Color::Red)",
            Colour.Green => "(Color::Green)",
            _ => throw new ArgumentOutOfRangeException(nameof(spinResult))
        };

        var ballColour = $"{colourString}{spinResult.Colour}(Color::White)";

        await output.TellPlayersAsync(_roundPlayers, [
            translations.Roulette.BallStopped.FormatExt(RouletteConstants.ToDisplayString(spinResult.Number), ballColour)
        ]);
    }

    private static SpinResult SpinWheel()
    {
        // American Roulette: 0, 00 (37), 1-36 = 38 outcomes
        var number = Random.Shared.Next(0, RouletteConstants.TotalOutcomes);
        var isEven = !RouletteConstants.IsZero(number) && number % 2 == 0;
        var color = RouletteConstants.GetColor(number);

        return new SpinResult(number, color, isEven);
    }

    #endregion

    #region Player Management

    public async Task<bool> PlayerJoinAsync(Player player)
    {
        var added = Players.TryAdd(player.Client, player);
        if (!HasPlayers.IsSet)
        {
            await output.BroadcastToServerAsync(player,
                [translations.Roulette.LongPrefix(translations.Roulette.PlayerStartedRoulette.FormatExt(player.Client.CleanedName))]);
            SignalPlayersAvailable();
            return added;
        }

        output.Tell(player, translations.Roulette.JoinDuringActiveMessage, true);
        return added;
    }

    public override async Task JoinGameAsync(EFClient player)
    {
        await PlayerJoinAsync(new Player(player));
        OnPlayerJoined();
        RaiseStateChanged();
    }

    public void PlayerLeave(EFClient client)
    {
        if (Players.TryGetValue(client, out var player))
        {
            output.Tell(player, translations.Roulette.LeaveMessage, true);
        }
        RemovePlayer(client);

        lock (_roundPlayers) _roundPlayers.RemoveAll(p => Equals(p.Client, client));

        ResetPlayersSignal();
        RaiseStateChanged();
    }

    public bool IsPlayerInGame(EFClient client) => IsPlayerPlaying(client);

    public override Task LeaveGameAsync(EFClient player)
    {
        PlayerLeave(player);
        OnPlayerLeft();
        return Task.CompletedTask;
    }

    #endregion

    #region Web Frontend

    /// <inheritdoc />
    public RouletteSnapshot GetSnapshot()
    {
        var phase = _gameState switch
        {
            RouletteGameState.WaitingForPlayers => "WaitingForPlayers",
            RouletteGameState.SpinningWheel => "Spinning",
            RouletteGameState.ResolvingBets => "Resolving",
            _ => "Betting"
        };

        var secondsRemaining = phase == "Betting" && _bettingEndsAt is { } endsAt
            ? Math.Max(0, (endsAt - DateTimeOffset.UtcNow).TotalSeconds)
            : 0;

        var players = Players.Values
            .Select(p => new RoulettePlayerView(
                p.Client.ClientId,
                p.Client.CleanedName,
                p.Bets.Select(placed => new RouletteBetView(placed.Label, placed.Bet.Stake)).ToList(),
                p.TotalStake,
                p.HasBet,
                p.LastResult,
                p.LastNet))
            .ToList();

        return new RouletteSnapshot
        {
            Phase = phase,
            SecondsRemaining = secondsRemaining,
            Players = players,
            LastSpin = _lastSpin is null ? null : ToSpinView(_lastSpin),
            History = _history.Select(ToSpinView).ToList()
        };
    }

    private static RouletteSpinView ToSpinView(SpinResult spin) =>
        new(spin.Number, RouletteConstants.ToDisplayString(spin.Number), spin.Colour.ToString());

    /// <summary>
    /// Places a batch of bets on behalf of a web participant in one shot (the web builds a selection of
    /// board bets, then commits them together). Runs under the same chat lock as in-game input, so web and
    /// chat bets are serialized. The whole batch is validated (every token parses, total stake affordable)
    /// before anything is debited, so it never partially commits. Returns null on success or an error string.
    /// </summary>
    public async Task<string?> PlaceWebBetsAsync(EFClient client, IReadOnlyList<(int Stake, string BetInput)> bets)
    {
        string? error = null;

        await ExecuteUnderChatLockAsync(async () =>
        {
            if (bets.Count == 0)
            {
                error = "Place at least one bet.";
                return;
            }

            if (!Players.TryGetValue(client, out var player))
            {
                error = "You're not seated at the table.";
                return;
            }

            // only players locked into the current round may bet; mid-round joiners wait for the next one
            bool inRound;
            lock (_roundPlayers) inRound = _roundPlayers.Contains(player);
            if (!inRound)
            {
                error = "You're in for the next round — hang tight.";
                return;
            }

            if (_gameState is not (RouletteGameState.CollectingBets or RouletteGameState.AwaitingBetCategory
                or RouletteGameState.AwaitingBetDetails))
            {
                error = "Betting is closed for this round.";
                return;
            }

            if (player.InputState is PlayerInputState.Complete)
            {
                error = "You've already placed your bets this round.";
                return;
            }

            if (bets.Any(bet => bet.Stake < 10))
            {
                error = "Minimum bet is 10 credits.";
                return;
            }

            // validate every token parses BEFORE debiting anything (no partial commits)
            if (bets.Any(bet => ParseBet(bet.Stake, bet.BetInput) is null))
            {
                error = "One of those isn't a valid bet.";
                return;
            }

            var total = bets.Sum(bet => (long)bet.Stake);
            var credits = await PersistenceService.GetClientCreditsAsync(client);
            if (total > credits)
            {
                error = "You don't have enough credits for those bets.";
                return;
            }

            foreach (var (stake, betInput) in bets)
            {
                CredifyDebugLog.Log("Roulette", $"BET {player.Client.CleanedName}: '{betInput}' stake={stake:N0}");
                await TryPlaceBetAsync(player, stake, betInput);
            }

            CheckAllPlayersCompleted();
        });

        CredifyDebugLog.Log("Roulette", error is null
            ? $"WEBBETS {client.CleanedName}: accepted {bets.Count} bet(s), total={bets.Sum(b => (long)b.Stake):N0}"
            : $"WEBBETS {client.CleanedName}: REJECTED -> {error}");
        RaiseStateChanged();
        return error;
    }

    #endregion
}
