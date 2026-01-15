using Credify.Configuration;

namespace Credify.Services;

/// <summary>
/// Service for wheel of fortune game logic.
/// Handles segment adjustment, payout calculation, and wheel spinning.
/// </summary>
public class WheelService
{
    private readonly CredifyConfiguration _credifyConfig;

    public WheelService(CredifyConfiguration credifyConfig)
    {
        _credifyConfig = credifyConfig;
    }

    /// <summary>
    /// Gets adjusted segments based on balance with enhanced dynamic scaling for loss segments.
    /// </summary>
    public List<WheelSegment> GetAdjustedSegments(long balance)
    {
        var segments = new List<WheelSegment>(_credifyConfig.Wheel.Segments);
        
        // Find loss segments and adjust weight based on balance (enhanced dynamic scaling)
        // Both 25% and 50% loss segments scale with balance
        var lossSegments = segments.Where(s => s.IsPercentageLoss).ToList();
        if (lossSegments.Any())
        {
            // Enhanced scaling: starts earlier and is more aggressive
            var baseWeight25 = 8.0; // Base weight for 25% LOSS
            var baseWeight50 = 6.0; // Base weight for 50% LOSS
            
            var (adjustedWeight25, adjustedWeight50) = balance switch
            {
                < 50_000 => (baseWeight25, baseWeight50),                    // No scaling
                < 100_000 => (baseWeight25 * 1.5, baseWeight50 * 1.5),      // 1.5x
                < 250_000 => (baseWeight25 * 3, baseWeight50 * 3),           // 3x
                < 500_000 => (baseWeight25 * 6, baseWeight50 * 6),           // 6x
                < 1_000_000 => (baseWeight25 * 12, baseWeight50 * 12),      // 12x
                _ => (baseWeight25 * 24, baseWeight50 * 24)                 // 24x
            };
            
            // Create new segment list with adjusted weights for loss segments
            segments = segments.Select(s => 
            {
                if (s.IsPercentageLoss)
                {
                    var is25Percent = s.PercentageMultiplier == -0.25;
                    var adjustedWeight = is25Percent ? adjustedWeight25 : adjustedWeight50;
                    
                    return new WheelSegment
                    {
                        Name = s.Name,
                        PercentageMultiplier = s.PercentageMultiplier,
                        IsPercentageLoss = true,
                        Weight = adjustedWeight,
                        IsOneHundredKOrDouble = s.IsOneHundredKOrDouble,
                        MinPayout = s.MinPayout,
                        MaxPayout = s.MaxPayout
                    };
                }
                return s;
            }).ToList();
        }
        
        return segments;
    }

    /// <summary>
    /// Calculates payout for a given segment and balance.
    /// </summary>
    public long CalculatePayout(WheelSegment segment, long balance)
    {
        // Handle BREAK EVEN segment (payout equals bet, profit = 0)
        if (segment.Name.Equals("BREAK EVEN", StringComparison.OrdinalIgnoreCase) ||
            (segment.PercentageMultiplier.HasValue && segment.PercentageMultiplier.Value == 0.0))
        {
            return balance; // Return bet amount, so profit = 0
        }
        
        // Handle percentage loss (no caps)
        if (segment is { IsPercentageLoss: true, PercentageMultiplier: not null })
        {
            return (long)(balance * segment.PercentageMultiplier.Value);
        }
        
        // Handle JACKPOT with tiered approach for better distribution
        if (segment.IsOneHundredKOrDouble)
        {
            // Tiered JACKPOT to create smoother progression through middle ranges
            return balance switch
            {
                < 50_000 => balance + 30_000,      // Fixed bonus: gets players to ~30-80K range
                < 100_000 => (long)(balance * 1.5), // 1.5X multiplier: gets players to 75K-150K range
                _ => (long)(balance * 1.25)        // 1.25X multiplier: slower growth for high balances
            };
        }
        
        // Handle percentage-based payouts with caps
        if (segment.PercentageMultiplier.HasValue)
        {
            var basePayout = (long)(balance * segment.PercentageMultiplier.Value);
            if (segment.MinPayout.HasValue) basePayout = Math.Max(basePayout, segment.MinPayout.Value);
            if (segment.MaxPayout.HasValue) basePayout = Math.Min(basePayout, segment.MaxPayout.Value);
            
            // Ensure "WIN" segments always result in at least break-even (payout >= bet)
            // This prevents "MEDIUM WIN" from showing a loss when betting full balance
            if (segment.Name.Contains("WIN", StringComparison.OrdinalIgnoreCase) && basePayout < balance)
            {
                basePayout = balance; // At least break even
            }
            
            return basePayout;
        }
        
        // If no payout method matches, return 0 (should not happen with proper configuration)
        return 0;
    }

    /// <summary>
    /// Spins the wheel and returns the selected segment using Random.Shared.
    /// </summary>
    public WheelSegment SpinWheel(List<WheelSegment> segments)
    {
        return SpinWheel(segments, Random.Shared);
    }

    /// <summary>
    /// Spins the wheel and returns the selected segment using the provided Random instance.
    /// </summary>
    public static WheelSegment SpinWheel(List<WheelSegment> segments, Random random)
    {
        var totalWeight = segments.Sum(s => s.Weight);
        var roll = random.NextDouble() * totalWeight;
        var cumulative = 0.0;
        
        foreach (var segment in segments)
        {
            cumulative += segment.Weight;
            if (roll < cumulative)
            {
                return segment;
            }
        }
        
        return segments.Last();
    }
}
