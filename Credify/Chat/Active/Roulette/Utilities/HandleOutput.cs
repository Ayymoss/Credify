using Credify.Chat.Active.Roulette.Models;
using Credify.Configuration;

namespace Credify.Chat.Active.Roulette.Utilities;

public class HandleOutput(TranslationsRoot translations)
{
    public static async Task TellAsync(List<Player> players, List<string> messages)
    {
        foreach (var player in players)
        {
            await player.Client.TellAsync(messages);
        }
    }

    public void Tell(Player player, string message, bool longPrefix = false)
    {
        var prefixedMessage = longPrefix ? translations.Roulette.LongPrefix(message) : translations.Roulette.Prefix(message);
        player.Client.Tell(prefixedMessage);
    }

    public static async Task TellAllServerAsync(Player player, List<string> messages)
    {
        await player.Client.CurrentServer.BroadcastAsync(messages);
    }

    public static async Task TellAllServersAsync(Player player, List<string> messages)
    {
        var servers = player.Client.CurrentServer.Manager.GetServers();
        foreach (var server in servers)
        {
            if (server.ConnectedClients.Count is 0) continue;
            await server.BroadcastAsync(messages);
        }
    }
}
