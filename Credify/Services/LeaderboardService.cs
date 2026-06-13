using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Credify.Chat.Feature.Achievements.Models;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Chat.Passive.Quests.Models;
using Credify.Constants;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;

namespace Credify.Services;

public sealed record LeaderboardEntry(int Rank, int ClientId, string Name, long Value);

/// <summary>
/// Builds the webfront's full leaderboards straight from the metadata table — independent of the
/// 5-entry in-game TopCredits cache (which exists to keep the !crtop chat output short). Credit
/// balances order in SQL; the kills/won/wagered/quest boards parse the per-client JSON blobs in
/// memory. Each board is cached briefly so tab-switching and the periodic refresh don't re-scan.
/// </summary>
public class LeaderboardService(IDatabaseContextFactory contextFactory)
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private readonly object _lock = new();
    private readonly Dictionary<string, (DateTimeOffset At, List<LeaderboardEntry> Rows)> _cache = new();

    public Task<List<LeaderboardEntry>> CreditsAsync(int limit) =>
        CachedAsync($"credits:{limit}", () => LoadCreditsAsync(limit));

    /// <summary>Ranks players by a cumulative objective total (Kill, Baller = won, CreditsSpent = wagered, …).</summary>
    public Task<List<LeaderboardEntry>> ObjectiveAsync(ObjectiveType objective, int limit) =>
        CachedAsync($"obj:{(int)objective}:{limit}", () => LoadObjectiveAsync(objective, limit));

    public Task<List<LeaderboardEntry>> QuestsAsync(int limit) =>
        CachedAsync($"quests:{limit}", () => LoadQuestsAsync(limit));

    private async Task<List<LeaderboardEntry>> CachedAsync(string key, Func<Task<List<LeaderboardEntry>>> build)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow - hit.At < Ttl)
            {
                return hit.Rows;
            }
        }

        var rows = await build();
        lock (_lock) _cache[key] = (DateTimeOffset.UtcNow, rows);
        return rows;
    }

    private async Task<List<LeaderboardEntry>> LoadCreditsAsync(int limit)
    {
        await using var ctx = contextFactory.CreateContext(false);
        // balances are numeric strings; for non-negative integers (length desc, value desc) is exact
        // numeric ordering and translates to SQL — negatives can't be top entries, so exclude them
        var rows = await ctx.Set<EFMeta>()
            .Where(m => m.Key == PluginConstants.CreditsAmount && m.ClientId > 1)
            .Where(m => !m.Value.StartsWith("-"))
            .OrderByDescending(m => m.Value.Length)
            .ThenByDescending(m => m.Value)
            .Take(limit)
            .Select(m => new { ClientId = m.ClientId!.Value, m.Value, m.Client.CurrentAlias.Name })
            .ToListAsync();

        return rows
            .Select((r, i) => new LeaderboardEntry(i + 1, r.ClientId, r.Name.StripColors(),
                long.TryParse(r.Value, out var v) ? v : 0))
            .ToList();
    }

    private async Task<List<LeaderboardEntry>> LoadObjectiveAsync(ObjectiveType objective, int limit)
    {
        await using var ctx = contextFactory.CreateContext(false);
        // one AchievementProgress blob per client; totals can't be ranked in SQL, so pull the (small)
        // blobs for everyone who has achievement state and rank in memory
        var rows = await ctx.Set<EFMeta>()
            .Where(m => m.Key == PluginConstants.AchievementsKey && m.ClientId > 1)
            .Select(m => new { ClientId = m.ClientId!.Value, m.Value, m.Client.CurrentAlias.Name })
            .ToListAsync();

        var objectiveKey = (int)objective;
        return rows
            .Select(r => new { r.ClientId, r.Name, Total = ObjectiveTotal(r.Value, objectiveKey) })
            .Where(r => r.Total > 0)
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.ClientId)
            .Take(limit)
            .Select((r, i) => new LeaderboardEntry(i + 1, r.ClientId, r.Name.StripColors(), r.Total))
            .ToList();
    }

    private async Task<List<LeaderboardEntry>> LoadQuestsAsync(int limit)
    {
        await using var ctx = contextFactory.CreateContext(false);
        var rows = await ctx.Set<EFMeta>()
            .Where(m => m.Key == PluginConstants.ClientQuestsKey && m.ClientId > 1)
            .Where(m => m.Value != "[]")
            .Select(m => new { ClientId = m.ClientId!.Value, m.Value, m.Client.CurrentAlias.Name })
            .ToListAsync();

        return rows
            .Select(r => new { r.ClientId, r.Name, Done = CompletedQuests(r.Value) })
            .Where(r => r.Done > 0)
            .OrderByDescending(r => r.Done)
            .ThenBy(r => r.ClientId)
            .Take(limit)
            .Select((r, i) => new LeaderboardEntry(i + 1, r.ClientId, r.Name.StripColors(), r.Done))
            .ToList();
    }

    private static long ObjectiveTotal(string blob, int objectiveKey)
    {
        try
        {
            var progress = JsonSerializer.Deserialize<AchievementProgress>(blob, Json);
            return progress?.Totals.GetValueOrDefault(objectiveKey) ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static long CompletedQuests(string blob)
    {
        try
        {
            var quests = JsonSerializer.Deserialize<List<QuestMeta>>(blob, Json);
            return quests?.Count(q => q.Completed) ?? 0;
        }
        catch (JsonException)
        {
            return 0;
        }
    }
}
