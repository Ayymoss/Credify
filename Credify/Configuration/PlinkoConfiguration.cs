namespace Credify.Configuration;

/// <summary>
/// Plinko is a webfront-only game (no in-game chat frontend). Multipliers aren't authored here — they're
/// derived from the binomial distribution by <see cref="Credify.Games.Plinko.PlinkoPayoutCalculator"/>, so the
/// return is always <see cref="HouseEdge"/> for any board. These knobs only choose which boards players can
/// pick and how aggressively each risk profile reshapes the (return-neutral) multiplier curve.
/// </summary>
public class PlinkoConfiguration
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>Minimum bet amount.</summary>
    public int MinBet { get; set; } = 10;

    /// <summary>Maximum bet amount (0 = unlimited).</summary>
    public int MaxBet { get; set; } = 50_000;

    /// <summary>
    /// Return-to-player factor (1 − house edge). 0.99 = a 1% house edge, applied uniformly across every bucket,
    /// row count and risk profile. Lower it to widen the house margin.
    /// </summary>
    public double HouseEdge { get; set; } = 0.99;

    /// <summary>Selectable peg-row counts. A board with N rows has N + 1 buckets.</summary>
    public int[] RowPresets { get; set; } = [8, 12, 16];

    /// <summary>Board selected by default when the page loads.</summary>
    public int DefaultRows { get; set; } = 12;

    // Risk volatility exponents (0 = flat 0.99× everywhere; higher = sharper "U", bigger edge payouts, the
    // centre dipping further below the stake). The expected return is HouseEdge at every setting.
    public double LowRiskVolatility { get; set; } = 0.50;
    public double MediumRiskVolatility { get; set; } = 0.72;
    public double HighRiskVolatility { get; set; } = 1.00;
}
