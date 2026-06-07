namespace Credify.Games.ThreeCardPoker;

/// <summary>
/// Three-Card Poker payouts and settlement (pure). Ante bonus pays on strong player hands regardless of the
/// dealer; Pair Plus is an independent side bet on the player's hand (resolved on the deal, so it pays even
/// if the player folds the ante/play). The dealer qualifies with Queen-high or better.
/// </summary>
public static class ThreeCardRules
{
    /// <summary>Ante bonus (paid on the ante regardless of outcome) as a multiple of the ante.</summary>
    public static int AnteBonusMultiplier(ThreeCardCategory category) => category switch
    {
        ThreeCardCategory.StraightFlush => 5,
        ThreeCardCategory.ThreeOfAKind => 4,
        ThreeCardCategory.Straight => 1,
        _ => 0
    };

    /// <summary>Pair Plus payout as a multiple of the side bet (0 = the side bet loses).</summary>
    public static int PairPlusMultiplier(ThreeCardCategory category) => category switch
    {
        ThreeCardCategory.StraightFlush => 40,
        ThreeCardCategory.ThreeOfAKind => 30,
        ThreeCardCategory.Straight => 6,
        ThreeCardCategory.Flush => 3,
        ThreeCardCategory.Pair => 1,
        _ => 0
    };

    public static bool DealerQualifies(ThreeCardHand dealer) =>
        dealer.Category > ThreeCardCategory.HighCard || dealer.Tiebreak[0] >= 12; // Queen-high or better

    /// <summary>
    /// Settle a hand. Stakes are assumed already debited (ante + pairPlus at the deal, and play = ante on
    /// Play). Returns the breakdown and the total credit to pay back.
    /// </summary>
    public static ThreeCardOutcome Settle(ThreeCardHand player, ThreeCardHand dealer, long ante, long pairPlus, bool played)
    {
        // Pair Plus resolves on the player's hand alone, regardless of fold.
        var pairPlusMult = PairPlusMultiplier(player.Category);
        var pairPlusReturn = pairPlus > 0 && pairPlusMult > 0 ? pairPlus * (1 + pairPlusMult) : 0;

        if (!played)
        {
            var foldStaked = ante + pairPlus;
            return new ThreeCardOutcome(false, "Fold", 0, 0, pairPlusReturn, pairPlusReturn, foldStaked, pairPlusReturn - foldStaked);
        }

        var play = ante;
        var staked = ante + pairPlus + play;
        var anteBonus = ante * AnteBonusMultiplier(player.Category);
        var qualifies = DealerQualifies(dealer);

        long antePlayReturn;
        string result;
        if (!qualifies)
        {
            antePlayReturn = ante * 2 + play; // ante pays 1:1, play pushes
            result = "Win";
        }
        else
        {
            var compare = player.CompareTo(dealer);
            if (compare > 0) { antePlayReturn = ante * 2 + play * 2; result = "Win"; }
            else if (compare == 0) { antePlayReturn = ante + play; result = "Push"; }
            else { antePlayReturn = 0; result = "Lose"; }
        }

        var total = antePlayReturn + anteBonus + pairPlusReturn;
        return new ThreeCardOutcome(qualifies, result, antePlayReturn, anteBonus, pairPlusReturn, total, staked, total - staked);
    }
}
