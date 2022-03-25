using System.Text.Json;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Data.Models.Server;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin;

public class PrimaryLogic
{
    public PrimaryLogic(IMetaServiceV2 metaService, IDatabaseContextFactory contextFactory)
    {
        _metaService = metaService;
        _contextFactory = contextFactory;
    }

    public static List<TopCreditEntry> TopCredits;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IMetaServiceV2 _metaService;
    
    // TODO: Handle IW4MAdmin Unloading - Write back all clients' credits

    /// <summary>
    /// Return true/false based on available funds
    /// </summary>
    /// <param name="client">EFClient</param>
    /// <param name="amount">Amount of credits to check EFClient has</param>
    /// <returns>Boolean. True if has funds. False if doesn't.</returns>
    public bool AvailableFunds(EFClient client, int amount) =>
        amount <= client?.GetAdditionalProperty<int>(Plugin.CreditsKey);

    /// <summary>
    /// Load player's credits from database, else create new cached credit
    /// </summary>
    /// <param name="client"></param>
    public async void InitialisePlayer(EFClient client)
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
        }

        client.SetAdditionalProperty(Plugin.CreditsKey, int.Parse(userCredits));
        client.Tell($"You have (Color::Cyan){int.Parse(userCredits):N0} (Color::White)credits");
    }

    /// <summary>
    /// Increment cached credits
    /// </summary>
    /// <param name="client">GameEvent</param>
    public void IncrementCredits(EFClient client)
    {
        var userCredits = client.GetAdditionalProperty<int>(Plugin.CreditsKey);
        userCredits++;
        client.SetAdditionalProperty(Plugin.CreditsKey, userCredits);
        OrderTop(client, userCredits);
    }

    /// <summary>
    /// Write back player credits to database
    /// </summary>
    /// <param name="client">EFClient</param>
    public async void WriteCredits(EFClient client) => await _metaService.SetPersistentMeta(Plugin.CreditsKey, client.GetAdditionalProperty<int>(Plugin.CreditsKey).ToString(), client.ClientId);

    /// <summary>
    /// Write Top Score back to database
    /// </summary>
    public async void WriteTopScore()
    {
        await _metaService!.RemovePersistentMeta(Plugin.CreditsTopKey);
        await _metaService.SetPersistentMetaValue(Plugin.CreditsTopKey, TopCredits);
    }

    /// <summary>
    /// Read Top Score from Database
    /// </summary>
    public async void ReadTopScore()
    {
        var topCreditsValue = (await _metaService.GetPersistentMeta(Plugin.CreditsTopKey))?.Value;

        TopCredits = topCreditsValue is null
            ? new List<TopCreditEntry>()
            : JsonSerializer.Deserialize<List<TopCreditEntry>>(topCreditsValue)!;
    }
    
    /// <summary>
    /// Return true if duplicate exists
    /// </summary>
    /// <param name="targetClientId">Client ID from EFClient</param>
    /// <returns>Boolean. True if exist. False if doesn't</returns>
    private bool ExistInTop(int targetClientId)
    {
        var topResult = TopCredits?.FirstOrDefault(i => i.ClientId == targetClientId);
        return topResult != null;
    }

    /// <summary>
    /// Order (Used on kill update/gamble)
    /// </summary>
    /// <param name="client">EFClient</param>
    /// <param name="amount">Amount of Credits from Additional Properties</param>
    public void OrderTop(EFClient client, int amount)
    {
        lock (TopCredits!)
        {
            // If the top hasn't got 5 entries yet add user - check for duplicates.
            if (TopCredits.Count < 5 && !ExistInTop(client.ClientId))
            {
                TopCredits.Add(new TopCreditEntry {ClientId = client.ClientId, Credits = amount});
            }

            //If the target's credits are greater than last item OR already exists in top, sort & update top.
            if (amount > TopCredits.Last().Credits || ExistInTop(client.ClientId))
            {
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

                TopCredits = TopCredits.OrderByDescending(credit => credit.Credits).Take(5).ToList();
            }
        }
    }
}

public class TopCreditEntry
{
    public int ClientId { get; init; }
    public int Credits { get; set; }
}


