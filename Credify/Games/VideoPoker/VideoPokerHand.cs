using System.Collections.Generic;
using System.Linq;
using Credify.Games.Cards;

namespace Credify.Games.VideoPoker;

/// <summary>
/// Pure five-card evaluator for Jacks-or-Better video poker. Presentation-agnostic and deterministic —
/// the same hand always classifies the same way. A pair only counts when it's Jacks or better.
/// </summary>
public static class VideoPokerHand
{
    public static VideoPokerRank Evaluate(IReadOnlyList<Card> cards)
    {
        var values = cards.Select(c => (int)c.Rank).OrderBy(v => v).ToArray();
        var countsDesc = values.GroupBy(v => v).Select(g => g.Count()).OrderByDescending(c => c).ToArray();
        var distinct5 = values.Distinct().Count() == 5;
        var isFlush = cards.Select(c => c.Suit).Distinct().Count() == 1;

        // Ace plays high (…Q,K,A) and low only in the wheel A-2-3-4-5 (sorted: 2,3,4,5,14)
        var wheel = distinct5 && values.SequenceEqual([2, 3, 4, 5, 14]);
        var isStraight = distinct5 && (values[4] - values[0] == 4 || wheel);
        var isRoyal = isFlush && distinct5 && values.SequenceEqual([10, 11, 12, 13, 14]);

        if (isRoyal) return VideoPokerRank.RoyalFlush;
        if (isFlush && isStraight) return VideoPokerRank.StraightFlush;
        if (countsDesc[0] == 4) return VideoPokerRank.FourOfAKind;
        if (countsDesc[0] == 3 && countsDesc[1] == 2) return VideoPokerRank.FullHouse;
        if (isFlush) return VideoPokerRank.Flush;
        if (isStraight) return VideoPokerRank.Straight;
        if (countsDesc[0] == 3) return VideoPokerRank.ThreeOfAKind;
        if (countsDesc[0] == 2 && countsDesc[1] == 2) return VideoPokerRank.TwoPair;

        if (countsDesc[0] == 2)
        {
            var pairValue = values.GroupBy(v => v).First(g => g.Count() == 2).Key;
            if (pairValue >= 11) return VideoPokerRank.JacksOrBetter; // J=11, Q=12, K=13, A=14
        }

        return VideoPokerRank.None;
    }

    /// <summary>Friendly name for the result banner / paytable.</summary>
    public static string Name(VideoPokerRank rank) => rank switch
    {
        VideoPokerRank.RoyalFlush => "Royal Flush",
        VideoPokerRank.StraightFlush => "Straight Flush",
        VideoPokerRank.FourOfAKind => "Four of a Kind",
        VideoPokerRank.FullHouse => "Full House",
        VideoPokerRank.Flush => "Flush",
        VideoPokerRank.Straight => "Straight",
        VideoPokerRank.ThreeOfAKind => "Three of a Kind",
        VideoPokerRank.TwoPair => "Two Pair",
        VideoPokerRank.JacksOrBetter => "Jacks or Better",
        _ => "No win"
    };
}
