using System.Collections.Concurrent;

namespace Credify.Games.Live;

/// <summary>A single active/just-finished Crash run, for the "who's playing" lobby.</summary>
public sealed record CrashLiveEntry(
    int ClientId,
    string Name,
    long Stake,
    DateTimeOffset StartedAt,
    string Status,          // "Flying" | "Cashed" | "Crashed"
    double FinalMultiplier); // meaningful once settled

/// <summary>
/// Tracks every player's in-flight (and just-finished) Crash run so each web session can show a live lobby of
/// who's playing. Crash is a PER-PLAYER game — everyone flies their own rocket concurrently — so unlike the
/// shared tables this is just a shared view, not a shared game. Singleton. Raises <see cref="Changed"/> on
/// add / status-change / removal so other open pages refresh promptly (each page also polls on its own timer
/// to keep flying multipliers fresh).
/// </summary>
public sealed class CrashLiveRegistry
{
    private readonly ConcurrentDictionary<int, CrashLiveEntry> _entries = new();

    public event Action? Changed;

    public IReadOnlyList<CrashLiveEntry> Snapshot() => _entries.Values.ToList();

    public void Launch(int clientId, string name, long stake, DateTimeOffset startedAt)
    {
        _entries[clientId] = new CrashLiveEntry(clientId, name, stake, startedAt, "Flying", 0);
        Changed?.Invoke();
    }

    public void Settle(int clientId, bool cashed, double multiplier)
    {
        if (_entries.TryGetValue(clientId, out var e))
        {
            _entries[clientId] = e with { Status = cashed ? "Cashed" : "Crashed", FinalMultiplier = multiplier };
            Changed?.Invoke();
        }
    }

    public void Remove(int clientId)
    {
        if (_entries.TryRemove(clientId, out _))
        {
            Changed?.Invoke();
        }
    }
}
