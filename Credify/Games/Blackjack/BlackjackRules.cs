using Credify.Games.Cards;

namespace Credify.Games.Blackjack;

/// <summary>
/// The pure rules of blackjack — hand evaluation and outcome determination — with no I/O, no credits and no
/// frontend concerns. The single source of truth for "how blackjack works"; the chat and web games both
/// consume it. Payouts (which need the configured multipliers) live in <see cref="BlackjackPayouts"/>.
/// </summary>
public static class BlackjackRules
{
    public const int BlackjackValue = 21;
    public const int DealerStandValue = 17;
    public const int CardsInANatural = 2;

    /// <summary>Best hand total, counting each ace as 11 then dropping to 1 while the hand would bust.</summary>
    public static int HandValue(IEnumerable<Card> hand)
    {
        var total = 0;
        var aces = 0;

        foreach (var card in hand)
        {
            total += card.BlackjackValue;
            if (card.Rank is Rank.Ace)
            {
                aces++;
            }

            while (total > BlackjackValue && aces > 0)
            {
                total -= 10;
                aces--;
            }
        }

        return total;
    }

    /// <summary>A "natural": 21 on the opening two cards.</summary>
    public static bool IsBlackjack(IReadOnlyCollection<Card> hand) =>
        hand.Count == CardsInANatural && HandValue(hand) == BlackjackValue;

    public static bool IsBusted(IEnumerable<Card> hand) => HandValue(hand) > BlackjackValue;

    /// <summary>True if an ace is still counted as 11 (the hand is "soft" and can't bust on the next card).</summary>
    public static bool HasSoftAce(IEnumerable<Card> hand)
    {
        var total = 0;
        var aces = 0;
        foreach (var card in hand)
        {
            total += card.BlackjackValue;
            if (card.Rank is Rank.Ace)
            {
                aces++;
            }
        }

        return aces > 0 && total <= BlackjackValue;
    }

    /// <summary>The dealer must keep drawing below 17.</summary>
    public static bool DealerShouldHit(IEnumerable<Card> dealerHand) => HandValue(dealerHand) < DealerStandValue;

    /// <summary>Settles a player hand against the dealer's, from the player's point of view.</summary>
    public static GameOutcome DetermineOutcome(
        IReadOnlyCollection<Card> playerHand,
        IReadOnlyCollection<Card> dealerHand)
    {
        var playerValue = HandValue(playerHand);

        if (playerValue > BlackjackValue)
        {
            return GameOutcome.Lose;
        }

        if (IsBlackjack(playerHand))
        {
            return IsBlackjack(dealerHand) ? GameOutcome.Push : GameOutcome.Blackjack;
        }

        var dealerValue = HandValue(dealerHand);
        if (dealerValue > BlackjackValue || playerValue > dealerValue)
        {
            return GameOutcome.Win;
        }

        return playerValue == dealerValue ? GameOutcome.Push : GameOutcome.Lose;
    }
}
