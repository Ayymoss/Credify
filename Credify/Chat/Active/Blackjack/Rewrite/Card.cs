using Credify.Chat.Active.Blackjack.Rewrite.Enums;

namespace Credify.Chat.Active.Blackjack.Rewrite;

public class Card(CardRank rank, CardSuit suit)
{
    public CardSuit Suit { get; } = suit;
    public CardRank Rank { get; } = rank;

    public int Value => Rank switch
    {
        CardRank.Ace => 11,
        CardRank.Two => 2,
        CardRank.Three => 3,
        CardRank.Four => 4,
        CardRank.Five => 5,
        CardRank.Six => 6,
        CardRank.Seven => 7,
        CardRank.Eight => 8,
        CardRank.Nine => 9,
        CardRank.Ten => 10,
        CardRank.Jack => 10,
        CardRank.Queen => 10,
        CardRank.King => 10,
        _ => throw new InvalidOperationException("Invalid card rank")
    };

    public override string ToString()
    {
        return $"{Rank} of {Suit}";
    }
}
