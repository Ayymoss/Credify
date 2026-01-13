namespace Credify.Chat.Active.Games.Poker.Models;

public class PokerCard(PokerCard.Suit suit, PokerCard.Rank rank)
{
    public Suit CardSuit { get; private set; } = suit;
    public Rank CardRank { get; private set; } = rank;

    /// <summary>
    /// Gets the numeric value for comparison (Ace = 14, King = 13, etc.)
    /// </summary>
    public int GetNumericValue() => (int)CardRank;

    public override string ToString()
    {
        var rankString = CardRank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            _ => ((int)CardRank).ToString()
        };
        
        var suitSymbol = CardSuit switch
        {
            Suit.Hearts => "♥",
            Suit.Diamonds => "♦",
            Suit.Clubs => "♣",
            Suit.Spades => "♠",
            _ => CardSuit.ToString()[0].ToString()
        };
        
        return $"{rankString}{suitSymbol}";
    }

    public enum Suit
    {
        Hearts,
        Diamonds,
        Clubs,
        Spades
    }

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
}
