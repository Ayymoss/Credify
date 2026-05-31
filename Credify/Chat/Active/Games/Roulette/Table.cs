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
    : BaseContinuousGame<Player>(persistenceService, config, communication)
{
    private RouletteGameState _gameState = RouletteGameState.WaitingForPlayers;
    private List<Player> _roundPlayers = [];
    private readonly StakeValidator _stakeValidator = new(persistenceService, 10);
    private CancellationTokenSource? _bettingTimeoutToken;

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

        // Phase 1: Collect bets via chat
        await CollectBetsAsync(token);

        // Remove players who didn't complete betting
        RemoveIncompleteBets();

        if (_roundPlayers.Count == 0)
        {
            _gameState = RouletteGameState.WaitingForPlayers;
            return;
        }

        // Phase 2: Spin wheel
        _gameState = RouletteGameState.SpinningWheel;
        await SpinWheelMessage(token);
        var spinResult = SpinWheel();

        // Phase 3: Resolve bets
        _gameState = RouletteGameState.ResolvingBets;
        await HandleResult(spinResult);

        // Cleanup
        foreach (var player in _roundPlayers)
        {
            player.ClearBet();
            ICredifyEventService.RaiseEvent(ObjectiveType.Roulette, player.Client);
        }

        await RemoveBrokePlayers();
        _gameState = RouletteGameState.WaitingForPlayers;
    }

    private async Task CollectBetsAsync(CancellationToken token)
    {
        _gameState = RouletteGameState.CollectingBets;

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
    private async Task<bool> TryPlaceBetAsync(Player player, int stake, string betInput)
    {
        BaseBet? bet = null;

        var outside = new RouletteOutsideBetParser(stake, translations.Roulette).Parse(betInput);
        if (outside is { IsValid: true, Result: not null })
        {
            bet = outside.Result;
        }
        else
        {
            var inside = new RouletteInsideBetParser(stake, translations.Roulette).Parse(betInput);
            if (inside is { IsValid: true, Result: not null }) bet = inside.Result;
        }

        if (bet is null) return false;

        player.CreateBet(bet);
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
        player.CreateBet(bet);
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
            if (player.Bet is null) continue;

            if (!player.Bet.HasWon(spinResult))
            {
                output.Tell(player, translations.Roulette.Lost.FormatExt(player.Bet.Stake.ToString("N0")));
                continue;
            }

            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Client, player.Bet.Payout);
            output.Tell(player, translations.Roulette.Won.FormatExt((player.Bet.Payout - player.Bet.Stake).ToString("N0")));
            await PersistenceService.AddCreditsAsync(player.Client, player.Bet.Payout);

            if (!Config.Roulette.AnnounceMaxPayoutWinners) continue;
            if (player.Bet is not StraightUpBet straightUpBet) continue;

            await output.BroadcastToAllServersAsync(player,
            [
                translations.Roulette.LongPrefix(translations.Roulette.HouseWin.FormatExt(player.Client.CleanedName,
                    (player.Bet.Payout - player.Bet.Stake).ToString("N0"), straightUpBet.Number))
            ]);
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
    }

    public bool IsPlayerInGame(EFClient client) => IsPlayerPlaying(client);

    public override Task LeaveGameAsync(EFClient player)
    {
        PlayerLeave(player);
        OnPlayerLeft();
        return Task.CompletedTask;
    }

    #endregion
}
