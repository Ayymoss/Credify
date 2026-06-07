using System;
using Credify.Configuration;

namespace Credify.Games.Plinko;

/// <summary>
/// Computes Plinko bucket multipliers and payouts. Multipliers are <b>derived</b> from the binomial landing
/// distribution rather than hand-authored tables, so the expected return is exactly the configured house edge
/// for <i>any</i> row count and risk profile — there are no per-board tables to keep in sync. Pure math, no I/O.
///
/// <para>
/// A ball landing in bucket <c>k</c> (of <c>N</c> rows) has probability <c>p_k = C(N,k) / 2^N</c>. We pay
/// <c>m_k = EDGE · p_k^(−v) / Σ_j p_j^(1−v)</c>, where <c>v</c> is the risk volatility. This guarantees
/// <c>Σ_k p_k·m_k = EDGE</c> regardless of <c>v</c>: raising <c>v</c> only reshapes the curve (rarer edge
/// buckets pay more, the common centre pays less) without changing the return. <c>v = 0</c> pays a flat
/// <c>EDGE×</c> everywhere; larger <c>v</c> sharpens the classic Plinko "U".
/// </para>
/// </summary>
public class PlinkoPayoutCalculator(PlinkoConfiguration config)
{
    /// <summary>Risk → volatility exponent. Larger = wider multiplier spread (riskier curve).</summary>
    public double VolatilityFor(PlinkoRisk risk) => risk switch
    {
        PlinkoRisk.Low => config.LowRiskVolatility,
        PlinkoRisk.High => config.HighRiskVolatility,
        _ => config.MediumRiskVolatility
    };

    /// <summary>Binomial landing probabilities <c>p_0..p_rows</c>, computed iteratively (no factorials).</summary>
    public double[] BucketProbabilities(int rows)
    {
        if (rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rows));
        }

        var probabilities = new double[rows + 1];
        // p_0 = 1/2^rows, then p_k = p_{k-1} · (rows - k + 1) / k. Stable for the row counts we support.
        probabilities[0] = Math.Pow(0.5, rows);
        for (var k = 1; k <= rows; k++)
        {
            probabilities[k] = probabilities[k - 1] * (rows - k + 1) / k;
        }

        return probabilities;
    }

    /// <summary>
    /// The full multiplier curve for a board: <c>m_k = EDGE · p_k^(−v) / Σ_j p_j^(1−v)</c>. Symmetric, with the
    /// largest payouts at the edges and the smallest in the centre.
    /// </summary>
    public double[] Multipliers(int rows, PlinkoRisk risk)
    {
        var probabilities = BucketProbabilities(rows);
        var volatility = VolatilityFor(risk);

        var normaliser = 0.0;
        for (var k = 0; k < probabilities.Length; k++)
        {
            normaliser += Math.Pow(probabilities[k], 1 - volatility);
        }

        var multipliers = new double[probabilities.Length];
        for (var k = 0; k < probabilities.Length; k++)
        {
            multipliers[k] = config.HouseEdge * Math.Pow(probabilities[k], -volatility) / normaliser;
        }

        return multipliers;
    }

    /// <summary>Multiplier for a single bucket.</summary>
    public double MultiplierForBucket(int rows, PlinkoRisk risk, int bucket)
    {
        var multipliers = Multipliers(rows, risk);
        return bucket >= 0 && bucket < multipliers.Length ? multipliers[bucket] : 0d;
    }

    /// <summary>Credits returned for landing in <paramref name="bucket"/>, floored to whole credits.</summary>
    public long CalculatePayout(long stake, int rows, PlinkoRisk risk, int bucket)
    {
        return (long)Math.Floor(stake * MultiplierForBucket(rows, risk, bucket));
    }
}
