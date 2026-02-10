namespace Credify.Configuration;

public class BountyContractConfiguration
{
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Minimum bounty amount that can be placed
    /// </summary>
    public int MinBounty { get; set; } = 100;
    
    /// <summary>
    /// Maximum bounty amount (0 = unlimited)
    /// </summary>
    public int MaxBounty { get; set; } = 100_000;
    
    /// <summary>
    /// Fee percentage taken by the bank when placing a bounty (0-100)
    /// </summary>
    public int PlacementFeePercent { get; set; } = 10;
    
    /// <summary>
    /// How long a bounty lasts before expiring (in hours). 0 = never expires (until target disconnects)
    /// </summary>
    public int ExpirationHours { get; set; } = 0;
    
    /// <summary>
    /// Whether bounties persist when target disconnects
    /// </summary>
    public bool PersistOnDisconnect { get; set; } = false;
    
    /// <summary>
    /// Maximum number of bounties a single player can place at once
    /// </summary>
    public int MaxActiveBountiesPerPlayer { get; set; } = 3;
    
    /// <summary>
    /// Whether to announce bounty placement to server
    /// </summary>
    public bool AnnouncePlacement { get; set; } = true;
    
    /// <summary>
    /// Whether to announce bounty claim to server
    /// </summary>
    public bool AnnounceClaim { get; set; } = true;
    
    /// <summary>
    /// Whether to announce bounty expiration to target
    /// </summary>
    public bool AnnounceExpiration { get; set; } = true;
}
