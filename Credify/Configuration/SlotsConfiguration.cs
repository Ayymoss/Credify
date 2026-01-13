namespace Credify.Configuration;

public class SlotsConfiguration
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
    /// Slot symbols and their weights (higher = more common)
    /// </summary>
    public List<SlotSymbol> Symbols { get; set; } =
    [
        new SlotSymbol { Name = "7", Display = "7", Weight = 5, IsJackpot = true },
        new SlotSymbol { Name = "BAR", Display = "BAR", Weight = 10 },
        new SlotSymbol { Name = "BELL", Display = "BELL", Weight = 15 },
        new SlotSymbol { Name = "CHERRY", Display = "CHERRY", Weight = 25 },
        new SlotSymbol { Name = "LEMON", Display = "LEMON", Weight = 25 },
        new SlotSymbol { Name = "ORANGE", Display = "ORANGE", Weight = 20 }
    ];
    
    /// <summary>
    /// Payout multipliers
    /// </summary>
    public double ThreeMatchMultiplier { get; set; } = 10.0;
    public double TwoMatchMultiplier { get; set; } = 2.0;
    public double JackpotMultiplier { get; set; } = 50.0;
}

public class SlotSymbol
{
    public string Name { get; set; } = "";
    public string Display { get; set; } = "";
    public int Weight { get; set; } = 10;
    public bool IsJackpot { get; set; } = false;
}
