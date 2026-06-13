using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Credify.Services;

/// <summary>One settled play in a player's session log — what <see cref="GameHistoryService"/> stores.</summary>
public sealed record GameHistoryEntry(string Game, string Icon, string Label, long Net, DateTimeOffset At);

/// <summary>
/// In-memory, per-client ring buffer of recent web-game results, shared by every game page so a player can see
/// their session's wins/losses (across all games) even if they miss the transient toast. Singleton: it lives
/// for the plugin lifetime and is keyed by client id, so the log survives navigating between game pages. It is
/// purely a UI convenience — it holds no money and is never the source of truth for balances.
/// </summary>
public sealed class GameHistoryService
{
    private const int MaxPerClient = 30;
    private readonly ConcurrentDictionary<int, LinkedList<GameHistoryEntry>> _byClient = new();

    /// <summary>Raised (with the affected client id and the entry itself) whenever a result is recorded,
    /// so an open history rail or session graph can refresh itself live instead of waiting for its page
    /// to re-render.</summary>
    public event Action<int, GameHistoryEntry>? Changed;

    public void Record(int clientId, GameHistoryEntry entry)
    {
        var list = _byClient.GetOrAdd(clientId, _ => new LinkedList<GameHistoryEntry>());
        lock (list)
        {
            list.AddFirst(entry);
            while (list.Count > MaxPerClient)
            {
                list.RemoveLast();
            }
        }

        Changed?.Invoke(clientId, entry);
    }

    /// <summary>Most-recent-first snapshot of a client's session log (empty if they've not played yet).</summary>
    public IReadOnlyList<GameHistoryEntry> Recent(int clientId)
    {
        if (!_byClient.TryGetValue(clientId, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.ToArray();
        }
    }
}
