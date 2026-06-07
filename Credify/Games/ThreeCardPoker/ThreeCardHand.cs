using System;
using System.Collections.Generic;
using System.Linq;
using Credify.Games.Cards;

namespace Credify.Games.ThreeCardPoker;

/// <summary>
/// A ranked three-card hand: its category plus tie-break ranks (high to low). Pure — evaluate three cards,
/// then compare hands directly. A-2-3 is the lowest straight (Ace plays low); Q-K-A is the highest.
/// </summary>
public sealed record ThreeCardHand(ThreeCardCategory Category, IReadOnlyList<int> Tiebreak) : IComparable<ThreeCardHand>
{
    public static ThreeCardHand Evaluate(IReadOnlyList<Card> cards)
    {
        var ranksDesc = cards.Select(card => (int)card.Rank).OrderByDescending(rank => rank).ToList();
        var ranksAsc = ranksDesc.AsEnumerable().Reverse().ToList();

        var isFlush = cards.Select(card => card.Suit).Distinct().Count() == 1;
        var isThree = ranksDesc.Distinct().Count() == 1;

        var allDistinct = ranksDesc.Distinct().Count() == 3;
        var normalStraight = allDistinct && ranksAsc[2] - ranksAsc[0] == 2;
        var wheel = ranksAsc.SequenceEqual([2, 3, 14]); // A-2-3
        var isStraight = normalStraight || wheel;
        var straightHigh = wheel ? 3 : ranksAsc[2];

        if (isStraight && isFlush)
        {
            return new ThreeCardHand(ThreeCardCategory.StraightFlush, [straightHigh]);
        }

        if (isThree)
        {
            return new ThreeCardHand(ThreeCardCategory.ThreeOfAKind, [ranksDesc[0]]);
        }

        if (isStraight)
        {
            return new ThreeCardHand(ThreeCardCategory.Straight, [straightHigh]);
        }

        if (isFlush)
        {
            return new ThreeCardHand(ThreeCardCategory.Flush, ranksDesc);
        }

        var pairRank = ranksDesc.GroupBy(rank => rank).Where(group => group.Count() == 2)
            .Select(group => group.Key).FirstOrDefault();
        if (pairRank != 0)
        {
            var kicker = ranksDesc.First(rank => rank != pairRank);
            return new ThreeCardHand(ThreeCardCategory.Pair, [pairRank, kicker]);
        }

        return new ThreeCardHand(ThreeCardCategory.HighCard, ranksDesc);
    }

    public int CompareTo(ThreeCardHand? other)
    {
        if (other is null)
        {
            return 1;
        }

        if (Category != other.Category)
        {
            return Category.CompareTo(other.Category);
        }

        for (var i = 0; i < Math.Min(Tiebreak.Count, other.Tiebreak.Count); i++)
        {
            var compare = Tiebreak[i].CompareTo(other.Tiebreak[i]);
            if (compare != 0)
            {
                return compare;
            }
        }

        return 0;
    }

    public string Name => Category switch
    {
        ThreeCardCategory.StraightFlush => "Straight flush",
        ThreeCardCategory.ThreeOfAKind => "Three of a kind",
        ThreeCardCategory.Straight => "Straight",
        ThreeCardCategory.Flush => "Flush",
        ThreeCardCategory.Pair => "Pair",
        _ => "High card"
    };
}
