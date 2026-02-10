using Credify.Chat.Passive.Quests.Enums;
using Credify.Constants;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Collections.Concurrent;

namespace Credify.Services;

/// <summary>
/// Service responsible for managing client credits operations.
/// </summary>
public class CreditsService(
    IMetaServiceV2 metaService,
    StatisticsService statisticsService)
{
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _clientLocks = new();

    private SemaphoreSlim GetClientLock(int clientId) => _clientLocks.GetOrAdd(clientId, _ => new SemaphoreSlim(1, 1));

    /// <summary>
    /// Checks if a client has sufficient funds for a transaction.
    /// </summary>
    public static bool AvailableFunds(EFClient client, long amount)
    {
        return amount <= client.GetAdditionalProperty<long>(PluginConstants.CreditsAmount);
    }

    /// <summary>
    /// Writes client credits to persistent storage.
    /// </summary>
    public async Task WriteClientCreditsAsync(EFClient client, long? amount = null)
    {
        var credits = amount is not null
            ? amount.ToString()
            : client.GetAdditionalProperty<long>(PluginConstants.CreditsAmount).ToString();
        await metaService.SetPersistentMeta(PluginConstants.CreditsAmount, credits, client.ClientId);
    }

    /// <summary>
    /// Gets the current credits balance for a client.
    /// </summary>
    public async Task<long> GetClientCreditsAsync(EFClient client)
    {
        long userCredits;
        if (client.IsIngame)
        {
            userCredits = client.GetAdditionalProperty<long>(PluginConstants.CreditsAmount);
            return userCredits;
        }

        userCredits = await LoadUserCreditsAsync(client);
        return userCredits;
    }

    /// <summary>
    /// Adds credits to a client's account.
    /// </summary>
    public async Task<long> AddCreditsAsync(EFClient client, long credits)
    {
        statisticsService.AddCreditsWon((ulong)credits);
        var balance = await AlterClientCreditsAsync(client, credits);
        return balance;
    }

    /// <summary>
    /// Removes credits from a client's account.
    /// </summary>
    public async Task<long> RemoveCreditsAsync(EFClient client, long credits)
    {
        var balance = await AlterClientCreditsAsync(client, -credits);
        statisticsService.AddCreditsSpent((ulong)credits);
        ICredifyEventService.RaiseEvent(ObjectiveType.CreditsSpent, client, credits);
        return balance;
    }

    /// <summary>
    /// Alters client credits by the specified amount.
    /// </summary>
    private async Task<long> AlterClientCreditsAsync(EFClient client, long amount)
    {
        var clientLock = GetClientLock(client.ClientId);
        await clientLock.WaitAsync();
        try
        {
            long credits, newCredits;
            if (client.IsIngame)
            {
                if (client.GetAdditionalProperty<long?>(PluginConstants.CreditsAmount) is null)
                {
                    await LoadUserCreditsAsync(client);
                }
                credits = client.GetAdditionalProperty<long>(PluginConstants.CreditsAmount);
                newCredits = credits + amount;
                client.SetAdditionalProperty(PluginConstants.CreditsAmount, newCredits);
            }
            else
            {
                credits = await LoadUserCreditsAsync(client);
                newCredits = credits + amount;
                await WriteClientCreditsAsync(client, newCredits);
            }

            statisticsService.OrderTop(client, newCredits);
            return newCredits;
        }
        finally
        {
            clientLock.Release();
        }
    }

    /// <summary>
    /// Loads user credits from persistent storage into client's property cache.
    /// </summary>
    private async Task<long> LoadUserCreditsAsync(EFClient client)
    {
        var userCredits = (await metaService.GetPersistentMeta(PluginConstants.CreditsAmount, client.ClientId))?.Value;
        var credits = userCredits is null ? 0 : long.Parse(userCredits);
        client.SetAdditionalProperty(PluginConstants.CreditsAmount, credits);
        return credits;
    }

    /// <summary>
    /// Loads user credits into the client's property cache.
    /// Called when a client joins the server.
    /// </summary>
    public async Task LoadCreditsOnJoinAsync(EFClient client)
    {
        await LoadUserCreditsAsync(client);
    }
}
