using System.Collections.Concurrent;
using Credify.Chat.Active.Games.Blackjack.Models;

namespace Credify.Chat.Active.Games.Blackjack.Services;

/// <summary>
/// Service responsible for deck management in Blackjack games.
/// Handles deck creation, shuffling, and card drawing.
/// </summary>
public class BlackjackDeckService
{
    private ConcurrentQueue<BlackjackCard> _deck = new();

    /// <summary>
    /// Creates and shuffles a new deck of cards.
    /// </summary>
    public ConcurrentQueue<BlackjackCard> CreateShuffledDeck()
    {
        var deck = new List<BlackjackCard>();
        var suits = Enum.GetValues(typeof(BlackjackCard.Suit)).Cast<BlackjackCard.Suit>();
        var ranks = Enum.GetValues(typeof(BlackjackCard.Rank)).Cast<BlackjackCard.Rank>().ToList();
        
        foreach (var suit in suits)
        {
            foreach (var rank in ranks)
            {
                deck.Add(new BlackjackCard(suit, rank));
            }
        }

        var shuffledDeck = new ConcurrentQueue<BlackjackCard>(deck.OrderBy(_ => Guid.NewGuid()));
        return shuffledDeck;
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
    public BlackjackCard DrawCardOrReshuffle()
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
