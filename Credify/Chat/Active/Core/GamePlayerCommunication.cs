using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Centralized service for player communication in active games.
/// Handles messaging patterns, prefixes, and multi-server broadcasts.
/// </summary>
public class GamePlayerCommunication(TranslationsRoot translations)
{
    /// <summary>
    /// Sends a message to a single player with optional game prefix.
    /// </summary>
    public async Task TellPlayerAsync(EFClient player, string titlePrefix, IEnumerable<string> messages)
    {
        var completeMessages = messages.Select(message => $"{titlePrefix} {message}");
        await player.TellAsync(completeMessages);
    }

    /// <summary>
    /// Sends a message to a single player with short game prefix.
    /// </summary>
    public async Task TellPlayerShortAsync(EFClient player, string shortPrefix, IEnumerable<string> messages)
    {
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
            await player.TellAsync(messagesList);
        }
    }

    /// <summary>
    /// Broadcasts a message to all servers.
    /// </summary>
    public async Task BroadcastToAllServersAsync(EFClient sourcePlayer, IEnumerable<string> messages)
    {
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
        await sourcePlayer.CurrentServer.BroadcastAsync(messages);
    }

    /// <summary>
    /// Formats a translation string with arguments using the FormatExt pattern.
    /// </summary>
    public static string FormatTranslation(string template, params object[] args)
    {
        // FormatExt is an extension method from SharedLibraryCore
        // We'll use it directly where needed, or implement similar logic here
        // For now, this is a placeholder that will use the extension method
        return template; // Actual formatting happens via extension method
    }
}
