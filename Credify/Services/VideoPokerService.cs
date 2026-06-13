using System.Collections.Concurrent;
using System.Threading.Tasks;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Games.Cards;
using Credify.Games.VideoPoker;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// Server-authoritative Jacks-or-Better video poker. A hand is two phases: <see cref="DealAsync"/>
/// debits the bet and deals five from a fresh deck (held server-side so the redraw uses the same
/// shoe); <see cref="DrawAsync"/> replaces the cards the player didn't hold, evaluates, and pays.
/// The hold mask is the player's only input — they can never see or influence the undrawn deck, so
/// holding is a real decision with no exploit. The paytable lives in config.
/// </summary>
public class VideoPokerService(PersistenceService persistence, CredifyConfiguration config)
{
    public bool IsEnabled => config.VideoPoker.IsEnabled;
    public long MinBet => config.VideoPoker.MinBet;
    public long MaxBet => config.VideoPoker.MaxBet;

    private sealed record InProgress(CardDeck Deck, Card[] Cards, long Bet);

    private readonly ConcurrentDictionary<int, InProgress> _hands = new();

    /// <summary>Debits the stake and deals five cards. Caller has already validated affordability.</summary>
    public async Task<Card[]> DealAsync(EFClient client, long bet)
    {
        await persistence.RemoveCreditsAsync(client, bet);

        var deck = new CardDeck();
        var cards = new Card[5];
        for (var i = 0; i < 5; i++) cards[i] = deck.Draw();

        _hands[client.ClientId] = new InProgress(deck, cards, bet); // a fresh deal replaces any stale hand
        return cards;
    }

    /// <summary>
    /// Replaces every position the player didn't hold, evaluates the final five and pays the paytable.
    /// Returns null if there is no hand in progress (e.g. a double-submit or a stale circuit).
    /// </summary>
    public async Task<VideoPokerReceipt?> DrawAsync(EFClient client, bool[] hold)
    {
        if (!_hands.TryRemove(client.ClientId, out var hand)) return null;

        var cards = (Card[])hand.Cards.Clone();
        for (var i = 0; i < cards.Length; i++)
        {
            var keep = i < hold.Length && hold[i];
            if (!keep) cards[i] = hand.Deck.Draw();
        }

        var rank = VideoPokerHand.Evaluate(cards);
        var winnings = (long)(hand.Bet * config.VideoPoker.Multiplier(rank));

        long newBalance;
        if (winnings > 0)
        {
            newBalance = await persistence.AddCreditsAsync(client, winnings);
            if (winnings > hand.Bet) // a genuine win, not a Jacks-or-Better push
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, winnings);
        }
        else
        {
            newBalance = await persistence.GetClientCreditsAsync(client);
        }

        return new VideoPokerReceipt(cards, rank, hand.Bet, winnings, winnings - hand.Bet, newBalance);
    }
}

/// <summary>The settled outcome of a draw — the final five cards and the payout breakdown.</summary>
public sealed record VideoPokerReceipt(
    Card[] Cards, VideoPokerRank Rank, long Bet, long Winnings, long Profit, long NewBalance);
