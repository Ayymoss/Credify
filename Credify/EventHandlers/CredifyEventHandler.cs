using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Enums;
using SharedLibraryCore.Database.Models;

namespace Credify.EventHandlers;

/// <summary>
/// Handles Credify-specific events (e.g., quest objectives).
/// </summary>
public class CredifyEventHandler(QuestManager questManager)
{
    public async Task HandleAsync(ObjectiveType objective, EFClient client, object? data)
    {
        await questManager.HandleCredifyEvent(objective, client, data);
    }
}
