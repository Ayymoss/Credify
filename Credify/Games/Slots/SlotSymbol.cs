namespace Credify.Games.Slots;

/// <summary>
/// A single reel symbol. Pure data — shared by the configuration (which seeds the reel strip),
/// the <see cref="SlotMachine"/> rules engine, and both the chat and web presentations.
/// </summary>
public class SlotSymbol
{
    public string Name { get; set; } = "";
    public string Display { get; set; } = "";

    /// <summary>Relative draw weight (higher = more common). Reels are independent weighted draws.</summary>
    public int Weight { get; set; } = 10;

    /// <summary>Marks the top symbol: three of these pays the jackpot multiplier instead of the three-match one.</summary>
    public bool IsJackpot { get; set; } = false;
}
