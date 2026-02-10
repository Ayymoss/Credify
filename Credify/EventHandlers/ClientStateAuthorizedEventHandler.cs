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
    CredifyConfiguration config)
{
    public async Task HandleAsync(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        await persistenceService.OnJoinAsync(clientEvent.Client);
        var userCredits = await persistenceService.GetClientCreditsAsync(clientEvent.Client);
        clientEvent.Client.Tell(config.Translations.Core.UserCredits.FormatExt(userCredits.ToString("N0")));
    }
}
