namespace Credify.Configuration;

public class WheelConfiguration
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Wheel segments with weights and percentage-based payouts.
    /// Higher weight = more likely to land on.
    /// </summary>
    public List<WheelSegment> Segments { get; set; } =
    [
        new() { Name = "25% LOSS", PercentageMultiplier = -0.25, IsPercentageLoss = true, Weight = 8 },
        new() { Name = "50% LOSS", PercentageMultiplier = -0.5, IsPercentageLoss = true, Weight = 6 },
        new() { Name = "BREAK EVEN", PercentageMultiplier = 0.0, MinPayout = 0, MaxPayout = 0, Weight = 6 },
        new() { Name = "SMALL WIN", PercentageMultiplier = 0.1, MinPayout = 1_000, MaxPayout = 8_000, Weight = 30 },
        new() { Name = "MEDIUM WIN", PercentageMultiplier = 0.25, MinPayout = 2_500, MaxPayout = 20_000, Weight = 28 },
        new() { Name = "LARGE WIN", PercentageMultiplier = 0.5, MinPayout = 10_000, MaxPayout = 50_000, Weight = 15 },
        new() { Name = "JACKPOT", IsOneHundredKOrDouble = true, Weight = 1.0 }
    ];
}

public class WheelSegment
{
    public string Name { get; set; } = "";
    public double Weight { get; set; } = 10;
    
    /// <summary>
    /// Percentage multiplier of balance (e.g., 0.1 for +10%, -0.5 for -50%)
    /// </summary>
    public double? PercentageMultiplier { get; set; }
    
    /// <summary>
    /// Minimum cap for percentage-based payouts
    /// </summary>
    public long? MinPayout { get; set; }
    
    /// <summary>
    /// Maximum cap for percentage-based payouts
    /// </summary>
    public long? MaxPayout { get; set; }
    
    /// <summary>
    /// Flag for percentage loss segments (no caps applied)
    /// </summary>
    public bool IsPercentageLoss { get; set; } = false;
    
    /// <summary>
    /// Flag for special "2X Cash" segment
    /// </summary>
    public bool IsOneHundredKOrDouble { get; set; } = false;
}
