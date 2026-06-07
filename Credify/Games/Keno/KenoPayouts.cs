using System.Collections.Generic;
using System.Linq;

namespace Credify.Games.Keno;

/// <summary>
/// Keno paytable: for a given number of spots picked (1–10) and how many of them were drawn (hits), the
/// gross payout multiplier on the stake. Any (spots, hits) not listed pays nothing.
///
/// Tuned (against the real hypergeometric odds of 20 drawn from 80) to ~88–95% RTP per spot count — in line
/// with Credify's other games. Each ticket pays from a few hits below the count (e.g. an 8-spot pays from
/// 4 hits, a ~10% chance of *something*), with rare, escalating jackpots up to the full catch.
/// </summary>
public static class KenoPayouts
{
    public const int MaxSpots = 10;
    public const int PoolSize = 80;
    public const int DrawCount = 20;

    private static readonly IReadOnlyDictionary<int, IReadOnlyDictionary<int, double>> Table =
        new Dictionary<int, IReadOnlyDictionary<int, double>>
        {
            [1] = new Dictionary<int, double> { [1] = 3.5 },
            [2] = new Dictionary<int, double> { [2] = 15 },
            [3] = new Dictionary<int, double> { [2] = 2.5, [3] = 42 },
            [4] = new Dictionary<int, double> { [2] = 1.5, [3] = 6, [4] = 120 },
            [5] = new Dictionary<int, double> { [3] = 2.5, [4] = 16, [5] = 800 },
            [6] = new Dictionary<int, double> { [3] = 2, [4] = 8.5, [5] = 76, [6] = 1500 },
            [7] = new Dictionary<int, double> { [4] = 4.5, [5] = 28, [6] = 325, [7] = 7500 },
            [8] = new Dictionary<int, double> { [4] = 2.5, [5] = 11, [6] = 84, [7] = 1250, [8] = 25000 },
            [9] = new Dictionary<int, double> { [4] = 1.5, [5] = 5.5, [6] = 30, [7] = 290, [8] = 5300, [9] = 50000 },
            [10] = new Dictionary<int, double> { [5] = 3.5, [6] = 15, [7] = 110, [8] = 1300, [9] = 29000, [10] = 100000 },
        };

    public static double Multiplier(int spots, int hits) =>
        Table.TryGetValue(spots, out var row) && row.TryGetValue(hits, out var mult) ? mult : 0;

    /// <summary>Paying (hits, multiplier) rows for a spot count, ascending — for the paytable UI.</summary>
    public static IEnumerable<(int Hits, double Multiplier)> Rows(int spots) =>
        spots > 0 && Table.TryGetValue(spots, out var row)
            ? row.OrderBy(entry => entry.Key).Select(entry => (entry.Key, entry.Value))
            : [];
}
