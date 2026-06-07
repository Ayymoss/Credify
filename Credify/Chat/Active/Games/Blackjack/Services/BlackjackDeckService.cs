using System.Collections.Concurrent;
using Credify.Games;
using Credify.Games.Cards;

namespace Credify.Chat.Active.Games.Blackjack.Services;

/// <summary>
/// Service responsible for deck management in chat Blackjack games. Handles deck creation, shuffling, and
/// card drawing. The 52-card composition and the (crypto) shuffle come from the shared card core; this
/// service only adds the concurrent queue + reshuffle-on-empty behaviour the chat game relies on.
/// </summary>
public class BlackjackDeckService
{
    private ConcurrentQueue<Card> _deck = new();

    /// <summary>
    /// Creates and shuffles a new deck of cards.
    /// </summary>
    public ConcurrentQueue<Card> CreateShuffledDeck()
    {
        var deck = CardDeck.BuildStandardDeck();
        Shuffle.InPlace(deck);
        return new ConcurrentQueue<Card>(deck);
    }

    /// <summary>
    /// Initializes the deck with a new shuffled deck.
    /// </summary>
    public void InitializeDeck()
    {
        _deck = CreateShuffledDeck();
    }

    /// <summary>
    /// Checks if the deck is empty.
    /// </summary>
    public bool IsDeckEmpty() => _deck.IsEmpty;

    /// <summary>
    /// Draws a card from the deck. Reshuffles if needed.
    /// Throws exception if deck cannot be replenished.
    /// </summary>
    public Card DrawCardOrReshuffle()
    {
        if (_deck.IsEmpty)
        {
            _deck = CreateShuffledDeck();
        }

        if (_deck.TryDequeue(out var drawnCard))
        {
            return drawnCard;
        }

        throw new InvalidOperationException("Deck is empty and could not be reshuffled!");
    }

    /// <summary>
    /// Resets the deck with a new shuffled deck.
    /// </summary>
    public void ReshuffleDeck()
    {
        _deck = CreateShuffledDeck();
    }
}
