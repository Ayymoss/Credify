using System.Collections.Concurrent;
using System.Text;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Chat.Active.Games.Blackjack.Services;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Blackjack;

public class BlackjackGame : BaseActiveGame<BlackJackPlayer>
{
    private BlackjackEnums.GameState _gameState = BlackjackEnums.GameState.WaitingForPlayers;
    private readonly ConcurrentDictionary<EFClient, BlackjackHand> _activePlayers = new();
    private List<BlackjackCard> _houseHand = [];
    private readonly BlackjackDeckService _deckService;
    private readonly BlackjackPayoutCalculator _payoutCalculator;
    private CancellationTokenSource _playerStakesToken = new();
    private CancellationTokenSource _dealerPlaysToken = new();
    private readonly SemaphoreSlim _chatLock = new(1, 1);
    private readonly SemaphoreSlim _startGameLock = new(1, 1);

    public BlackjackGame(
        PersistenceService persistenceService,
        CredifyConfiguration credifyConfig,
        GamePlayerCommunication communication)
        : base(persistenceService, credifyConfig, communication)
    {
        _deckService = new BlackjackDeckService();
        _payoutCalculator = new BlackjackPayoutCalculator(credifyConfig.Blackjack);
        _deckService.InitializeDeck();
    }

    #region Core Game Flow

    private async Task StartGameAsync()
    {
        if (_gameState != BlackjackEnums.GameState.WaitingForPlayers) return;
        if (Players.IsEmpty) return;

        _gameState = BlackjackEnums.GameState.SettingUpGame;
        _houseHand =
        [
            _deckService.DrawCardOrReshuffle(),
            _deckService.DrawCardOrReshuffle()
        ];

        foreach (var player in Players) player.Value.Queued = false;

        var activePlayers = Players
            .Where(x => !x.Value.Queued)
            .ToDictionary(x => x.Key, x => x.Value);

        foreach (var player in activePlayers) _activePlayers.TryAdd(player.Key, new BlackjackHand());

        Utilities.ExecuteAfterDelay(TimeSpan.FromSeconds(GameConstants.Timeouts.DefaultGameStartDelay), 
            RequestPlayerStakesAsync, CancellationToken.None);
    }

    private async Task RequestPlayerStakesAsync(CancellationToken token)
    {
        _gameState = BlackjackEnums.GameState.RequestPlayerStakes;

        var insufficientFunds = new List<EFClient>();
        foreach (var player in _activePlayers)
        {
            player.Value.State = BlackjackEnums.PlayerState.Playing;
            var playerFunds = await PersistenceService.GetClientCreditsAsync(player.Key);
            if (playerFunds < GameConstants.MinimumCredits)
            {
                insufficientFunds.Add(player.Key);
                continue;
            }

            await TellPlayerAsync(player.Key, true, 
                [Config.Translations.Blackjack.PlaceBets.FormatExt(playerFunds.ToString("N0"))]);
        }

        foreach (var player in insufficientFunds)
        {
            _activePlayers.TryRemove(player, out _);
            Players.TryRemove(player, out _);
            await TellPlayerAsync(player, true, [Config.Translations.Blackjack.InsufficientFunds]);
        }

        if (_activePlayers.IsEmpty)
        {
            await EndGameAsync(CancellationToken.None);
            return;
        }

        Utilities.ExecuteAfterDelay(Config.Blackjack.TimeoutForPlayerAction, DealCardsAsync,
            _playerStakesToken.Token);
    }

    private async Task DealCardsAsync(CancellationToken token)
    {
        if (_gameState is not BlackjackEnums.GameState.RequestPlayerStakes) return;
        _gameState = BlackjackEnums.GameState.DealCards;

        var noBets = Players.Where(x => x.Value.Stake is null)
            .Where(x => _activePlayers.ContainsKey(x.Key))
            .Select(player => player.Key)
            .ToList();

        foreach (var player in noBets)
        {
            _activePlayers.TryRemove(player, out _);
            Players.TryRemove(player, out _);
            await TellPlayerAsync(player, true, [Config.Translations.Blackjack.BetTimeout]);
        }

        if (_activePlayers.IsEmpty)
        {
            await EndGameAsync(CancellationToken.None);
            return;
        }

        foreach (var player in _activePlayers)
        {
            player.Value.Cards =
            [
                _deckService.DrawCardOrReshuffle(),
                _deckService.DrawCardOrReshuffle()
            ];
            await TellPlayerAsync(player.Key, false,
            [
                Config.Translations.Blackjack.DealerInitialCard.FormatExt(_houseHand[0]),
                Config.Translations.Blackjack.PlayerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.Value.Cards),
                    string.Join(", ", player.Value.Cards.Select(x => x.ToString())))
            ]);
        }

        await _playerStakesToken.CancelAsync();
        await RequestPlayerDecisionsAsync();
    }

    private async Task RequestPlayerDecisionsAsync()
    {
        _gameState = BlackjackEnums.GameState.RequestPlayerDecisions;

        foreach (var player in _activePlayers)
        {
            if (BlackjackPayoutCalculator.CalculateHandValue(player.Value.Cards) == GameConstants.Blackjack.BlackjackValue)
            {
                player.Value.State = BlackjackEnums.PlayerState.Stand;
                await TellPlayerAsync(player.Key, false, [Config.Translations.Blackjack.BlackjackConfirmation]);
                var decisionStateRemainders = GetDecisionStateRemainders();
                if (decisionStateRemainders.Count(x => !Equals(x, player.Key)) is not 0)
                    await TellPlayerAsync(player.Key, false,
                        [Config.Translations.Blackjack.PlayersDeciding.FormatExt(decisionStateRemainders.Count)]);
                continue;
            }

            await TellPlayerAsync(player.Key, false, [Config.Translations.Blackjack.PlayerDecision]);
        }

        var decisionStateRemaindersOverride = GetDecisionStateRemainders();
        if (decisionStateRemaindersOverride.Count is 0)
        {
            await DealerPlaysAsync(CancellationToken.None);
            return;
        }

        Utilities.ExecuteAfterDelay(Config.Blackjack.TimeoutForPlayerAction, DealerPlaysAsync, _dealerPlaysToken.Token);
    }

    private async Task DealerPlaysAsync(CancellationToken token)
    {
        if (_gameState is not BlackjackEnums.GameState.RequestPlayerDecisions) return;
        _gameState = BlackjackEnums.GameState.DealerPlays;

        while (BlackjackPayoutCalculator.CalculateHandValue(_houseHand) < GameConstants.Blackjack.DealerStandValue)
        {
            _houseHand.Add(_deckService.DrawCardOrReshuffle());
        }

        foreach (var player in _activePlayers)
        {
            await TellPlayerAsync(player.Key, false,
            [
                Config.Translations.Blackjack.DealerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(_houseHand),
                    string.Join(", ", _houseHand.Select(x => x.ToString()))),
                Config.Translations.Blackjack.PlayerCards.FormatExt(
                    BlackjackPayoutCalculator.CalculateHandValue(player.Value.Cards),
                    string.Join(", ", player.Value.Cards.Select(x => x.ToString())))
            ]);
        }

        foreach (var player in _activePlayers)
        {
            var outcome = _payoutCalculator.DetermineOutcome(player.Value.Cards, _houseHand);
            player.Value.Outcome = outcome;
            var stake = Players[player.Key].Stake!.Value;
            Players[player.Key].Payout = _payoutCalculator.CalculatePayout(stake, outcome);

            // Send outcome messages
            await SendOutcomeMessageAsync(player.Key, player.Value, outcome);
        }

        await _dealerPlaysToken.CancelAsync();
        await PayoutAsync();
    }

    private async Task SendOutcomeMessageAsync(EFClient player, BlackjackHand hand, BlackjackEnums.GameOutcome outcome)
    {
        var playerValue = BlackjackPayoutCalculator.CalculateHandValue(hand.Cards);
        var houseValue = BlackjackPayoutCalculator.CalculateHandValue(_houseHand);

        switch (outcome)
        {
            case BlackjackEnums.GameOutcome.Lose:
                if (BlackjackPayoutCalculator.IsBusted(hand.Cards))
                    await TellPlayerAsync(player, false, [Config.Translations.Blackjack.PlayerBustConfirmation]);
                else
                    await TellPlayerAsync(player, false, 
                        [Config.Translations.Blackjack.Lose.FormatExt(playerValue)]);
                break;
            case BlackjackEnums.GameOutcome.Blackjack:
                await TellPlayerAsync(player, false, [Config.Translations.Blackjack.Win.FormatExt(playerValue)]);
                // Broadcast announcement
                await Communication.BroadcastToAllServersAsync(player,
                    [$"{Config.Translations.Blackjack.Title} " +
                     Config.Translations.Blackjack.Announcement.FormatExt(player.CleanedName,
                         _payoutCalculator.CalculateNetProfit(Players[player].Stake!.Value, outcome).ToString("N0"))]);
                break;
            case BlackjackEnums.GameOutcome.Win:
                if (BlackjackPayoutCalculator.IsBusted(_houseHand))
                    await TellPlayerAsync(player, false, 
                        [Config.Translations.Blackjack.DealerBust.FormatExt(houseValue)]);
                else
                    await TellPlayerAsync(player, false, 
                        [Config.Translations.Blackjack.Win.FormatExt(playerValue)]);
                break;
            case BlackjackEnums.GameOutcome.Push:
                if (BlackjackPayoutCalculator.IsBlackjack(hand.Cards))
                    await TellPlayerAsync(player, false, [Config.Translations.Blackjack.BlackjackPush]);
                else
                    await TellPlayerAsync(player, false, [Config.Translations.Blackjack.Push]);
                break;
        }
    }

    private async Task PayoutAsync()
    {
        _gameState = BlackjackEnums.GameState.Payout;

        foreach (var player in _activePlayers)
        {
            if (_activePlayers.Count is not 1) 
                await TellPlayerAsync(player.Key, false, [FormatPlayerOutcomes()]);
            if (Players[player.Key].Payout is 0) continue;

            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Key, Players[player.Key].Payout!.Value);
            await PersistenceService.AddCreditsAsync(player.Key, Players[player.Key].Payout!.Value);
            await TellPlayerAsync(player.Key, false,
            [
                Config.Translations.Blackjack.Payout.FormatExt(
                    (Players[player.Key].Payout - Players[player.Key].Stake)?.ToString("N0"), 
                    Players[player.Key].Stake?.ToString("N0"))
            ]);
        }

        Utilities.ExecuteAfterDelay(TimeSpan.FromSeconds(GameConstants.Timeouts.DefaultPayoutDelay), 
            EndGameAsync, CancellationToken.None);
    }

    private async Task EndGameAsync(CancellationToken token)
    {
        foreach (var player in _activePlayers.Keys)
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Blackjack, player);
        }

        _activePlayers.Clear();
        _houseHand.Clear();
        await _dealerPlaysToken.CancelAsync();
        await _playerStakesToken.CancelAsync();
        _playerStakesToken.Dispose();
        _dealerPlaysToken.Dispose();
        _playerStakesToken = new CancellationTokenSource();
        _dealerPlaysToken = new CancellationTokenSource();
        _gameState = BlackjackEnums.GameState.WaitingForPlayers;

        foreach (var player in Players.Keys.ToList())
        {
            Players[player] = new BlackJackPlayer(true);
            await TellPlayerAsync(player, true, 
                [Config.Translations.Blackjack.StartingGame.FormatExt(Players.Count)]);
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

    public override async Task JoinGameAsync(EFClient player)
    {
        Players.TryAdd(player, new BlackJackPlayer(true));
        if (_gameState is not BlackjackEnums.GameState.WaitingForPlayers)
        {
            await TellPlayerAsync(player, false, [Config.Translations.Blackjack.Queued]);
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

    public override async Task LeaveGameAsync(EFClient player)
    {
        Players.TryRemove(player, out _);
        _activePlayers.TryRemove(player, out _);
        if (Players.IsEmpty) await EndGameAsync(CancellationToken.None);
    }

    public override async Task HandleChatAsync(EFClient player, string message)
    {
        if (!_activePlayers.TryGetValue(player, out var playerHand)) return;
        if (playerHand.State != BlackjackEnums.PlayerState.Playing) return;

        try
        {
            await _chatLock.WaitAsync();

            switch (_gameState)
            {
                case BlackjackEnums.GameState.RequestPlayerStakes:
                    if (Players[player].Stake is not null) return;
                    if (!long.TryParse(message, out var stake)) return;

                    var amount = await PersistenceService.GetClientCreditsAsync(player);
                    if (amount >= stake)
                    {
                        Players[player].Stake = stake;
                        await PersistenceService.RemoveCreditsAsync(player, stake);
                        await TellPlayerAsync(player, false, 
                            [Config.Translations.Blackjack.AcceptedBet.FormatExt(stake)]);
                        var requestStakesRemainders = GetRequestStakesRemainders();
                        if (requestStakesRemainders.Count is 0)
                        {
                            await DealCardsAsync(CancellationToken.None);
                            return;
                        }

                        if (requestStakesRemainders.Count(x => !Equals(x, player)) is not 0)
                            await TellPlayerAsync(player, false,
                                [Config.Translations.Blackjack.WaitingForBets.FormatExt(requestStakesRemainders.Count)]);
                        return;
                    }

                    await TellPlayerAsync(player, false, [Config.Translations.Core.InsufficientCredits]);
                    break;
                case BlackjackEnums.GameState.RequestPlayerDecisions:
                    await HandlePlayerDecisionAsync(player, playerHand, message);
                    break;
            }
        }
        finally
        {
            if (_chatLock.CurrentCount is 0) _chatLock.Release();
        }
    }

    #endregion

    #region Helper Methods

    private async Task HandlePlayerDecisionAsync(EFClient player, BlackjackHand playerHand, string message)
    {
        var choiceMap = new Dictionary<string, string>
        {
            { "h", "hit" },
            { "s", "stand" },
            { "c", "cards" }
        };

        var newMessage = message.ToLower();
        if (choiceMap.TryGetValue(newMessage, out var mapChoice)) newMessage = mapChoice;
        if (!Enum.TryParse<BlackjackEnums.PlayerChoice>(newMessage, true, out var playerDecision)) return;

        switch (playerDecision)
        {
            case BlackjackEnums.PlayerChoice.Hit:
                await HitAsync(player, playerHand);
                break;
            case BlackjackEnums.PlayerChoice.Stand:
                playerHand.State = BlackjackEnums.PlayerState.Stand;
                await TellPlayerAsync(player, false,
                    [Config.Translations.Blackjack.PlayerStand.FormatExt(
                        BlackjackPayoutCalculator.CalculateHandValue(playerHand.Cards))]);
                break;
            case BlackjackEnums.PlayerChoice.Cards:
                await TellPlayerAsync(player, false,
                [
                    Config.Translations.Blackjack.DealerInitialCard.FormatExt(_houseHand[0]),
                    Config.Translations.Blackjack.PlayerCards.FormatExt(
                        BlackjackPayoutCalculator.CalculateHandValue(playerHand.Cards),
                        string.Join(", ", playerHand.Cards.Select(x => x.ToString())))
                ]);
                break;
        }

        var decisionStateRemainders = GetDecisionStateRemainders();
        if (decisionStateRemainders.Count is 0)
        {
            await DealerPlaysAsync(CancellationToken.None);
            return;
        }

        if (decisionStateRemainders.Count(x => !Equals(x, player)) is not 0)
            await TellPlayerAsync(player, false,
                [Config.Translations.Blackjack.PlayersDeciding.FormatExt(decisionStateRemainders.Count)]);
    }

    private async Task HitAsync(EFClient player, BlackjackHand playerHand)
    {
        if (_deckService.IsDeckEmpty())
        {
            _deckService.ReshuffleDeck();
            await Communication.TellPlayersAsync(_activePlayers.Keys,
                [Config.Translations.Blackjack.NewDeckShuffled]);
        }

        var card = _deckService.DrawCardOrReshuffle();
        playerHand.Cards.Add(card);

        var cards = playerHand.Cards.Select(x => x.ToString()).ToList();
        var coloredCards = new StringBuilder();

        for (var i = 0; i < cards.Count; i++)
        {
            if (i == cards.Count - 1) coloredCards.Append($"(Color::Red){cards[i]}");
            else coloredCards.Append($"(Color::Accent){cards[i]}, ");
        }

        var handValue = BlackjackPayoutCalculator.CalculateHandValue(playerHand.Cards);

        await TellPlayerAsync(player, false,
        [
            Config.Translations.Blackjack.PlayerHit.FormatExt(handValue, coloredCards),
            Config.Translations.Blackjack.PlayerDecision
        ]);

        if (handValue == GameConstants.Blackjack.BlackjackValue)
        {
            playerHand.State = BlackjackEnums.PlayerState.Stand;
            await TellPlayerAsync(player, false,
                [Config.Translations.Blackjack.PlayerStand.FormatExt(handValue)]);
            return;
        }

        if (BlackjackPayoutCalculator.IsBusted(playerHand.Cards))
        {
            playerHand.State = BlackjackEnums.PlayerState.Busted;
            await TellPlayerAsync(player, false,
                [Config.Translations.Blackjack.PlayerBust.FormatExt(handValue, coloredCards)]);
        }
    }

    private async Task TellPlayerAsync(EFClient player, bool hasLongTitle, IEnumerable<string> messages)
    {
        var title = hasLongTitle
            ? Config.Translations.Blackjack.Title
            : Config.Translations.Blackjack.TitleShort;
        await Communication.TellPlayerAsync(player, title, messages);
    }

    private string FormatPlayerOutcomes()
    {
        return string.Join(", ", _activePlayers.Select(x =>
            Config.Translations.Blackjack.PlayerOutcomeMessage
                .FormatExt(FormatOutcome(x.Value.Outcome), x.Key.CleanedName, 
                    BlackjackPayoutCalculator.CalculateHandValue(x.Value.Cards))));

        string FormatOutcome(BlackjackEnums.GameOutcome outcome) =>
            outcome switch
            {
                BlackjackEnums.GameOutcome.Blackjack => Config.Translations.Blackjack.OutcomeBlackjack,
                BlackjackEnums.GameOutcome.Win => Config.Translations.Blackjack.OutcomeWin,
                BlackjackEnums.GameOutcome.Push => Config.Translations.Blackjack.OutcomePush,
                _ => Config.Translations.Blackjack.OutcomeLose
            };
    }

    private List<EFClient> GetRequestStakesRemainders() => Players.Where(x => x.Value.Stake is null)
        .Where(x => _activePlayers.ContainsKey(x.Key))
        .Select(x => x.Key)
        .ToList();

    private List<EFClient> GetDecisionStateRemainders() => _activePlayers
        .Where(x => x.Value.State is BlackjackEnums.PlayerState.Playing)
        .Select(x => x.Key)
        .ToList();

    #endregion
}
