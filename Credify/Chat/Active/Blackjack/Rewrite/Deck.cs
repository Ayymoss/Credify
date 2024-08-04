using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Credify.Chat.Active.Blackjack.Rewrite.Enums;

namespace Credify.Chat.Active.Blackjack.Rewrite;

public class Deck
{
    private ConcurrentQueue<Card> _cards = ReplenishDeck();

    public Card DealCard()
    {
        if (_cards.IsEmpty) _cards = ReplenishDeck();

        if (!_cards.TryDequeue(out var card)) throw new InvalidOperationException("Deck is empty");

        return card;
    }

    private static ConcurrentQueue<Card> ReplenishDeck()
    {
        var cards = CreateDeck();
        return new ConcurrentQueue<Card>(Shuffle(cards));
    }

    private static List<Card> CreateDeck()
    {
        List<Card> cards = [];
        foreach (var suit in Enum.GetValues<CardSuit>())
        {
            foreach (var rank in Enum.GetValues<CardRank>())
            {
                cards.Add(new Card(rank, suit));
            }
        }

        return cards;
    }

    private static IEnumerable<Card> Shuffle(IEnumerable<Card> cards) => cards.OrderBy(_ => Guid.NewGuid());

    public static void ThrowIfDeckIsNotInitialised([NotNull] Deck? deck)
    {
        if (deck is null) throw new InvalidOperationException("Deck is not initialized.");
    }
}
