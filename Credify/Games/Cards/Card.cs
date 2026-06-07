namespace Credify.Games.Cards;

public enum Suit
{
    Hearts,
    Diamonds,
    Clubs,
    Spades
}

/// <summary>
/// Card rank. Backing values are the card's identity (Jack/Queen/King distinct, Ace high) — NOT the
/// blackjack point value — so a frontend can tell a Jack from a King. Game-specific point values are
/// derived (see <see cref="Card.BlackjackValue"/>); a future game can add its own projection the same way.
/// </summary>
public enum Rank
{
    Two = 2,
    Three = 3,
    Four = 4,
    Five = 5,
    Six = 6,
    Seven = 7,
    Eight = 8,
    Nine = 9,
    Ten = 10,
    Jack = 11,
    Queen = 12,
    King = 13,
    Ace = 14
}

/// <summary>
/// A single playing card from a standard 52-card deck. Immutable, presentation-agnostic: it exposes the
/// raw identity plus a few helpers each frontend can choose from (glyph for the webfront, letter for the
/// charset-limited in-game chat). Shared by every Credify card game.
/// </summary>
public sealed record Card(Suit Suit, Rank Rank)
{
    public bool IsRed => Suit is Suit.Hearts or Suit.Diamonds;

    /// <summary>Blackjack point value: number cards face value, J/Q/K = 10, Ace = 11 (soft).</summary>
    public int BlackjackValue => Rank switch
    {
        Rank.Ace => 11,
        >= Rank.Jack => 10,
        _ => (int)Rank
    };

    /// <summary>Short rank label for display: A, K, Q, J, 10, 9 … 2.</summary>
    public string RankLabel => Rank switch
    {
        Rank.Ace => "A",
        Rank.King => "K",
        Rank.Queen => "Q",
        Rank.Jack => "J",
        _ => ((int)Rank).ToString()
    };

    /// <summary>Unicode suit glyph (webfront — the in-game chat charset can't render these).</summary>
    public char SuitGlyph => Suit switch
    {
        Suit.Hearts => '♥',
        Suit.Diamonds => '♦',
        Suit.Clubs => '♣',
        _ => '♠'
    };

    /// <summary>Single-letter suit (in-game chat, where glyphs don't render): H, D, C, S.</summary>
    public char SuitLetter => Suit switch
    {
        Suit.Hearts => 'H',
        Suit.Diamonds => 'D',
        Suit.Clubs => 'C',
        _ => 'S'
    };
}
