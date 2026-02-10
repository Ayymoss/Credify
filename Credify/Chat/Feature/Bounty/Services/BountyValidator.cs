using Credify.Configuration;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Feature.Bounty.Services;

/// <summary>
/// Service responsible for validating bounty contract operations.
/// </summary>
public class BountyValidator(BountyContractConfiguration config)
{
    /// <summary>
    /// Validates if a bounty can be placed.
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidatePlacement(
        EFClient placer, 
        EFClient target, 
        long amount,
        int currentActiveBountiesCount)
    {
        if (!config.IsEnabled)
        {
            return (false, "Bounty contracts are disabled");
        }

        if (placer.ClientId == target.ClientId)
        {
            return (false, "Cannot place bounty on yourself");
        }

        if (amount < config.MinBounty)
        {
            return (false, $"Minimum bounty is {config.MinBounty:N0}");
        }

        if (config.MaxBounty > 0 && amount > config.MaxBounty)
        {
            return (false, $"Maximum bounty is {config.MaxBounty:N0}");
        }

        if (currentActiveBountiesCount >= config.MaxActiveBountiesPerPlayer)
        {
            return (false, $"You can only have {config.MaxActiveBountiesPerPlayer} active bounties");
        }

        return (true, null);
    }

    /// <summary>
    /// Validates if a bounty can be claimed.
    /// </summary>
    public bool CanClaimBounty(EFClient killer, EFClient target)
    {
        if (!config.IsEnabled)
        {
            return false;
        }

        // Don't claim your own bounty
        return killer.ClientId != target.ClientId;
    }
}
