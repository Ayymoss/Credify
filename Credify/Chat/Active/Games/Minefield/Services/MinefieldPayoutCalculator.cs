using Credify.Configuration;

namespace Credify.Chat.Active.Games.Minefield.Services;

/// <summary>
/// Computes Minefield multipliers and payouts using the standard "Mines" formula,
/// adjusted by a configurable house edge (real-world casinos use ~1%, i.e. 0.99).
/// </summary>
public class MinefieldPayoutCalculator(MinefieldConfiguration config)
{
    /// <summary>
    /// Fair, edge-adjusted multiplier after clearing <paramref name="dug"/> safe tiles.
    /// </summary>
    /// <param name="totalTiles">Total tiles in the field (N).</param>
    /// <param name="mines">Number of mines (M).</param>
    /// <param name="dug">Safe tiles cleared so far (k).</param>
    /// <remarks>
    /// multiplier(k) = EDGE * product_{i=0..k-1} ( (N - i) / (N - M - i) ).
    /// This is exactly the inverse of the cumulative survival probability times the
    /// edge, so expected value stays below the stake for any mine count.
    /// </remarks>
    public double CalculateMultiplier(int totalTiles, int mines, int dug)
    {
        var multiplier = config.HouseEdge;
        for (var i = 0; i < dug; i++)
        {
            multiplier *= (double)(totalTiles - i) / (totalTiles - mines - i);
        }

        return multiplier;
    }

    /// <summary>
    /// Total credits returned to the player for cashing out at the current progress.
    /// Floored to whole credits.
    /// </summary>
    public long CalculatePayout(long stake, int totalTiles, int mines, int dug)
    {
        return (long)Math.Floor(stake * CalculateMultiplier(totalTiles, mines, dug));
    }

    /// <summary>
    /// Probability (0..1) of clearing <paramref name="dug"/> safe tiles in a row -
    /// i.e. the odds the player beat to get this far. Smaller = more impressive.
    /// </summary>
    public double CalculateSurvivalProbability(int totalTiles, int mines, int dug)
    {
        var probability = 1.0;
        for (var i = 0; i < dug; i++)
        {
            probability *= (double)(totalTiles - mines - i) / (totalTiles - i);
        }

        return probability;
    }

    /// <summary>
    /// Probability (0..1) that the very next tile is a mine, given <paramref name="dug"/>
    /// safe tiles already cleared. This is the per-turn risk a player faces on a dig.
    /// </summary>
    public double CalculateNextTileMineChance(int totalTiles, int mines, int dug)
    {
        return (double)mines / (totalTiles - dug);
    }

    /// <summary>
    /// Formats a 0..1 probability as a percentage string (without the "pct" suffix,
    /// which is added in translations - the "%" symbol is not renderable in chat).
    /// Scales precision so tiny odds stay legible.
    /// </summary>
    public static string FormatProbabilityPct(double probability)
    {
        var pct = probability * 100;
        return pct switch
        {
            >= 10 => pct.ToString("0"),
            >= 1 => pct.ToString("0.0"),
            >= 0.01 => pct.ToString("0.00"),
            _ => pct.ToString("0.0000")
        };
    }
}
