using Microsoft.VisualBasic;
using SharedLibraryCore;


namespace CreditsPlugin;

public class TopCreditsLogic
{
    public static List<TopCreditEntry> TopCredits;

    // Return true if duplicate exists
    private static bool ExistInTop(GameEvent e) => TopCredits.Any(i => i.ClientId == e.Origin.ClientId);

    // Order based on ORIGIN (Used on kill update/gamble)
    public static void OriginOrderTop(GameEvent e, int credits)
    {
        lock (TopCredits)
        {
            // If the top hasn't got 5 entries yet add user - check for duplicates.
            if (TopCredits.Count < 5 && !ExistInTop(e))
            {
                TopCredits.Add(new TopCreditEntry {ClientId = e.Origin.ClientId, Credits = credits});
            }

            // If the target's credits are greater than last item, sort & update top.
            if (credits > TopCredits.Last().Credits)
            {
                var existingCredEntry = TopCredits.FirstOrDefault(credit => credit.ClientId == e.Origin.ClientId);

                if (existingCredEntry is null)
                {
                    TopCredits.Add(new TopCreditEntry
                    {
                        ClientId = e.Origin.ClientId,
                        Credits = credits
                    });
                }
                else
                {
                    existingCredEntry.Credits = credits;
                }

                TopCredits = TopCredits.OrderByDescending(credit => credit.Credits).Take(5).ToList();
            }
        }
    }
    
    // Order based on TARGET (Used in Set Credits Command)
    public static void TargetOrderTop(GameEvent e, int credits)
    {
        lock (TopCredits)
        {
            // If the top hasn't got 5 entries yet add user - check for duplicates.
            if (TopCredits.Count < 5 && !ExistInTop(e))
            {
                TopCredits.Add(new TopCreditEntry {ClientId = e.Target.ClientId, Credits = credits});
            }

            // If the target's credits are greater than last, sort & update top.
            if (credits > TopCredits.Last().Credits)
            {
                var existingCredEntry = TopCredits.FirstOrDefault(credit => credit.ClientId == e.Target.ClientId);

                if (existingCredEntry is null)
                {
                    TopCredits.Add(new TopCreditEntry
                    {
                        ClientId = e.Target.ClientId,
                        Credits = credits
                    });
                }
                else
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