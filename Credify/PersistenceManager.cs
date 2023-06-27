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

    public async Task AddBankCreditsAsync(long credits)
    {
        BankCredits += credits;
        await WriteBankCreditsAsync();
    }

    public bool AvailableFunds(EFClient client, long amount) =>
        amount <= client.GetAdditionalProperty<long>(Plugin.CreditsAmount);

    public async Task WriteClientCreditsAsync(EFClient client, long? amount = null)
    {
        await _metaService.SetPersistentMeta(Plugin.CreditsAmount, amount is not null
            ? amount.ToString()
            : client.GetAdditionalProperty<long>(Plugin.CreditsAmount).ToString(), client.ClientId);
    }

    public async Task WriteTopScoreAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.TopKey, TopCredits);

    public async Task WriteStatisticsAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.StatisticsKey, StatisticsState);

    public async Task WriteLastLotteryWinnerAsync(int clientId, string client, long amount) =>
        await _metaService.SetPersistentMeta(Plugin.LastLottoWinner, $"{clientId}::{client}::{amount}");

    public async Task<(int ClientId, string ClientName, long PayOut)?> ReadLastLotteryWinnerAsync()
    {
        var winnerMeta = await _metaService.GetPersistentMeta(Plugin.LastLottoWinner);
        if (winnerMeta is null) return null;
        var data = winnerMeta.Value.Split("::", 3);
        return (int.Parse(data[0]), data[1], long.Parse(data[2]));
    }

    public async Task WriteRecentBoughtItemsAsync(ClientShopContext item)
    {
        var items = await ReadRecentBoughtItemsAsync();
        var ordered = items.OrderByDescending(x => x.Bought)
            .Take(9)
            .ToList();

        ordered.Add(item);

        await _metaService.SetPersistentMetaValue(Plugin.RecentBoughtItems, ordered);
    }

    public async Task<IEnumerable<ClientShopContext>> ReadRecentBoughtItemsAsync()
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

    public async Task WriteBankCreditsAsync() =>
        await _metaService.SetPersistentMeta(Plugin.BankCreditsKey, BankCredits.ToString());

    public async Task ReadTopScoreAsync()
    {
        var topCreditsValue = await _metaService.GetPersistentMetaValue<List<TopCreditEntry>>(Plugin.TopKey);
        TopCredits = topCreditsValue ?? new List<TopCreditEntry>();
    }

    private async Task<List<ClientShopItem>> ReadClientShopItemsAsync(EFClient client)
    {
        var shopItems = await _metaService
            .GetPersistentMetaValue<List<ClientShopItem>>(Plugin.ShopKey, client.ClientId);

        shopItems ??= new List<ClientShopItem>();
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
        await ReadClientShopItemsAsync(client);
        var userCredits = await LoadUserCreditsAsync(client);
        client.Tell(_credifyConfig.Translations.UserCredits.FormatExt($"{userCredits:N0}"));
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

    public async Task<long> AlterClientCreditsAsync(long amount, int? clientId = null, EFClient? client = null)
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
        var userCredits = (await _metaService.GetPersistentMeta(Plugin.CreditsAmount, client.ClientId))?.Value;
        var credits = long.Parse(userCredits ?? "0");
        client.SetAdditionalProperty(Plugin.CreditsAmount, credits);
        return credits;
    }

    public void OnKill(EFClient client)
    {
        var userCredits = client.GetAdditionalProperty<long>(Plugin.CreditsAmount);
        userCredits++;
        client.SetAdditionalProperty(Plugin.CreditsAmount, userCredits);
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
        if (client.ClientId is 0 or 1) return;
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

    public void ResetTop() => TopCredits = new List<TopCreditEntry>();

    public void ResetStatistics() => StatisticsState = new StatisticsState();
}
