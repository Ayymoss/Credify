using System.Collections.Concurrent;
using Credify.Chat.Active.Games.Poker.Models;

namespace Credify.Chat.Active.Games.Poker.Services;

/// <summary>
/// Service responsible for deck management in Poker games.
/// Handles deck creation, shuffling, and card dealing.
/// </summary>
public class PokerDeckService
{
    private ConcurrentQueue<PokerCard> _deck = new();

    /// <summary>
    /// Creates a standard 52-card deck.
    /// </summary>
    public List<PokerCard> CreateDeck()
    {
        var deck = new List<PokerCard>();
        var suits = Enum.GetValues(typeof(PokerCard.Suit)).Cast<PokerCard.Suit>();
        var ranks = Enum.GetValues(typeof(PokerCard.Rank)).Cast<PokerCard.Rank>();

        foreach (var suit in suits)
        {
            foreach (var rank in ranks)
            {
                deck.Add(new PokerCard(suit, rank));
            }
        }

        return deck;
    }

    /// <summary>
    /// Creates and shuffles a new deck.
    /// </summary>
    public void InitializeDeck()
    {
        var deck = CreateDeck();
        var shuffledDeck = deck.OrderBy(_ => Guid.NewGuid()).ToList();
        _deck = new ConcurrentQueue<PokerCard>(shuffledDeck);
    }

    /// <summary>
    /// Deals a single card from the deck.
    /// </summary>
    public PokerCard DealCard()
    {
        if (_deck.TryDequeue(out var card))
        {
            return card;
        }

        throw new InvalidOperationException("Deck is empty! Cannot deal card.");
    }

    /// <summary>
    /// Deals multiple cards at once.
    /// </summary>
    public List<PokerCard> DealCards(int count)
    {
        var cards = new List<PokerCard>();
        for (int i = 0; i < count; i++)
        {
            cards.Add(DealCard());
        }

        return cards;
    }
}
