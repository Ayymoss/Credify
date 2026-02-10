using System.Text.Json;
using Data.Models;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;

namespace GameServer.Mocks;

/// <summary>
/// In-memory implementation of IMetaServiceV2 for testing purposes. Stores all meta data in dictionaries.
/// </summary>
public class MockMetaService : IMetaServiceV2
{
    private readonly Dictionary<(string Key, int ClientId), string> _clientMeta = new();
    private readonly Dictionary<string, string> _globalMeta = new();

    #region PER_CLIENT

    public Task SetPersistentMeta(string metaKey, string metaValue, int clientId, CancellationToken token = default)
    {
        _clientMeta[(metaKey, clientId)] = metaValue;
        return Task.CompletedTask;
    }

    public Task SetPersistentMetaValue<T>(string metaKey, T metaValue, int clientId, CancellationToken token = default) where T : class
    {
        _clientMeta[(metaKey, clientId)] = JsonSerializer.Serialize(metaValue);
        return Task.CompletedTask;
    }

    public Task SetPersistentMetaForLookupKey(string metaKey, string lookupKey, int lookupId, int clientId, CancellationToken token = default)
    {
        _clientMeta[(metaKey, clientId)] = $"{lookupKey}:{lookupId}";
        return Task.CompletedTask;
    }

    public Task IncrementPersistentMeta(string metaKey, int incrementAmount, int clientId, CancellationToken token = default)
    {
        var key = (metaKey, clientId);
        var current = _clientMeta.TryGetValue(key, out var val) && int.TryParse(val, out var i) ? i : 0;
        _clientMeta[key] = (current + incrementAmount).ToString();
        return Task.CompletedTask;
    }

    public Task DecrementPersistentMeta(string metaKey, int decrementAmount, int clientId, CancellationToken token = default)
    {
        var key = (metaKey, clientId);
        var current = _clientMeta.TryGetValue(key, out var val) && int.TryParse(val, out var i) ? i : 0;
        _clientMeta[key] = (current - decrementAmount).ToString();
        return Task.CompletedTask;
    }

    public Task<EFMeta?> GetPersistentMeta(string metaKey, int clientId, CancellationToken token = default)
    {
        if (_clientMeta.TryGetValue((metaKey, clientId), out var value))
        {
            return Task.FromResult<EFMeta?>(new EFMeta { Key = metaKey, Value = value });
        }
        return Task.FromResult<EFMeta?>(null);
    }

    public Task<T?> GetPersistentMetaValue<T>(string metaKey, int clientId, CancellationToken token = default) where T : class
    {
        if (_clientMeta.TryGetValue((metaKey, clientId), out var value))
        {
            try { return Task.FromResult(JsonSerializer.Deserialize<T>(value)); }
            catch { return Task.FromResult<T?>(default); }
        }
        return Task.FromResult<T?>(default);
    }

    public Task<EFMeta?> GetPersistentMetaByLookup(string metaKey, string lookupKey, int clientId, CancellationToken token = default)
    {
        return GetPersistentMeta(metaKey, clientId, token);
    }

    public Task RemovePersistentMeta(string metaKey, int clientId, CancellationToken token = default)
    {
        _clientMeta.Remove((metaKey, clientId));
        return Task.CompletedTask;
    }

    #endregion

    #region GLOBAL

    public Task SetPersistentMeta(string metaKey, string metaValue, CancellationToken token = default)
    {
        _globalMeta[metaKey] = metaValue;
        return Task.CompletedTask;
    }

    public Task SetPersistentMetaValue<T>(string metaKey, T metaValue, CancellationToken token = default) where T : class
    {
        _globalMeta[metaKey] = JsonSerializer.Serialize(metaValue);
        return Task.CompletedTask;
    }

    public Task RemovePersistentMeta(string metaKey, CancellationToken token = default)
    {
        _globalMeta.Remove(metaKey);
        return Task.CompletedTask;
    }

    public Task<EFMeta?> GetPersistentMeta(string metaKey, CancellationToken token = default)
    {
        if (_globalMeta.TryGetValue(metaKey, out var value))
        {
            return Task.FromResult<EFMeta?>(new EFMeta { Key = metaKey, Value = value });
        }
        return Task.FromResult<EFMeta?>(null);
    }

    public Task<T?> GetPersistentMetaValue<T>(string metaKey, CancellationToken token = default) where T : class
    {
        if (_globalMeta.TryGetValue(metaKey, out var value))
        {
            try { return Task.FromResult(JsonSerializer.Deserialize<T>(value)); }
            catch { return Task.FromResult<T?>(default); }
        }
        return Task.FromResult<T?>(default);
    }

    #endregion

    #region RUNTIME_META

    public void AddRuntimeMeta<T, TReturn>(MetaType metaKey, Func<T, CancellationToken, Task<IEnumerable<TReturn>>> metaAction)
        where TReturn : IClientMeta
        where T : PaginationRequest
    {
        // No-op for testing
    }

    public Task<IEnumerable<IClientMeta>> GetRuntimeMeta(ClientPaginationRequest request, CancellationToken token = default)
    {
        return Task.FromResult<IEnumerable<IClientMeta>>([]);
    }

    public Task<IEnumerable<T>> GetRuntimeMeta<T>(ClientPaginationRequest request, MetaType metaType, CancellationToken token = default)
        where T : IClientMeta
    {
        return Task.FromResult<IEnumerable<T>>([]);
    }

    #endregion
}
