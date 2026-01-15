using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Generic interface for game output handling. Provides consistent API for player communication across all active games.
/// </summary>
/// <typeparam name="TPlayer">The player type used by the game (e.g., PokerPlayer, Player, EFClient)</typeparam>
public interface IGameOutputHandler<in TPlayer>
{
    /// <summary>
    /// Sends messages to a single player with game-specific prefix.
    /// </summary>
    Task TellPlayerAsync(TPlayer player, IEnumerable<string> messages, bool longPrefix = false);

    /// <summary>
    /// Sends messages to multiple players.
    /// </summary>
    Task TellPlayersAsync(IEnumerable<TPlayer> players, IEnumerable<string> messages);

    /// <summary>
    /// Broadcasts messages to all players on the same server.
    /// </summary>
    Task BroadcastToServerAsync(TPlayer player, IEnumerable<string> messages);

    /// <summary>
    /// Broadcasts messages to all players across all servers.
    /// </summary>
    Task BroadcastToAllServersAsync(TPlayer player, IEnumerable<string> messages);

    /// <summary>
    /// Synchronous single-message convenience method with inline prefix.
    /// </summary>
    void Tell(TPlayer player, string message, bool longPrefix = false);
}
