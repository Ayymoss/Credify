using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Utilities;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;
using Player = Credify.Chat.Active.Games.Roulette.Models.Player;

namespace Credify.Chat.Active.Games.Roulette;

/// <summary>
/// Manager for Roulette game. Implements IActiveGame for consistency with other active games.
/// </summary>
public class RouletteManager : IActiveGame
{
    private readonly Table _table;

    public RouletteManager(
        CredifyConfiguration config,
        TranslationsRoot translations,
        PersistenceService persistenceService,
        GamePlayerCommunication communication)
    {
        var input = new RouletteHandleInput(persistenceService, config);
        var output = new RouletteHandleOutput(translations, communication);
        _table = new Table(config, translations, persistenceService, communication, input, output);
    }

    /// <summary>
    /// Starts the continuous roulette game loop (runs in background).
    /// </summary>
    public async Task StartGameAsync(CancellationToken token) => await _table.GameLoopAsync(token);

    /// <summary>
    /// IActiveGame implementation - allows a player to join the game.
    /// </summary>
    public async Task JoinGameAsync(EFClient player) => await _table.PlayerJoinAsync(new Player(player));

    /// <summary>
    /// IActiveGame implementation - removes a player from the game.
    /// </summary>
    public Task LeaveGameAsync(EFClient player)
    {
        _table.PlayerLeave(player);
        return Task.CompletedTask;
    }

    /// <summary>
    /// IActiveGame implementation - handles chat messages (roulette doesn't use chat input during gameplay).
    /// </summary>
    public Task HandleChatAsync(EFClient player, string message) => Task.CompletedTask;

    /// <summary>
    /// IActiveGame implementation - checks if a player is in the game.
    /// </summary>
    public bool IsPlayerPlaying(EFClient player) => _table.IsPlayerPlaying(player);

    /// <summary>
    /// IActiveGame implementation - gets the current number of players.
    /// </summary>
    public int GetPlayerCount() => _table.GetPlayerCount();
}
