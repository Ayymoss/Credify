using System.Collections.Concurrent;
using Credify.Chat.Feature.Bounty.Models;
using Credify.Chat.Feature.Bounty.Services;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Feature.Bounty;

/// <summary>
/// Manages player-placed bounty contracts
/// </summary>
public class BountyContractManager(
    CredifyConfiguration config,
    PersistenceService persistenceService)
{
    // Key: Target ClientId, Value: List of bounties on that target
    private readonly ConcurrentDictionary<int, List<BountyContract>> _bounties = new();
    
    // Track bounties placed by each player
    private readonly ConcurrentDictionary<int, List<Guid>> _placerBounties = new();
    
    private readonly Lock _bountyLock = new();
    private readonly BountyValidator _validator = new(config.BountyContract);

    /// <summary>
    /// Places a bounty on a target player
    /// </summary>
    public async Task<BountyPlacementResult> PlaceBountyAsync(EFClient placer, EFClient target, long amount)
    {
        // Get current active bounties count for placer
        var currentActiveBounties = _placerBounties.TryGetValue(placer.ClientId, out var placerBounties) 
            ? placerBounties.Count 
            : 0;

        // Validate placement using validator
        var (isValid, errorMessage) = _validator.ValidatePlacement(placer, target, amount, currentActiveBounties);
        if (!isValid)
        {
            return new BountyPlacementResult(false, errorMessage);
        }

        // Check if placer has enough credits
        if (!PersistenceService.AvailableFunds(placer, amount))
        {
            return new BountyPlacementResult(false, "Insufficient credits");
        }

        // Calculate fee
        var fee = (long)(amount * config.BountyContract.PlacementFeePercent / 100.0);
        var netBounty = amount - fee;

        // Deduct full amount from placer
        await persistenceService.RemoveCreditsAsync(placer, amount);

        // Create bounty contract
        DateTimeOffset? expiresAt = config.BountyContract.ExpirationHours > 0
            ? DateTimeOffset.UtcNow.AddHours(config.BountyContract.ExpirationHours)
            : null;

        var contract = new BountyContract
        {
            PlacerClientId = placer.ClientId,
            PlacerName = placer.CleanedName,
            TargetClientId = target.ClientId,
            TargetName = target.CleanedName,
            Amount = netBounty,
            ExpiresAt = expiresAt
        };

        lock (_bountyLock)
        {
            // Add to target's bounties
            if (!_bounties.TryGetValue(target.ClientId, out var targetBounties))
            {
                targetBounties = [];
                _bounties[target.ClientId] = targetBounties;
            }
            targetBounties.Add(contract);

            // Track placer's bounties
            if (!_placerBounties.TryGetValue(placer.ClientId, out var placerList))
            {
                placerList = [];
                _placerBounties[placer.ClientId] = placerList;
            }
            placerList.Add(contract.Id);
        }

        return new BountyPlacementResult(true, null)
        {
            Contract = contract,
            Fee = fee
        };
    }

    /// <summary>
    /// Claims all bounties on a target when they are killed
    /// </summary>
    public async Task<BountyClaimResult> ClaimBountiesAsync(EFClient killer, EFClient target)
    {
        // Validate claim using validator
        if (!_validator.CanClaimBounty(killer, target))
        {
            return new BountyClaimResult(false, 0, []);
        }

        List<BountyContract> claimedBounties = [];
        long totalClaimed = 0;

        lock (_bountyLock)
        {
            if (!_bounties.TryGetValue(target.ClientId, out var targetBounties))
            {
                return new BountyClaimResult(false, 0, []);
            }

            // Get all active, non-expired bounties
            var activeBounties = targetBounties
                .Where(b => b.IsActive && !b.IsExpired)
                .ToList();

            if (activeBounties.Count == 0)
            {
                return new BountyClaimResult(false, 0, []);
            }

            foreach (var bounty in activeBounties)
            {
                bounty.IsActive = false;
                totalClaimed += bounty.Amount;
                claimedBounties.Add(bounty);

                // Remove from placer's tracking
                if (_placerBounties.TryGetValue(bounty.PlacerClientId, out var placerList))
                {
                    placerList.Remove(bounty.Id);
                }
            }

            // Remove all claimed bounties from target
            targetBounties.RemoveAll(b => claimedBounties.Contains(b));
        }

        // Award credits to killer
        if (totalClaimed > 0)
        {
            await persistenceService.AddCreditsAsync(killer, totalClaimed);
        }

        return new BountyClaimResult(true, totalClaimed, claimedBounties);
    }

    /// <summary>
    /// Gets all active bounties on a target
    /// </summary>
    public List<BountyContract> GetBountiesOnTarget(int targetClientId)
    {
        lock (_bountyLock)
        {
            if (_bounties.TryGetValue(targetClientId, out var bounties))
            {
                return bounties.Where(b => b.IsActive && !b.IsExpired).ToList();
            }
            return [];
        }
    }

    /// <summary>
    /// Gets total bounty amount on a target
    /// </summary>
    public long GetTotalBountyOnTarget(int targetClientId)
    {
        return GetBountiesOnTarget(targetClientId).Sum(b => b.Amount);
    }

    /// <summary>
    /// Gets all active bounties across all targets
    /// </summary>
    public List<BountyContract> GetAllActiveBounties()
    {
        lock (_bountyLock)
        {
            return _bounties.Values
                .SelectMany(b => b)
                .Where(b => b.IsActive && !b.IsExpired)
                .OrderByDescending(b => b.Amount)
                .ToList();
        }
    }

    /// <summary>
    /// Cleans up bounties when a player disconnects (if not persisting)
    /// </summary>
    public void OnPlayerDisconnect(EFClient player)
    {
        if (config.BountyContract.PersistOnDisconnect) return;

        lock (_bountyLock)
        {
            // Remove bounties on this player
            _bounties.TryRemove(player.ClientId, out _);

            // Deactivate bounties placed by this player
            if (_placerBounties.TryRemove(player.ClientId, out var placerBountyIds))
            {
                foreach (var targetBounties in _bounties.Values)
                {
                    foreach (var bounty in targetBounties.Where(b => placerBountyIds.Contains(b.Id)))
                    {
                        bounty.IsActive = false;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Cleans up expired bounties (call periodically)
    /// </summary>
    public void CleanupExpiredBounties()
    {
        lock (_bountyLock)
        {
            foreach (var (_, bounties) in _bounties)
            {
                foreach (var bounty in bounties.Where(b => b.IsExpired && b.IsActive))
                {
                    bounty.IsActive = false;
                    
                    // Remove from placer tracking
                    if (_placerBounties.TryGetValue(bounty.PlacerClientId, out var placerList))
                    {
                        placerList.Remove(bounty.Id);
                    }
                }
            }
        }
    }
}

public record BountyPlacementResult(bool Success, string? ErrorMessage)
{
    public BountyContract? Contract { get; init; }
    public long Fee { get; init; }
}

public record BountyClaimResult(bool Success, long TotalClaimed, List<BountyContract> ClaimedBounties);
