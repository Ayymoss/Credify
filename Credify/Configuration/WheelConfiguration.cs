namespace Credify.Configuration;

public class WheelConfiguration
{
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Minimum bet amount
    /// </summary>
    public int MinBet { get; set; } = 10;
    
    /// <summary>
    /// Maximum bet amount (0 = unlimited)
    /// </summary>
    public int MaxBet { get; set; } = 50_000;
    
    /// <summary>
    /// Wheel segments with weights and multipliers.
    /// Higher weight = more likely to land on.
    /// </summary>
    public List<WheelSegment> Segments { get; set; } =
    [
        new WheelSegment { Name = "BANKRUPT", Multiplier = 0.0, Weight = 15 },
        new WheelSegment { Name = "x0.5", Multiplier = 0.5, Weight = 25 },
        new WheelSegment { Name = "x1", Multiplier = 1.0, Weight = 20 },
        new WheelSegment { Name = "x1.5", Multiplier = 1.5, Weight = 18 },
        new WheelSegment { Name = "x2", Multiplier = 2.0, Weight = 12 },
        new WheelSegment { Name = "x3", Multiplier = 3.0, Weight = 7 },
        new WheelSegment { Name = "x5", Multiplier = 5.0, Weight = 2.5 },
        new WheelSegment { Name = "JACKPOT", Multiplier = 25.0, Weight = 0.5, IsJackpot = true }
    ];
}

public class WheelSegment
{
    public string Name { get; set; } = "";
    public double Multiplier { get; set; } = 1.0;
    public double Weight { get; set; } = 10;
    public bool IsJackpot { get; set; } = false;
}
