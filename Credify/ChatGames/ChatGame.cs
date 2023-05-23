using Credify.ChatGames.Models;
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames;

public abstract class ChatGame
{
    public GameState GameState { get; set; }
    protected GameStateInfo GameInfo { get; set; } = new();
    protected readonly SemaphoreSlim MessageReceivedLock = new(1, 1);

    public abstract Task Start();
    public abstract Task HandleChatMessage(EFClient client, string message);
}
