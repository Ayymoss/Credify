namespace Credify.Games.Plinko;

/// <summary>
/// Risk profile for a Plinko drop. Higher risk widens the multiplier spread: the centre buckets pay less
/// (often below the stake) while the rare edge buckets pay far more. The expected value is identical across
/// every profile — the house edge is baked into <see cref="PlinkoPayoutCalculator"/> independently of risk.
/// </summary>
public enum PlinkoRisk
{
    Low,
    Medium,
    High
}
