using SharedLibraryCore;

namespace CreditsPlugin;

public class CreditLogic
{
    public static bool AvailableFunds(GameEvent e, int amount) =>
        amount > e.Origin.GetAdditionalProperty<int>("Credits");

    public static List<TopCreditEntry> TopCredits;
    

    // Return true if duplicate exists
    private static bool ExistInTop(int targetClientId)
    {
        var topResult = TopCredits.FirstOrDefault(i => i.ClientId == targetClientId);
        return topResult != null;
    }

    // Order (Used on kill update/gamble)
    public static void OrderTop(GameEvent e, int credits, int target)
    {
        var targetClientId = 0;
        if ((CreditTarget) target == CreditTarget.Origin) targetClientId = e.Origin.ClientId;
        if ((CreditTarget) target == CreditTarget.Target) targetClientId = e.Target.ClientId;
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