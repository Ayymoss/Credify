using System.Diagnostics.CodeAnalysis;
using Credify.Chat.Active.Blackjack.Rewrite.Enums;

namespace Credify.Chat.Active.Blackjack.Rewrite;

public class Hand(List<Card> initialCards, int bet = default)
{
    public List<Card> Cards { get; } = initialCards;
    public int Bet { get; set; } = bet;
    public bool IsDoubledDown { get; set; }
    public bool IsInsured { get; set; }
    public bool IsSurrendered { get; set; }

    public int GetHandValue()
    {
        var aceCount = Cards.Count(x => x.Rank is CardRank.Ace);
        var totalValue = Cards.Sum(card => card.Value);

        while (totalValue > 21 && aceCount > 0)
        {
            totalValue -= 10;
            aceCount--;
        }

        return totalValue;
    }

    public bool IsBlackjack() => Cards.Count is 2 && GetHandValue() is 21;
    
    public static void ThrowIfDeckIsNotInitialised([NotNull] Hand? hand)
    {
        if (hand is null) throw new InvalidOperationException("Hand is not initialized.");
    }
}
