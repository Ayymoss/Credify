using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Models;
using Credify.Configuration;

namespace Credify.Chat.Active.Games.Roulette.Utilities;

/// <summary>
/// Roulette-specific output handler that wraps GamePlayerCommunication with roulette-specific formatting.
/// </summary>
public class HandleOutput(TranslationsRoot translations, GamePlayerCommunication communication)
{
    public async Task TellAsync(List<Player> players, List<string> messages)
    {
        await communication.TellPlayersAsync(players.Select(p => p.Client), messages);
    }

    public void Tell(Player player, string message, bool longPrefix = false)
    {
        var prefixedMessage = longPrefix 
            ? translations.Roulette.LongPrefix(message) 
            : translations.Roulette.Prefix(message);
        player.Client.Tell(prefixedMessage);
    }

    public async Task TellAllServerAsync(Player player, List<string> messages)
    {
        await communication.BroadcastToServerAsync(player.Client, messages);
    }

    public async Task TellAllServersAsync(Player player, List<string> messages)
    {
        await communication.BroadcastToAllServersAsync(player.Client, messages);
    }
}
