using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Blackjack;

public class BlackjackManager
{
    private readonly BlackjackGame _game;

    public BlackjackManager(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager)
    {
        _game = new BlackjackGame(persistenceManager, credifyConfig);
    }

    public async Task HandleChatEventAsync(EFClient player, string message) => await _game.HandleChatAsync(player, message);
    public async Task JoinGameAsync(EFClient player) => await _game.JoinGameAsync(player);
    public async Task LeaveGameAsync(EFClient player) => await _game.LeaveGameAsync(player);
    public bool IsPlayerPlaying(EFClient player) => _game.IsPlayerPlaying(player);
    public int GetPlayerCount() => _game.GetPlayerCount();
}
