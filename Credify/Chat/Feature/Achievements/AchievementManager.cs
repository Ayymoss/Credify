using Credify.Chat.Feature.Achievements.Models;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Chat.Feature.Achievements;

/// <summary>
/// Tracks permanent, one-time achievements (distinct from the repeatable quest system).
/// Each achievement accumulates a total for its objective and unlocks - granting a title and
/// a one-off credit reward - once its threshold is reached.
/// </summary>
public class AchievementManager(
    CredifyConfiguration config,
    PersistenceService persistenceService,
    IMetaServiceV2 metaService)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>Loads a client's achievement state into memory on join.</summary>
    public async Task LoadAsync(EFClient client)
    {
        var progress = await metaService.GetPersistentMetaValue<AchievementProgress>(PluginConstants.AchievementsKey, client.ClientId)
                       ?? new AchievementProgress();
        client.SetAdditionalProperty(PluginConstants.AchievementsKey, progress);
    }

    /// <summary>Persists a client's achievement state (on unlock and on disconnect).</summary>
    public async Task SaveAsync(EFClient client)
    {
        var progress = client.GetAdditionalProperty<AchievementProgress>(PluginConstants.AchievementsKey);
        if (progress is not null)
            await metaService.SetPersistentMetaValue(PluginConstants.AchievementsKey, progress, client.ClientId);
    }

    public AchievementProgress GetProgress(EFClient client) =>
        client.GetAdditionalProperty<AchievementProgress>(PluginConstants.AchievementsKey) ?? new AchievementProgress();

    /// <summary>The player's most prestigious unlocked title (highest threshold), or null.</summary>
    public string? TopTitle(EFClient client)
    {
        var progress = GetProgress(client);
        return config.Achievement.Achievements
            .Where(a => progress.Unlocked.Contains(a.Id))
            .OrderByDescending(a => a.Threshold)
            .Select(a => a.Name)
            .FirstOrDefault();
    }

    /// <summary>Routes credit-amount events (Baller/CreditsSpent/Donation) and count events.</summary>
    public async Task HandleCredifyEvent(ObjectiveType objective, EFClient client, object? data)
    {
        var amount = objective switch
        {
            ObjectiveType.Baller or ObjectiveType.CreditsSpent or ObjectiveType.Donation =>
                data is long value ? Math.Max(0, value) : 0,
            _ => 1
        };

        if (amount > 0) await ProgressAsync(client, objective, amount);
    }

    /// <summary>Driven from ClientKilled (the Kill objective isn't raised via the event service).</summary>
    public async Task HandleKill(EFClient client) => await ProgressAsync(client, ObjectiveType.Kill, 1);

    private async Task ProgressAsync(EFClient client, ObjectiveType objective, long amount)
    {
        if (!config.Achievement.IsEnabled || client.ClientId <= 1) return;

        await _lock.WaitAsync();
        try
        {
            var progress = client.GetAdditionalProperty<AchievementProgress>(PluginConstants.AchievementsKey);
            if (progress is null)
            {
                await LoadAsync(client);
                progress = client.GetAdditionalProperty<AchievementProgress>(PluginConstants.AchievementsKey)!;
            }

            var key = (int)objective;
            progress.Totals.TryGetValue(key, out var total);
            total += amount;
            progress.Totals[key] = total;

            var newlyUnlocked = config.Achievement.Achievements
                .Where(a => a.Enabled && a.Objective == objective && a.Threshold <= total && !progress.Unlocked.Contains(a.Id))
                .ToList();

            foreach (var achievement in newlyUnlocked)
            {
                progress.Unlocked.Add(achievement.Id);
                if (achievement.Reward > 0) await persistenceService.AddCreditsAsync(client, achievement.Reward);

                // CurrentServer is null for web-only players (resolved from the DB, not connected in-game),
                // so guard the announce — an unlock earned on the webfront must never crash the circuit.
                if (config.Achievement.AnnounceUnlocks && client.CurrentServer is not null)
                {
                    client.CurrentServer.Broadcast(config.Translations.Achievements.Unlocked
                        .FormatExt(client.CleanedName, achievement.Name, achievement.Reward.ToString("N0")));
                }
            }

            client.SetAdditionalProperty(PluginConstants.AchievementsKey, progress);
            if (newlyUnlocked.Count > 0) await SaveAsync(client);
        }
        finally
        {
            if (_lock.CurrentCount is 0) _lock.Release();
        }
    }
}
