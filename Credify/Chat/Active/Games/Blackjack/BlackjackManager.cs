using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Blackjack.Utilities;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// Manager for Blackjack game. Implements IActiveGame for consistency with other active games.
/// Injects utilities for input/output handling following Poker/Roulette patterns.
/// Note: Insurance bets are not currently supported.
/// </summary>
public class BlackjackManager : IActiveGame
{
    private readonly BlackjackHandleInput _inputHandler;
    private readonly BlackjackHandleOutput _outputHandler;
    private readonly BlackjackGame _game;

    public BlackjackManager(
        CredifyConfiguration config,
        PersistenceService persistenceService,
        GamePlayerCommunication communication)
    {
        _inputHandler = new BlackjackHandleInput(config.Translations.Blackjack);
        _outputHandler = new BlackjackHandleOutput(config.Translations.Blackjack, communication);
        _game = new BlackjackGame(
            persistenceService, 
            config, 
            communication,
            _inputHandler,
            _outputHandler);
    }

    public async Task HandleChatAsync(EFClient player, string message) => await _game.HandleChatAsync(player, message);
    public async Task JoinGameAsync(EFClient player) => await _game.JoinGameAsync(player);
    public async Task LeaveGameAsync(EFClient player) => await _game.LeaveGameAsync(player);
    public bool IsPlayerPlaying(EFClient player) => _game.IsPlayerPlaying(player);
    public int GetPlayerCount() => _game.GetPlayerCount();
}
