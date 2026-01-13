using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;

namespace Credify.Chat.Active.Games.Poker.Services;

/// <summary>
/// Service responsible for evaluating poker hands and comparing them.
/// </summary>
public class PokerHandEvaluator
{
    /// <summary>
    /// Evaluates the best 5-card hand from 7 cards (2 hole cards + 5 community cards).
    /// </summary>
    public PokerHand EvaluateBestHand(List<PokerCard> holeCards, List<PokerCard> communityCards)
    {
        if (holeCards.Count != 2) throw new ArgumentException("Must have exactly 2 hole cards", nameof(holeCards));
        if (communityCards.Count != 5) throw new ArgumentException("Must have exactly 5 community cards", nameof(communityCards));

        var allCards = holeCards.Concat(communityCards).ToList();
        var allCombinations = Get5CardCombinations(allCards);
        
        PokerHand? bestHand = null;
        foreach (var combination in allCombinations)
        {
            var hand = Evaluate5CardHand(combination);
            if (bestHand is null || hand.CompareTo(bestHand) > 0)
            {
                bestHand = hand;
            }
        }

        return bestHand!;
    }

    /// <summary>
    /// Evaluates a 5-card poker hand.
    /// </summary>
    public PokerHand Evaluate5CardHand(List<PokerCard> cards)
    {
        if (cards.Count != 5) throw new ArgumentException("Must have exactly 5 cards", nameof(cards));

        var sortedCards = cards.OrderByDescending(c => c.GetNumericValue()).ToList();
        var values = sortedCards.Select(c => c.GetNumericValue()).ToList();
        var suits = sortedCards.Select(c => c.CardSuit).ToList();

        var isFlush = suits.Distinct().Count() == 1;
        var isStraight = IsStraight(values);
        var isRoyal = isStraight && values[0] == 14 && values[1] == 13;

        // Royal Flush
        if (isRoyal && isFlush)
        {
            return new PokerHand
            {
                Rank = HandRank.RoyalFlush,
                Cards = sortedCards,
                Kickers = []
            };
        }

        // Straight Flush
        if (isStraight && isFlush)
        {
            return new PokerHand
            {
                Rank = HandRank.StraightFlush,
                Cards = sortedCards,
                Kickers = [values[0]]
            };
        }

        // Four of a Kind
        var fourOfAKind = GetFourOfAKind(values);
        if (fourOfAKind.HasValue)
        {
            var kicker = values.First(v => v != fourOfAKind.Value);
            return new PokerHand
            {
                Rank = HandRank.FourOfAKind,
                Cards = sortedCards,
                Kickers = [fourOfAKind.Value, kicker]
            };
        }

        // Full House
        var threeOfAKind = GetThreeOfAKind(values);
        var pair = GetPair(values, threeOfAKind);
        if (threeOfAKind.HasValue && pair.HasValue)
        {
            return new PokerHand
            {
                Rank = HandRank.FullHouse,
                Cards = sortedCards,
                Kickers = [threeOfAKind.Value, pair.Value]
            };
        }

        // Flush
        if (isFlush)
        {
            return new PokerHand
            {
                Rank = HandRank.Flush,
                Cards = sortedCards,
                Kickers = values
            };
        }

        // Straight
        if (isStraight)
        {
            return new PokerHand
            {
                Rank = HandRank.Straight,
                Cards = sortedCards,
                Kickers = [values[0]]
            };
        }

        // Three of a Kind
        if (threeOfAKind.HasValue)
        {
            var kickers = values.Where(v => v != threeOfAKind.Value).OrderByDescending(v => v).ToList();
            return new PokerHand
            {
                Rank = HandRank.ThreeOfAKind,
                Cards = sortedCards,
                Kickers = [threeOfAKind.Value, kickers[0], kickers[1]]
            };
        }

        // Two Pair
        var twoPairResult = GetTwoPair(values);
        if (twoPairResult.pair1.HasValue && twoPairResult.pair2.HasValue)
        {
            var kicker = values.First(v => v != twoPairResult.pair1.Value && v != twoPairResult.pair2.Value);
            var pairs = new[] { twoPairResult.pair1.Value, twoPairResult.pair2.Value }.OrderByDescending(v => v).ToList();
            return new PokerHand
            {
                Rank = HandRank.TwoPair,
                Cards = sortedCards,
                Kickers = [pairs[0], pairs[1], kicker]
            };
        }

        // Pair
        if (pair.HasValue)
        {
            var kickers = values.Where(v => v != pair.Value).OrderByDescending(v => v).ToList();
            return new PokerHand
            {
                Rank = HandRank.Pair,
                Cards = sortedCards,
                Kickers = [pair.Value, kickers[0], kickers[1], kickers[2]]
            };
        }

        // High Card
        return new PokerHand
        {
            Rank = HandRank.HighCard,
            Cards = sortedCards,
            Kickers = values
        };
    }

    private List<List<PokerCard>> Get5CardCombinations(List<PokerCard> cards)
    {
        var combinations = new List<List<PokerCard>>();
        
        // Generate all C(7,5) combinations
        for (int i = 0; i < cards.Count; i++)
        {
            for (int j = i + 1; j < cards.Count; j++)
            {
                combinations.Add(cards.Where((_, idx) => idx != i && idx != j).ToList());
            }
        }

        return combinations;
    }

    private bool IsStraight(List<int> values)
    {
        // Special case for A-2-3-4-5 (wheel)
        if (values.SequenceEqual([14, 5, 4, 3, 2]))
        {
            return true;
        }

        // Check for regular straight
        for (int i = 0; i < values.Count - 1; i++)
        {
            if (values[i] - values[i + 1] != 1)
            {
                return false;
            }
        }

        return true;
    }

    private int? GetFourOfAKind(List<int> values)
    {
        return values.GroupBy(v => v)
            .Where(g => g.Count() == 4)
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    private int? GetThreeOfAKind(List<int> values)
    {
        return values.GroupBy(v => v)
            .Where(g => g.Count() == 3)
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    private int? GetPair(List<int> values, int? excludeValue = null)
    {
        return values.GroupBy(v => v)
            .Where(g => g.Count() == 2 && (excludeValue == null || g.Key != excludeValue))
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    private (int? pair1, int? pair2) GetTwoPair(List<int> values)
    {
        var pairs = values.GroupBy(v => v)
            .Where(g => g.Count() == 2)
            .Select(g => g.Key)
            .OrderByDescending(v => v)
            .ToList();

        if (pairs.Count >= 2)
        {
            return (pairs[0], pairs[1]);
        }

        return (null, null);
    }
}
