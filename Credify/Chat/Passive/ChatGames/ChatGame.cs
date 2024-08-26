using Credify.Chat.Active.Blackjack.Models;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames;

public abstract class ChatGame
{
    public GameState GameState { get; set; }
    protected GameStateInfo GameInfo { get; set; } = new();
    protected readonly SemaphoreSlim MessageReceivedLock = new(1, 1);
    public abstract Task StartAsync();
    public abstract Task HandleChatMessageAsync(EFClient client, string message);
}
