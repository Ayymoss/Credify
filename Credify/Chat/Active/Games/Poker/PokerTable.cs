using System.Collections.Concurrent;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Chat.Active.Games.Poker.Services;
using Credify.Chat.Active.Games.Poker.Utilities;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Poker;

/// <summary>
/// Main poker table managing game flow, betting rounds, and hand lifecycle.
/// </summary>
public class PokerTable(
    CredifyConfiguration config,
    TranslationsRoot translations,
    PersistenceService persistenceService,
    GamePlayerCommunication communication,
    IGameInputParser<PokerActionResult> input,
    PokerHandleInput inputConcrete, // For FormatAvailableActions convenience method
    IGameOutputHandler<PokerPlayer> output,
    PokerDeckService deckService,
    PokerHandEvaluator handEvaluator,
    PokerBettingService bettingService,
    PokerActionValidator actionValidator)
    : BaseContinuousGame<PokerPlayer>(persistenceService, config, communication)
{
    private readonly GameStateMachine<PokerGameState> _stateMachine = new PokerStateMachine();
    private List<PokerPlayer> _playersInHand = [];
    private List<PokerCard> _communityCards = [];
    private BettingRound _currentRound = new();
    private long _totalPot = 0; // Accumulated pot across all betting rounds in a hand
    private int _dealerButtonPosition = -1;
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PokerTranslations _pokerTrans = translations.Poker;

    /// <summary>
    /// Gets the current game state.
    /// </summary>
    private PokerGameState GameState => _stateMachine.CurrentState;

    /// <summary>
    /// Transitions to a new game state.
    /// </summary>
    private bool TransitionToState(PokerGameState newState) => _stateMachine.TransitionTo(newState);

    /// <summary>
    /// Forces a transition to a new game state (use with caution).
    /// </summary>
    private void ForceTransitionToState(PokerGameState newState) => _stateMachine.ForceTransitionTo(newState);

    /// <summary>
    /// Checks if currently in the specified state.
    /// </summary>
    private bool IsInState(PokerGameState state) => _stateMachine.IsInState(state);

    protected override int GetMinimumPlayers() => config.Poker.MinPlayers;

    protected override TimeSpan GetDelayBetweenRounds() => TimeSpan.FromSeconds(3);

    protected override TimeSpan GetDelayWaitingForPlayers() => TimeSpan.FromSeconds(1);

    protected override async Task ExecuteGameRoundAsync(CancellationToken token)
    {
        await StartNewHandAsync(token);
    }


    /// <summary>
    /// Starts a new poker hand.
    /// </summary>
    private async Task StartNewHandAsync(CancellationToken token)
    {
        ForceTransitionToState(PokerGameState.BetweenHands);
        _playersInHand = Players.Values.ToList();
        
        // Remove players with insufficient chips
        var playersToRemove = new List<PokerPlayer>();
        foreach (var player in _playersInHand)
        {
            // Check player's in-game chips, not their global credits
            if (player.Chips < config.Poker.SmallBlind * 2)
            {
                playersToRemove.Add(player);
            }
        }

        foreach (var player in playersToRemove)
        {
            PlayerLeave(player.Client);
        }

        _playersInHand = Players.Values.ToList();
        
        if (_playersInHand.Count < config.Poker.MinPlayers)
        {
            ForceTransitionToState(PokerGameState.WaitingForPlayers);
            
            // Notify remaining players that we're waiting for more
            if (_playersInHand.Count > 0)
            {
                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.NotEnoughPlayers
                ]);
            }
            
            ResetPlayersSignal();
            return;
        }

        // Rotate dealer button
        _dealerButtonPosition = (_dealerButtonPosition + 1) % _playersInHand.Count;
        
        // Reset players for new hand
        foreach (var player in _playersInHand)
        {
            player.ResetForNewHand();
            player.Position = _playersInHand.IndexOf(player);
            player.IsDealer = player.Position == _dealerButtonPosition;
        }

        // Set blinds positions (relative to dealer button in player list)
        var smallBlindIndex = (_dealerButtonPosition + 1) % _playersInHand.Count;
        var bigBlindIndex = (_dealerButtonPosition + 2) % _playersInHand.Count;
        
        _playersInHand[smallBlindIndex].IsSmallBlind = true;
        _playersInHand[bigBlindIndex].IsBigBlind = true;

        deckService.InitializeDeck();
        _communityCards.Clear();
        _currentRound.Reset();
        _totalPot = 0; // Reset total pot for new hand

        // Post blinds
        var smallBlindAmount = bettingService.PostSmallBlind(_playersInHand[smallBlindIndex], _currentRound);
        var bigBlindAmount = bettingService.PostBigBlind(_playersInHand[bigBlindIndex], _currentRound);
        _totalPot = smallBlindAmount + bigBlindAmount; // Initialize total pot with blinds

        // Split the long message into two lines for readability
        await output.TellPlayersAsync(_playersInHand, [
            _pokerTrans.LongPrefix(_pokerTrans.StartingHand.FormatExt(_playersInHand.Count)),
            _pokerTrans.Prefix(_pokerTrans.DealerButton.FormatExt(_playersInHand[_dealerButtonPosition].Client.CleanedName) + " | " +
                _pokerTrans.SmallBlindPosted.FormatExt(
                    _playersInHand[smallBlindIndex].Client.CleanedName, 
                    _playersInHand[smallBlindIndex].CurrentBet.ToString("N0")) + " | " +
                _pokerTrans.BigBlindPosted.FormatExt(
                    _playersInHand[bigBlindIndex].Client.CleanedName,
                    _playersInHand[bigBlindIndex].CurrentBet.ToString("N0")))
        ]);

        // Deal hole cards
        ForceTransitionToState(PokerGameState.PreFlop);
        foreach (var player in _playersInHand)
        {
            player.HoleCards = deckService.DealCards(2);
            await output.TellPlayerAsync(player, [
                _pokerTrans.YourCards.FormatExt(
                    string.Join(", ", player.HoleCards.Select(c => c.ToString())))
            ], false);
        }

        // Pre-flop betting round
        await ExecuteBettingRoundAsync(token);

        // Flop
        if (GetActivePlayers().Count > 1)
        {
            ForceTransitionToState(PokerGameState.Flop);
            _communityCards.AddRange(deckService.DealCards(3));
            await output.TellPlayersAsync(_playersInHand, [
                _pokerTrans.FlopDealt.FormatExt(
                    string.Join(", ", _communityCards.Select(c => c.ToString())))
            ]);
            await ExecuteBettingRoundAsync(token);
        }

        // Turn
        if (GetActivePlayers().Count > 1)
        {
            ForceTransitionToState(PokerGameState.Turn);
            _communityCards.Add(deckService.DealCard());
            await output.TellPlayersAsync(_playersInHand, [
                _pokerTrans.TurnDealt.FormatExt(_communityCards.Last().ToString())
            ]);
            await ExecuteBettingRoundAsync(token);
        }

        // River
        if (GetActivePlayers().Count > 1)
        {
            ForceTransitionToState(PokerGameState.River);
            _communityCards.Add(deckService.DealCard());
            await output.TellPlayersAsync(_playersInHand, [
                _pokerTrans.RiverDealt.FormatExt(_communityCards.Last().ToString())
            ]);
            await ExecuteBettingRoundAsync(token);
        }

        // Showdown
        await ExecuteShowdownAsync();
    }

    /// <summary>
    /// Executes a betting round.
    /// </summary>
    private async Task ExecuteBettingRoundAsync(CancellationToken token)
    {
        // Don't reset the pot - it accumulates across betting rounds
        _currentRound.CurrentBet = 0; // Reset current bet for new round
        _currentRound.IsComplete = false;
        var activePlayers = GetActivePlayers();
        
        foreach (var player in activePlayers)
        {
            player.ResetForNewRound();
        }

        // First action is after big blind (pre-flop) or small blind (post-flop)
        // Convert dealer position to active player index
        var dealerActiveIndex = activePlayers.FindIndex(p => p.Position == _dealerButtonPosition);
        var startIndex = IsInState(PokerGameState.PreFlop) 
            ? (dealerActiveIndex + 3) % activePlayers.Count 
            : (dealerActiveIndex + 1) % activePlayers.Count;
        
        if (startIndex < 0) startIndex = 0; // Fallback

        var actionIndex = startIndex;
        var roundComplete = false;

        while (!roundComplete && activePlayers.Count > 1)
        {
            var player = activePlayers[actionIndex];
            
            if (!player.HasActedThisRound || (player.CurrentBet < _currentRound.CurrentBet && player.Chips > 0 && !player.IsAllIn))
            {
                await ProcessPlayerActionAsync(player, token);
                player.HasActedThisRound = true;
            }

            actionIndex = (actionIndex + 1) % activePlayers.Count;
            
            // Check if we've completed a full round
            if (actionIndex == startIndex)
            {
                roundComplete = bettingService.IsBettingRoundComplete(activePlayers, _currentRound);
                if (!roundComplete)
                {
                    startIndex = actionIndex; // Reset for new round
                }
            }

            activePlayers = GetActivePlayers();
            if (activePlayers.Count <= 1) break;
        }

        // Collect bets into pot
        foreach (var player in _playersInHand)
        {
            _totalPot += player.CurrentBet;
            player.CurrentBet = 0;
        }

        await output.TellPlayersAsync(_playersInHand, [
            $"{_pokerTrans.BettingComplete} {_pokerTrans.PotSize.FormatExt(_totalPot.ToString("N0"))}"
        ]);
    }

    /// <summary>
    /// Processes a single player's action during betting round.
    /// </summary>
    private async Task ProcessPlayerActionAsync(PokerPlayer player, CancellationToken token)
    {
        var actionPrompt = inputConcrete.FormatAvailableActions(player, _currentRound, bettingService);

        // Tell OTHER players who we're waiting for
        var otherPlayers = _playersInHand.Where(p => p != player).ToList();
        if (otherPlayers.Count > 0)
        {
            await output.TellPlayersAsync(otherPlayers, [
                _pokerTrans.WaitingForPlayer.FormatExt(player.Client.CleanedName)
            ]);
        }

        // Tell the active player it's their turn with bright prompt
        await output.TellPlayerAsync(player, [
            _pokerTrans.ActionPrompt.FormatExt(actionPrompt),
            $"{_pokerTrans.CurrentBet.FormatExt(_currentRound.CurrentBet.ToString("N0"))} | {_pokerTrans.PotSize.FormatExt(_totalPot.ToString("N0"))}",
            _pokerTrans.YourChips.FormatExt(player.Chips.ToString("N0"))
        ], false);

        // Mark player as waiting for action
        _pendingActionPlayers[player.Client] = null; // Placeholder - actual handling done in HandleChatAsync
        
        // Wait for action with timeout
        using var timeoutSource = new CancellationTokenSource(config.Poker.TimeoutForPlayerAction);
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, token);
        
        var actionTaken = false;
        var actionCompleted = new TaskCompletionSource<bool>();

        // Store completion source for chat handler
        _pendingActionCompletions[player.Client] = actionCompleted;

        try
        {
            var completed = await Task.WhenAny(
                actionCompleted.Task,
                Task.Delay(config.Poker.TimeoutForPlayerAction, linkedToken.Token)
            );

            if (completed == actionCompleted.Task && await actionCompleted.Task)
            {
                actionTaken = true;
            }

            if (!actionTaken)
            {
                // Timeout - auto-fold
                await ExecuteActionAsync(player, PlayerAction.Fold, null);
                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.ActionTimeout.FormatExt(player.Client.CleanedName)
                ]);
            }
        }
        finally
        {
            _pendingActionPlayers.TryRemove(player.Client, out _);
            _pendingActionCompletions.TryRemove(player.Client, out _);
        }
    }

    /// <summary>
    /// Dictionary to track players waiting for action (used by HandleChatAsync).
    /// </summary>
    private readonly ConcurrentDictionary<EFClient, object?> _pendingActionPlayers = new();
    
    /// <summary>
    /// Dictionary to track action completion sources for timeout handling.
    /// </summary>
    private readonly ConcurrentDictionary<EFClient, TaskCompletionSource<bool>> _pendingActionCompletions = new();

    /// <summary>
    /// Executes a validated player action.
    /// </summary>
    private async Task ExecuteActionAsync(PokerPlayer player, PlayerAction action, long? raiseAmount)
    {
        var amountToCall = _currentRound.CurrentBet - player.CurrentBet;

        switch (action)
        {
            case PlayerAction.Fold:
                player.IsFolded = true;
                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.PlayerFolded.FormatExt(player.Client.CleanedName)
                ]);
                break;

            case PlayerAction.Check:
                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.PlayerChecked.FormatExt(player.Client.CleanedName)
                ]);
                break;

            case PlayerAction.Call:
                var callAmount = Math.Min(amountToCall, player.Chips);
                player.Chips -= callAmount;
                player.CurrentBet += callAmount;
                player.TotalInvestedThisHand += callAmount;
                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.PlayerCalled.FormatExt(player.Client.CleanedName, callAmount.ToString("N0"))
                ]);
                break;

            case PlayerAction.Raise:
                if (!raiseAmount.HasValue)
                {
                    raiseAmount = _currentRound.CurrentBet + (amountToCall > 0 ? amountToCall : config.Poker.BigBlind);
                }

                var raiseTotal = Math.Min(raiseAmount.Value, player.CurrentBet + player.Chips);
                var additionalBet = raiseTotal - player.CurrentBet;
                player.Chips -= additionalBet;
                player.CurrentBet = raiseTotal;
                player.TotalInvestedThisHand += additionalBet;
                _currentRound.CurrentBet = raiseTotal;
                
                // Reset acted status for other players (they need to respond to raise)
                foreach (var p in _playersInHand.Where(p => !p.IsFolded && p != player))
                {
                    if (p.CurrentBet < _currentRound.CurrentBet && !p.IsAllIn)
                    {
                        p.HasActedThisRound = false;
                    }
                }

                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.PlayerRaised.FormatExt(player.Client.CleanedName, raiseTotal.ToString("N0"))
                ]);
                break;

            case PlayerAction.AllIn:
                var allInAmount = player.Chips;
                player.Chips = 0;
                player.CurrentBet += allInAmount;
                player.TotalInvestedThisHand += allInAmount;
                player.IsAllIn = true;
                
                if (player.CurrentBet > _currentRound.CurrentBet)
                {
                    _currentRound.CurrentBet = player.CurrentBet;
                    foreach (var p in _playersInHand.Where(p => !p.IsFolded && p != player && !p.IsAllIn))
                    {
                        p.HasActedThisRound = false;
                    }
                }

                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.PlayerAllIn.FormatExt(player.Client.CleanedName, allInAmount.ToString("N0"))
                ]);
                break;
        }

        player.LastAction = action;
    }

    /// <summary>
    /// Handles the top-up action to add chips from player's credits.
    /// </summary>
    private async Task HandleTopUpAsync(PokerPlayer player)
    {
        var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
        
        if (credits <= 0)
        {
            await output.TellPlayerAsync(player, [_pokerTrans.TopUpNoCredits], false);
            return;
        }

        // Top up to minimum buy-in or use all available credits
        var topUpAmount = Math.Min(credits, config.Poker.MinimumBuyIn);
        
        await PersistenceService.RemoveCreditsAsync(player.Client, topUpAmount);
        player.Chips += topUpAmount;
        
        await output.TellPlayerAsync(player, [
            _pokerTrans.TopUpSuccess.FormatExt(topUpAmount.ToString("N0"), player.Chips.ToString("N0"))
        ], false);
    }

    /// <summary>
    /// Executes showdown and distributes pots.
    /// </summary>
    private async Task ExecuteShowdownAsync()
    {
        ForceTransitionToState(PokerGameState.Showdown);
        var activePlayers = GetActivePlayers();

        if (activePlayers.Count == 1)
        {
            // Single winner - no showdown needed
            var winner = activePlayers[0];
            // Collect any remaining bets into pot
            foreach (var player in _playersInHand)
            {
                _totalPot += player.CurrentBet;
            }
            winner.Chips += _totalPot;
            await output.TellPlayersAsync(_playersInHand, [
                _pokerTrans.PlayerWins.FormatExt(
                    winner.Client.CleanedName,
                    _totalPot.ToString("N0"),
                    "")
            ]);
            
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, winner.Client, _totalPot);
            // Don't cash out - chips persist at table until player leaves
            return;
        }

        // Collect any remaining bets into pot before calculating side pots
        foreach (var player in _playersInHand)
        {
            _totalPot += player.CurrentBet;
            player.CurrentBet = 0;
        }

        // Evaluate hands
        var playerHands = new Dictionary<PokerPlayer, PokerHand>();
        foreach (var player in activePlayers)
        {
            var hand = handEvaluator.EvaluateBestHand(player.HoleCards, _communityCards);
            playerHands[player] = hand;
        }

        // Calculate side pots - need to include total pot
        var sidePots = bettingService.CalculateSidePots(_playersInHand, _totalPot);
        
        // Show hands
        await output.TellPlayersAsync(_playersInHand, [_pokerTrans.Showdown]);
        foreach (var player in activePlayers)
        {
            var hand = playerHands[player];
            var handName = GetHandRankName(hand.Rank);
            await output.TellPlayersAsync(_playersInHand, [
                _pokerTrans.PlayerShowsHand.FormatExt(
                    player.Client.CleanedName,
                    string.Join(", ", player.HoleCards.Select(c => c.ToString())),
                    handName)
            ]);
        }

        // Distribute pots
        bettingService.DistributePot(sidePots, playerHands);

        // Announce winners
        foreach (var sidePot in sidePots)
        {
            var eligibleHands = sidePot.EligiblePlayers
                .Where(p => playerHands.ContainsKey(p))
                .ToDictionary(p => p, p => playerHands[p]);

            if (eligibleHands.Count == 0) continue;

            PokerHand? bestHand = null;
            foreach (var hand in eligibleHands.Values)
            {
                if (bestHand is null || hand.CompareTo(bestHand) > 0)
                {
                    bestHand = hand;
                }
            }
            var winners = eligibleHands
                .Where(kvp => kvp.Value.IsEqualTo(bestHand!))
                .Select(kvp => kvp.Key)
                .ToList();

            var handName = GetHandRankName(bestHand!.Rank);
            var amountPerWinner = sidePot.Amount / winners.Count;

            if (winners.Count == 1)
            {
                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.PlayerWins.FormatExt(
                        winners[0].Client.CleanedName,
                        amountPerWinner.ToString("N0"),
                        handName)
                ]);
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, winners[0].Client, amountPerWinner);
            }
            else
            {
                var winnerNames = string.Join(", ", winners.Select(w => w.Client.CleanedName));
                await output.TellPlayersAsync(_playersInHand, [
                    _pokerTrans.SplitPot.FormatExt(winnerNames, amountPerWinner.ToString("N0"))
                ]);
                foreach (var winner in winners)
                {
                    ICredifyEventService.RaiseEvent(ObjectiveType.Baller, winner.Client, amountPerWinner);
                }
            }
        }

        // Chips stay at the table - only cashed out when player leaves
    }

    /// <summary>
    /// Gets the active (non-folded, non-eliminated) players.
    /// </summary>
    private List<PokerPlayer> GetActivePlayers()
    {
        return _playersInHand.Where(p => !p.IsFolded && (p.Chips > 0 || p.IsAllIn)).ToList();
    }

    /// <summary>
    /// Gets the name of a hand rank for display.
    /// </summary>
    private string GetHandRankName(HandRank rank)
    {
        return rank switch
        {
            HandRank.RoyalFlush => _pokerTrans.HandRoyalFlush,
            HandRank.StraightFlush => _pokerTrans.HandStraightFlush,
            HandRank.FourOfAKind => _pokerTrans.HandFourOfAKind,
            HandRank.FullHouse => _pokerTrans.HandFullHouse,
            HandRank.Flush => _pokerTrans.HandFlush,
            HandRank.Straight => _pokerTrans.HandStraight,
            HandRank.ThreeOfAKind => _pokerTrans.HandThreeOfAKind,
            HandRank.TwoPair => _pokerTrans.HandTwoPair,
            HandRank.Pair => _pokerTrans.HandPair,
            _ => _pokerTrans.HandHighCard
        };
    }

    /// <summary>
    /// IActiveGame implementation - handles chat messages from players during gameplay.
    /// </summary>
    public override async Task HandleChatAsync(EFClient client, string message)
    {
        if (!_pendingActionPlayers.ContainsKey(client))
        {
            return; // Player not waiting for action
        }

        if (!Players.TryGetValue(client, out var player))
        {
            return;
        }

        if (!_pendingActionCompletions.TryGetValue(client, out var completion))
        {
            return;
        }

        try
        {
            await _actionLock.WaitAsync();

            var parseResult = input.Parse(message);
            var availableActions = inputConcrete.FormatAvailableActions(player, _currentRound, bettingService);
            
            if (!parseResult.IsValid || parseResult.Result is null)
            {
                await output.TellPlayerAsync(player, [
                    _pokerTrans.InvalidAction.FormatExt(availableActions)
                ], false);
                return;
            }

            var action = parseResult.Result.Action;
            var raiseAmount = parseResult.Result.RaiseAmount;

            // Handle TopUp action separately (not a betting action)
            if (action == PlayerAction.TopUp)
            {
                await HandleTopUpAsync(player);
                return; // Don't mark action as complete, player still needs to act
            }

            // Try the parsed action first
            var (actionValid, actionErrorMsg) = actionValidator.ValidateAction(
                player, action, raiseAmount, _currentRound);

            // If Check/Call failed, try the other one (both use 'c')
            if (!actionValid && (action == PlayerAction.Check || action == PlayerAction.Call))
            {
                var alternateAction = action == PlayerAction.Check ? PlayerAction.Call : PlayerAction.Check;
                var (altValid, _) = actionValidator.ValidateAction(player, alternateAction, null, _currentRound);
                if (altValid)
                {
                    action = alternateAction;
                    actionValid = true;
                    actionErrorMsg = null;
                }
            }

            if (!actionValid)
            {
                await output.TellPlayerAsync(player, [
                    (actionErrorMsg ?? _pokerTrans.InvalidAction) + $" Available: {availableActions}"
                ], false);
                return;
            }

            // Execute action
            await ExecuteActionAsync(player, action, raiseAmount);
            completion.TrySetResult(true);
        }
        finally
        {
            if (_actionLock.CurrentCount == 0) _actionLock.Release();
        }
    }

    /// <summary>
    /// Adds a player to the waiting list.
    /// </summary>
    public async Task<bool> PlayerJoinAsync(PokerPlayer player, long buyIn)
    {
        var added = Players.TryAdd(player.Client, player);
        if (added)
        {
            player.Chips = buyIn;
            await PersistenceService.RemoveCreditsAsync(player.Client, buyIn);
            
            SignalPlayersAvailable();

            if (IsInState(PokerGameState.WaitingForPlayers))
            {
                await output.TellPlayerAsync(player, [_pokerTrans.JoinGame], false);
            }
            else
            {
                await output.TellPlayerAsync(player, [
                    _pokerTrans.JoinGame,
                    _pokerTrans.WaitingForPlayers.FormatExt(
                        Math.Max(0, config.Poker.MinPlayers - Players.Count))
                ], false);
            }
        }

        return added;
    }

    /// <summary>
    /// Removes a player from the game.
    /// </summary>
    public void PlayerLeave(EFClient client)
    {
        if (Players.TryRemove(client, out var player))
        {
            // If player has chips, return them
            if (player.Chips > 0)
            {
                Task.Run(async () => await PersistenceService.AddCreditsAsync(client, player.Chips));
            }

            _playersInHand.RemoveAll(p => p.Client.Equals(client));
            _pendingActionPlayers.TryRemove(client, out _);

            ResetPlayersSignal();
        }
    }

    /// <summary>
    /// Checks if a player is in the game.
    /// </summary>
    public bool IsPlayerInGame(EFClient client) => IsPlayerPlaying(client);

    /// <summary>
    /// IActiveGame implementation - allows a player to join the game with a buy-in.
    /// </summary>
    public override async Task JoinGameAsync(EFClient player)
    {
        var buyIn = Config.Poker.MinimumBuyIn;
        var pokerPlayer = new PokerPlayer(player, 0);
        await PlayerJoinAsync(pokerPlayer, buyIn);
        OnPlayerJoined();
    }

    /// <summary>
    /// IActiveGame implementation - removes a player from the game.
    /// </summary>
    public override Task LeaveGameAsync(EFClient player)
    {
        PlayerLeave(player);
        OnPlayerLeft();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Shows cards and game state to a player (for cards/river commands).
    /// </summary>
    public async Task ShowCardsAsync(EFClient client, bool showRiverOnly = false)
    {
        if (!Players.TryGetValue(client, out var player))
        {
            return; // Player not in game
        }

        var messages = new List<string>();

        // Show hole cards (unless river-only)
        if (!showRiverOnly && player.HoleCards != null && player.HoleCards.Count > 0)
        {
            messages.Add(_pokerTrans.YourCards.FormatExt(
                string.Join(", ", player.HoleCards.Select(c => c.ToString()))));
        }

        // Show community cards if they exist
        if (_communityCards.Count > 0)
        {
            messages.Add(_pokerTrans.CommunityCards.FormatExt(
                string.Join(", ", _communityCards.Select(c => c.ToString()))));
        }

        // Show betting info
        if (messages.Count > 0)
        {
            messages.Add($"{_pokerTrans.PotSize.FormatExt(_totalPot.ToString("N0"))} | {_pokerTrans.CurrentBet.FormatExt(_currentRound.CurrentBet.ToString("N0"))} | {_pokerTrans.YourChips.FormatExt(player.Chips.ToString("N0"))}");
            await output.TellPlayerAsync(player, messages, false);
        }
    }

}
