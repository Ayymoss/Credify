using Credify.Chat.Feature.Bounty;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Database.Models;

namespace Credify.EventHandlers;

/// <summary>
/// Handles client state authorized events, loading client data on join.
/// </summary>
public class ClientStateAuthorizedEventHandler(
    PersistenceService persistenceService,
    BountyContractManager bountyContractManager,
    CredifyConfiguration config)
{
    public async Task HandleAsync(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        await persistenceService.OnJoinAsync(clientEvent.Client);
        var userCredits = await persistenceService.GetClientCreditsAsync(clientEvent.Client);
        clientEvent.Client.Tell(config.Translations.Economy.UserCredits.FormatExt(userCredits.ToString("N0")));

        // Remind a returning player that they're still a target (the one-time placement
        // warning is long gone if they reconnected).
        if (config.BountyContract.IsEnabled)
        {
            var bounties = bountyContractManager.GetBountiesOnTarget(clientEvent.Client.ClientId);
            if (bounties.Count > 0)
            {
                var total = bounties.Sum(b => b.Amount);
                clientEvent.Client.Tell(config.Translations.BountyContract.OnYouWarning
                    .FormatExt(total.ToString("N0"), bounties.Count));
            }
        }
    }
}
