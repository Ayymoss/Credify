using Credify.Constants;
using Credify.Models;
using Credify.Services;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

/// <summary>
/// Service responsible for managing statistics and top scores.
/// </summary>
public class StatisticsService(
    IMetaServiceV2 metaService,
    CredifyCache cache)
{
    /// <summary>
    /// Writes the top credits leaderboard to persistent storage.
    /// </summary>
    public async Task WriteTopScoreAsync()
    {
        await metaService.SetPersistentMetaValue(PluginConstants.TopKey, cache.TopCredits);
    }

    /// <summary>
    /// Reads the top credits leaderboard from persistent storage.
    /// </summary>
    public async Task ReadTopScoreAsync()
    {
        cache.TopCredits = await metaService.GetPersistentMetaValue<List<TopCreditEntry>>(PluginConstants.TopKey) ?? [];
    }

    /// <summary>
    /// Reads statistics from persistent storage.
    /// </summary>
    public async Task ReadStatisticsAsync()
    {
        var store = await metaService.GetPersistentMetaValue<StatisticsStateStore>(PluginConstants.StatisticsKey);

        if (store is null) cache.StatisticsState = new StatisticsState();
        else cache.StatisticsState.SetReadCredits(store);
    }

    /// <summary>
    /// Writes statistics to persistent storage.
    /// </summary>
    public async Task WriteStatisticsAsync()
    {
        await metaService.SetPersistentMetaValue(PluginConstants.StatisticsKey, cache.StatisticsState.GetWriteCredits());
    }

    /// <summary>
    /// Adds credits won to statistics.
    /// </summary>
    public void AddCreditsWon(ulong credits)
    {
        cache.StatisticsState.AddCreditsWon(credits);
    }

    /// <summary>
    /// Adds credits spent to statistics.
    /// </summary>
    public void AddCreditsSpent(ulong credits)
    {
        cache.StatisticsState.AddCreditsSpent(credits);
    }

    /// <summary>
    /// Increments the credits earned counter.
    /// </summary>
    public void IncrementCreditsEarned()
    {
        cache.StatisticsState.IncrementCreditsEarned();
    }

    /// <summary>
    /// Updates the top credits leaderboard for a client.
    /// </summary>
    public void OrderTop(EFClient client, long amount)
    {
        if (client.ClientId is 0 or 1) return;
        lock (cache.TopCredits)
        {
            //If the target's credits are greater than last item OR already exists in top, sort & update top.
            if (amount <= cache.TopCredits.LastOrDefault()?.Credits && !ExistInTop(client.ClientId)) return;

            var existingCredEntry = cache.TopCredits.FirstOrDefault(credit => credit.ClientId == client.ClientId);
            // Doesn't exist in top - Create new entry and sort
            if (existingCredEntry is null)
            {
                cache.TopCredits.Add(new TopCreditEntry
                {
                    ClientId = client.ClientId,
                    Credits = amount
                });

                ICredifyEventService.RaiseEvent(Credify.Chat.Passive.Quests.Enums.ObjectiveType.TopHolder, client);
            }
            else // Exists already in top, just set credits and update
            {
                existingCredEntry.Credits = amount;
            }

            cache.TopCredits = cache.TopCredits
                .OrderByDescending(credit => credit.Credits)
                .Take(5)
                .ToList();
        }
    }

    private bool ExistInTop(int targetClientId)
    {
        var topResult = cache.TopCredits.FirstOrDefault(i => i.ClientId == targetClientId);
        return topResult is not null;
    }

    /// <summary>
    /// Resets the top credits leaderboard.
    /// </summary>
    public void ResetTop() => cache.TopCredits = [];

    /// <summary>
    /// Resets statistics.
    /// </summary>
    public void ResetStatistics() => cache.StatisticsState = new StatisticsState();
}
