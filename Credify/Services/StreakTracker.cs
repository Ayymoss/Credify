using System.Collections.Concurrent;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// Represents a recent kill for anti-boost tracking.
/// </summary>
/// <param name="VictimClientId">The client ID of the victim</param>
/// <param name="KillTime">When the kill occurred</param>
internal record RecentKill(int VictimClientId, DateTime KillTime);

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
    
    // Key: ClientId (killer), Value: List of recent kills for anti-boost detection
    private readonly ConcurrentDictionary<int, List<RecentKill>> _recentKills = new();
    
    /// <summary>
    /// Called when a player gets a kill. Increments their streak and checks for rewards/bounties.
    /// </summary>
    /// <param name="killer">The player who got the kill</param>
    /// <param name="victim">The player who died, or null if not applicable</param>
    /// <param name="serverPlayerCount">Current number of players on the server (0 = use minimum bounty). Used to scale auto-bounty.</param>
    public async Task<StreakResult> OnKillAsync(EFClient killer, EFClient? victim, int serverPlayerCount = 0)
    {
        var result = new StreakResult();
        
        if (!config.Streak.IsEnabled && !config.Bounty.AutoBountyEnabled)
            return result;
        
        // Check for boost detection if enabled and victim is present
        bool killIsValid = true;
        if (victim is not null && config.Bounty.AntiBoostEnabled)
        {
            killIsValid = IsKillValidForStreak(killer.ClientId, victim.ClientId);
            if (killIsValid)
            {
                // Record the valid kill
                RecordKill(killer.ClientId, victim.ClientId);
            }
        }
        
        // Only increment streak if kill is valid
        if (killIsValid)
        {
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
            
            // Scaled amounts: config values are minimums; scale up with player count (sub-linear to avoid inflation)
            var (scaledInitialBounty, scaledPerKill) = GetScaledAutoBountyAmounts(serverPlayerCount);
            
            // Check for auto-bounty trigger
            if (config.Bounty.AutoBountyEnabled && newStreak == config.Bounty.AutoBountyThreshold)
            {
                // Verify unique victims requirement
                if (!config.Bounty.AntiBoostEnabled || HasEnoughUniqueVictims(killer.ClientId))
                {
                    _activeBounties[killer.ClientId] = scaledInitialBounty;
                    result.BountyPlaced = scaledInitialBounty;
                    result.ShouldAnnounceBounty = config.Bounty.AnnounceBountyPlaced;
                }
            }
            // Update existing bounty for additional kills (skip when already at cap so we never "increase by $0")
            else if (config.Bounty.AutoBountyEnabled && 
                     newStreak > config.Bounty.AutoBountyThreshold && 
                     _activeBounties.TryGetValue(killer.ClientId, out var currentBounty) &&
                     currentBounty < config.Bounty.MaxAutoBounty)
            {
                var newBounty = Math.Min(
                    currentBounty + scaledPerKill,
                    config.Bounty.MaxAutoBounty);
                _activeBounties[killer.ClientId] = newBounty;
            }
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
        // Recent kills are kept for anti-boost tracking even after death
    }
    
    /// <summary>
    /// Called when a player disconnects. Cleans up their data.
    /// </summary>
    public void OnDisconnect(EFClient player)
    {
        _killStreaks.TryRemove(player.ClientId, out _);
        _activeBounties.TryRemove(player.ClientId, out _);
        _recentKills.TryRemove(player.ClientId, out _);
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
    
    /// <summary>
    /// Auto-bounty from player count: linear (4, 250) → (64, MaxAutoBounty), capped at MaxAutoBounty.
    /// </summary>
    private (int scaledInitialBounty, int scaledPerKill) GetScaledAutoBountyAmounts(int serverPlayerCount)
    {
        const int minPlayers = 4, maxPlayers = 64, minBounty = 250;
        int maxBounty = config.Bounty.MaxAutoBounty;
        double slope = (maxBounty - minBounty) / (double)(maxPlayers - minPlayers);
        int initial = (int)Math.Round(minBounty + (serverPlayerCount - minPlayers) * slope);
        int perKill = (int)Math.Round((minBounty + (serverPlayerCount - minPlayers) * slope) / 10);
        return (Math.Min(initial, maxBounty), Math.Min(perKill, maxBounty / 10));
    }
    
    /// <summary>
    /// Records a kill for anti-boost tracking.
    /// </summary>
    private void RecordKill(int killerClientId, int victimClientId)
    {
        var recentKillsList = _recentKills.GetOrAdd(killerClientId, _ => new List<RecentKill>());
        lock (recentKillsList)
        {
            recentKillsList.Add(new RecentKill(victimClientId, DateTime.UtcNow));
        }
    }
    
    /// <summary>
    /// Gets recent kills for a killer within the time window, with cleanup.
    /// </summary>
    private List<RecentKill> GetRecentKillsForKiller(int killerClientId)
    {
        if (!_recentKills.TryGetValue(killerClientId, out var kills))
        {
            return new List<RecentKill>();
        }
        
        var timeWindow = TimeSpan.FromMinutes(config.Bounty.AntiBoostTimeWindowMinutes);
        var cutoffTime = DateTime.UtcNow - timeWindow;
        
        lock (kills)
        {
            // Remove old kills outside time window
            kills.RemoveAll(k => k.KillTime < cutoffTime);
            return new List<RecentKill>(kills);
        }
    }
    
    /// <summary>
    /// Checks if a kill is valid for streak counting (anti-boost detection).
    /// </summary>
    private bool IsKillValidForStreak(int killerClientId, int victimClientId)
    {
        var recentKills = GetRecentKillsForKiller(killerClientId);
        var timeWindow = TimeSpan.FromMinutes(config.Bounty.AntiBoostTimeWindowMinutes);
        var cutoffTime = DateTime.UtcNow - timeWindow;
        
        // Count kills on this victim within time window
        var killsOnThisVictim = recentKills
            .Count(k => k.VictimClientId == victimClientId && k.KillTime >= cutoffTime);
        
        // If exceeds max kills per victim, invalid
        return killsOnThisVictim < config.Bounty.MaxKillsPerVictim;
    }
    
    /// <summary>
    /// Checks if the killer has enough unique victims to qualify for bounty.
    /// </summary>
    private bool HasEnoughUniqueVictims(int killerClientId)
    {
        var recentKills = GetRecentKillsForKiller(killerClientId);
        var timeWindow = TimeSpan.FromMinutes(config.Bounty.AntiBoostTimeWindowMinutes);
        var cutoffTime = DateTime.UtcNow - timeWindow;
        
        var uniqueVictims = recentKills
            .Where(k => k.KillTime >= cutoffTime)
            .Select(k => k.VictimClientId)
            .Distinct()
            .Count();
        
        return uniqueVictims >= config.Bounty.RequireUniqueVictimsForStreak;
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
