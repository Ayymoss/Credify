using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Blackjack;

// Global game. Doesn't need any server-context.
// Anyone can join and bet against the house.
// Speed up game if people are responsive

public class BlackjackMeta
{
    private readonly BlackjackGame _game;

    public BlackjackMeta(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager)
    {
        _game = new BlackjackGame(persistenceManager, credifyConfig);
    }

    public async Task HandleChatEvent(EFClient player, string message) => await _game.HandleChatAsync(player, message);
    public async Task JoinGame(EFClient player) => await _game.JoinGameAsync(player);
    public async Task LeaveGame(EFClient player) => await _game.LeaveGameAsync(player);
    public bool IsPlayerPlaying(EFClient player) => _game.IsPlayerPlaying(player);

    public int GetPlayerCount() => _game.GetPlayerCount();
}
