using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;

namespace Credify.Games.CasinoHoldem;

/// <summary>
/// Casino Hold'em payouts and settlement (pure). Reuses the existing poker hand ranking. The Ante pays per a
/// paytable on the player's final hand; the Call bet (2× ante) pays even money on a win and pushes when the
/// dealer fails to qualify (a pair of 4s or better).
/// </summary>
public static class CasinoHoldemRules
{
    /// <summary>Ante paytable as a multiple of the ante (the ante's winnings on top of the bet).</summary>
    public static int AnteMultiplier(HandRank rank) => rank switch
    {
        HandRank.RoyalFlush => 100,
        HandRank.StraightFlush => 20,
        HandRank.FourOfAKind => 10,
        HandRank.FullHouse => 3,
        HandRank.Flush => 2,
        _ => 1 // straight or lower
    };

    public static bool DealerQualifies(PokerHand dealer) =>
        dealer.Rank > HandRank.Pair || (dealer.Rank == HandRank.Pair && dealer.Kickers.Count > 0 && dealer.Kickers[0] >= 4);

    /// <summary>
    /// Settle a hand. Stakes are assumed already debited (ante at the deal, and call = 2× ante on Call).
    /// Returns the breakdown and the total credit to pay back.
    /// </summary>
    public static CasinoHoldemOutcome Settle(PokerHand player, PokerHand dealer, long ante, bool called)
    {
        if (!called)
        {
            return new CasinoHoldemOutcome(false, "Fold", 0, 0, 0, ante, -ante);
        }

        var call = ante * 2;
        var staked = ante + call;
        var anteMultiplier = AnteMultiplier(player.Rank);
        var qualifies = DealerQualifies(dealer);

        long anteReturn;
        long callReturn;
        string result;
        if (!qualifies)
        {
            anteReturn = ante * (1 + anteMultiplier); // ante pays per the table
            callReturn = call;                        // call pushes
            result = "Win";
        }
        else
        {
            var compare = player.CompareTo(dealer);
            if (compare > 0) { anteReturn = ante * (1 + anteMultiplier); callReturn = call * 2; result = "Win"; }
            else if (compare == 0) { anteReturn = ante; callReturn = call; result = "Push"; }
            else { anteReturn = 0; callReturn = 0; result = "Lose"; }
        }

        var total = anteReturn + callReturn;
        return new CasinoHoldemOutcome(qualifies, result, anteReturn, callReturn, total, staked, total - staked);
    }
}
