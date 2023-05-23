using System.Globalization;
using Credify.Models;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify;

public class PersistenceManager
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly IMetaServiceV2 _metaService;
    public List<TopCreditEntry> TopCredits { get; private set; } = new();
    public long BankCredits { get; private set; }
    public StatisticsState StatisticsState { get; private set; } = new();

    public PersistenceManager(IMetaServiceV2 metaService, IDatabaseContextFactory contextFactory,
        CredifyConfiguration credifyConfig)
    {
        _metaService = metaService;
        _contextFactory = contextFactory;
        _credifyConfig = credifyConfig;
    }

    public void ResetBank() => BankCredits = 0;

    public async Task AddBankCredits(long credits)
    {
        BankCredits += credits;
        await WriteBankCredits();
    }

    public bool AvailableFunds(EFClient client, long amount) =>
        amount <= client.GetAdditionalProperty<long>(Plugin.Key);

    public async Task WriteClientCredits(EFClient client, long? amount = null)
    {
        await _metaService.SetPersistentMeta(Plugin.Key, amount is not null
            ? amount.ToString()
            : client.GetAdditionalProperty<long>(Plugin.Key).ToString(), client.ClientId);
    }

    public async Task WriteTopScoreAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.TopKey, TopCredits);

    public async Task WriteStatisticsAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.StatisticsKey, StatisticsState);

    public async Task WriteLastLotteryWinner(int clientId, string client, long amount) =>
        await _metaService.SetPersistentMeta(Plugin.LastLottoWinner, $"{clientId}::{client}::{amount}");

    public async Task<(int ClientId, string ClientName, long PayOut)?> ReadLastLotteryWinner()
    {
        var winnerMeta = await _metaService.GetPersistentMeta(Plugin.LastLottoWinner);
        if (winnerMeta is null) return null;
        var data = winnerMeta.Value.Split("::", 3);
        return (int.Parse(data[0]), data[1], long.Parse(data[2]));
    }

    public async Task WriteRecentBoughtItems(ClientShopContext item)
    {
        var items = await ReadRecentBoughtItems();
        var ordered = items.OrderByDescending(x => x.Bought)
            .Take(9)
            .ToList();

        ordered.Add(item);

        await _metaService.SetPersistentMetaValue(Plugin.RecentBoughtItems, ordered);
    }

    public async Task<IEnumerable<ClientShopContext>> ReadRecentBoughtItems()
    {
        var items = await _metaService.GetPersistentMetaValue<List<ClientShopContext>>(Plugin.RecentBoughtItems);
        return items ?? new List<ClientShopContext>();
    }

    public async Task WriteLotteryAsync(List<Lottery> lotteries) =>
        await _metaService.SetPersistentMetaValue(Plugin.LotteryKey, lotteries);

    public async Task WriteNextLotteryAsync(DateTimeOffset dateTime) =>
        await _metaService.SetPersistentMeta(Plugin.NextLotteryKey,
            dateTime.ToString("o", CultureInfo.InvariantCulture));

    public async Task WriteClientShopAsync(EFClient client, List<ClientShopItem> shopItems) =>
        await _metaService.SetPersistentMetaValue(Plugin.ShopKey, shopItems, client.ClientId);

    private async Task WriteBankCredits() =>
        await _metaService.SetPersistentMeta(Plugin.BankCreditsKey, BankCredits.ToString());

    public async Task ReadTopScoreAsync()
    {
        var topCreditsValue = await _metaService.GetPersistentMetaValue<List<TopCreditEntry>>(Plugin.TopKey);
        TopCredits = topCreditsValue ?? new List<TopCreditEntry>();
    }

    private async Task<List<ClientShopItem>> ReadClientShopItems(EFClient client)
    {
        var shopItems = await _metaService
            .GetPersistentMetaValue<List<ClientShopItem>>(Plugin.ShopKey, client.ClientId);

        shopItems ??= new List<ClientShopItem>();
        client.SetAdditionalProperty(Plugin.ShopKey, shopItems);

        return shopItems;
    }

    public async Task<List<ClientShopItem>> GetClientShopItems(EFClient client)
    {
        List<ClientShopItem> userCredits;
        if (client.IsIngame)
        {
            userCredits = client.GetAdditionalProperty<List<ClientShopItem>>(Plugin.ShopKey);
            return userCredits;
        }

        userCredits = await ReadClientShopItems(client);
        return userCredits;
    }

    public async Task ReadBankCreditsAsync()
    {
        var bankCredits = (await _metaService.GetPersistentMeta(Plugin.BankCreditsKey))?.Value;

        BankCredits = bankCredits is null
            ? 0
            : long.Parse(bankCredits);
    }

    public async Task<DateTimeOffset?> ReadNextLotteryAsync()
    {
        var nextLotto = (await _metaService.GetPersistentMeta(Plugin.NextLotteryKey))?.Value;
        if (nextLotto is null) return null;
        return DateTimeOffset.Parse(nextLotto);
    }

    public async Task<List<Lottery>> ReadLotteryAsync()
    {
        var lotteries = await _metaService.GetPersistentMetaValue<List<Lottery>>(Plugin.LotteryKey);
        return lotteries ?? new List<Lottery>();
    }

    public async Task ReadStatisticsAsync()
    {
        var statistics = await _metaService.GetPersistentMetaValue<StatisticsState>(Plugin.StatisticsKey);
        StatisticsState = statistics ?? new StatisticsState();
    }

    public async Task OnJoinAsync(EFClient client)
    {
        await ReadClientShopItems(client);
        var userCredits = await LoadUserCredits(client);
        client.Tell(_credifyConfig.Translations.UserCredits.FormatExt($"{userCredits:N0}"));
    }

    public async Task<long> GetClientCredits(EFClient client)
    {
        long userCredits;
        if (client.IsIngame)
        {
            userCredits = client.GetAdditionalProperty<long>(Plugin.Key);
            return userCredits;
        }

        userCredits = await LoadUserCredits(client);
        return userCredits;
    }

    public async Task<long> AlterClientCredits(long amount, int? clientId = null, EFClient? client = null)
    {
        if (clientId is not null)
        {
            await using var context = _contextFactory.CreateContext(false);
            var result = await context.Clients.FirstOrDefaultAsync(x => x.ClientId == clientId);
            client = result.ToPartialClient();
        }

        if (client is null) throw new ArgumentNullException(nameof(client));

        long credits, newCredits;
        if (client.IsIngame)
        {
            credits = client.GetAdditionalProperty<long>(Plugin.Key);
            newCredits = credits + amount;
            client.SetAdditionalProperty(Plugin.Key, newCredits);
            OrderTop(client, newCredits);
            return newCredits;
        }

        credits = await LoadUserCredits(client);
        newCredits = credits + amount;
        await WriteClientCredits(client, newCredits);
        OrderTop(client, newCredits);
        return newCredits;
    }

    private async Task<long> LoadUserCredits(EFClient client)
    {
        // Get pre-initialised credits
        var userCredits = (await _metaService.GetPersistentMeta(Plugin.Key, client.ClientId))?.Value;

        // If null, get total kills, if no kills (ie: new player) set to 0
        if (userCredits is null)
        {
            await using var context = _contextFactory.CreateContext(false);
            var clientTotalKills = await context.Set<EFClientStatistics>()
                .Where(c => c.ClientId == client.ClientId)
                .SumAsync(c => c.Kills);

            // Arbitrary, if they have less than 100 kills don't bother sorting
            if (clientTotalKills > 100)
            {
                OrderTop(client, clientTotalKills);
            }

            userCredits = clientTotalKills.ToString();
            StatisticsState.CreditsEarned += (ulong)clientTotalKills;
        }

        var credits = long.Parse(userCredits);
        client.SetAdditionalProperty(Plugin.Key, credits);
        return credits;
    }

    public void OnKill(EFClient client)
    {
        var userCredits = client.GetAdditionalProperty<long>(Plugin.Key);
        userCredits++;
        client.SetAdditionalProperty(Plugin.Key, userCredits);
        OrderTop(client, userCredits);
        StatisticsState.CreditsEarned++;
    }

    private bool ExistInTop(int targetClientId)
    {
        var topResult = TopCredits.FirstOrDefault(i => i.ClientId == targetClientId);
        return topResult is not null;
    }

    public void OrderTop(EFClient client, long amount)
    {
        lock (TopCredits)
        {
            // If the top hasn't got 5 entries yet add user - check for duplicates.
            if (TopCredits.Count < 5 && !ExistInTop(client.ClientId))
            {
                TopCredits.Add(new TopCreditEntry {ClientId = client.ClientId, Credits = amount});
            }

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
}
