using SharedLibraryCore;

namespace CreditsPlugin;

public class TopCreditsLogic
{
    public static List<TopCreditEntry> TopCredits;

    public static void OriginOrderTop(GameEvent e, int credits)
    {
        lock (TopCredits)
        {
            // If nothing is in the DB, add the first kill.
            if (!TopCredits.Any())
            {
                TopCredits.Add(new TopCreditEntry
                {
                    ClientId = e.Origin.ClientId,
                    Credits = credits
                });
                return;
            }

            // If something exists, check last.
            if (credits > TopCredits.Last().Credits)
            {
                var existingCredEntry =
                    TopCredits.FirstOrDefault(credit => credit.ClientId == e.Origin.ClientId);

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

    public static void TargetOrderTop(GameEvent e, int credits)
    {
        lock (TopCredits)
        {
            // If nothing is in the DB, add the first kill.
            if (!TopCredits.Any())
            {
                TopCredits.Add(new TopCreditEntry
                {
                    ClientId = e.Target.ClientId,
                    Credits = credits
                });
                return;
            }

            // If something exists, check last.
            if (credits > TopCredits.Last().Credits)
            {
                var existingCredEntry =
                    TopCredits.FirstOrDefault(credit => credit.ClientId == e.Target.ClientId);

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