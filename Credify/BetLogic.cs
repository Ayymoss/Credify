using System.Text.Json;
using Credify.Models;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify;

public class BetLogic
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly IMetaServiceV2 _metaService;
    public List<TopCreditEntry> TopCredits { get; private set; } = new();
    public StatisticsState StatisticsState { get; private set; } = new();

    public BetLogic(IMetaServiceV2 metaService, IDatabaseContextFactory contextFactory, CredifyConfiguration credifyConfig)
    {
        _metaService = metaService;
        _contextFactory = contextFactory;
        _credifyConfig = credifyConfig;
    }

    public bool AvailableFunds(EFClient client, int amount) =>
        amount <= client.GetAdditionalProperty<int>(Plugin.CreditsKey);

    public async Task OnDisconnectAsync(EFClient client) =>
        await _metaService.SetPersistentMeta(Plugin.CreditsKey,
            client.GetAdditionalProperty<int>(Plugin.CreditsKey).ToString(), client.ClientId);

    public async Task WriteTopScoreAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.CreditsTopKey, TopCredits);

    public async Task WriteStatisticsAsync() =>
        await _metaService.SetPersistentMetaValue(Plugin.CreditsStatisticsKey, StatisticsState);

    public async Task ReadTopScoreAsync()
    {
        var topCreditsValue = (await _metaService.GetPersistentMeta(Plugin.CreditsTopKey))?.Value;

        TopCredits = topCreditsValue is null
            ? new List<TopCreditEntry>()
            : JsonSerializer.Deserialize<List<TopCreditEntry>>(topCreditsValue)!;
    }

    public async Task ReadStatisticsAsync()
    {
        var statistics = (await _metaService.GetPersistentMeta(Plugin.CreditsStatisticsKey))?.Value;
        if (statistics is null) return;

        var json = JsonSerializer.Deserialize<StatisticsState>(statistics);
        if (json is null) Console.WriteLine("Failed to deserialize statistics");
        else StatisticsState = json;
    }

    public async Task OnJoinAsync(EFClient client)
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

            // Arbitrary, if it's less than 100 and they exist don't bother sorting
            if (clientTotalKills > 100)
            {
                OrderTop(client, clientTotalKills);
            }

            userCredits = clientTotalKills.ToString();

            StatisticsState.CreditsEarned += clientTotalKills;
        }

        client.SetAdditionalProperty(Plugin.CreditsKey, int.Parse(userCredits));
        client.Tell(_credifyConfig.Translations.UserCredits.FormatExt($"{int.Parse(userCredits):N0}"));
    }

    public void OnKill(EFClient client)
    {
        var userCredits = client.GetAdditionalProperty<int>(Plugin.CreditsKey);
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

    public void OrderTop(EFClient client, int amount)
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
