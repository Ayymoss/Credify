namespace Credify.Chat.Feature.Bounty.Models;

/// <summary>
/// Represents a player-placed bounty contract
/// </summary>
public class BountyContract
{
    /// <summary>
    /// Unique identifier for the bounty
    /// </summary>
    public Guid Id { get; init; } = Guid.NewGuid();
    
    /// <summary>
    /// Client who placed the bounty
    /// </summary>
    public required int PlacerClientId { get; init; }
    
    /// <summary>
    /// Name of the player who placed the bounty (for display purposes)
    /// </summary>
    public required string PlacerName { get; init; }
    
    /// <summary>
    /// Client who is the target of the bounty
    /// </summary>
    public required int TargetClientId { get; init; }
    
    /// <summary>
    /// Name of the target (for display purposes)
    /// </summary>
    public required string TargetName { get; init; }
    
    /// <summary>
    /// The bounty amount (after fees deducted)
    /// </summary>
    public required long Amount { get; set; }
    
    /// <summary>
    /// When the bounty was placed
    /// </summary>
    public DateTimeOffset PlacedAt { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// When the bounty expires (null = never)
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }
    
    /// <summary>
    /// Whether the bounty is still active
    /// </summary>
    public bool IsActive { get; set; } = true;
    
    /// <summary>
    /// Gets remaining time until expiration
    /// </summary>
    public TimeSpan? TimeRemaining => ExpiresAt.HasValue ? ExpiresAt.Value - DateTimeOffset.UtcNow : null;
    
    /// <summary>
    /// Whether the bounty has expired
    /// </summary>
    public bool IsExpired => ExpiresAt.HasValue && DateTimeOffset.UtcNow > ExpiresAt.Value;
}
