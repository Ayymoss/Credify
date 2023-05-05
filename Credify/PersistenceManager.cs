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
    public void AddBankCredits(long credits) => BankCredits += credits;

    public bool AvailableFunds(EFClient client, long amount) =>
        amount <= client.GetAdditionalProperty<long>(Plugin.CreditsKey);

    public async Task WriteClientCredits(EFClient client, long? amount = null)
    {
        await _metaService.SetPersistentMeta(Plugin.CreditsKey, amount is not null
            ? amount.ToString()
            : client.GetAdditionalProperty<long>(Plugin.CreditsKey).ToString(), client.ClientId);
    }

    public async Task WriteTopScoreAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.CreditsTopKey, TopCredits);

    public async Task WriteStatisticsAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.CreditsStatisticsKey, StatisticsState);

    public async Task WriteLotteryAsync(List<Lottery> lotteries) =>
        await _metaService.SetPersistentMetaValue(Plugin.CreditsLotteryKey, lotteries);

    public async Task WriteNextLotteryAsync(DateTimeOffset dateTime) =>
        await _metaService.SetPersistentMeta(Plugin.CreditsNextLotteryKey,
            dateTime.ToString("o", CultureInfo.InvariantCulture));

    public async Task WriteClientShopAsync(EFClient client, List<ClientShopItem> shopItems) =>
        await _metaService.SetPersistentMetaValue(Plugin.CreditsShopKey, shopItems, client.ClientId);


    public async Task ReadTopScoreAsync()
    {
        var topCreditsValue = await _metaService.GetPersistentMetaValue<List<TopCreditEntry>>(Plugin.CreditsTopKey);
        TopCredits = topCreditsValue ?? new List<TopCreditEntry>();
    }

    private async Task<List<ClientShopItem>> ReadClientShopItems(EFClient client)
    {
        var shopItems = await _metaService
            .GetPersistentMetaValue<List<ClientShopItem>>(Plugin.CreditsShopKey, client.ClientId);

        shopItems ??= new List<ClientShopItem>();
        client.SetAdditionalProperty(Plugin.CreditsShopKey, shopItems);

        return shopItems;
    }

    public async Task<List<ClientShopItem>> GetClientShopItems(EFClient client)
    {
        List<ClientShopItem> userCredits;
        if (client.IsIngame)
        {
            userCredits = client.GetAdditionalProperty<List<ClientShopItem>>(Plugin.CreditsShopKey);
            return userCredits;
        }

        userCredits = await ReadClientShopItems(client);
        return userCredits;
    }

    public async Task ReadBankCreditsAsync()
    {
        var bankCredits = (await _metaService.GetPersistentMeta(Plugin.CreditsTopKey))?.Value;

        BankCredits = bankCredits is null
            ? 0
            : long.Parse(bankCredits);
    }

    public async Task<DateTime?> ReadNextLotteryAsync()
    {
        var nextLotto = (await _metaService.GetPersistentMeta(Plugin.CreditsNextLotteryKey))?.Value;
        if (nextLotto is null) return null;
        return DateTime.Parse(nextLotto);
    }

    public async Task<List<Lottery>> ReadLotteryAsync()
    {
        var lotteries = await _metaService.GetPersistentMetaValue<List<Lottery>>(Plugin.CreditsLotteryKey);
        return lotteries ?? new List<Lottery>();
    }

    public async Task ReadStatisticsAsync()
    {
        var statistics = await _metaService.GetPersistentMetaValue<StatisticsState>(Plugin.CreditsStatisticsKey);
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
            userCredits = client.GetAdditionalProperty<long>(Plugin.CreditsKey);
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
            credits = client.GetAdditionalProperty<long>(Plugin.CreditsKey);
            newCredits = credits + amount;
            client.SetAdditionalProperty(Plugin.CreditsKey, newCredits);
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
        var userCredits = (await _metaService.GetPersistentMeta(Plugin.CreditsKey, client.ClientId))?.Value;

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
            StatisticsState.CreditsEarned += clientTotalKills;
        }

        var credits = long.Parse(userCredits);
        client.SetAdditionalProperty(Plugin.CreditsKey, credits);
        return credits;
    }

    public void OnKill(EFClient client)
    {
        var userCredits = client.GetAdditionalProperty<long>(Plugin.CreditsKey);
        userCredits++;
        client.SetAdditionalProperty(Plugin.CreditsKey, userCredits);
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
