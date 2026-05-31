using Credify.Chat.Feature.Achievements;
using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Enums;
using SharedLibraryCore.Database.Models;

namespace Credify.EventHandlers;

/// <summary>
/// Handles Credify-specific events (quest objectives and achievement progress).
/// </summary>
public class CredifyEventHandler(QuestManager questManager, AchievementManager achievementManager)
{
    public async Task HandleAsync(ObjectiveType objective, EFClient client, object? data)
    {
        await Task.WhenAll(
            questManager.HandleCredifyEvent(objective, client, data),
            achievementManager.HandleCredifyEvent(objective, client, data));
    }
}
