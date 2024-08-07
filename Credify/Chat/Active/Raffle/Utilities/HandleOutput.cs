using Credify.Configuration;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Raffle.Utilities;

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

    public static async Task TellAllServersAsync(EFClient client, List<string> messages)
    {
        var servers = client.CurrentServer.Manager.GetServers();
        foreach (var server in servers)
        {
            if (server.ConnectedClients.Count is 0) continue;
            await server.BroadcastAsync(messages);
        }
    }
}
