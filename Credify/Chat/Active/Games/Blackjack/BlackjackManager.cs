using Credify.Chat.Active.Core;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// Manager for Blackjack game. Implements IActiveGame for consistency with other active games.
/// Note: Insurance bets are not currently supported.
/// </summary>
public class BlackjackManager(
    CredifyConfiguration credifyConfig,
    PersistenceService persistenceService,
    GamePlayerCommunication communication)
    : IActiveGame
{
    private readonly BlackjackGame _game = new(persistenceService, credifyConfig, communication);

    public async Task HandleChatAsync(EFClient player, string message) => await _game.HandleChatAsync(player, message);
    public async Task JoinGameAsync(EFClient player) => await _game.JoinGameAsync(player);
    public async Task LeaveGameAsync(EFClient player) => await _game.LeaveGameAsync(player);
    public bool IsPlayerPlaying(EFClient player) => _game.IsPlayerPlaying(player);
    public int GetPlayerCount() => _game.GetPlayerCount();
}
