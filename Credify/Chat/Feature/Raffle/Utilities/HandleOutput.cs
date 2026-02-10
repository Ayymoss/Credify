using Credify.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Chat.Feature.Raffle.Utilities;

public class HandleOutput(TranslationsRoot translations)
{
    public void Tell(EFClient client, string message)
    {
        client.Tell(translations.Raffle.Prefix(message));
    }

    public static async Task TellAllServerAsync(EFClient client, List<string> messages)
    {
        await client.CurrentServer.BroadcastAsync(messages);
    }

    public static async Task TellAllServersAsync(IManager manager, List<string> messages)
    {
        var servers = manager.GetServers();
        foreach (var server in servers)
        {
            if (server.ConnectedClients.Count is 0) continue;
            await server.BroadcastAsync(messages);
        }
    }
}
