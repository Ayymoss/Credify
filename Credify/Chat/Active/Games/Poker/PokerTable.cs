using System.Collections.Concurrent;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
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
    private readonly List<PokerCard> _communityCards = [];
    private readonly BettingRound _currentRound = new();
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

    protected override int GetMinimumPlayers() => Config.Poker.MinPlayers;

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

        // Check players with insufficient chips and offer top-up
        var playersToRemove = new List<PokerPlayer>();
        foreach (var player in _playersInHand)
        {
            // Check player's in-game chips
            if (player.Chips < Config.Poker.SmallBlind * 2)
            {
                // No auto-topup - declare elimination immediately
                await output.TellPlayerAsync(player, [
                    _pokerTrans.PlayerEliminated.FormatExt(player.Client.CleanedName),
                    "Type /join to join again."
                ], false);
                playersToRemove.Add(player);
            }
        }

        foreach (var player in playersToRemove)
        {
            PlayerLeave(player.Client);
        }

        _playersInHand = Players.Values.ToList();

        if (_playersInHand.Count < Config.Poker.MinPlayers)
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
        // Set blinds positions (relative to dealer button in player list)
        int smallBlindIndex, bigBlindIndex;

        if (_playersInHand.Count == 2)
        {
            // Heads-Up: Dealer is Small Blind, Other is Big Blind
            smallBlindIndex = _dealerButtonPosition;
            bigBlindIndex = (_dealerButtonPosition + 1) % _playersInHand.Count;
        }
        else
        {
            // Standard Ring Game
            smallBlindIndex = (_dealerButtonPosition + 1) % _playersInHand.Count;
            bigBlindIndex = (_dealerButtonPosition + 2) % _playersInHand.Count;
        }

        _playersInHand[smallBlindIndex].IsSmallBlind = true;
        _playersInHand[bigBlindIndex].IsBigBlind = true;

        deckService.InitializeDeck();
        _communityCards.Clear();
        _currentRound.Reset();
        _totalPot = 0; // Reset total pot for new hand

        // Post blinds
        var smallBlindAmount = bettingService.PostSmallBlind(_playersInHand[smallBlindIndex], _currentRound);
        var bigBlindAmount = bettingService.PostBigBlind(_playersInHand[bigBlindIndex], _currentRound);
        // Total pot will be updated when betting round completes to avoid double counting

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
        await ExecuteBettingRoundAsync(token, false);

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
    private async Task ExecuteBettingRoundAsync(CancellationToken token, bool resetPlayers = true)
    {
        // Don't reset the pot - it accumulates across betting rounds
        _currentRound.CurrentBet = _playersInHand.Max(p => p.CurrentBet); // Reset current bet for new round (respecting blinds)
        _currentRound.IsComplete = false;
        var activePlayers = GetActivePlayers();

        if (resetPlayers)
        {
            foreach (var player in activePlayers)
            {
                player.ResetForNewRound();
            }
        }

        // Check if all active players are all-in - no betting needed
        var playersWhoCanAct = activePlayers.Where(p => !p.IsAllIn && p.Chips > 0).ToList();
        if (playersWhoCanAct.Count <= 1)
        {
            // All players are all-in or only one can act - skip betting
            return;
        }

        // First action is after big blind (pre-flop) or small blind (post-flop)
        // Convert dealer position to active player index
        // Calculate starting player
        var dealerActiveIndex = activePlayers.FindIndex(p => p.Position == _dealerButtonPosition);
        int startIndex;

        if (IsInState(PokerGameState.PreFlop))
        {
            // Pre-Flop
            if (activePlayers.Count == 2)
            {
                // Heads-Up: Dealer (SB) acts first
                startIndex = dealerActiveIndex;
            }
            else
            {
                // Ring: Player after BB acts first (Dealer + 3)
                startIndex = (dealerActiveIndex + 3) % activePlayers.Count;
            }
        }
        else
        {
            // Post-Flop: Player after Dealer acts first (SB in Ring, BB in Heads-Up)
            startIndex = (dealerActiveIndex + 1) % activePlayers.Count;
        }

        if (startIndex < 0) startIndex = 0; // Fallback

        var actionIndex = startIndex;
        var roundComplete = false;

        while (!roundComplete && activePlayers.Count > 1)
        {
            var player = activePlayers[actionIndex];

            // Only ask for action if player can actually act (not all-in, has chips)
            var canAct = !player.IsAllIn && player.Chips > 0;
            var needsToAct = !player.HasActedThisRound ||
                             (player.CurrentBet < _currentRound.CurrentBet && player.Chips > 0 && !player.IsAllIn);

            if (canAct && needsToAct)
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
                    // Check if there are still players who can act
                    playersWhoCanAct = activePlayers.Where(p => !p.IsAllIn && p.Chips > 0).ToList();
                    if (playersWhoCanAct.Count <= 1)
                    {
                        roundComplete = true; // No more betting possible
                    }
                    else
                    {
                        startIndex = actionIndex; // Reset for new round
                    }
                }
            }

            activePlayers = GetActivePlayers();
            if (activePlayers.Count <= 1) break;
        }

        // Return uncalled bets if any (e.g. All-In overshoot)
        var refunds = bettingService.ReturnUncalledBets(_playersInHand);
        foreach (var (player, amount) in refunds)
        {
            await output.TellPlayersAsync(_playersInHand, [
                _pokerTrans.Prefix($"{player.Client.CleanedName} was refunded {amount:N0} (uncalled bet)")
            ]);
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
        var actionPrompt = inputConcrete.FormatAvailableActions(player, _currentRound);

        // Tell OTHER players who we're waiting for (with prefix)
        var otherPlayers = _playersInHand.Where(p => p != player).ToList();
        foreach (var other in otherPlayers)
        {
            await output.TellPlayerAsync(other, [
                _pokerTrans.WaitingForPlayer.FormatExt(player.Client.CleanedName, Config.Poker.TimeoutForPlayerAction.TotalSeconds)
            ], false);
        }

        // Tell the active player it's their turn - split into separate lines for clarity
        await output.TellPlayerAsync(player, [
            "(Color::Red)>>> YOUR TURN <<<",
            actionPrompt,
            $"{_pokerTrans.CurrentBet.FormatExt(_currentRound.CurrentBet.ToString("N0"))} | {_pokerTrans.PotSize.FormatExt(_totalPot.ToString("N0"))}",
            _pokerTrans.YourChips.FormatExt(player.Chips.ToString("N0"))
        ], false);

        // Mark player as waiting for action
        _pendingActionPlayers[player.Client] = null; // Placeholder - actual handling done in HandleChatAsync

        // Wait for action with timeout
        using var timeoutSource = new CancellationTokenSource(Config.Poker.TimeoutForPlayerAction);
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, token);

        var actionTaken = false;
        var actionCompleted = new TaskCompletionSource<bool>();

        // Store completion source for chat handler
        _pendingActionCompletions[player.Client] = actionCompleted;

        try
        {
            var completed = await Task.WhenAny(
                actionCompleted.Task,
                Task.Delay(Config.Poker.TimeoutForPlayerAction, linkedToken.Token)
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
                // UX Guard: Prevent accidental fold if player can check for free
                if (amountToCall <= 0)
                {
                    // Auto-convert to Check for better UX, or just warn. 
                    // Auto-converting is safer/friendlier for accidentally typing 'f'
                    await output.TellPlayerAsync(player, [
                        _pokerTrans.Prefix("You can Check for free! Action converted to Check.")
                    ], false);
                    goto case PlayerAction.Check;
                }

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
                    raiseAmount = _currentRound.CurrentBet + (amountToCall > 0 ? amountToCall : Config.Poker.BigBlind);
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
        var topUpAmount = Math.Min(credits, Config.Poker.MinimumBuyIn);

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

        // Calculate side pots
        var sidePots = bettingService.CalculateSidePots(_playersInHand, _totalPot);

        // Prepare consolidated output messages
        var outputMessages = new List<string>();

        // 1. Header & Community Cards
        outputMessages.Add(_pokerTrans.Showdown);
        outputMessages.Add(_pokerTrans.CommunityCards.FormatExt(string.Join(", ", _communityCards.Select(c => c.ToString()))));

        // 2. Player Hands
        foreach (var player in activePlayers)
        {
            var hand = playerHands[player];
            var handName = GetDescriptiveHandName(hand);
            outputMessages.Add(_pokerTrans.PlayerShowsHand.FormatExt(
                player.Client.CleanedName,
                string.Join(", ", player.HoleCards.Select(c => c.ToString())),
                handName));
        }

        // 3. Logic: Distribute pots
        bettingService.DistributePot(sidePots, playerHands);

        // 4. Winners
        var eventWinners = new List<(EFClient Client, long Amount)>();

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

            var handName = GetDescriptiveHandName(bestHand!);
            var amountPerWinner = sidePot.Amount / winners.Count;

            if (winners.Count == 1)
            {
                outputMessages.Add(_pokerTrans.PlayerWins.FormatExt(
                    winners[0].Client.CleanedName,
                    amountPerWinner.ToString("N0"),
                    handName));
                eventWinners.Add((winners[0].Client, amountPerWinner));
            }
            else
            {
                var winnerNames = string.Join(", ", winners.Select(w => w.Client.CleanedName));
                outputMessages.Add(_pokerTrans.SplitPot.FormatExt(winnerNames, amountPerWinner.ToString("N0")));
                foreach (var winner in winners)
                {
                    eventWinners.Add((winner.Client, amountPerWinner));
                }
            }
        }

        // Send all messages in one batch
        await output.TellPlayersAsync(_playersInHand, outputMessages);

        // Raise events (silent)
        foreach (var (client, amount) in eventWinners)
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, amount);
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
    private string GetDescriptiveHandName(PokerHand hand)
    {
        var rankName = GetHandRankName(hand.Rank);
        if (hand.Kickers.Count == 0) return rankName;

        string GetRankName(int value)
        {
            return value switch
            {
                14 => "Ace",
                13 => "King",
                12 => "Queen",
                11 => "Jack",
                _ => value.ToString()
            };
        }

        switch (hand.Rank)
        {
            case HandRank.HighCard:
                return $"{rankName} {GetRankName(hand.Kickers[0])}";
            case HandRank.Pair:
                return $"{rankName} of {GetRankName(hand.Kickers[0])}s";
            case HandRank.TwoPair:
                return $"{rankName} - {GetRankName(hand.Kickers[0])}s and {GetRankName(hand.Kickers[1])}s";
            case HandRank.ThreeOfAKind:
                return $"{rankName} - {GetRankName(hand.Kickers[0])}s";
            case HandRank.Straight:
            case HandRank.Flush:
            case HandRank.StraightFlush:
                return $"{rankName} ({GetRankName(hand.Kickers[0])} High)";
            case HandRank.FullHouse:
                return $"{rankName} - {GetRankName(hand.Kickers[0])}s full of {GetRankName(hand.Kickers[1])}s";
            case HandRank.FourOfAKind:
                return $"{rankName} - {GetRankName(hand.Kickers[0])}s";
            default:
                return rankName;
        }
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
            var availableActions = inputConcrete.FormatAvailableActions(player, _currentRound);

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
                        Math.Max(0, Config.Poker.MinPlayers - Players.Count))
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

            // CRITICAL: Cancel any pending action so the game doesn't wait forever
            if (_pendingActionCompletions.TryRemove(client, out var completion))
            {
                completion.TrySetResult(false); // Signal action not taken (will trigger fold)
            }

            // If we dropped below minimum players, notify remaining players and transition state
            if (Players.Count > 0 && Players.Count < Config.Poker.MinPlayers)
            {
                ForceTransitionToState(PokerGameState.WaitingForPlayers);

                // Notify remaining players that we're waiting for more
                Task.Run(async () =>
                {
                    await output.TellPlayersAsync(Players.Values.ToList(), [
                        _pokerTrans.NotEnoughPlayers
                    ]);
                });
            }

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
            messages.Add(
                $"{_pokerTrans.PotSize.FormatExt(_totalPot.ToString("N0"))} | {_pokerTrans.CurrentBet.FormatExt(_currentRound.CurrentBet.ToString("N0"))} | {_pokerTrans.YourChips.FormatExt(player.Chips.ToString("N0"))}");
            await output.TellPlayerAsync(player, messages, false);
        }
    }
}
