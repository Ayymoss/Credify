using System.Globalization;
using Credify.Chat.Active.Raffle.Models;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Chat.Passive.Quests.Models;
using Credify.Configuration;
using Credify.Models;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;

namespace Credify.Services;

public class PersistenceService(
    IMetaServiceV2 metaService,
    CredifyConfiguration credifyConfig,
    ClientService clientService,
    CredifyCache cache)
{
    public void ResetBank() => cache.BankCredits = 0;
    public void AddBankCredits(long credits) => cache.AddBankCredits(credits);
    public static bool AvailableFunds(EFClient client, long amount) => amount <= client.GetAdditionalProperty<long>(Plugin.CreditsAmount);

    public async Task WriteClientCreditsAsync(EFClient client, long? amount = null)
    {
        var credits = amount is not null
            ? amount.ToString()
            : client.GetAdditionalProperty<long>(Plugin.CreditsAmount).ToString();
        await metaService.SetPersistentMeta(Plugin.CreditsAmount, credits, client.ClientId);
    }

    public async Task WriteTopScoreAsync()
    {
        await metaService.SetPersistentMetaValue(Plugin.TopKey, cache.TopCredits);
    }

    public async Task ReadStatisticsAsync()
    {
        var store = await metaService.GetPersistentMetaValue<StatisticsStateStore>(Plugin.StatisticsKey);

        if (store is null) cache.StatisticsState = new StatisticsState();
        else cache.StatisticsState.SetReadCredits(store);
    }

    public async Task WriteStatisticsAsync()
    {
        await metaService.SetPersistentMetaValue(Plugin.StatisticsKey, cache.StatisticsState.GetWriteCredits());
    }

    public async Task WriteLastRaffleWinnerAsync(LastWinner lastWinner)
    {
        await metaService.SetPersistentMetaValue(Plugin.LastRaffleWinner, lastWinner);
    }

    public async Task<LastWinner?> ReadLastRaffleWinnerAsync()
    {
        return await metaService.GetPersistentMetaValue<LastWinner>(Plugin.LastRaffleWinner);
    }

    public async Task<List<Player>?> ReadRaffleAsync()
    {
        return await metaService.GetPersistentMetaValue<List<Player>>(Plugin.RaffleKey) ?? [];
    }

    public async Task WriteRaffle(List<Player> rafflePlayers)
    {
        await metaService.SetPersistentMetaValue(Plugin.RaffleKey, rafflePlayers);
    }

    public async Task WriteRecentBoughtItemsAsync(ClientShopContext item)
    {
        var items = await ReadRecentBoughtItemsAsync();
        var ordered = items.OrderByDescending(x => x.Bought)
            .Take(9)
            .ToList();

        ordered.Add(item);
        await metaService.SetPersistentMetaValue(Plugin.RecentBoughtItems, ordered);
    }

    public async Task<IEnumerable<ClientShopContext>> ReadRecentBoughtItemsAsync()
    {
        return await metaService.GetPersistentMetaValue<List<ClientShopContext>>(Plugin.RecentBoughtItems) ?? [];
    }

    public async Task WriteNextRaffleAsync(DateTimeOffset dateTime)
    {
        await metaService.SetPersistentMeta(Plugin.NextRaffleKey, dateTime.ToString("o", CultureInfo.InvariantCulture));
    }

    public async Task WriteClientShopAsync(EFClient client, List<ClientShopItem> shopItems)
    {
        await metaService.SetPersistentMetaValue(Plugin.ShopKey, shopItems, client.ClientId);
    }

    public async Task WriteBankCreditsAsync() => await metaService.SetPersistentMeta(Plugin.BankCreditsKey, cache.BankCredits.ToString());

    public async Task ReadTopScoreAsync()
    {
        cache.TopCredits = await metaService.GetPersistentMetaValue<List<TopCreditEntry>>(Plugin.TopKey) ?? [];
    }

    private async Task<List<ClientShopItem>> ReadClientShopItemsAsync(EFClient client)
    {
        var shopItems = await metaService.GetPersistentMetaValue<List<ClientShopItem>>(Plugin.ShopKey, client.ClientId) ?? [];
        client.SetAdditionalProperty(Plugin.ShopKey, shopItems);
        return shopItems;
    }

    public async Task<List<ClientShopItem>> GetClientShopItemsAsync(EFClient client)
    {
        List<ClientShopItem> userCredits;
        if (client.IsIngame)
        {
            userCredits = client.GetAdditionalProperty<List<ClientShopItem>>(Plugin.ShopKey);
            return userCredits;
        }

        userCredits = await ReadClientShopItemsAsync(client);
        return userCredits;
    }

    public async Task<List<QuestMeta>> ReadClientQuestsAsync(EFClient client)
    {
        var quests = await metaService.GetPersistentMetaValue<List<QuestMeta>>(Plugin.ClientQuestsKey, client.ClientId) ?? [];
        client.SetAdditionalProperty(Plugin.ClientQuestsKey, quests);
        return quests;
    }

    public async Task WriteClientQuestsAsync(EFClient client)
    {
        var quests = client.GetAdditionalProperty<List<QuestMeta>>(Plugin.ClientQuestsKey) ?? [];
        await metaService.SetPersistentMetaValue(Plugin.ClientQuestsKey, quests, client.ClientId);
    }

    public async Task ReadBankCreditsAsync()
    {
        var bankCredits = (await metaService.GetPersistentMeta(Plugin.BankCreditsKey))?.Value;
        var credits = bankCredits is null
            ? 0
            : long.Parse(bankCredits);
        cache.BankCredits = credits;
    }

    public async Task<DateTimeOffset?> ReadNextRaffleAsync()
    {
        var nextLotto = (await metaService.GetPersistentMeta(Plugin.NextRaffleKey))?.Value;
        if (nextLotto is null) return null;
        return DateTimeOffset.Parse(nextLotto);
    }

    public async Task OnJoinAsync(EFClient client)
    {
        await ReadClientQuestsAsync(client);
        await ReadClientShopItemsAsync(client);
        await LoadUserCreditsAsync(client);
    }

    public async Task<long> GetClientCreditsAsync(EFClient client)
    {
        long userCredits;
        if (client.IsIngame)
        {
            userCredits = client.GetAdditionalProperty<long>(Plugin.CreditsAmount);
            return userCredits;
        }

        userCredits = await LoadUserCreditsAsync(client);
        return userCredits;
    }

    public async Task<long> AddCreditsAsync(EFClient client, long credits)
    {
        cache.StatisticsState.AddCreditsWon((ulong)credits);
        var balance = await AlterClientCreditsAsync(client, credits);
        return balance;
    }

    public async Task<long> RemoveCreditsAsync(EFClient client, long credits)
    {
        cache.StatisticsState.AddCreditsSpent((ulong)credits);
        ICredifyEventService.RaiseEvent(ObjectiveType.CreditsSpent, client, credits);
        var balance = await AlterClientCreditsAsync(client, -credits);
        return balance;
    }

    private async Task<long> AlterClientCreditsAsync(EFClient client, long amount) // TODO: Optimise
    {
        long credits, newCredits;
        if (client.IsIngame)
        {
            credits = client.GetAdditionalProperty<long>(Plugin.CreditsAmount);
            newCredits = credits + amount;
            client.SetAdditionalProperty(Plugin.CreditsAmount, newCredits);
        }
        else
        {
            credits = await LoadUserCreditsAsync(client);
            newCredits = credits + amount;
            await WriteClientCreditsAsync(client, newCredits);
        }

        OrderTop(client, newCredits);
        return newCredits;
    }

    private async Task<long> LoadUserCreditsAsync(EFClient client)
    {
        // Get pre-initialised credits
        var userCredits = (await metaService.GetPersistentMeta(Plugin.CreditsAmount, client.ClientId))?.Value;
        var credits = userCredits is null ? 0 : long.Parse(userCredits);
        client.SetAdditionalProperty(Plugin.CreditsAmount, credits);
        return credits;
    }

    public async Task OnKill(EFClient client)
    {
        await AddCreditsAsync(client, 1);
        AddBankCredits(1000000);
        cache.StatisticsState.IncrementCreditsEarned();
    }

    private bool ExistInTop(int targetClientId)
    {
        var topResult = cache.TopCredits.FirstOrDefault(i => i.ClientId == targetClientId);
        return topResult is not null;
    }

    public void OrderTop(EFClient client, long amount)
    {
        if (client.ClientId is 0 or 1) return;
        lock (cache.TopCredits)
        {
            //// If the top hasn't got 5 entries yet add user - check for duplicates.
            //if (cache.TopCredits.Count < 5 && !ExistInTop(client.ClientId))
            //{
            //    cache.TopCredits.Add(new TopCreditEntry { ClientId = client.ClientId, Credits = amount });
            //}

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

                ICredifyEventService.RaiseEvent(ObjectiveType.TopHolder, client);
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

    public void ResetTop() => cache.TopCredits = [];
    public void ResetStatistics() => cache.StatisticsState = new StatisticsState();
}
