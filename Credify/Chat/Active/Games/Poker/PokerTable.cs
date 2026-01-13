using System.Collections.Concurrent;
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
    PokerHandleInput input,
    PokerHandleOutput output,
    PokerDeckService deckService,
    PokerHandEvaluator handEvaluator,
    PokerBettingService bettingService,
    PokerActionValidator actionValidator)
{
    private readonly ConcurrentDictionary<EFClient, PokerPlayer> _waitingPlayers = [];
    private List<PokerPlayer> _playersInHand = [];
    private List<PokerCard> _communityCards = [];
    private BettingRound _currentRound = new();
    private long _totalPot = 0; // Accumulated pot across all betting rounds in a hand
    private PokerGameState _gameState = PokerGameState.WaitingForPlayers;
    private int _dealerButtonPosition = -1;
    private readonly ManualResetEventSlim _hasPlayers = new(false);
    private readonly SemaphoreSlim _actionLock = new(1, 1);
    private readonly PokerTranslations _pokerTrans = translations.Poker;

    /// <summary>
    /// Main game loop - runs continuously.
    /// </summary>
    public async Task GameLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _hasPlayers.Wait(token);
            
            if (_waitingPlayers.Count < config.Poker.MinPlayers)
            {
                await Task.Delay(1000, token);
                continue;
            }

            await StartNewHandAsync(token);
            
            // Brief pause between hands
            if (_waitingPlayers.Count >= config.Poker.MinPlayers)
            {
                await Task.Delay(3000, token);
            }
        }
    }

    /// <summary>
    /// Starts a new poker hand.
    /// </summary>
    private async Task StartNewHandAsync(CancellationToken token)
    {
        _gameState = PokerGameState.BetweenHands;
        _playersInHand = _waitingPlayers.Values.ToList();
        
        // Remove players with insufficient chips
        var playersToRemove = new List<PokerPlayer>();
        foreach (var player in _playersInHand)
        {
            var credits = await persistenceService.GetClientCreditsAsync(player.Client);
            if (credits < config.Poker.SmallBlind * 2)
            {
                playersToRemove.Add(player);
            }
        }

        foreach (var player in playersToRemove)
        {
            PlayerLeave(player.Client);
        }

        _playersInHand = _waitingPlayers.Values.ToList();
        
        if (_playersInHand.Count < config.Poker.MinPlayers)
        {
            _gameState = PokerGameState.WaitingForPlayers;
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

        await output.TellPlayersAsync(_playersInHand, [
            _pokerTrans.LongPrefix(_pokerTrans.StartingHand.FormatExt(_playersInHand.Count)),
            _pokerTrans.DealerButton.FormatExt(_playersInHand[_dealerButtonPosition].Client.CleanedName)
        ]);

        // Post blinds
        var smallBlindAmount = bettingService.PostSmallBlind(_playersInHand[smallBlindIndex], _currentRound);
        var bigBlindAmount = bettingService.PostBigBlind(_playersInHand[bigBlindIndex], _currentRound);
        _totalPot = smallBlindAmount + bigBlindAmount; // Initialize total pot with blinds

        await output.TellPlayersAsync(_playersInHand, [
            _pokerTrans.SmallBlindPosted.FormatExt(
                _playersInHand[smallBlindIndex].Client.CleanedName, 
                _playersInHand[smallBlindIndex].CurrentBet.ToString("N0")),
            _pokerTrans.BigBlindPosted.FormatExt(
                _playersInHand[bigBlindIndex].Client.CleanedName,
                _playersInHand[bigBlindIndex].CurrentBet.ToString("N0"))
        ]);

        // Deal hole cards
        _gameState = PokerGameState.PreFlop;
        foreach (var player in _playersInHand)
        {
            player.HoleCards = deckService.DealCards(2);
            await output.TellPlayerAsync(player, [
                _pokerTrans.YourCards.FormatExt(
                    string.Join(", ", player.HoleCards.Select(c => c.ToString())))
            ], false);
        }

        await output.TellPlayersAsync(_playersInHand, [_pokerTrans.CardsDealt]);

        // Pre-flop betting round
        await ExecuteBettingRoundAsync(token);

        // Flop
        if (GetActivePlayers().Count > 1)
        {
            _gameState = PokerGameState.Flop;
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
            _gameState = PokerGameState.Turn;
            _communityCards.Add(deckService.DealCard());
            await output.TellPlayersAsync(_playersInHand, [
                _pokerTrans.TurnDealt.FormatExt(_communityCards.Last().ToString())
            ]);
            await ExecuteBettingRoundAsync(token);
        }

        // River
        if (GetActivePlayers().Count > 1)
        {
            _gameState = PokerGameState.River;
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
        var startIndex = _gameState == PokerGameState.PreFlop 
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
            _pokerTrans.BettingComplete,
            _pokerTrans.PotSize.FormatExt(_totalPot.ToString("N0"))
        ]);
    }

    /// <summary>
    /// Processes a single player's action during betting round.
    /// </summary>
    private async Task ProcessPlayerActionAsync(PokerPlayer player, CancellationToken token)
    {
        var actionPrompt = input.FormatAvailableActions(player, _currentRound, bettingService);

        await output.TellPlayerAsync(player, [
            _pokerTrans.ActionPrompt.FormatExt(actionPrompt),
            _pokerTrans.CurrentBet.FormatExt(_currentRound.CurrentBet.ToString("N0")),
            _pokerTrans.PotSize.FormatExt(_totalPot.ToString("N0")),
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
    /// Executes showdown and distributes pots.
    /// </summary>
    private async Task ExecuteShowdownAsync()
    {
        _gameState = PokerGameState.Showdown;
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
            await persistenceService.AddCreditsAsync(winner.Client, winner.Chips);
            winner.Chips = 0;
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

        // Cash out chips to credits
        foreach (var player in _playersInHand)
        {
            if (player.Chips > 0)
            {
                await persistenceService.AddCreditsAsync(player.Client, player.Chips);
                player.Chips = 0;
            }
            
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Client);
        }
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
    /// Handles a chat message from a player (called by PokerManager).
    /// </summary>
    public async Task HandleChatAsync(EFClient client, string message)
    {
        if (!_pendingActionPlayers.ContainsKey(client))
        {
            return; // Player not waiting for action
        }

        if (!_waitingPlayers.TryGetValue(client, out var player))
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

            var (isValid, action, raiseAmount, errorMessage) = input.ParseAction(message);
            
            if (!isValid || !action.HasValue)
            {
                await output.TellPlayerAsync(player, [
                    _pokerTrans.Prefix(errorMessage ?? _pokerTrans.InvalidAction)
                ], false);
                return;
            }

            var (actionValid, actionErrorMsg) = actionValidator.ValidateAction(player, action.Value, raiseAmount, _currentRound);
            if (!actionValid)
            {
                await output.TellPlayerAsync(player, [
                    _pokerTrans.Prefix(actionErrorMsg ?? _pokerTrans.InvalidAction)
                ], false);
                return;
            }

            // Execute action
            await ExecuteActionAsync(player, action.Value, raiseAmount);
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
        var added = _waitingPlayers.TryAdd(player.Client, player);
        if (added)
        {
            player.Chips = buyIn;
            await persistenceService.RemoveCreditsAsync(player.Client, buyIn);
            
            if (!_hasPlayers.IsSet)
            {
                _hasPlayers.Set();
            }

            if (_gameState == PokerGameState.WaitingForPlayers)
            {
                await output.TellPlayerAsync(player, [_pokerTrans.JoinGame], false);
            }
            else
            {
                await output.TellPlayerAsync(player, [
                    _pokerTrans.JoinGame,
                    _pokerTrans.WaitingForPlayers.FormatExt(
                        Math.Max(0, config.Poker.MinPlayers - _waitingPlayers.Count))
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
        if (_waitingPlayers.TryRemove(client, out var player))
        {
            // If player has chips, return them
            if (player.Chips > 0)
            {
                Task.Run(async () => await persistenceService.AddCreditsAsync(client, player.Chips));
            }

            _playersInHand.RemoveAll(p => p.Client.Equals(client));
            _pendingActionPlayers.TryRemove(client, out _);

            if (_waitingPlayers.IsEmpty)
            {
                _hasPlayers.Reset();
            }
        }
    }

    /// <summary>
    /// Checks if a player is in the game.
    /// </summary>
    public bool IsPlayerInGame(EFClient client) => _waitingPlayers.ContainsKey(client);

    /// <summary>
    /// Gets the current number of players.
    /// </summary>
    public int GetPlayerCount() => _waitingPlayers.Count;
}
