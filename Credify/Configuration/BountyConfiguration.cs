namespace Credify.Configuration;

public class BountyConfiguration
{
    public bool AutoBountyEnabled { get; set; } = true;
    
    /// <summary>
    /// Number of kills without dying to trigger auto-bounty
    /// </summary>
    public int AutoBountyThreshold { get; set; } = 10;
    
    /// <summary>
    /// Base bounty amount placed on the streak holder
    /// </summary>
    public int AutoBountyAmount { get; set; } = 500;
    
    /// <summary>
    /// Additional bounty per kill above threshold
    /// </summary>
    public int BountyPerAdditionalKill { get; set; } = 50;
    
    /// <summary>
    /// Maximum auto-bounty amount
    /// </summary>
    public int MaxAutoBounty { get; set; } = 2000;
    
    /// <summary>
    /// Whether to announce when a bounty is placed
    /// </summary>
    public bool AnnounceBountyPlaced { get; set; } = true;
    
    /// <summary>
    /// Whether to announce when a bounty is claimed
    /// </summary>
    public bool AnnounceBountyClaimed { get; set; } = true;
}
