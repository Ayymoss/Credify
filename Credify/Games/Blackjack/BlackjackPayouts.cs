using Credify.Configuration;

namespace Credify.Games.Blackjack;

/// <summary>
/// Translates a settled <see cref="GameOutcome"/> into a credit payout using the configured multipliers.
/// Shared by chat and web so the economy is identical across both. Returns the TOTAL credits to hand back
/// to the player (stake is assumed already debited); a loss returns 0, a push returns the stake.
/// </summary>
public static class BlackjackPayouts
{
    public static long Payout(long stake, GameOutcome outcome, BlackjackConfiguration config) => outcome switch
    {
        GameOutcome.Blackjack => (long)Math.Round(stake * config.PayoutBlackjack),
        GameOutcome.Win => (long)Math.Round(stake * config.PayoutWin),
        GameOutcome.Push => stake,
        _ => 0
    };

    public static long NetProfit(long stake, GameOutcome outcome, BlackjackConfiguration config) =>
        Payout(stake, outcome, config) - stake;
}
