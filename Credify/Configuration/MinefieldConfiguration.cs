namespace Credify.Configuration;

public class MinefieldConfiguration
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>Total tiles in the field. 25 (5x5) mirrors the classic layout.</summary>
    public int TotalTiles { get; set; } = 25;

    /// <summary>
    /// House edge multiplier applied to every payout. Real-world "Mines" runs a 1%
    /// edge, i.e. 0.99. Lower = more house favour.
    /// </summary>
    public double HouseEdge { get; set; } = 0.99d;

    /// <summary>
    /// How long the player may idle between digs before the session auto-cashes at
    /// the current multiplier. Reuses the same window as the other active games.
    /// </summary>
    public TimeSpan TimeoutForPlayerAction { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A manual cash-out is announced to the current server when the odds the player
    /// beat are at or below this percentage (e.g. 5 = beat 1-in-20 or rarer) and they
    /// finished in profit. A full clear (only mines left) always announces to ALL
    /// servers regardless of this threshold.
    /// </summary>
    public double BroadcastProbabilityThresholdPct { get; set; } = 5d;
}
