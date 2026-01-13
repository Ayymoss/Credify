using System.Collections.Concurrent;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// Tracks kill streaks per player and awards credits for streak milestones.
/// Also handles auto-bounty placement on high-streak players.
/// </summary>
public class StreakTracker(
    CredifyConfiguration config,
    PersistenceService persistenceService)
{
    // Key: ClientId, Value: Current kill streak count
    private readonly ConcurrentDictionary<int, int> _killStreaks = new();
    
    // Key: ClientId, Value: Current bounty amount on this player
    private readonly ConcurrentDictionary<int, int> _activeBounties = new();
    
    /// <summary>
    /// Called when a player gets a kill. Increments their streak and checks for rewards/bounties.
    /// </summary>
    public async Task<StreakResult> OnKillAsync(EFClient killer, EFClient? victim)
    {
        var result = new StreakResult();
        
        if (!config.Streak.IsEnabled && !config.Bounty.AutoBountyEnabled)
            return result;
        
        // Increment killer's streak
        var newStreak = _killStreaks.AddOrUpdate(killer.ClientId, 1, (_, current) => current + 1);
        result.CurrentStreak = newStreak;
        
        // Check for streak rewards
        if (config.Streak.IsEnabled && config.Streak.KillStreakRewards.TryGetValue(newStreak, out var reward))
        {
            await persistenceService.AddCreditsAsync(killer, reward);
            result.StreakReward = reward;
            result.ShouldAnnounceStreak = config.Streak.AnnounceKillStreaks && 
                                          newStreak >= config.Streak.MinimumStreakToAnnounce;
        }
        
        // Check for auto-bounty trigger
        if (config.Bounty.AutoBountyEnabled && newStreak == config.Bounty.AutoBountyThreshold)
        {
            var bountyAmount = config.Bounty.AutoBountyAmount;
            _activeBounties[killer.ClientId] = bountyAmount;
            result.BountyPlaced = bountyAmount;
            result.ShouldAnnounceBounty = config.Bounty.AnnounceBountyPlaced;
        }
        // Update existing bounty for additional kills
        else if (config.Bounty.AutoBountyEnabled && 
                 newStreak > config.Bounty.AutoBountyThreshold && 
                 _activeBounties.ContainsKey(killer.ClientId))
        {
            var currentBounty = _activeBounties[killer.ClientId];
            var newBounty = Math.Min(
                currentBounty + config.Bounty.BountyPerAdditionalKill,
                config.Bounty.MaxAutoBounty);
            _activeBounties[killer.ClientId] = newBounty;
        }
        
        // If victim had a bounty, killer claims it
        if (victim is not null && _activeBounties.TryRemove(victim.ClientId, out var claimedBounty))
        {
            await persistenceService.AddCreditsAsync(killer, claimedBounty);
            result.BountyClaimed = claimedBounty;
            result.BountyVictim = victim;
            result.ShouldAnnounceBountyClaimed = config.Bounty.AnnounceBountyClaimed;
        }
        
        return result;
    }
    
    /// <summary>
    /// Called when a player dies. Resets their streak.
    /// </summary>
    public void OnDeath(EFClient player)
    {
        _killStreaks.TryRemove(player.ClientId, out _);
        // Note: Bounty is NOT removed on death - it can still be claimed
    }
    
    /// <summary>
    /// Called when a player disconnects. Cleans up their data.
    /// </summary>
    public void OnDisconnect(EFClient player)
    {
        _killStreaks.TryRemove(player.ClientId, out _);
        _activeBounties.TryRemove(player.ClientId, out _);
    }
    
    /// <summary>
    /// Gets the current bounty on a player, if any.
    /// </summary>
    public int GetBounty(EFClient player)
    {
        return _activeBounties.TryGetValue(player.ClientId, out var bounty) ? bounty : 0;
    }
    
    /// <summary>
    /// Gets all active bounties on a server.
    /// </summary>
    public Dictionary<int, int> GetAllBounties()
    {
        return new Dictionary<int, int>(_activeBounties);
    }
    
    /// <summary>
    /// Gets the current kill streak for a player.
    /// </summary>
    public int GetStreak(EFClient player)
    {
        return _killStreaks.TryGetValue(player.ClientId, out var streak) ? streak : 0;
    }
}

/// <summary>
/// Result of processing a kill for streaks and bounties.
/// </summary>
public class StreakResult
{
    public int CurrentStreak { get; set; }
    public int StreakReward { get; set; }
    public bool ShouldAnnounceStreak { get; set; }
    
    public int BountyPlaced { get; set; }
    public bool ShouldAnnounceBounty { get; set; }
    
    public int BountyClaimed { get; set; }
    public EFClient? BountyVictim { get; set; }
    public bool ShouldAnnounceBountyClaimed { get; set; }
    
    public bool HasStreakReward => StreakReward > 0;
    public bool HasBountyPlaced => BountyPlaced > 0;
    public bool HasBountyClaimed => BountyClaimed > 0;
}
