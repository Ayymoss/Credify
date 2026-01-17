using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// Service that tracks all active games and prevents players from joining multiple games simultaneously.
/// </summary>
public class ActiveGameTracker
{
    private readonly List<IActiveGame> _activeGames = new();

    /// <summary>
    /// Registers an active game to be tracked.
    /// </summary>
    public void RegisterGame(IActiveGame game)
    {
        if (!_activeGames.Contains(game))
        {
            _activeGames.Add(game);
        }
    }

    /// <summary>
    /// Checks if a player is currently in any active game.
    /// </summary>
    /// <param name="player">The player to check</param>
    /// <param name="excludeGame">Optional game to exclude from the check (e.g., when leaving a game)</param>
    /// <returns>True if the player is in any active game, false otherwise</returns>
    public bool IsPlayerInAnyGame(EFClient player, IActiveGame? excludeGame = null)
    {
        return _activeGames
            .Where(game => excludeGame == null || game != excludeGame)
            .Any(game => game.IsPlayerPlaying(player));
    }

    /// <summary>
    /// Gets the name of the game the player is currently in (for error messages).
    /// </summary>
    public string? GetGameNamePlayerIsIn(EFClient player, IActiveGame? excludeGame = null)
    {
        var gameIn = _activeGames
            .Where(game => excludeGame == null || game != excludeGame)
            .FirstOrDefault(game => game.IsPlayerPlaying(player));

        return gameIn?.GetType().Name.Replace("Manager", "");
    }
}
