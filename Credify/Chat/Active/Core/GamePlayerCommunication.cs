using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Centralized service for player communication in active games.
/// Handles messaging patterns, prefixes, and multi-server broadcasts.
/// </summary>
public class GamePlayerCommunication
{
    // Note: translations parameter was removed as it was unused
    // Players who joined from the webfront aren't connected to a game server (CurrentServer is null);
    // they render the game from web state snapshots instead of receiving chat lines. Chat output to them
    // is simply skipped — calling EFClient.Tell/TellAsync with no server would throw.
    private static bool HasChatChannel(EFClient player) => player.CurrentServer is not null;

    /// <summary>
    /// Sends a message to a single player with optional game prefix.
    /// </summary>
    public async Task TellPlayerAsync(EFClient player, string titlePrefix, IEnumerable<string> messages)
    {
        if (!HasChatChannel(player)) return;
        var completeMessages = messages.Select(message => $"{titlePrefix} {message}");
        await player.TellAsync(completeMessages);
    }

    /// <summary>
    /// Sends a message to a single player with short game prefix.
    /// </summary>
    public async Task TellPlayerShortAsync(EFClient player, string shortPrefix, IEnumerable<string> messages)
    {
        if (!HasChatChannel(player)) return;
        var completeMessages = messages.Select(message => $"{shortPrefix} {message}");
        await player.TellAsync(completeMessages);
    }

    /// <summary>
    /// Sends a message to multiple players.
    /// </summary>
    public async Task TellPlayersAsync(IEnumerable<EFClient> players, IEnumerable<string> messages)
    {
        var messagesList = messages.ToList();
        foreach (var player in players)
        {
            if (!HasChatChannel(player)) continue;
            await player.TellAsync(messagesList);
        }
    }

    /// <summary>
    /// Broadcasts a message to all servers.
    /// </summary>
    public async Task BroadcastToAllServersAsync(EFClient sourcePlayer, IEnumerable<string> messages)
    {
        // a web-only source player has no server/manager context to broadcast from
        if (!HasChatChannel(sourcePlayer)) return;
        var servers = sourcePlayer.CurrentServer.Manager.GetServers();
        var messagesList = messages.ToList();
        foreach (var server in servers)
        {
            if (server.ConnectedClients.Count is 0) continue;
            await server.BroadcastAsync(messagesList);
        }
    }

    /// <summary>
    /// Broadcasts a message to the current server.
    /// </summary>
    public async Task BroadcastToServerAsync(EFClient sourcePlayer, IEnumerable<string> messages)
    {
        if (!HasChatChannel(sourcePlayer)) return;
        await sourcePlayer.CurrentServer.BroadcastAsync(messages);
    }
}
