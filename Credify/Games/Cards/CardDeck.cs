namespace Credify.Games.Cards;

/// <summary>
/// A standard 52-card deck that draws in shuffled order and auto-reshuffles when exhausted. Shuffling is the
/// shared crypto <see cref="Shuffle"/>. This is the convenience wrapper the single-threaded web games use;
/// the chat game keeps its own concurrent queue but builds and shuffles through the same primitives, so the
/// 52-card composition and the shuffle live in one place.
/// </summary>
public sealed class CardDeck
{
    private readonly Queue<Card> _cards = new();

    public CardDeck() => Reset();

    public int Remaining => _cards.Count;

    /// <summary>Rebuilds a full 52-card deck and shuffles it.</summary>
    public void Reset()
    {
        _cards.Clear();
        var cards = BuildStandardDeck();
        Shuffle.InPlace(cards);
        foreach (var card in cards)
        {
            _cards.Enqueue(card);
        }
    }

    /// <summary>Draws the next card, reshuffling a fresh deck first if this one is empty.</summary>
    public Card Draw()
    {
        if (_cards.Count == 0)
        {
            Reset();
        }

        return _cards.Dequeue();
    }

    /// <summary>The 52-card composition, unshuffled. Shared by the chat deck service too.</summary>
    public static List<Card> BuildStandardDeck()
    {
        var cards = new List<Card>(52);
        foreach (var suit in Enum.GetValues<Suit>())
        {
            foreach (var rank in Enum.GetValues<Rank>())
            {
                cards.Add(new Card(suit, rank));
            }
        }

        return cards;
    }
}
