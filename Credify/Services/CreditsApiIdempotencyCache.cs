using System.Collections.Concurrent;

namespace Credify.Services;

/// <summary>
/// Replay guard for <c>POST /api/credits/{clientId}/adjust</c>. A caller that retries a request
/// (timeout, network blip) with the same <c>Idempotency-Key</c> header gets the recorded outcome
/// back instead of applying the delta twice. Keys are scoped per client and expire after
/// <see cref="Ttl"/>; expired entries are swept lazily on access, so the cache needs no timer.
/// In-memory only — a host restart forgets keys, which is acceptable for an admin tooling API
/// (the retry window is seconds, not days).
/// </summary>
public sealed class CreditsApiIdempotencyCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, (DateTimeOffset StoredAt, long Balance)> _entries = new();

    private static string EntryKey(int clientId, string idempotencyKey) => $"{clientId}:{idempotencyKey}";

    public bool TryGet(int clientId, string idempotencyKey, out long balance)
    {
        Sweep();
        if (_entries.TryGetValue(EntryKey(clientId, idempotencyKey), out var entry) &&
            DateTimeOffset.UtcNow - entry.StoredAt < Ttl)
        {
            balance = entry.Balance;
            return true;
        }

        balance = 0;
        return false;
    }

    public void Store(int clientId, string idempotencyKey, long balance) =>
        _entries[EntryKey(clientId, idempotencyKey)] = (DateTimeOffset.UtcNow, balance);

    private void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (key, entry) in _entries)
        {
            if (now - entry.StoredAt >= Ttl)
            {
                _entries.TryRemove(key, out _);
            }
        }
    }
}
