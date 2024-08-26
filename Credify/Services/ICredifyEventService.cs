using Credify.Chat.Passive.Quests.Enums;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

public interface ICredifyEventService
{
    static event Action<ObjectiveType, EFClient, object?>? OnCredifyEvent;

    static void RaiseEvent(ObjectiveType objective, EFClient client, object? data = null)
    {
        OnCredifyEvent?.Invoke(objective, client, data);
    }
}
