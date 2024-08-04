using System.Globalization;
using Credify.Configuration;
using Credify.Models;
using Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify;

public class PersistenceManager(
    IMetaServiceV2 metaService,
    IDatabaseContextFactory contextFactory,
    CredifyConfiguration credifyConfig)
{
    public List<TopCreditEntry> TopCredits { get; private set; } = [];
    private long _bankCredits;
    public long BankCredits => Interlocked.Read(ref _bankCredits);
    public StatisticsState StatisticsState { get; private set; } = new();

    public void ResetBank() => _bankCredits = 0;

    public void AddBankCreditsAsync(long credits)
    {
        Interlocked.Add(ref _bankCredits, credits);
    }

    public static bool AvailableFunds(EFClient client, long amount) =>
        amount <= client.GetAdditionalProperty<long>(Plugin.CreditsAmount);

    public async Task WriteClientCreditsAsync(EFClient client, long? amount = null)
    {
        await metaService.SetPersistentMeta(Plugin.CreditsAmount, amount is not null
            ? amount.ToString()
            : client.GetAdditionalProperty<long>(Plugin.CreditsAmount).ToString(), client.ClientId);
    }

    public async Task WriteTopScoreAsync() =>
        await metaService.SetPersistentMetaValue(Plugin.TopKey, TopCredits);

    public async Task WriteStatisticsAsync() =>
        await metaService.SetPersistentMetaValue(Plugin.StatisticsKey, StatisticsState);

    public async Task WriteLastLotteryWinnerAsync(int clientId, string client, long amount, int lastPlayers) =>
        await metaService.SetPersistentMeta(Plugin.LastLottoWinner, $"{clientId}::{client}::{amount}::{lastPlayers}");

    public async Task<(int ClientId, string ClientName, long PayOut, int LastPlayers)?> ReadLastLotteryWinnerAsync()
    {
        var winnerMeta = await metaService.GetPersistentMeta(Plugin.LastLottoWinner);
        if (winnerMeta is null) return null;
        var data = winnerMeta.Value.Split("::", 4);
        return (int.Parse(data[0]), data[1], long.Parse(data[2]), int.Parse(data[3]));
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
        var items = await metaService.GetPersistentMetaValue<List<ClientShopContext>>(Plugin.RecentBoughtItems);
        return items ?? [];
    }

    public async Task WriteLotteryAsync(List<Lottery> lotteries) => await metaService.SetPersistentMetaValue(Plugin.LotteryKey, lotteries);

    public async Task WriteNextLotteryAsync(DateTimeOffset dateTime) =>
        await metaService.SetPersistentMeta(Plugin.NextLotteryKey, dateTime.ToString("o", CultureInfo.InvariantCulture));

    public async Task WriteClientShopAsync(EFClient client, List<ClientShopItem> shopItems) =>
        await metaService.SetPersistentMetaValue(Plugin.ShopKey, shopItems, client.ClientId);

    public async Task WriteBankCreditsAsync() => await metaService.SetPersistentMeta(Plugin.BankCreditsKey, BankCredits.ToString());

    public async Task ReadTopScoreAsync()
    {
        var topCreditsValue = await metaService.GetPersistentMetaValue<List<TopCreditEntry>>(Plugin.TopKey);
        TopCredits = topCreditsValue ?? [];
    }

    private async Task<List<ClientShopItem>> ReadClientShopItemsAsync(EFClient client)
    {
        var shopItems = await metaService.GetPersistentMetaValue<List<ClientShopItem>>(Plugin.ShopKey, client.ClientId);

        shopItems ??= [];
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

    public async Task ReadBankCreditsAsync()
    {
        var bankCredits = (await metaService.GetPersistentMeta(Plugin.BankCreditsKey))?.Value;
        _bankCredits = bankCredits is null
            ? 0
            : long.Parse(bankCredits);
    }

    public async Task<DateTimeOffset?> ReadNextLotteryAsync()
    {
        var nextLotto = (await metaService.GetPersistentMeta(Plugin.NextLotteryKey))?.Value;
        if (nextLotto is null) return null;
        return DateTimeOffset.Parse(nextLotto);
    }

    public async Task<List<Lottery>> ReadLotteryAsync()
    {
        var lotteries = await metaService.GetPersistentMetaValue<List<Lottery>>(Plugin.LotteryKey);
        return lotteries ?? [];
    }

    public async Task ReadStatisticsAsync()
    {
        var statistics = await metaService.GetPersistentMetaValue<StatisticsState>(Plugin.StatisticsKey);
        StatisticsState = statistics ?? new StatisticsState();
    }

    public async Task OnJoinAsync(EFClient client)
    {
        await ReadClientShopItemsAsync(client);
        var userCredits = await LoadUserCreditsAsync(client);
        client.Tell(credifyConfig.Translations.Core.UserCredits.FormatExt(userCredits.ToString("N0")));
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

    public async Task<long> AddCreditsAsync(int clientId, long credits)
    {
        lock (StatisticsState) StatisticsState.CreditsWon += (ulong)credits;
        var balance = await AlterClientCreditsAsync(clientId, credits);
        return balance;
    }

    public async Task<long> AddCreditsAsync(EFClient client, long credits)
    {
        lock (StatisticsState) StatisticsState.CreditsWon += (ulong)credits;
        var balance = await AlterClientCreditsAsync(client, credits);
        return balance;
    }

    public async Task<long> RemoveCreditsAsync(int clientId, long credits)
    {
        lock (StatisticsState) StatisticsState.CreditsSpent += (ulong)credits;
        var balance = await AlterClientCreditsAsync(clientId, -credits);
        return balance;
    }

    public async Task<long> RemoveCreditsAsync(EFClient client, long credits)
    {
        lock (StatisticsState) StatisticsState.CreditsSpent += (ulong)credits;
        var balance = await AlterClientCreditsAsync(client, -credits);
        return balance;
    }

    private async Task<long> AlterClientCreditsAsync(int clientId, long amount)
    {
        await using var context = contextFactory.CreateContext(false);
        var client = (await context.Clients.FirstOrDefaultAsync(x => x.ClientId == clientId))?.ToPartialClient();

        ArgumentNullException.ThrowIfNull(client);
        return await AlterClientCreditsAsync(client, amount);
    }

    private async Task<long> AlterClientCreditsAsync(EFClient client, long amount) // TODO: Optimise
    {
        long credits, newCredits;
        if (client.IsIngame)
        {
            credits = client.GetAdditionalProperty<long>(Plugin.CreditsAmount);
            newCredits = credits + amount;
            client.SetAdditionalProperty(Plugin.CreditsAmount, newCredits);
            OrderTop(client, newCredits);
            return newCredits;
        }

        credits = await LoadUserCreditsAsync(client);
        newCredits = credits + amount;
        await WriteClientCreditsAsync(client, newCredits);
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

    public void OnKill(EFClient client)
    {
        var userCredits = client.GetAdditionalProperty<long>(Plugin.CreditsAmount);
        userCredits++;
        client.SetAdditionalProperty(Plugin.CreditsAmount, userCredits);
        AddBankCreditsAsync(1);
        lock (StatisticsState) StatisticsState.CreditsEarned++;
    }

    private bool ExistInTop(int targetClientId)
    {
        var topResult = TopCredits.FirstOrDefault(i => i.ClientId == targetClientId);
        return topResult is not null;
    }

    public void OrderTop(EFClient client, long amount)
    {
        if (client.ClientId is 0 or 1) return;
        lock (TopCredits)
        {
            // If the top hasn't got 5 entries yet add user - check for duplicates.
            if (TopCredits.Count < 5 && !ExistInTop(client.ClientId))
                TopCredits.Add(new TopCreditEntry { ClientId = client.ClientId, Credits = amount });

            //If the target's credits are greater than last item OR already exists in top, sort & update top.
            if (amount <= TopCredits.Last().Credits && !ExistInTop(client.ClientId)) return;

            var existingCredEntry = TopCredits.FirstOrDefault(credit => credit.ClientId == client.ClientId);
            // Doesn't exist in top - Create new entry and sort
            if (existingCredEntry is null)
            {
                TopCredits.Add(new TopCreditEntry
                {
                    ClientId = client.ClientId,
                    Credits = amount
                });
            }
            else // Exists already in top, just set credits and update
            {
                existingCredEntry.Credits = amount;
            }

            TopCredits = TopCredits
                .OrderByDescending(credit => credit.Credits)
                .Take(5)
                .ToList();
        }
    }

    public void ResetTop() => TopCredits = [];

    public void ResetStatistics() => StatisticsState = new StatisticsState();
}
