using System.Collections.Concurrent;
using System.Text;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Blackjack.Enums;
using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Chat.Active.Games.Blackjack.Services;
using Credify.Chat.Active.Games.Blackjack.Utilities;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Games.Blackjack;
using Credify.Games.Cards;
using Credify.Games.Live;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// Main Blackjack game class managing game flow, player actions, and payouts.
/// Uses Core abstractions for input/output handling and stake validation.
/// </summary>
public class BlackjackGame : BaseActiveGame<BlackjackPlayer>, IWebObservableGame<BlackjackSnapshot>
{
    private readonly GameStateMachine<GameState> _stateMachine;

    /// <inheritdoc />
    public event Action? StateChanged;

    private void RaiseStateChanged() => StateChanged?.Invoke();

    // when the current timed player window (betting / insurance / decisions) closes; null otherwise.
    private DateTimeOffset? _phaseEndsAt;

    /// <summary>
    /// Gets the current game state.
    /// </summary>
    protected GameState GameState => _stateMachine.CurrentState;

    /// <summary>
    /// Transitions to a new game state.
    /// </summary>
    protected void TransitionToState(GameState newState)
    {
        var previous = _stateMachine.CurrentState;
        _stateMachine.TransitionTo(newState);
        CredifyDebugLog.Log("Blackjack",
            $"STATE {previous} -> {newState} | dealer=[{string.Join(" ", _houseHand.Select(c => c.ToChatString()))}] seats=[{RosterStates()}]");
        RaiseStateChanged();
    }

    // ── debug helper (see CredifyDebugLog) ──
    private string RosterStates() => string.Join(", ", Players.Values.Select(p =>
        $"{p.Client.CleanedName}[{p.State} stake={(p.Stake?.ToString("N0") ?? "-")}" +
        $"{(p.SittingOut ? " sittingOut" : "")}{(p.Queued ? " queued" : "")}]"));

    /// <summary>
    /// Checks if currently in the specified state.
    /// </summary>
    protected bool IsInState(GameState state) => _stateMachine.IsInState(state);

    /// <summary>
    /// Checks if currently in any of the specified states.
    /// </summary>
    protected bool IsInAnyState(params GameState[] states) => _stateMachine.IsInAnyState(states);
    private List<Card> _houseHand = [];
    private readonly BlackjackDeckService _deckService;
    private readonly BlackjackPayoutCalculator _payoutCalculator;
    private readonly IGameInputParser<BlackjackActionResult> _inputHandler;
    private readonly BlackjackHandleInput _inputHandlerConcrete; // For FormatAvailableActions convenience methods
    private readonly IGameOutputHandler<BlackjackPlayer> _outputHandler;
    private readonly BlackjackHandleOutput _outputHandlerConcrete; // For TellClientAsync convenience method
    private readonly StakeValidator _stakeValidator;
    private CancellationTokenSource? _playerStakesToken;
    private CancellationTokenSource? _dealerPlaysToken;
    private CancellationTokenSource? _insuranceToken;
    private readonly SemaphoreSlim _startGameLock = new(1, 1);

    public BlackjackGame(
        PersistenceService persistenceService,
        CredifyConfiguration config,
        GamePlayerCommunication communication,
        BlackjackHandleInput inputHandler,
        BlackjackHandleOutput outputHandler)
        : base(persistenceService, config, communication)
    {
        _stateMachine = new BlackjackStateMachine();
        _deckService = new BlackjackDeckService();
        _payoutCalculator = new BlackjackPayoutCalculator(config.Blackjack);
        _inputHandler = inputHandler;
        _inputHandlerConcrete = inputHandler;
        _outputHandler = outputHandler;
        _outputHandlerConcrete = outputHandler;
        _stakeValidator = new StakeValidator(persistenceService);
        _deckService.InitializeDeck();
    }

    #region Core Game Flow

    private async Task StartGameAsync()
    {
        if (!IsInState(GameState.WaitingForPlayers)) return;
        if (Players.IsEmpty) return;

        TransitionToState(GameState.SettingUpGame);
        _houseHand =
        [
            _deckService.DrawCardOrReshuffle(),
            _deckService.DrawCardOrReshuffle()
        ];

        // Mark all existing players as active (not queued)
        foreach (var player in Players.Values) player.Queued = false;

        SharedLibraryCore.Utilities.ExecuteAfterDelay(TimeSpan.FromSeconds(GameConstants.Timeouts.DefaultGameStartDelay),
            RequestPlayerStakesAsync, CancellationToken.None);
    }

    private async Task RequestPlayerStakesAsync(CancellationToken token)
    {
        TransitionToState(GameState.RequestPlayerStakes);

        var insufficientFunds = new List<EFClient>();
        foreach (var (client, player) in Players)
        {
            if (player.Queued) continue;
            if (player.SittingOut) continue; // keep their seat, skip the prompt

            player.State = PlayerState.Playing;
            var playerFunds = await PersistenceService.GetClientCreditsAsync(client);
            if (playerFunds < GameConstants.MinimumCredits)
            {
                insufficientFunds.Add(client);
                continue;
            }

            await _outputHandler.TellPlayerAsync(player,
            [
                Config.Translations.Blackjack.PlaceBets.FormatExt(playerFunds.ToString("N0")),
                Config.Translations.Blackjack.BetHint
            ], true);
        }

        foreach (var client in insufficientFunds)
        {
            Players.TryRemove(client, out _);
            await _outputHandlerConcrete.TellClientAsync(client, [Config.Translations.Blackjack.InsufficientFunds], true);
        }

        // Need at least one player who is actually playing this round (not queued, not sitting out).
        if (!Players.Any(p => p.Value is { Queued: false, SittingOut: false }))
        {
            await EndGameAsync(CancellationToken.None);
            return;
        }

        _playerStakesToken?.Dispose();
        _playerStakesToken = new CancellationTokenSource();
        _phaseEndsAt = DateTimeOffset.UtcNow + Config.Blackjack.TimeoutForPlayerAction;
        RaiseStateChanged();
        SharedLibraryCore.Utilities.ExecuteAfterDelay(Config.Blackjack.TimeoutForPlayerAction, DealCardsAsync,
            _playerStakesToken.Token);
        ScheduleTimeWarning(_playerStakesToken.Token, () => Players
            .Where(p => p.Value is { Queued: false, SittingOut: false, Stake: null })
            .Select(p => p.Value));
    }

    private async Task DealCardsAsync(CancellationToken token)
    {
        if (!IsInState(GameState.RequestPlayerStakes)) return;
        TransitionToState(GameState.DealCards);

        var noBets = Players
            .Where(x => x.Value is { Queued: false, SittingOut: false, Stake: null })
            .Select(x => x.Key)
            .ToList();

        foreach (var client in noBets)
        {
            Players.TryRemove(client, out _);
            await _outputHandlerConcrete.TellClientAsync(client, [Config.Translations.Blackjack.BetTimeout], true);
        }

        if (!ActivePlayers.Any())
        {
            await EndGameAsync(CancellationToken.None);
            return;
        }

        foreach (var (client, player) in ActivePlayers)
        {
            player.Cards =
            [
                _deckService.DrawCardOrReshuffle(),
                _deckService.DrawCardOrReshuffle()
            ];

            await _outputHandler.TellPlayerAsync(player, [BuildHandSummary(player)]);
        }

        _playerStakesToken?.Cancel();
        _playerStakesToken?.Dispose();
        _playerStakesToken = null;
        
        // Check if dealer shows Ace - offer insurance
        if (_houseHand[0].Rank == Rank.Ace)
        {
            await OfferInsuranceAsync();
        }
        else
        {
            await RequestPlayerDecisionsAsync();
        }
    }

    private async Task OfferInsuranceAsync()
    {
        TransitionToState(GameState.OfferingInsurance);
        
        // Track which players can take insurance (have enough funds)
        var eligibleCount = 0;
        foreach (var (client, player) in ActivePlayers)
        {
            var insuranceCost = player.Stake!.Value / 2;
            var playerFunds = await PersistenceService.GetClientCreditsAsync(client);
            
            if (playerFunds >= insuranceCost)
            {
                eligibleCount++;
                await _outputHandler.TellPlayerAsync(player,
                    [Config.Translations.Blackjack.InsuranceOffer.FormatExt(insuranceCost.ToString("N0"))]);
            }
            else
            {
                // Player can't afford insurance, mark as declined
                player.HasInsurance = false;
            }
        }
        
        // If no one can afford insurance, skip directly to decisions
        if (eligibleCount == 0)
        {
            await RequestPlayerDecisionsAsync();
            return;
        }
        
        // Use player action timeout for insurance window (not the tiny 2-second delay)
        _insuranceToken?.Dispose();
        _insuranceToken = new CancellationTokenSource();
        _phaseEndsAt = DateTimeOffset.UtcNow + Config.Blackjack.TimeoutForPlayerAction;
        RaiseStateChanged();
        SharedLibraryCore.Utilities.ExecuteAfterDelay(
            Config.Blackjack.TimeoutForPlayerAction,
            async (token) =>
            {
                if (!token.IsCancellationRequested && IsInState(GameState.OfferingInsurance))
                {
                    await RequestPlayerDecisionsAsync();
                }
            },
            _insuranceToken.Token);
    }

    private async Task RequestPlayerDecisionsAsync()
    {
        TransitionToState(GameState.RequestPlayerDecisions);
        
        // Check insurance results if dealer has blackjack
        var dealerHasBlackjack = BlackjackPayoutCalculator.IsBlackjack(_houseHand);
        foreach (var (client, player) in ActivePlayers)
        {
            if (player.HasInsurance)
            {
                if (dealerHasBlackjack)
                {
                    var insurancePayout = Convert.ToInt64(player.InsuranceBet * Config.Blackjack.PayoutInsurance);
                    await PersistenceService.AddCreditsAsync(client, insurancePayout);
                    await _outputHandler.TellPlayerAsync(player,
                        [Config.Translations.Blackjack.InsuranceWin.FormatExt(insurancePayout.ToString("N0"))]);
                }
                else
                {
                    await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.InsuranceLose]);
                }
            }
        }

        // Insurance is never valid during decisions phase (window has passed)
        // Always pass false for dealerShowsAce here
        foreach (var (client, player) in ActivePlayers)
        {
            if (BlackjackPayoutCalculator.CalculateHandValue(player.Cards) == GameConstants.Blackjack.BlackjackValue)
            {
                player.State = PlayerState.Stand;
                await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.BlackjackConfirmation]);

                var decisionStateRemainders = GetDecisionStateRemainders();
                if (decisionStateRemainders.Count(x => !Equals(x, client)) is not 0)
                {
                    await _outputHandler.TellPlayerAsync(player,
                        [Config.Translations.Blackjack.PlayersDeciding.FormatExt(decisionStateRemainders.Count)]);
                }
                continue;
            }

            await _outputHandler.TellPlayerAsync(player, [_inputHandlerConcrete.FormatAvailableActions(player, false)]);
        }

        var remainingPlayers = GetDecisionStateRemainders();
        CredifyDebugLog.Log("Blackjack",
            $"DECISIONS awaiting {remainingPlayers.Count} player(s): [{string.Join(", ", remainingPlayers.Select(c => c.CleanedName))}] " +
            $"dealerUp={_houseHand[0].ToChatString()}");
        if (remainingPlayers.Count is 0)
        {
            await DealerPlaysAsync(CancellationToken.None);
            return;
        }

        _dealerPlaysToken?.Dispose();
        _dealerPlaysToken = new CancellationTokenSource();
        _phaseEndsAt = DateTimeOffset.UtcNow + Config.Blackjack.TimeoutForPlayerAction;
        RaiseStateChanged();
        SharedLibraryCore.Utilities.ExecuteAfterDelay(Config.Blackjack.TimeoutForPlayerAction, DealerPlaysAsync, _dealerPlaysToken.Token);
        ScheduleTimeWarning(_dealerPlaysToken.Token, () => ActivePlayers
            .Where(x => x.Value.State is PlayerState.Playing or PlayerState.PlayingSplitHand)
            .Select(x => x.Value));
    }

    private async Task DealerPlaysAsync(CancellationToken token)
    {
        if (!IsInState(GameState.RequestPlayerDecisions)) return;
        TransitionToState(GameState.DealerPlays);

        // Skip dealer draw if all players (and their split hands) have busted
        var allPlayersBusted = ActivePlayers.All(p =>
            BlackjackPayoutCalculator.IsBusted(p.Value.Cards) &&
            (!p.Value.HasSplit || BlackjackPayoutCalculator.IsBusted(p.Value.SplitCards)));

        if (!allPlayersBusted)
        {
            while (BlackjackPayoutCalculator.CalculateHandValue(_houseHand) < GameConstants.Blackjack.DealerStandValue)
            {
                _houseHand.Add(_deckService.DrawCardOrReshuffle());
            }
        }

        foreach (var (client, player) in ActivePlayers)
        {
            await _outputHandler.TellPlayerAsync(player,
            [
                Config.Translations.Blackjack.DealerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(_houseHand),
                    string.Join(", ", _houseHand.Select(x => x.ToChatString()))),
                Config.Translations.Blackjack.PlayerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.Cards),
                    string.Join(", ", player.Cards.Select(x => x.ToChatString())))
            ]);
            
            // Show split hand if player has one
            if (player.HasSplit)
            {
                await _outputHandler.TellPlayerAsync(player,
                    [Config.Translations.Blackjack.SplitHand.FormatExt(
                        BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                        string.Join(", ", player.SplitCards.Select(x => x.ToChatString())))]);
            }
        }

        foreach (var (client, player) in ActivePlayers)
        {
            // Main hand outcome
            var outcome = _payoutCalculator.DetermineOutcome(player.Cards, _houseHand);
            player.Outcome = outcome;
            player.Payout = _payoutCalculator.CalculatePayout(player.Stake!.Value, outcome);
            await SendOutcomeMessageAsync(client, player, outcome, false);
            
            // Split hand outcome
            if (player.HasSplit)
            {
                var splitOutcome = _payoutCalculator.DetermineOutcome(player.SplitCards, _houseHand);
                player.SplitOutcome = splitOutcome;
                player.SplitPayout = _payoutCalculator.CalculatePayout(player.SplitStake!.Value, splitOutcome);
                await SendOutcomeMessageAsync(client, player, splitOutcome, true);
            }
        }

        _dealerPlaysToken?.Cancel();
        _dealerPlaysToken?.Dispose();
        _dealerPlaysToken = null;
        await PayoutAsync();
    }

    private async Task SendOutcomeMessageAsync(EFClient client, BlackjackPlayer player, GameOutcome outcome, bool isSplitHand = false)
    {
        var currentCards = isSplitHand ? player.SplitCards : player.Cards;
        var currentStake = isSplitHand ? player.SplitStake!.Value : player.Stake!.Value;
        var playerValue = BlackjackPayoutCalculator.CalculateHandValue(currentCards);
        var houseValue = BlackjackPayoutCalculator.CalculateHandValue(_houseHand);
        var handPrefix = isSplitHand ? "Split: " : "";

        switch (outcome)
        {
            case GameOutcome.Lose:
                if (BlackjackPayoutCalculator.IsBusted(currentCards))
                    await _outputHandler.TellPlayerAsync(player, [$"{handPrefix}{Config.Translations.Blackjack.PlayerBustConfirmation}"]);
                else
                    await _outputHandler.TellPlayerAsync(player,
                        [$"{handPrefix}{Config.Translations.Blackjack.Lose.FormatExt(playerValue)}"]);
                break;

            case GameOutcome.Blackjack:
                await _outputHandler.TellPlayerAsync(player, [$"{handPrefix}{Config.Translations.Blackjack.Win.FormatExt(playerValue)}"]);
                if (!isSplitHand) // Only broadcast for main hand blackjack
                {
                    await Communication.BroadcastToAllServersAsync(client,
                        [$"{Config.Translations.Blackjack.Title} " +
                         Config.Translations.Blackjack.Announcement.FormatExt(client.CleanedName,
                             _payoutCalculator.CalculateNetProfit(currentStake, outcome).ToString("N0"))]);
                }
                break;

            case GameOutcome.Win:
                if (BlackjackPayoutCalculator.IsBusted(_houseHand))
                    await _outputHandler.TellPlayerAsync(player,
                        [$"{handPrefix}{Config.Translations.Blackjack.DealerBust.FormatExt(houseValue)}"]);
                else
                    await _outputHandler.TellPlayerAsync(player,
                        [$"{handPrefix}{Config.Translations.Blackjack.Win.FormatExt(playerValue)}"]);
                break;

            case GameOutcome.Push:
                if (BlackjackPayoutCalculator.IsBlackjack(currentCards))
                    await _outputHandler.TellPlayerAsync(player, [$"{handPrefix}{Config.Translations.Blackjack.BlackjackPush}"]);
                else
                    await _outputHandler.TellPlayerAsync(player, [$"{handPrefix}{Config.Translations.Blackjack.Push}"]);
                break;
        }
    }

    private async Task PayoutAsync()
    {
        TransitionToState(GameState.Payout);

        foreach (var (client, player) in ActivePlayers)
        {
            if (ActivePlayers.Count() is not 1)
                await _outputHandler.TellPlayerAsync(player, [FormatPlayerOutcomes()]);

            // Main hand payout
            var mainPayout = player.Payout ?? 0;
            CredifyDebugLog.Log("Blackjack",
                $"PAYOUT {player.Client.CleanedName} stake={(player.Stake?.ToString("N0") ?? "-")} payout={mainPayout:N0} " +
                $"net={mainPayout - (player.Stake ?? 0):N0} hand=[{string.Join(" ", player.Cards.Select(c => c.ToChatString()))}]" +
                $"={BlackjackPayoutCalculator.CalculateHandValue(player.Cards)}{(player.HasSplit ? " (+split)" : "")}");
            if (mainPayout > 0)
            {
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, mainPayout);
                await PersistenceService.AddCreditsAsync(client, mainPayout);
                await _outputHandler.TellPlayerAsync(player,
                [
                    Config.Translations.Blackjack.Payout.FormatExt(
                        (mainPayout - player.Stake)?.ToString("N0"),
                        player.Stake?.ToString("N0"))
                ]);
            }
            
            // Split hand payout
            if (player.HasSplit)
            {
                var splitPayout = player.SplitPayout ?? 0;
                if (splitPayout > 0)
                {
                    ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, splitPayout);
                    await PersistenceService.AddCreditsAsync(client, splitPayout);
                    await _outputHandler.TellPlayerAsync(player,
                    [
                        $"Split: {Config.Translations.Blackjack.Payout.FormatExt(
                            (splitPayout - player.SplitStake)?.ToString("N0"),
                            player.SplitStake?.ToString("N0"))}"
                    ]);
                }
            }
        }

        // Hold the Payout state long enough for the web table's staggered dealer reveal (~0.9s + 0.65s per
        // card) plus time to actually read the outcome — at the old 2s the next round tore the table down
        // mid-reveal. Scales with the dealer's hand since bigger hands take longer to flip.
        var payoutDelay = Math.Clamp(2 + _houseHand.Count, 5, 8);
        SharedLibraryCore.Utilities.ExecuteAfterDelay(TimeSpan.FromSeconds(payoutDelay),
            EndGameAsync, CancellationToken.None);
    }

    private async Task EndGameAsync(CancellationToken token)
    {
        foreach (var client in ActivePlayers.Select(x => x.Key))
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Blackjack, client);
        }
        _houseHand.Clear();
        _dealerPlaysToken?.Cancel();
        _dealerPlaysToken?.Dispose();
        _dealerPlaysToken = null;
        _playerStakesToken?.Cancel();
        _playerStakesToken?.Dispose();
        _playerStakesToken = null;
        _insuranceToken?.Cancel();
        _insuranceToken?.Dispose();
        _insuranceToken = null;
        TransitionToState(GameState.WaitingForPlayers);

        foreach (var (client, player) in Players)
        {
            player.ResetForNewRound();
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.StartingGame.FormatExt(Players.Count)], true);
        }

        // Safety net: if everyone left sitting (e.g. other players disconnected), un-sit them
        // so the table can't stall - there's no one left to sit out for.
        if (!Players.IsEmpty && Players.All(p => p.Value.SittingOut))
        {
            foreach (var player in Players.Values) player.SittingOut = false;
        }

        try
        {
            await _startGameLock.WaitAsync(token);
            if (!Players.IsEmpty) await StartGameAsync();
        }
        finally
        {
            if (_startGameLock.CurrentCount is 0) _startGameLock.Release();
        }
    }

    #endregion

    #region IActiveGame Implementation

    public override async Task JoinGameAsync(EFClient client)
    {
        // Use GetOrAdd to handle both new joins and rejoins cleanly
        var isRejoin = Players.ContainsKey(client);
        var player = Players.GetOrAdd(client, _ => new BlackjackPlayer { Client = client, Queued = true });
        CredifyDebugLog.Log("Blackjack",
            $"JOIN {client.CleanedName} (rejoin={isRejoin}) | state={GameState} seats({Players.Count})=[{RosterStates()}]");
        
        // If player was already in dict (rejoining), reset their state for a fresh start
        if (isRejoin)
        {
            player.ResetForNewRound();
            player.Queued = true;
        }

        // Defensive: If game is stuck in an intermediate state with no active players,
        // force reset to WaitingForPlayers (handles edge cases from timeouts/disconnects)
        if (!IsInState(GameState.WaitingForPlayers) && !ActivePlayers.Any())
        {
            _playerStakesToken?.Cancel();
            _playerStakesToken?.Dispose();
            _playerStakesToken = null;
            _dealerPlaysToken?.Cancel();
            _dealerPlaysToken?.Dispose();
            _dealerPlaysToken = null;
            _insuranceToken?.Cancel();
            _insuranceToken?.Dispose();
            _insuranceToken = null;
            TransitionToState(GameState.WaitingForPlayers);
        }

        if (!IsInState(GameState.WaitingForPlayers))
        {
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.Queued]);
            return;
        }

        try
        {
            await _startGameLock.WaitAsync();
            if (Players.Count is 1) await StartGameAsync();
        }
        finally
        {
            if (_startGameLock.CurrentCount is 0) _startGameLock.Release();
        }

        RaiseStateChanged();
    }

    public override async Task LeaveGameAsync(EFClient client)
    {
        Players.TryRemove(client, out _);
        if (Players.IsEmpty) await EndGameAsync(CancellationToken.None);
        RaiseStateChanged();
    }

    public override async Task HandleChatAsync(EFClient client, string message)
    {
        if (!Players.TryGetValue(client, out var player) || player.Queued)
        {
            CredifyDebugLog.Log("Blackjack", $"DROP {client.CleanedName}: '{message}' (not seated or queued for next round) state={GameState}");
            return;
        }

        CredifyDebugLog.Log("Blackjack",
            $"RECV {client.CleanedName}: '{message}' | state={GameState} playerState={player.State} stake={(player.Stake?.ToString("N0") ?? "-")}");

        // During stake collection, allow input even without stake set
        // During insurance offering, allow input regardless of player state (they haven't acted yet)
        // During gameplay, require stake and correct player state
        if (GameState == GameState.RequestPlayerStakes)
        {
            // Allow stake input
        }
        else if (GameState == GameState.OfferingInsurance)
        {
            // Allow insurance input - player has stake but hasn't made decisions yet
            if (player.Stake is null)
            {
                CredifyDebugLog.Log("Blackjack", $"DROP {client.CleanedName}: '{message}' (insurance phase but no stake)");
                return;
            }
        }
        else if (player.Stake is null)
        {
            CredifyDebugLog.Log("Blackjack", $"DROP {client.CleanedName}: '{message}' (no stake set, state={GameState})");
            return; // No stake set and not in stake collection phase
        }
        else if (player.State != PlayerState.Playing && player.State != PlayerState.PlayingSplitHand)
        {
            CredifyDebugLog.Log("Blackjack", $"DROP {client.CleanedName}: '{message}' (playerState={player.State} not awaiting a decision)");
            return; // Not in a valid input state
        }

        await ExecuteUnderChatLockAsync(async () =>
        {
            switch (GameState)
            {
                case GameState.RequestPlayerStakes:
                    await HandleStakeInputAsync(client, player, message);
                    break;
                case GameState.OfferingInsurance:
                    await HandleInsuranceInputAsync(client, player, message);
                    break;
                case GameState.RequestPlayerDecisions:
                    await HandlePlayerDecisionAsync(client, player, message);
                    break;
            }
        });

        RaiseStateChanged();
    }

    #endregion

    #region Input Handlers

    private async Task HandleStakeInputAsync(EFClient client, BlackjackPlayer player, string message)
    {
        if (player.Stake is not null) return;

        var trimmed = message.Trim();

        // Sit out / rejoin without leaving the table. Only allowed when someone else is
        // still playing - sitting out alone would just stall the table, so tell them to leave.
        if (trimmed.Equals("sit", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("skip", StringComparison.OrdinalIgnoreCase))
        {
            var someoneElsePlaying = Players.Any(p =>
                !Equals(p.Key, client) && p.Value is { Queued: false, SittingOut: false });
            if (!someoneElsePlaying)
            {
                await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SitAlone]);
                return;
            }

            player.SittingOut = true;
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SitOut]);
            return;
        }
        if (trimmed.Equals("back", StringComparison.OrdinalIgnoreCase))
        {
            player.SittingOut = false;
            var funds = await PersistenceService.GetClientCreditsAsync(client);
            await _outputHandler.TellPlayerAsync(player,
            [
                Config.Translations.Blackjack.SitBack,
                Config.Translations.Blackjack.PlaceBets.FormatExt(funds.ToString("N0"))
            ]);
            return;
        }

        // Repeat the previous stake.
        if (trimmed.Equals("same", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Equals("again", StringComparison.OrdinalIgnoreCase))
        {
            if (player.LastStake is null)
            {
                await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.NoPreviousBet]);
                return;
            }
            trimmed = player.LastStake.Value.ToString();
        }

        var stakeResult = await _stakeValidator.ValidateStakeAsync(
            trimmed,
            client,
            Config.Translations.Core.InsufficientCredits,
            Config.Translations.Blackjack.PlaceBets.FormatExt("0"), // Fallback message
            Config.Translations.Blackjack.PlaceBets.FormatExt("0")  // Fallback message
        );

        if (!stakeResult.IsValid)
        {
            await _outputHandler.TellPlayerAsync(player, [stakeResult.ErrorMessage ?? Config.Translations.Blackjack.PlaceBets.FormatExt("0")]);
            return;
        }

        player.Stake = stakeResult.Result;
        player.LastStake = stakeResult.Result; // remember for "same"
        player.SittingOut = false;             // betting rejoins
        CredifyDebugLog.Log("Blackjack", $"STAKE {player.Client.CleanedName} bet={stakeResult.Result:N0}");
        await PersistenceService.RemoveCreditsAsync(client, stakeResult.Result);
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.AcceptedBet.FormatExt(stakeResult.Result)]);

        var requestStakesRemainders = GetRequestStakesRemainders();
        if (requestStakesRemainders.Count is 0)
        {
            await DealCardsAsync(CancellationToken.None);
            return;
        }

        if (requestStakesRemainders.Count(x => !Equals(x, client)) is not 0)
        {
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.WaitingForBets.FormatExt(requestStakesRemainders.Count)]);
        }
    }

    private async Task HandleInsuranceInputAsync(EFClient client, BlackjackPlayer player, string message)
    {
        if (player.HasInsurance || player.InsuranceDeclined) return; // already responded this offer

        var parseResult = _inputHandler.Parse(message);
        if (!parseResult.IsValid)
        {
            await _outputHandler.TellPlayerAsync(player, [parseResult.ErrorMessage ?? Config.Translations.Blackjack.PlayerDecision]);
            return;
        }

        var action = parseResult.Result!.Action;

        // Stand during the insurance window = actively decline, so the round can advance immediately
        // instead of making everyone wait out the timer. (Stand is unambiguous here — decisions
        // haven't started, so there's no hand to "stand" on yet.)
        if (action == PlayerAction.Stand)
        {
            player.InsuranceDeclined = true;
            CredifyDebugLog.Log("Blackjack", $"INSURANCE {player.Client.CleanedName} declined");
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.InsuranceDeclined]);
            await ProceedIfAllInsuranceResponsesInAsync();
            return;
        }

        if (action != PlayerAction.Insurance) return; // ignore unrelated input during the offer

        var insuranceCost = player.Stake!.Value / 2;
        var playerFunds = await PersistenceService.GetClientCreditsAsync(client);

        if (playerFunds < insuranceCost)
        {
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.InsuranceInsufficientFunds]);
            return;
        }

        await PersistenceService.RemoveCreditsAsync(client, insuranceCost);
        player.HasInsurance = true;
        player.InsuranceBet = insuranceCost;
        CredifyDebugLog.Log("Blackjack", $"INSURANCE {player.Client.CleanedName} cost={insuranceCost:N0}");
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.InsuranceTaken.FormatExt(insuranceCost.ToString("N0"))]);

        await ProceedIfAllInsuranceResponsesInAsync();
    }

    /// <summary>
    /// Advances out of the insurance window the moment every eligible player has answered — taken it,
    /// explicitly declined it, or can't afford it (no choice, so counts as answered). This is what lets a
    /// manual decline skip the wait; without an answer from someone who could still take it, the offer
    /// stays open until the timeout fires.
    /// </summary>
    private async Task ProceedIfAllInsuranceResponsesInAsync()
    {
        foreach (var (otherClient, otherPlayer) in ActivePlayers)
        {
            if (otherPlayer.HasInsurance || otherPlayer.InsuranceDeclined) continue;
            var otherCredits = await PersistenceService.GetClientCreditsAsync(otherClient);
            if (otherCredits >= otherPlayer.Stake!.Value / 2)
            {
                return; // someone who could still take insurance hasn't answered yet
            }
        }

        _insuranceToken?.Cancel();
        _insuranceToken?.Dispose();
        _insuranceToken = null;
        await RequestPlayerDecisionsAsync();
    }

    private async Task HandlePlayerDecisionAsync(EFClient client, BlackjackPlayer player, string message)
    {
        var parseResult = _inputHandler.Parse(message);
        if (!parseResult.IsValid)
        {
            await _outputHandler.TellPlayerAsync(player, [parseResult.ErrorMessage ?? Config.Translations.Blackjack.PlayerDecision]);
            return;
        }

        var action = parseResult.Result!.Action;
        var isPlayingSplit = player.State == PlayerState.PlayingSplitHand;
        var currentCards = isPlayingSplit ? player.SplitCards : player.Cards;
        CredifyDebugLog.Log("Blackjack",
            $"DECISION {player.Client.CleanedName}: {action}{(isPlayingSplit ? " (split hand)" : "")} " +
            $"| hand=[{string.Join(" ", currentCards.Select(c => c.ToChatString()))}]={BlackjackPayoutCalculator.CalculateHandValue(currentCards)}");

        switch (action)
        {
            case PlayerAction.Hit:
                await HitAsync(client, player, isPlayingSplit);
                break;
            case PlayerAction.Stand:
                await HandleStandAsync(player, isPlayingSplit);
                break;
            case PlayerAction.Cards:
                await ShowCardsAsync(player, isPlayingSplit);
                break;
            case PlayerAction.Double:
                await HandleDoubleAsync(client, player, isPlayingSplit);
                break;
            case PlayerAction.Split:
                await HandleSplitAsync(client, player);
                break;
            case PlayerAction.Insurance:
                // Insurance is only valid during the offering phase, ignore here
                break;
        }

        await CheckRoundCompletionAsync(client, player);
    }

    private async Task HandleStandAsync(BlackjackPlayer player, bool isPlayingSplit)
    {
        var currentCards = isPlayingSplit ? player.SplitCards : player.Cards;
        var handValue = BlackjackPayoutCalculator.CalculateHandValue(currentCards);
        
        if (isPlayingSplit)
        {
            player.SplitState = PlayerState.Stand;
            player.State = PlayerState.Stand; // Done with both hands
        }
        else if (player.HasSplit)
        {
            // Finished main hand, move to split hand
            player.State = PlayerState.PlayingSplitHand;
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SplitNowPlayingSecond]);
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.SplitHand.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                    string.Join(", ", player.SplitCards.Select(x => x.ToChatString())))]);
            await _outputHandler.TellPlayerAsync(player, [_inputHandlerConcrete.FormatAvailableActions()]);
            return; // Don't check round completion yet
        }
        else
        {
            player.State = PlayerState.Stand;
        }
        
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.PlayerStand.FormatExt(handValue)]);
    }

    private async Task ShowCardsAsync(BlackjackPlayer player, bool isPlayingSplit)
    {
        var currentCards = isPlayingSplit ? player.SplitCards : player.Cards;
        await _outputHandler.TellPlayerAsync(player,
        [
            Config.Translations.Blackjack.DealerInitialCard.FormatExt(_houseHand[0]),
            Config.Translations.Blackjack.PlayerCards.FormatExt(
                BlackjackPayoutCalculator.CalculateHandValue(currentCards),
                string.Join(", ", currentCards.Select(x => x.ToChatString())))
        ]);
        
        if (player.HasSplit && !isPlayingSplit)
        {
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.SplitHand.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                    string.Join(", ", player.SplitCards.Select(x => x.ToChatString())))]);
        }
    }

    private async Task HandleDoubleAsync(EFClient client, BlackjackPlayer player, bool isPlayingSplit)
    {
        var currentCards = isPlayingSplit ? player.SplitCards : player.Cards;
        
        // Can only double on 2-card hand
        if (currentCards.Count != 2)
        {
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.DoubleNotAllowed]);
            return;
        }
        
        // Check if doubling is allowed after split
        if (isPlayingSplit && !Config.Blackjack.AllowDoubleAfterSplit)
        {
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.DoubleNotAllowed]);
            return;
        }
        
        var currentStake = isPlayingSplit ? player.SplitStake!.Value : player.Stake!.Value;
        var playerFunds = await PersistenceService.GetClientCreditsAsync(client);
        
        if (playerFunds < currentStake)
        {
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.DoubleInsufficientFunds]);
            return;
        }
        
        // Double the stake
        await PersistenceService.RemoveCreditsAsync(client, currentStake);
        if (isPlayingSplit)
            player.SplitStake = currentStake * 2;
        else
            player.Stake = currentStake * 2;
        
        player.HasDoubled = true;
        
        await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.DoubleDown]);
        
        // Draw exactly one card
        var card = _deckService.DrawCardOrReshuffle();
        currentCards.Add(card);
        
        var handValue = BlackjackPayoutCalculator.CalculateHandValue(currentCards);
        var coloredCards = FormatCardsWithHighlight(currentCards);
        
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.DoubleDownResult.FormatExt(handValue, coloredCards)]);
        
        // Auto-stand after double
        if (isPlayingSplit)
        {
            player.SplitState = BlackjackPayoutCalculator.IsBusted(currentCards) ? PlayerState.Busted : PlayerState.Stand;
            player.State = PlayerState.Stand;
        }
        else if (player.HasSplit)
        {
            // Move to split hand
            player.State = PlayerState.PlayingSplitHand;
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SplitNowPlayingSecond]);
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.SplitHand.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                    string.Join(", ", player.SplitCards.Select(x => x.ToChatString())))]);
            await _outputHandler.TellPlayerAsync(player, [_inputHandlerConcrete.FormatAvailableActions()]);
            return;
        }
        else
        {
            player.State = BlackjackPayoutCalculator.IsBusted(currentCards) ? PlayerState.Busted : PlayerState.Stand;
        }
    }

    private async Task HandleSplitAsync(EFClient client, BlackjackPlayer player)
    {
        if (!player.CanSplit())
        {
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SplitNotAllowed]);
            return;
        }
        
        var playerFunds = await PersistenceService.GetClientCreditsAsync(client);
        if (playerFunds < player.Stake!.Value)
        {
            await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SplitInsufficientFunds]);
            return;
        }
        
        // Take second stake
        await PersistenceService.RemoveCreditsAsync(client, player.Stake!.Value);
        player.SplitStake = player.Stake;
        player.HasSplit = true;
        
        // Move second card to split hand
        player.SplitCards.Add(player.Cards[1]);
        player.Cards.RemoveAt(1);
        
        // Deal one card to each hand
        player.Cards.Add(_deckService.DrawCardOrReshuffle());
        player.SplitCards.Add(_deckService.DrawCardOrReshuffle());
        
        await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.Split]);
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.PlayerCards.FormatExt(
                BlackjackPayoutCalculator.CalculateHandValue(player.Cards),
                string.Join(", ", player.Cards.Select(x => x.ToChatString())))]);
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.SplitHand.FormatExt(
                BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                string.Join(", ", player.SplitCards.Select(x => x.ToChatString())))]);
        await _outputHandler.TellPlayerAsync(player, [_inputHandlerConcrete.FormatAvailableActions()]);
    }

    private async Task CheckRoundCompletionAsync(EFClient client, BlackjackPlayer player)
    {
        var decisionStateRemainders = GetDecisionStateRemainders();
        if (decisionStateRemainders.Count is 0)
        {
            await DealerPlaysAsync(CancellationToken.None);
            return;
        }

        if (decisionStateRemainders.Count(x => !Equals(x, client)) is not 0)
        {
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.PlayersDeciding.FormatExt(decisionStateRemainders.Count)]);
        }
    }

    private async Task HitAsync(EFClient client, BlackjackPlayer player, bool isPlayingSplit = false)
    {
        if (_deckService.IsDeckEmpty())
        {
            _deckService.ReshuffleDeck();
            await _outputHandler.TellPlayersAsync(ActivePlayers.Select(x => x.Value), [Config.Translations.Blackjack.NewDeckShuffled]);
        }

        var card = _deckService.DrawCardOrReshuffle();
        var currentCards = isPlayingSplit ? player.SplitCards : player.Cards;
        currentCards.Add(card);

        var coloredCards = FormatCardsWithHighlight(currentCards);
        var handValue = BlackjackPayoutCalculator.CalculateHandValue(currentCards);

        await _outputHandler.TellPlayerAsync(player,
        [
            Config.Translations.Blackjack.PlayerHit.FormatExt(handValue, coloredCards),
            _inputHandlerConcrete.FormatAvailableActions()
        ]);

        if (handValue == GameConstants.Blackjack.BlackjackValue)
        {
            if (isPlayingSplit)
            {
                player.SplitState = PlayerState.Stand;
                player.State = PlayerState.Stand;
            }
            else if (player.HasSplit)
            {
                // Move to split hand
                player.State = PlayerState.PlayingSplitHand;
                await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SplitNowPlayingSecond]);
                await _outputHandler.TellPlayerAsync(player,
                    [Config.Translations.Blackjack.SplitHand.FormatExt(
                        BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                        string.Join(", ", player.SplitCards.Select(x => x.ToChatString())))]);
                await _outputHandler.TellPlayerAsync(player, [_inputHandlerConcrete.FormatAvailableActions()]);
                return;
            }
            else
            {
                player.State = PlayerState.Stand;
            }
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.PlayerStand.FormatExt(handValue)]);
            return;
        }

        if (BlackjackPayoutCalculator.IsBusted(currentCards))
        {
            if (isPlayingSplit)
            {
                player.SplitState = PlayerState.Busted;
                player.State = PlayerState.Stand; // Done with split hand (busted), mark as complete
            }
            else if (player.HasSplit)
            {
                // Main hand busted, move to split hand
                player.State = PlayerState.PlayingSplitHand;
                await _outputHandler.TellPlayerAsync(player,
                    [Config.Translations.Blackjack.PlayerBust.FormatExt(handValue, coloredCards)]);
                await _outputHandler.TellPlayerAsync(player, [Config.Translations.Blackjack.SplitNowPlayingSecond]);
                await _outputHandler.TellPlayerAsync(player,
                    [Config.Translations.Blackjack.SplitHand.FormatExt(
                        BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                        string.Join(", ", player.SplitCards.Select(x => x.ToChatString())))]);
                await _outputHandler.TellPlayerAsync(player, [_inputHandlerConcrete.FormatAvailableActions()]);
                return;
            }
            else
            {
                player.State = PlayerState.Busted;
            }
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.PlayerBust.FormatExt(handValue, coloredCards)]);
        }
    }

    private string FormatCardsWithHighlight(List<Card> cards)
    {
        var cardStrings = cards.Select(x => x.ToChatString()).ToList();
        var coloredCards = new StringBuilder();

        for (var i = 0; i < cardStrings.Count; i++)
        {
            if (i == cardStrings.Count - 1) coloredCards.Append($"(Color::Red){cardStrings[i]}");
            else coloredCards.Append($"(Color::Accent){cardStrings[i]}, ");
        }

        return coloredCards.ToString();
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets players actively in the current round (have placed a stake).
    /// </summary>
    private IEnumerable<KeyValuePair<EFClient, BlackjackPlayer>> ActivePlayers => 
        Players.Where(x => !x.Value.Queued && x.Value.Stake is not null);

    private string FormatPlayerOutcomes()
    {
        return string.Join(", ", ActivePlayers.Select(x =>
            Config.Translations.Blackjack.PlayerOutcomeMessage
                .FormatExt(FormatOutcome(x.Value.Outcome), x.Key.CleanedName,
                    BlackjackPayoutCalculator.CalculateHandValue(x.Value.Cards))));

        string FormatOutcome(GameOutcome outcome) =>
            outcome switch
            {
                GameOutcome.Blackjack => Config.Translations.Blackjack.OutcomeBlackjack,
                GameOutcome.Win => Config.Translations.Blackjack.OutcomeWin,
                GameOutcome.Push => Config.Translations.Blackjack.OutcomePush,
                _ => Config.Translations.Blackjack.OutcomeLose
            };
    }

    private List<EFClient> GetRequestStakesRemainders() => Players
        .Where(x => x.Value is { Queued: false, SittingOut: false, Stake: null })
        .Select(x => x.Key)
        .ToList();

    /// <summary>
    /// Compact one-line hand summary: "You 18 (10, 8) vs Dealer K".
    /// </summary>
    private string BuildHandSummary(BlackjackPlayer player) =>
        Config.Translations.Blackjack.HandSummary.FormatExt(
            BlackjackPayoutCalculator.CalculateHandValue(player.Cards),
            string.Join(", ", player.Cards.Select(c => c.ToChatString())),
            _houseHand[0].ToChatString());

    /// <summary>
    /// Sends a "10s left" nudge ~10s before a phase timeout (no visible clock in chat).
    /// The selector is evaluated when the warning fires, so only players still pending
    /// at that moment are nudged. Tied to the phase token, so it no-ops once the phase ends.
    /// </summary>
    private void ScheduleTimeWarning(CancellationToken phaseToken, Func<IEnumerable<BlackjackPlayer>> stillPending)
    {
        var warnAfter = Config.Blackjack.TimeoutForPlayerAction - TimeSpan.FromSeconds(10);
        if (warnAfter <= TimeSpan.Zero) return;

        SharedLibraryCore.Utilities.ExecuteAfterDelay(warnAfter, async ct =>
        {
            if (ct.IsCancellationRequested) return;
            var pending = stillPending().ToList();
            if (pending.Count > 0)
                await _outputHandler.TellPlayersAsync(pending, [Config.Translations.Blackjack.TimeWarning]);
        }, phaseToken);
    }

    private List<EFClient> GetDecisionStateRemainders() => ActivePlayers
        .Where(x => x.Value.State is PlayerState.Playing or PlayerState.PlayingSplitHand)
        .Select(x => x.Key)
        .ToList();

    #endregion

    #region Web Frontend

    /// <inheritdoc />
    public BlackjackSnapshot GetSnapshot()
    {
        var state = GameState;
        var revealed = state is GameState.DealerPlays or GameState.Payout;

        var phase = state switch
        {
            GameState.WaitingForPlayers or GameState.SettingUpGame => "Waiting",
            GameState.RequestPlayerStakes => "Betting",
            GameState.DealCards => "Dealing",
            GameState.OfferingInsurance => "Insurance",
            GameState.RequestPlayerDecisions => "Decisions",
            GameState.DealerPlays => "DealerPlaying",
            GameState.Payout => "Payout",
            _ => "Waiting"
        };

        var secondsRemaining = (phase is "Betting" or "Insurance" or "Decisions") && _phaseEndsAt is { } endsAt
            ? Math.Max(0, (endsAt - DateTimeOffset.UtcNow).TotalSeconds)
            : 0;

        var dealerCards = _houseHand.Count == 0
            ? new List<Card>()
            : revealed ? _houseHand.ToList() : _houseHand.Take(1).ToList();
        var dealerValue = _houseHand.Count == 0
            ? 0
            : revealed ? BlackjackRules.HandValue(_houseHand) : _houseHand[0].BlackjackValue;

        // a seat's Outcome/Net are only valid once the round resolves into Payout — DetermineOutcome and
        // CalculatePayout run on the way into that state. `revealed` is broader (it also covers DealerPlays,
        // so the dealer's hole card flips while it draws); using it to gate the outcome would expose the
        // not-yet-computed result, where p.Outcome defaults to GameOutcome.Blackjack (enum 0) and
        // Net reads (0 - stake). That surfaced as bogus "Blackjack! -stake" rows in the web history rail.
        var outcomeSettled = state is GameState.Payout;
        var seats = Players.Values.Select(p => BuildSeat(p, state, outcomeSettled)).ToList();

        return new BlackjackSnapshot
        {
            Phase = phase,
            SecondsRemaining = secondsRemaining,
            DealerCards = dealerCards,
            DealerHasHole = !revealed && _houseHand.Count > 1,
            DealerValue = dealerValue,
            Seats = seats
        };
    }

    private BlackjackSeatView BuildSeat(BlackjackPlayer p, GameState state, bool settled)
    {
        var seatState =
            p.Queued ? "Waiting" :
            p.SittingOut ? "SittingOut" :
            state == GameState.RequestPlayerStakes && p.Stake is null ? "Betting" :
            p.State switch
            {
                PlayerState.Playing => "Playing",
                PlayerState.PlayingSplitHand => "PlayingSplit",
                PlayerState.Stand => "Stand",
                PlayerState.Busted => "Busted",
                _ => "Playing"
            };

        var net = settled && p.Stake is not null ? (p.Payout ?? 0) - p.Stake.Value : 0;
        if (settled && p.HasSplit)
        {
            net += (p.SplitPayout ?? 0) - (p.SplitStake ?? 0);
        }

        return new BlackjackSeatView
        {
            ClientId = p.Client.ClientId,
            Name = p.Client.CleanedName,
            Stake = p.Stake,
            Cards = p.Cards.ToList(),
            Value = BlackjackRules.HandValue(p.Cards),
            IsSoft = BlackjackRules.HasSoftAce(p.Cards),
            IsBlackjack = BlackjackRules.IsBlackjack(p.Cards),
            Busted = BlackjackRules.IsBusted(p.Cards),
            State = seatState,
            Outcome = settled && p.Stake is not null ? OutcomeString(p.Outcome) : "",
            Net = net,
            HasSplit = p.HasSplit,
            SplitCards = p.SplitCards.ToList(),
            SplitValue = BlackjackRules.HandValue(p.SplitCards),
            SplitOutcome = settled && p.HasSplit ? OutcomeString(p.SplitOutcome) : "",
            HasInsurance = p.HasInsurance,
            CanDouble = p.CanDouble(),
            CanSplit = p.CanSplit(),
            InsuranceEligible = state == GameState.OfferingInsurance && !p.HasInsurance && p.Cards.Count == 2 && !p.HasSplit
        };
    }

    private static string OutcomeString(GameOutcome outcome) => outcome switch
    {
        GameOutcome.Blackjack => "Blackjack",
        GameOutcome.Win => "Win",
        GameOutcome.Push => "Push",
        _ => "Lose"
    };

    #endregion
}
