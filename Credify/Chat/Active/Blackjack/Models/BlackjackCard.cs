namespace Credify.Chat.Active.Blackjack.Models;

public class BlackjackCard(BlackjackCard.Suit suit, BlackjackCard.Rank rank)
{
    public Suit CardSuit { get; private set; } = suit;
    public Rank CardRank { get; private set; } = rank;

    public int GetValue() => (int)CardRank;

    public override string ToString()
    {
        var rankString = CardRank is Rank.Ace ? "A" : ((int)CardRank).ToString();
        return $"{rankString}-{CardSuit.ToString().ToUpper()}";
    }

    public enum Suit
    {
        H,
        D,
        C,
        S
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
        Jack = 10,
        Queen = 10,
        King = 10,
        Ace = 11
    }
}
