using System.Collections.Concurrent;
using SharedLibraryCore.Database.Models;

namespace Credify.Games.Live;

/// <summary>
/// Gives a web participant a STABLE <see cref="EFClient"/> identity across page loads / Blazor circuits.
/// The live games key their player rosters by EFClient instance, so a web-only player (not connected to a
/// game server) needs the same instance returned every time they act — otherwise each page load would look
/// like a different player. When the player IS connected in-game, callers should prefer the live client and
/// skip this cache; this only backs web-only players. Singleton.
/// </summary>
public sealed class CredifyWebPlayers
{
    private readonly ConcurrentDictionary<int, EFClient> _cache = new();

    /// <summary>Returns the cached web client for this id, creating it from <paramref name="factory"/> once.</summary>
    public EFClient GetOrAdd(int clientId, Func<EFClient> factory) => _cache.GetOrAdd(clientId, _ => factory());

    public void Forget(int clientId) => _cache.TryRemove(clientId, out _);
}
