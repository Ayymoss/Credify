using System.Text.Json;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin;

public class PrimaryLogic
{
    public PrimaryLogic(IMetaService metaService)
    {
        _metaService = metaService;
    }

    public static List<TopCreditEntry> TopCredits;
    private static IMetaService _metaService;

    /// <summary>
    /// Return true/false based on available funds
    /// </summary>
    /// <param name="player"></param>
    /// <param name="amount"></param>
    /// <returns>Boolean. True if has funds. False if doesn't.</returns>
    public static bool AvailableFunds(GameEvent player, int amount) =>
        amount > player.Origin.GetAdditionalProperty<int>("Credits");

    /// <summary>
    /// Load player's credits from database, else create new cached credit
    /// </summary>
    /// <param name="player"></param>
    public static async void InitialisePlayer(GameEvent player)
    {
        var userCredits = (await _metaService.GetPersistentMeta("Credits", player.Origin))?.Value ?? "0";
        player.Origin.SetAdditionalProperty("Credits", int.Parse(userCredits));
        player.Origin.Tell($"You have (Color::Cyan){userCredits} (Color::White)credits.");
    }

    /// <summary>
    /// Increment cached credits
    /// </summary>
    /// <param name="player"></param>
    public static void IncrementCredits(GameEvent player)
    {
        var userCredits = player.Origin.GetAdditionalProperty<int>("Credits");
        userCredits++;
        player.Origin.SetAdditionalProperty("Credits", userCredits);
        OrderTop(player, userCredits, 0);
    }

    /// <summary>
    /// Write back player credits to database
    /// </summary>
    /// <param name="player"></param>
    public static async void WriteCredits(GameEvent player)
    {
        await _metaService.SetPersistentMeta("Credits", player.Origin.GetAdditionalProperty<int>("Credits").ToString(),
            player.Origin.ClientId);
    }

    /// <summary>
    /// Write Top Score back to database
    /// </summary>
    public async void WriteTopScore()
    {
        await _metaService.RemovePersistentMeta("TopCredits");
        await _metaService.AddPersistentMeta("TopCredits", JsonSerializer.Serialize(TopCredits));
    }

    /// <summary>
    /// Read Top Score from Database
    /// </summary>
    public async void ReadTopScore()
    {
        var topCreditsValue = (await _metaService.GetPersistentMeta("TopCredits")).FirstOrDefault()?.Value;
        
        TopCredits = topCreditsValue is null
            ? new List<TopCreditEntry>()
            : JsonSerializer.Deserialize<List<TopCreditEntry>>(topCreditsValue)!;
    }

    /// <summary>
    /// Return true if duplicate exists
    /// </summary>
    /// <param name="targetClientId"></param>
    /// <returns>Boolean. True if exist. False if doesn't</returns>
    private static bool ExistInTop(int targetClientId)
    {
        var topResult = TopCredits.FirstOrDefault(i => i.ClientId == targetClientId);
        return topResult != null;
    }

    /// <summary>
    /// Order (Used on kill update/gamble)
    /// </summary>
    /// <param name="player"></param>
    /// <param name="credits"></param>
    /// <param name="target"></param>
    public static void OrderTop(GameEvent player, int credits, int target)
    {
        var targetClientId = 0;
        if ((CreditTarget) target == CreditTarget.Origin) targetClientId = player.Origin.ClientId;
        if ((CreditTarget) target == CreditTarget.Target) targetClientId = player.Target.ClientId;
        if (targetClientId == 0) return;

        lock (TopCredits)
        {
            // If the top hasn't got 5 entries yet add user - check for duplicates.
            if (TopCredits.Count < 5 && !ExistInTop(targetClientId))
            {
                TopCredits.Add(new TopCreditEntry {ClientId = targetClientId, Credits = credits});
            }

            //If the target's credits are greater than last item OR already exists in top, sort & update top.
            if (credits > TopCredits.Last().Credits || ExistInTop(targetClientId))
            {
                var existingCredEntry = TopCredits.FirstOrDefault(credit => credit.ClientId == targetClientId);
                // Doesn't exist in top - Create new entry and sort
                if (existingCredEntry is null)
                {
                    TopCredits.Add(new TopCreditEntry
                    {
                        ClientId = targetClientId,
                        Credits = credits
                    });
                }
                else // Exists already in top, just set credits and update
                {
                    existingCredEntry.Credits = credits;
                }

                TopCredits = TopCredits.OrderByDescending(credit => credit.Credits).Take(5).ToList();
            }
        }
    }
}

public class TopCreditEntry
{
    public int ClientId { get; set; }
    public int Credits { get; set; }
}

public enum CreditTarget
{
    Origin,
    Target
}
