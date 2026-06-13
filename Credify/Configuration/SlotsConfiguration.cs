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
    // Tuned to RTP = 100% — no house edge. Credify is a fair-economy casino: the bank neither
    // gains nor loses in expectation, so the credit pool stays stable instead of bleeding players.
    // With the default weights above (total 100), per spin:
    //   - three-of-a-kind (non-7): 4.3625%  -> 22x  => 0.95975
    //   - jackpot (7 7 7):         0.0125%  -> 322x => 0.04025
    //   - any two matching:        46.875%  -> 0x   (no win, like a real single-line reel)
    //   RTP = 0.95975 + 0.04025 = 1.0000
    //
    // To keep it 1:1 after any change, re-solve  P(3oak)*Three + P(jackpot)*Jackpot = 1, where
    // P(3oak) = Σ p^3 over the non-7 symbols and P(jackpot) = p7^3 (p = weight / total weight).
    // WARNING: "any two matching" lands ~47% of spins with 6 symbols, so even a 2x there blows the
    // RTP far past 100%. Keep TwoMatchMultiplier at 0 unless you re-derive everything.

    /// <summary>Three matching (non-jackpot) symbols. Drives almost all of the RTP.</summary>
    public double ThreeMatchMultiplier { get; set; } = 22.0;

    /// <summary>Two matching symbols. 0 = no payout (realistic for a single-line 3-reel slot).</summary>
    public double TwoMatchMultiplier { get; set; } = 0.0;

    /// <summary>Three jackpot symbols (7 7 7). Rare (~1 in 8000) so it can pay big.</summary>
    public double JackpotMultiplier { get; set; } = 322.0;

    /// <summary>
    /// "Gamble" (double-or-nothing) feature: after a win the player may risk it on a fair 50/50
    /// red/black flip. Each flip is exactly even money, so it adds zero house edge — the game stays 1:1.
    /// </summary>
    public bool GambleEnabled { get; set; } = true;

    /// <summary>Maximum consecutive gambles before the winnings are force-collected (bounds variance).</summary>
    public int GambleMaxSteps { get; set; } = 4;
}
