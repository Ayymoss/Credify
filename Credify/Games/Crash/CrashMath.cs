using System.Security.Cryptography;
using Credify.Configuration;

namespace Credify.Games.Crash;

/// <summary>
/// Pure Crash math — the crash-point distribution and the multiplier-over-time curve — with no I/O. Shared
/// so the (web) game and any future consumer agree on the maths. The crash point is drawn once when a round
/// starts; the multiplier then grows continuously with elapsed time. A round is fair: payout if you cash at
/// x is x, and P(crash ≥ x) = (1 − edge)/x, so expected value = 1 − edge.
/// </summary>
public static class CrashMath
{
    /// <summary>
    /// Draws a crash point using a crypto RNG. Distribution: raw = (1 − edge)/(1 − u), clamped to
    /// [1, MaxMultiplier], rounded to 2 dp. Lower u → near 1.0 (early bust); u → 1 → near the ceiling.
    /// </summary>
    public static double NextCrashPoint(CrashConfiguration config)
    {
        // uniform (0,1) from the crypto RNG
        var u = RandomNumberGenerator.GetInt32(0, 1_000_000) / 1_000_000.0;
        var raw = (1.0 - config.HouseEdge) / (1.0 - u);
        return Math.Clamp(Math.Round(raw, 2), 1.0, config.MaxMultiplier);
    }

    /// <summary>
    /// Multiplier after <paramref name="elapsedSeconds"/> of flight. Continuous form of the chat game's
    /// per-tick growth: multiplier = GrowthPerTick ^ (elapsed / TickInterval), so the tuning carries over.
    /// </summary>
    public static double MultiplierAt(CrashConfiguration config, double elapsedSeconds)
    {
        var ticks = elapsedSeconds / config.TickInterval.TotalSeconds;
        return Math.Pow(config.GrowthPerTick, ticks);
    }

    /// <summary>Seconds of flight needed to reach <paramref name="multiplier"/> (inverse of MultiplierAt).</summary>
    public static double TimeToReach(CrashConfiguration config, double multiplier)
    {
        if (multiplier <= 1.0)
        {
            return 0;
        }

        var ticks = Math.Log(multiplier) / Math.Log(config.GrowthPerTick);
        return ticks * config.TickInterval.TotalSeconds;
    }
}
