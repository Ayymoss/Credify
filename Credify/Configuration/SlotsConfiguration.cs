using Credify.Games.Slots;

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
    
    // Payout multipliers (gross return: a win pays back bet * multiplier).
    //
    // RTP is tuned to a realistic ~95% (4.6% house edge). With the default weights
    // above (total 100), per spin:
    //   - three-of-a-kind (non-7): 4.3625%  -> 21x  => 0.9161
    //   - jackpot (7 7 7):         0.0125%  -> 300x => 0.0375
    //   - any two matching:        46.875%  -> 0x   (no win, like a real single-line reel)
    //   RTP = 0.9161 + 0.0375 = ~0.954
    //
    // WARNING: paying "any two matching" is what made this game pay out at ~138% RTP.
    // With 6 symbols it lands ~47% of spins, so even a 2x there hands the player a huge
    // edge. Keep TwoMatchMultiplier at 0 unless you re-derive the whole RTP.

    /// <summary>Three matching (non-jackpot) symbols. Drives almost all of the RTP.</summary>
    public double ThreeMatchMultiplier { get; set; } = 21.0;

    /// <summary>Two matching symbols. 0 = no payout (realistic for a single-line 3-reel slot).</summary>
    public double TwoMatchMultiplier { get; set; } = 0.0;

    /// <summary>Three jackpot symbols (7 7 7). Rare (~1 in 8000) so it can pay big.</summary>
    public double JackpotMultiplier { get; set; } = 300.0;
}
