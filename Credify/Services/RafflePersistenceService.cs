using System.Globalization;
using Credify.Chat.Feature.Raffle.Models;
using Credify.Constants;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

/// <summary>
/// Service responsible for managing raffle data persistence.
/// </summary>
public class RafflePersistenceService(
    IMetaServiceV2 metaService)
{
    /// <summary>
    /// Writes the last raffle winner to persistent storage.
    /// </summary>
    public async Task WriteLastRaffleWinnerAsync(LastWinner lastWinner)
    {
        await metaService.SetPersistentMetaValue(PluginConstants.LastRaffleWinner, lastWinner);
    }

    /// <summary>
    /// Reads the last raffle winner from persistent storage.
    /// </summary>
    public async Task<LastWinner?> ReadLastRaffleWinnerAsync()
    {
        return await metaService.GetPersistentMetaValue<LastWinner>(PluginConstants.LastRaffleWinner);
    }

    /// <summary>
    /// Reads raffle entries from persistent storage.
    /// </summary>
    public async Task<List<Player>> ReadRaffleAsync()
    {
        return await metaService.GetPersistentMetaValue<List<Player>>(PluginConstants.RaffleKey) ?? [];
    }

    /// <summary>
    /// Writes raffle entries to persistent storage.
    /// </summary>
    public async Task WriteRaffleAsync(List<Player> rafflePlayers)
    {
        await metaService.SetPersistentMetaValue(PluginConstants.RaffleKey, rafflePlayers);
    }

    /// <summary>
    /// Writes the next raffle draw time to persistent storage.
    /// </summary>
    public async Task WriteNextRaffleAsync(DateTimeOffset dateTime)
    {
        await metaService.SetPersistentMeta(PluginConstants.NextRaffleKey, dateTime.ToString("o", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Reads the next raffle draw time from persistent storage.
    /// </summary>
    public async Task<DateTimeOffset?> ReadNextRaffleAsync()
    {
        var nextLotto = (await metaService.GetPersistentMeta(PluginConstants.NextRaffleKey))?.Value;
        if (nextLotto is null) return null;
        return DateTimeOffset.Parse(nextLotto);
    }
}
