using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Standard interface for all active games in the Credify plugin.
/// Provides consistent contract for game lifecycle and player interactions.
/// </summary>
public interface IActiveGame
{
    /// <summary>
    /// Allows a player to join the game.
    /// </summary>
    Task JoinGameAsync(EFClient player);

    /// <summary>
    /// Removes a player from the game.
    /// </summary>
    Task LeaveGameAsync(EFClient player);

    /// <summary>
    /// Handles incoming chat messages from players during gameplay.
    /// </summary>
    Task HandleChatAsync(EFClient player, string message);

    /// <summary>
    /// Checks if a player is currently in the game.
    /// </summary>
    bool IsPlayerPlaying(EFClient player);

    /// <summary>
    /// Gets the current number of players in the game.
    /// </summary>
    int GetPlayerCount();
}
