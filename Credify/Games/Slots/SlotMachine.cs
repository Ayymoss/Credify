using System;
using System.Collections.Generic;
using System.Linq;

namespace Credify.Games.Slots;

/// <summary>
/// Pure 3-reel slot rules: independent weighted draws + win classification. No I/O, no economy —
/// the chat command and the web page both spin through here so the odds and payouts can never drift
/// apart. The RNG is injectable so the same engine can be driven by <see cref="Random.Shared"/>,
/// a crypto source, or a seeded sequence in tests.
/// </summary>
public static class SlotMachine
{
    /// <param name="next">Returns an int in [0, maxExclusive). Defaults to <see cref="Random.Shared"/>.</param>
    public static SlotSpin Spin(IReadOnlyList<SlotSymbol> symbols, double twoMatchMultiplier,
        double threeMatchMultiplier, double jackpotMultiplier, Func<int, int>? next = null)
    {
        if (symbols is null || symbols.Count == 0)
        {
            throw new ArgumentException("At least one reel symbol is required.", nameof(symbols));
        }

        next ??= Random.Shared.Next;
        var totalWeight = symbols.Sum(symbol => symbol.Weight);

        var reels = new SlotSymbol[3];
        for (var i = 0; i < reels.Length; i++)
        {
            reels[i] = PickWeighted(symbols, totalWeight, next);
        }

        var (outcome, multiplier) = Evaluate(reels, twoMatchMultiplier, threeMatchMultiplier, jackpotMultiplier);
        return new SlotSpin(reels, outcome, multiplier);
    }

    private static SlotSymbol PickWeighted(IReadOnlyList<SlotSymbol> symbols, int totalWeight, Func<int, int> next)
    {
        var roll = next(totalWeight);
        var cumulative = 0;
        foreach (var symbol in symbols)
        {
            cumulative += symbol.Weight;
            if (roll < cumulative)
            {
                return symbol;
            }
        }

        return symbols[^1];
    }

    private static (SlotOutcome Outcome, double Multiplier) Evaluate(IReadOnlyList<SlotSymbol> reels,
        double twoMatchMultiplier, double threeMatchMultiplier, double jackpotMultiplier)
    {
        var allMatch = reels[0].Name == reels[1].Name && reels[1].Name == reels[2].Name;
        if (allMatch)
        {
            return reels[0].IsJackpot
                ? (SlotOutcome.Jackpot, jackpotMultiplier)
                : (SlotOutcome.ThreeOfAKind, threeMatchMultiplier);
        }

        var anyPair = reels[0].Name == reels[1].Name
                      || reels[1].Name == reels[2].Name
                      || reels[0].Name == reels[2].Name;

        return anyPair ? (SlotOutcome.TwoOfAKind, twoMatchMultiplier) : (SlotOutcome.Loss, 0d);
    }
}
