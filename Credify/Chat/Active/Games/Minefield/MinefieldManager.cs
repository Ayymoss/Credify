using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Minefield.Utilities;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Minefield;

/// <summary>
/// Manager for the Minefield game. Implements IActiveGame for consistency with the
/// other active games and wires the input/output handlers into the game.
/// </summary>
public class MinefieldManager : IActiveGame
{
    private readonly MinefieldGame _game;

    public MinefieldManager(
        CredifyConfiguration config,
        PersistenceService persistenceService,
        GamePlayerCommunication communication)
    {
        var inputHandler = new MinefieldHandleInput(config.Translations.Minefield);
        var outputHandler = new MinefieldHandleOutput(config.Translations.Minefield, communication);
        _game = new MinefieldGame(persistenceService, config, communication, inputHandler, outputHandler);
    }

    public async Task HandleChatAsync(EFClient player, string message) => await _game.HandleChatAsync(player, message);
    public async Task JoinGameAsync(EFClient player) => await _game.JoinGameAsync(player);
    public async Task LeaveGameAsync(EFClient player) => await _game.LeaveGameAsync(player);
    public bool IsPlayerPlaying(EFClient player) => _game.IsPlayerPlaying(player);
    public int GetPlayerCount() => _game.GetPlayerCount();
}
