namespace Credify.Configuration;

public class CrashConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public int MinBet { get; set; } = 10;
    public int MaxBet { get; set; } = 50_000;

    /// <summary>
    /// House edge applied to the crash-point distribution. Expected RTP = 1 - HouseEdge,
    /// since payout if you cash at x is x and P(crash >= x) = (1-edge)/x. Default 3pct.
    /// </summary>
    public double HouseEdge { get; set; } = 0.03;

    /// <summary>Multiplier ceiling (caps the rare runaway crash point and bounds round length).</summary>
    public double MaxMultiplier { get; set; } = 25.0;

    /// <summary>How long players have to place a bet before the rocket launches.</summary>
    public TimeSpan BettingDuration { get; set; } = TimeSpan.FromSeconds(12);

    /// <summary>Delay between multiplier ticks during flight.</summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromMilliseconds(1200);

    /// <summary>Multiplier growth per tick (1.12 = +12pct each tick).</summary>
    public double GrowthPerTick { get; set; } = 1.12;
}
