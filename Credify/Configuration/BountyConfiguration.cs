namespace Credify.Configuration;

public class BountyConfiguration
{
    public bool AutoBountyEnabled { get; set; } = true;
    
    /// <summary>
    /// Number of kills without dying to trigger auto-bounty
    /// </summary>
    public int AutoBountyThreshold { get; set; } = 15;
    
    /// <summary>
    /// Base bounty amount placed on the streak holder
    /// </summary>
    public int AutoBountyAmount { get; set; } = 150;
    
    /// <summary>
    /// Additional bounty per kill above threshold
    /// </summary>
    public int BountyPerAdditionalKill { get; set; } = 15;
    
    /// <summary>
    /// Maximum auto-bounty amount
    /// </summary>
    public int MaxAutoBounty { get; set; } = 10000;
    
    /// <summary>
    /// Whether to announce when a bounty is placed
    /// </summary>
    public bool AnnounceBountyPlaced { get; set; } = true;
    
    /// <summary>
    /// Whether to announce when a bounty is claimed
    /// </summary>
    public bool AnnounceBountyClaimed { get; set; } = true;
    
    // Anti-Boost Configuration
    /// <summary>
    /// Whether anti-boost detection is enabled
    /// </summary>
    public bool AntiBoostEnabled { get; set; } = true;
    
    /// <summary>
    /// Maximum kills allowed on the same victim within the time window
    /// </summary>
    public int MaxKillsPerVictim { get; set; } = 3;
    
    /// <summary>
    /// Time window in minutes for tracking recent kills for anti-boost detection
    /// </summary>
    public int AntiBoostTimeWindowMinutes { get; set; } = 5;
    
    /// <summary>
    /// Minimum number of unique victims required to reach the bounty threshold
    /// </summary>
    public int RequireUniqueVictimsForStreak { get; set; } = 3;
}
