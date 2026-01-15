using System.Collections.Concurrent;
using System.Text;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack.Enums;
using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Chat.Active.Games.Blackjack.Services;
using Credify.Chat.Active.Games.Blackjack.Utilities;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// Main Blackjack game class managing game flow, player actions, and payouts.
/// Uses Core abstractions for input/output handling and stake validation.
/// </summary>
public class BlackjackGame : BaseActiveGame<BlackjackPlayer>
{
    private readonly GameStateMachine<GameState> _stateMachine;

    /// <summary>
    /// Gets the current game state.
    /// </summary>
    protected GameState GameState => _stateMachine.CurrentState;

    /// <summary>
    /// Transitions to a new game state.
    /// </summary>
    protected bool TransitionToState(GameState newState) => _stateMachine.TransitionTo(newState);

    /// <summary>
    /// Forces a transition to a new game state (use with caution).
    /// </summary>
    protected void ForceTransitionToState(GameState newState) => _stateMachine.ForceTransitionTo(newState);

    /// <summary>
    /// Checks if currently in the specified state.
    /// </summary>
    protected bool IsInState(GameState state) => _stateMachine.IsInState(state);

    /// <summary>
    /// Checks if currently in any of the specified states.
    /// </summary>
    protected bool IsInAnyState(params GameState[] states) => _stateMachine.IsInAnyState(states);
    private List<BlackjackCard> _houseHand = [];
    private readonly BlackjackDeckService _deckService;
    private readonly BlackjackPayoutCalculator _payoutCalculator;
    private readonly IGameInputParser<BlackjackActionResult> _inputHandler;
    private readonly BlackjackHandleInput _inputHandlerConcrete; // For FormatAvailableActions convenience methods
    private readonly IGameOutputHandler<BlackjackPlayer> _outputHandler;
    private readonly BlackjackHandleOutput _outputHandlerConcrete; // For TellClientAsync convenience method
    private readonly StakeValidator _stakeValidator;
    private CancellationTokenSource? _playerStakesToken;
    private CancellationTokenSource? _dealerPlaysToken;
    private readonly SemaphoreSlim _chatLock = new(1, 1);
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

        ForceTransitionToState(GameState.SettingUpGame);
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
        ForceTransitionToState(GameState.RequestPlayerStakes);

        var insufficientFunds = new List<EFClient>();
        foreach (var (client, player) in Players)
        {
            if (player.Queued) continue;
            
            player.State = PlayerState.Playing;
            var playerFunds = await PersistenceService.GetClientCreditsAsync(client);
            if (playerFunds < GameConstants.MinimumCredits)
            {
                insufficientFunds.Add(client);
                continue;
            }

            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.PlaceBets.FormatExt(playerFunds.ToString("N0"))], true);
        }

        foreach (var client in insufficientFunds)
        {
            Players.TryRemove(client, out _);
            await _outputHandlerConcrete.TellClientAsync(client, [Config.Translations.Blackjack.InsufficientFunds], true);
        }

        // Check if there are any non-queued players remaining (stakes not set yet, can't use ActivePlayers)
        if (!Players.Any(p => !p.Value.Queued))
        {
            await EndGameAsync(CancellationToken.None);
            return;
        }

        _playerStakesToken?.Dispose();
        _playerStakesToken = new CancellationTokenSource();
        SharedLibraryCore.Utilities.ExecuteAfterDelay(Config.Blackjack.TimeoutForPlayerAction, DealCardsAsync,
            _playerStakesToken.Token);
    }

    private async Task DealCardsAsync(CancellationToken token)
    {
        if (!IsInState(GameState.RequestPlayerStakes)) return;
        ForceTransitionToState(GameState.DealCards);

        var noBets = Players
            .Where(x => !x.Value.Queued && x.Value.Stake is null)
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

            await _outputHandler.TellPlayerAsync(player,
            [
                Config.Translations.Blackjack.DealerInitialCard.FormatExt(_houseHand[0]),
                Config.Translations.Blackjack.PlayerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.Cards),
                    string.Join(", ", player.Cards.Select(x => x.ToString())))
            ]);
        }

        _playerStakesToken?.Cancel();
        _playerStakesToken?.Dispose();
        _playerStakesToken = null;
        
        // Check if dealer shows Ace - offer insurance
        if (_houseHand[0].CardRank == BlackjackCard.Rank.Ace)
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
        ForceTransitionToState(GameState.OfferingInsurance);
        
        foreach (var (client, player) in ActivePlayers)
        {
            var insuranceCost = player.Stake!.Value / 2;
            var playerFunds = await PersistenceService.GetClientCreditsAsync(client);
            
            if (playerFunds >= insuranceCost)
            {
                await _outputHandler.TellPlayerAsync(player,
                    [Config.Translations.Blackjack.InsuranceOffer.FormatExt(insuranceCost.ToString("N0"))]);
            }
        }
        
        // Give players a brief window to take insurance, then proceed
        SharedLibraryCore.Utilities.ExecuteAfterDelay(
            TimeSpan.FromSeconds(GameConstants.Timeouts.DefaultGameStartDelay),
            async (token) => await RequestPlayerDecisionsAsync(),
            CancellationToken.None);
    }

    private async Task RequestPlayerDecisionsAsync()
    {
        ForceTransitionToState(GameState.RequestPlayerDecisions);
        
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
        if (remainingPlayers.Count is 0)
        {
            await DealerPlaysAsync(CancellationToken.None);
            return;
        }

        _dealerPlaysToken?.Dispose();
        _dealerPlaysToken = new CancellationTokenSource();
        SharedLibraryCore.Utilities.ExecuteAfterDelay(Config.Blackjack.TimeoutForPlayerAction, DealerPlaysAsync, _dealerPlaysToken.Token);
    }

    private async Task DealerPlaysAsync(CancellationToken token)
    {
        if (!IsInState(GameState.RequestPlayerDecisions)) return;
        ForceTransitionToState(GameState.DealerPlays);

        while (BlackjackPayoutCalculator.CalculateHandValue(_houseHand) < GameConstants.Blackjack.DealerStandValue)
        {
            _houseHand.Add(_deckService.DrawCardOrReshuffle());
        }

        foreach (var (client, player) in ActivePlayers)
        {
            await _outputHandler.TellPlayerAsync(player,
            [
                Config.Translations.Blackjack.DealerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(_houseHand),
                    string.Join(", ", _houseHand.Select(x => x.ToString()))),
                Config.Translations.Blackjack.PlayerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.Cards),
                    string.Join(", ", player.Cards.Select(x => x.ToString())))
            ]);
            
            // Show split hand if player has one
            if (player.HasSplit)
            {
                await _outputHandler.TellPlayerAsync(player,
                    [Config.Translations.Blackjack.SplitHand.FormatExt(
                        BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                        string.Join(", ", player.SplitCards.Select(x => x.ToString())))]);
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
        ForceTransitionToState(GameState.Payout);

        foreach (var (client, player) in ActivePlayers)
        {
            if (ActivePlayers.Count() is not 1)
                await _outputHandler.TellPlayerAsync(player, [FormatPlayerOutcomes()]);

            // Main hand payout
            var mainPayout = player.Payout ?? 0;
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

        SharedLibraryCore.Utilities.ExecuteAfterDelay(TimeSpan.FromSeconds(GameConstants.Timeouts.DefaultPayoutDelay),
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
        ForceTransitionToState(GameState.WaitingForPlayers);

        foreach (var (client, player) in Players)
        {
            player.ResetForNewRound();
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.StartingGame.FormatExt(Players.Count)], true);
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
        var player = new BlackjackPlayer { Client = client, Queued = true };
        Players.TryAdd(client, player);

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
    }

    public override async Task LeaveGameAsync(EFClient client)
    {
        Players.TryRemove(client, out _);
        if (Players.IsEmpty) await EndGameAsync(CancellationToken.None);
    }

    public override async Task HandleChatAsync(EFClient client, string message)
    {
        if (!Players.TryGetValue(client, out var player) || player.Queued) return;
        
        // During stake collection, allow input even without stake set
        // During gameplay, require stake and correct player state
        if (GameState == GameState.RequestPlayerStakes)
        {
            // Allow stake input
        }
        else if (player.Stake is null)
        {
            return; // No stake set and not in stake collection phase
        }
        else if (player.State != PlayerState.Playing && player.State != PlayerState.PlayingSplitHand)
        {
            return; // Not in a valid input state
        }

        try
        {
            await _chatLock.WaitAsync();

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
        }
        finally
        {
            if (_chatLock.CurrentCount is 0) _chatLock.Release();
        }
    }

    #endregion

    #region Input Handlers

    private async Task HandleStakeInputAsync(EFClient client, BlackjackPlayer player, string message)
    {
        if (player.Stake is not null) return;

        var stakeResult = await _stakeValidator.ValidateStakeAsync(
            message,
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
        if (player.HasInsurance) return; // Already took insurance
        
        var parseResult = _inputHandler.Parse(message);
        if (!parseResult.IsValid)
        {
            await _outputHandler.TellPlayerAsync(player, [parseResult.ErrorMessage ?? Config.Translations.Blackjack.PlayerDecision]);
            return;
        }
        if (parseResult.Result!.Action != PlayerAction.Insurance) return;
        
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
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.InsuranceTaken.FormatExt(insuranceCost.ToString("N0"))]);
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
                    string.Join(", ", player.SplitCards.Select(x => x.ToString())))]);
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
                string.Join(", ", currentCards.Select(x => x.ToString())))
        ]);
        
        if (player.HasSplit && !isPlayingSplit)
        {
            await _outputHandler.TellPlayerAsync(player,
                [Config.Translations.Blackjack.SplitHand.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                    string.Join(", ", player.SplitCards.Select(x => x.ToString())))]);
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
                    string.Join(", ", player.SplitCards.Select(x => x.ToString())))]);
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
                string.Join(", ", player.Cards.Select(x => x.ToString())))]);
        await _outputHandler.TellPlayerAsync(player,
            [Config.Translations.Blackjack.SplitHand.FormatExt(
                BlackjackPayoutCalculator.CalculateHandValue(player.SplitCards),
                string.Join(", ", player.SplitCards.Select(x => x.ToString())))]);
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
                        string.Join(", ", player.SplitCards.Select(x => x.ToString())))]);
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
                        string.Join(", ", player.SplitCards.Select(x => x.ToString())))]);
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

    private string FormatCardsWithHighlight(List<BlackjackCard> cards)
    {
        var cardStrings = cards.Select(x => x.ToString()).ToList();
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

    private List<EFClient> GetRequestStakesRemainders() => ActivePlayers
        .Where(x => x.Value.Stake is null)
        .Select(x => x.Key)
        .ToList();

    private List<EFClient> GetDecisionStateRemainders() => ActivePlayers
        .Where(x => x.Value.State is PlayerState.Playing or PlayerState.PlayingSplitHand)
        .Select(x => x.Key)
        .ToList();

    #endregion
}
