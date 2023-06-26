using System.Collections.Concurrent;
using System.Text;
using Credify.ChatGames.Models;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Blackjack;

public class BlackjackGame
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;
    private BlackjackEnums.GameState _gameState;
    private readonly ConcurrentDictionary<EFClient, BlackJackPlayer> _players;
    private readonly ConcurrentDictionary<EFClient, BlackjackHand> _activePlayers;
    private List<BlackjackCard> _houseHand = new();
    private ConcurrentQueue<BlackjackCard> _deck = new();
    private CancellationTokenSource _playerStakesToken = new();
    private CancellationTokenSource _dealerPlaysToken = new();
    private readonly SemaphoreSlim _chatLock = new(1, 1);
    private readonly SemaphoreSlim _startGameLock = new(1, 1);

    public BlackjackGame(PersistenceManager persistenceManager, CredifyConfiguration credifyConfig)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        _players = new ConcurrentDictionary<EFClient, BlackJackPlayer>();
        _activePlayers = new ConcurrentDictionary<EFClient, BlackjackHand>();
        _gameState = BlackjackEnums.GameState.WaitingForPlayers;
    }

    #region Core

    private async Task StartGameAsync()
    {
        if (_gameState != BlackjackEnums.GameState.WaitingForPlayers) return;
        if (_players.IsEmpty) return;

        _gameState = BlackjackEnums.GameState.RequestPlayerStakes;
        _houseHand = new List<BlackjackCard>();
        _deck = ResetDeck();
        _houseHand.Add(await DrawCardAsync());
        _houseHand.Add(await DrawCardAsync());

        foreach (var player in _players) player.Value.Queued = false;

        var activePlayers = _players
            .Where(x => !x.Value.Queued)
            .ToDictionary(x => x.Key, x => x.Value);

        foreach (var player in activePlayers) _activePlayers.TryAdd(player.Key, new BlackjackHand());

        Utilities.ExecuteAfterDelay(TimeSpan.FromSeconds(1), RequestPlayerStakesAsync, CancellationToken.None);
    }

    private async Task RequestPlayerStakesAsync(CancellationToken token)
    {
        _gameState = BlackjackEnums.GameState.RequestPlayerStakes;

        var insufficientFunds = new List<EFClient>();
        foreach (var player in _activePlayers)
        {
            player.Value.State = BlackjackEnums.PlayerState.Playing;
            var playerFunds = await _persistenceManager.GetClientCreditsAsync(player.Key);
            if (playerFunds < 10)
            {
                insufficientFunds.Add(player.Key);
                continue;
            }

            player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitle} " +
                            $"{_credifyConfig.Translations.BlackjackPlaceBets.FormatExt($"{playerFunds:N0}")}");
        }

        foreach (var player in insufficientFunds)
        {
            _activePlayers.TryRemove(player, out _);
            _players.TryRemove(player, out _);
            player.Tell($"{_credifyConfig.Translations.BlackjackTitle} " +
                        $"{_credifyConfig.Translations.BlackjackInsufficientFunds}");
        }

        if (_activePlayers.IsEmpty)
        {
            await EndGameAsync(CancellationToken.None);
            return;
        }

        Utilities.ExecuteAfterDelay(_credifyConfig.Blackjack.TimeoutForPlayerActions, DealCardsAsync,
            _playerStakesToken.Token);
    }

    private async Task DealCardsAsync(CancellationToken token)
    {
        if (_gameState is not BlackjackEnums.GameState.RequestPlayerStakes) return;
        _gameState = BlackjackEnums.GameState.DealCards;

        var noBets = _players.Where(x => x.Value.Stake is null)
            .Where(x => _activePlayers.ContainsKey(x.Key))
            .Select(player => player.Key)
            .ToList();

        foreach (var player in noBets)
        {
            _activePlayers.TryRemove(player, out _);
            _players.TryRemove(player, out _);
            player.Tell($"{_credifyConfig.Translations.BlackjackTitle} " +
                        $"{_credifyConfig.Translations.BlackjackBetTimeout}");
        }

        if (_activePlayers.IsEmpty)
        {
            await EndGameAsync(CancellationToken.None);
            return;
        }

        foreach (var player in _activePlayers)
        {
            player.Value.Cards = new List<BlackjackCard>
            {
                await DrawCardAsync(),
                await DrawCardAsync()
            };
            await player.Key.TellAsync(new[]
            {
                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackjackDealerInitialCard.FormatExt(_houseHand[0])}",
                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackjackPlayerCards.FormatExt(CalculateHandValue(player.Value.Cards), string.Join(", ", player.Value.Cards.Select(x => x.ToString())))}"
            }, token);
        }

        _playerStakesToken.Cancel();
        await RequestPlayerDecisionsAsync();
    }

    private async Task RequestPlayerDecisionsAsync()
    {
        _gameState = BlackjackEnums.GameState.RequestPlayerDecisions;

        foreach (var player in _activePlayers)
        {
            if (CalculateHandValue(player.Value.Cards) is 21)
            {
                player.Value.State = BlackjackEnums.PlayerState.Stand;
                player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                $"{_credifyConfig.Translations.BlackjackBlackjackConfirmation}");
                var decisionStateRemainders = GetDecisionStateRemainders();
                if (decisionStateRemainders.Count(x => !Equals(x, player.Key)) is not 0)
                    player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                    $"{_credifyConfig.Translations.BlackjackPlayersDeciding.FormatExt(decisionStateRemainders.Count)}");
                continue;
            }

            player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                            $"{_credifyConfig.Translations.BlackJackPlayerDecision}");
        }

        var decisionStateRemaindersOverride = GetDecisionStateRemainders();
        if (decisionStateRemaindersOverride.Count is 0)
        {
            await DealerPlaysAsync(CancellationToken.None);
            return;
        }

        Utilities.ExecuteAfterDelay(_credifyConfig.Blackjack.TimeoutForPlayerActions, DealerPlaysAsync,
            _dealerPlaysToken.Token);
    }

    private async Task DealerPlaysAsync(CancellationToken token)
    {
        if (_gameState is not BlackjackEnums.GameState.RequestPlayerDecisions) return;
        _gameState = BlackjackEnums.GameState.DealerPlays;

        while (CalculateHandValue(_houseHand) < 17)
        {
            _houseHand.Add(await DrawCardAsync());
        }

        foreach (var player in _activePlayers)
        {
            await player.Key.TellAsync(new[]
            {
                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackjackDealerCards.FormatExt(CalculateHandValue(_houseHand), string.Join(", ", _houseHand.Select(x => x.ToString())))}",
                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackjackPlayerCards.FormatExt(CalculateHandValue(player.Value.Cards), string.Join(", ", player.Value.Cards.Select(x => x.ToString())))}"
            }, token);
        }

        foreach (var player in _activePlayers)
        {
            var houseValue = CalculateHandValue(_houseHand);
            var playerValue = CalculateHandValue(player.Value.Cards);

            if (playerValue > 21)
            {
                player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                $"{_credifyConfig.Translations.BlackJackPlayerBustConfirmation}");
                player.Value.Outcome = BlackjackEnums.GameOutcome.Lose;
                _players[player.Key].Payout = 0;
                continue;
            }

            if (IsBlackjack(player.Value.Cards))
            {
                if (IsBlackjack(_houseHand))
                {
                    player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                    $"{_credifyConfig.Translations.BlackjackBlackjackPush}");
                    player.Value.Outcome = BlackjackEnums.GameOutcome.Push;
                    _players[player.Key].Payout = _players[player.Key].Stake;
                }
                else
                {
                    _players[player.Key].Payout = Convert.ToInt64(Math.Round(_players[player.Key].Stake!.Value *
                                                                             _credifyConfig.Blackjack.PayoutBlackjack));
                    player.Value.Outcome = BlackjackEnums.GameOutcome.Blackjack;
                    foreach (var server in player.Key.CurrentServer.Manager.GetServers())
                    {
                        if (server.ConnectedClients.Count is 0) continue;
                        server.Broadcast($"{_credifyConfig.Translations.BlackjackTitle} " +
                                         $"{_credifyConfig.Translations.BlackjackAnnouncement.FormatExt(player.Key.CleanedName, $"{_players[player.Key].Payout - _players[player.Key].Stake:N0}")}");
                    }
                }

                continue;
            }

            if (houseValue > 21)
            {
                player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                $"{_credifyConfig.Translations.BlackjackDealerBust.FormatExt(houseValue)}");
                player.Value.Outcome = BlackjackEnums.GameOutcome.Win;
                _players[player.Key].Payout = Convert.ToInt64(Math.Round(_players[player.Key].Stake!.Value *
                                                                         _credifyConfig.Blackjack.PayoutDealerBust));
                continue;
            }

            if (houseValue < playerValue)
            {
                player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                $"{_credifyConfig.Translations.BlackjackWin.FormatExt(playerValue)}");
                player.Value.Outcome = BlackjackEnums.GameOutcome.Win;
                _players[player.Key].Payout = Convert.ToInt64(Math.Round(_players[player.Key].Stake!.Value *
                                                                         _credifyConfig.Blackjack.PayoutWin));
                continue;
            }

            if (houseValue == playerValue)
            {
                player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                $"{_credifyConfig.Translations.BlackjackPush}");
                player.Value.Outcome = BlackjackEnums.GameOutcome.Push;
                _players[player.Key].Payout = _players[player.Key].Stake;
                continue;
            }

            if (houseValue > playerValue)
            {
                player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                $"{_credifyConfig.Translations.BlackjackLose.FormatExt(playerValue)}");
                player.Value.Outcome = BlackjackEnums.GameOutcome.Lose;
                _players[player.Key].Payout = 0;
            }
        }

        _dealerPlaysToken.Cancel();
        await PayoutAsync();
    }

    private async Task PayoutAsync()
    {
        _gameState = BlackjackEnums.GameState.Payout;

        foreach (var player in _activePlayers)
        {
            player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} {FormatPlayerOutcomes()}");
            if (_players[player.Key].Payout is 0) continue;

            await _persistenceManager.AlterClientCreditsAsync(_players[player.Key].Payout!.Value, client: player.Key);
            var playerCredits = await _persistenceManager.GetClientCreditsAsync(player.Key);
            _persistenceManager.OrderTop(player.Key, playerCredits);
            _persistenceManager.StatisticsState.CreditsWon += (ulong)_players[player.Key].Payout!.Value;

            player.Key.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                            $"{_credifyConfig.Translations.BlackjackPayout.FormatExt($"{_players[player.Key].Payout - _players[player.Key].Stake:N0}", $"{_players[player.Key].Stake:N0}")}");
        }

        Utilities.ExecuteAfterDelay(TimeSpan.FromSeconds(2), EndGameAsync, CancellationToken.None);
    }

    private async Task EndGameAsync(CancellationToken token)
    {
        _activePlayers.Clear();
        _houseHand.Clear();
        _deck.Clear();
        _dealerPlaysToken.Cancel();
        _playerStakesToken.Cancel();
        _playerStakesToken.Dispose();
        _dealerPlaysToken.Dispose();
        _playerStakesToken = new CancellationTokenSource();
        _dealerPlaysToken = new CancellationTokenSource();
        _gameState = BlackjackEnums.GameState.WaitingForPlayers;

        foreach (var player in _players.Keys.ToList())
        {
            _players[player] = new BlackJackPlayer {Queued = true};
            player.Tell($"{_credifyConfig.Translations.BlackjackTitle} " +
                        $"{_credifyConfig.Translations.BlackjackStartingGame.FormatExt(_players.Count)}");
        }

        try
        {
            await _startGameLock.WaitAsync(token);
            if (!_players.IsEmpty) await StartGameAsync();
        }
        finally
        {
            if (_startGameLock.CurrentCount is 0) _startGameLock.Release();
        }
    }

    #endregion

    #region Public Methods

    public async Task JoinGameAsync(EFClient player)
    {
        _players.TryAdd(player, new BlackJackPlayer {Queued = true});
        if (_gameState is not BlackjackEnums.GameState.WaitingForPlayers)
        {
            player.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                        $"{_credifyConfig.Translations.BlackjackQueued}");
            return;
        }

        try
        {
            await _startGameLock.WaitAsync();
            if (_players.Count is 1) await StartGameAsync();
        }
        finally
        {
            if (_startGameLock.CurrentCount is 0) _startGameLock.Release();
        }
    }

    public async Task LeaveGameAsync(EFClient player)
    {
        _players.TryRemove(player, out _);
        _activePlayers.TryRemove(player, out _);
        if (_players.IsEmpty) await EndGameAsync(CancellationToken.None);
    }

    public async Task HandleChatAsync(EFClient player, string message)
    {
        if (!_activePlayers.TryGetValue(player, out var playerHand)) return;
        if (playerHand.State != BlackjackEnums.PlayerState.Playing) return;

        try
        {
            await _chatLock.WaitAsync();

            switch (_gameState)
            {
                case BlackjackEnums.GameState.RequestPlayerStakes:
                    if (_players[player].Stake is not null) return;
                    if (!long.TryParse(message, out var stake)) return;

                    var amount = await _persistenceManager.GetClientCreditsAsync(player);
                    if (amount >= stake)
                    {
                        _players[player].Stake = stake;
                        await _persistenceManager.AlterClientCreditsAsync(-stake, client: player);
                        _persistenceManager.StatisticsState.CreditsSpent += (ulong)stake;
                        player.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                    $"{_credifyConfig.Translations.BlackjackAcceptedBet.FormatExt(stake)}");
                        var requestStakesRemainders = GetRequestStakesRemainders();
                        if (requestStakesRemainders.Count is 0)
                        {
                            await DealCardsAsync(CancellationToken.None);
                            return;
                        }

                        if (requestStakesRemainders.Count(x => !Equals(x, player)) is not 0)
                            player.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                        $"{_credifyConfig.Translations.BlackjackWaitingForBets.FormatExt(requestStakesRemainders.Count)}");
                        return;
                    }

                    player.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                $"{_credifyConfig.Translations.InsufficientCredits}");
                    break;
                case BlackjackEnums.GameState.RequestPlayerDecisions:
                    var choiceMap = new Dictionary<string, string>
                    {
                        {"h", "hit"},
                        {"s", "stand"},
                        {"c", "cards"}
                    };

                    var newMessage = message.ToLower();
                    if (choiceMap.ContainsKey(newMessage)) newMessage = choiceMap[newMessage];
                    if (!Enum.TryParse<BlackjackEnums.PlayerChoice>(newMessage, true, out var playerDecision)) return;

                    switch (playerDecision)
                    {
                        case BlackjackEnums.PlayerChoice.Hit:
                            await HitAsync(player);
                            var cards = playerHand.Cards.Select(x => x.ToString()).ToList();
                            var coloredCards = new StringBuilder();

                            for (var i = 0; i < cards.Count; i++)
                            {
                                if (i == cards.Count - 1) coloredCards.Append($"(Color::Red){cards[i]}");
                                else coloredCards.Append($"(Color::Accent){cards[i]}, ");
                            }

                            if (IsBust(player))
                            {
                                playerHand.State = BlackjackEnums.PlayerState.Busted;
                                player.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                            $"{_credifyConfig.Translations.BlackjackPlayerBust.FormatExt(CalculateHandValue(playerHand.Cards), coloredCards)}");
                                break;
                            }

                            await player.TellAsync(new[]
                            {
                                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackjackPlayerHit.FormatExt(CalculateHandValue(playerHand.Cards), coloredCards)}",
                                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackJackPlayerDecision}"
                            });
                            break;
                        case BlackjackEnums.PlayerChoice.Stand:
                            playerHand.State = BlackjackEnums.PlayerState.Stand;
                            player.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                        $"{_credifyConfig.Translations.BlackjackPlayerStand.FormatExt(CalculateHandValue(playerHand.Cards))}");
                            break;
                        case BlackjackEnums.PlayerChoice.Cards:
                            await player.TellAsync(new[]
                            {
                                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackjackDealerInitialCard.FormatExt(_houseHand[0])}",
                                $"{_credifyConfig.Translations.BlackjackTitleShort} {_credifyConfig.Translations.BlackjackPlayerCards.FormatExt(CalculateHandValue(playerHand.Cards), string.Join(", ", playerHand.Cards.Select(x => x.ToString())))}"
                            });
                            break;
                    }

                    var decisionStateRemainders = GetDecisionStateRemainders();
                    if (decisionStateRemainders.Count is 0)
                    {
                        await DealerPlaysAsync(CancellationToken.None);
                        break;
                    }

                    if (decisionStateRemainders.Count(x => !Equals(x, player)) is not 0)
                        player.Tell($"{_credifyConfig.Translations.BlackjackTitleShort} " +
                                    $"{_credifyConfig.Translations.BlackjackPlayersDeciding.FormatExt(decisionStateRemainders.Count)}");
                    break;
            }
        }
        finally
        {
            if (_chatLock.CurrentCount is 0) _chatLock.Release();
        }
    }

    public bool IsPlayerPlaying(EFClient player) => _players.ContainsKey(player);

    public int GetPlayerCount() => _players.Count;

    #endregion

    #region Helpers

    private async Task HitAsync(EFClient player)
    {
        _activePlayers.TryGetValue(player, out var playerHand);
        if (playerHand is null) return;
        var card = await DrawCardAsync();
        playerHand.Cards.Add(card);
    }

    private async Task<BlackjackCard> DrawCardAsync()
    {
        if (_deck.IsEmpty)
        {
            _deck = ResetDeck();
            foreach (var player in _activePlayers.Keys.ToList())
            {
                player.Tell($"{_credifyConfig.Translations.BlackjackTitle} " +
                            $"{_credifyConfig.Translations.BlackjackNewDeckShuffled}");
            }
        }

        _deck.TryDequeue(out var drawnCard);
        if (drawnCard is not null) return drawnCard;

        // This should never happen. But need to do this to satisfy the nullability from TryDequeue.
        foreach (var player in _activePlayers.Keys.ToList())
        {
            player.Tell("ERROR: Deck is empty, ending game.");
        }

        await EndGameAsync(CancellationToken.None);
        throw new Exception("Deck is empty!");
    }

    private string FormatPlayerOutcomes()
    {
        string FormatOutcome(BlackjackEnums.GameOutcome outcome) =>
            outcome switch
            {
                BlackjackEnums.GameOutcome.Blackjack => _credifyConfig.Translations.BlackjackOutcomeBlackjack,
                BlackjackEnums.GameOutcome.Win => _credifyConfig.Translations.BlackjackOutcomeWin,
                BlackjackEnums.GameOutcome.Lose => _credifyConfig.Translations.BlackjackOutcomeLose,
                BlackjackEnums.GameOutcome.Push => _credifyConfig.Translations.BlackjackOutcomePush,
                _ => string.Empty
            };

        var message = string.Join(", ", _activePlayers.Select(x =>
            _credifyConfig.Translations.BlackjackPlayerOutcomeMessage
                .FormatExt(FormatOutcome(x.Value.Outcome), x.Key.CleanedName, CalculateHandValue(x.Value.Cards))));
        return message;
    }

    private static ConcurrentQueue<BlackjackCard> ResetDeck()
    {
        var deck = new List<BlackjackCard>();
        foreach (BlackjackCard.Suit suit in Enum.GetValues(typeof(BlackjackCard.Suit)))
        {
            foreach (BlackjackCard.Rank rank in Enum.GetValues(typeof(BlackjackCard.Rank)))
            {
                deck.Add(new BlackjackCard(suit, rank));
            }
        }

        var shuffledDeck = new ConcurrentQueue<BlackjackCard>(deck.OrderBy(_ => Guid.NewGuid()));
        return shuffledDeck;
    }

    private List<EFClient> GetRequestStakesRemainders()
    {
        var players = _players.Where(x => x.Value.Stake is null)
            .Where(x => _activePlayers.ContainsKey(x.Key))
            .Select(x => x.Key)
            .ToList();
        return players;
    }

    private List<EFClient> GetDecisionStateRemainders()
    {
        var players = _activePlayers
            .Where(x => x.Value.State is BlackjackEnums.PlayerState.Playing)
            .Select(x => x.Key)
            .ToList();
        return players;
    }

    private bool IsBust(EFClient player)
    {
        _activePlayers.TryGetValue(player, out var hand);
        var total = hand is not null ? CalculateHandValue(hand.Cards) : 0;
        return total > 21;
    }

    private static int CalculateHandValue(IEnumerable<BlackjackCard> hand)
    {
        var totalValue = 0;
        var aces = 0;

        foreach (var card in hand)
        {
            var cardValue = card.GetValue();
            if (cardValue is 11)
            {
                aces++;
                totalValue += 11;
            }
            else totalValue += cardValue;

            while (totalValue > 21 && aces > 0)
            {
                totalValue -= 10;
                aces--;
            }
        }

        return totalValue;
    }

    private static bool IsBlackjack(IEnumerable<BlackjackCard> hand)
    {
        var cards = hand.ToList();
        return cards.Count is 2 && CalculateHandValue(cards) is 21;
    }

    #endregion
}
