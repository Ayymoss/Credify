using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack.Enums;
using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Configuration;

namespace Credify.Chat.Active.Games.Blackjack.Services;

/// <summary>
/// Service responsible for calculating hand values, determining game outcomes, and calculating payouts.
/// </summary>
public class BlackjackPayoutCalculator(BlackjackConfiguration config)
{
    /// <summary>
    /// Calculates the value of a blackjack hand, handling aces appropriately.
    /// </summary>
    public static int CalculateHandValue(IEnumerable<BlackjackCard> hand)
    {
        var totalValue = 0;
        var aces = 0;

        foreach (var card in hand)
        {
            var cardValue = card.GetValue();
            if (cardValue == 11)
            {
                aces++;
                totalValue += 11;
            }
            else
            {
                totalValue += cardValue;
            }

            // Adjust aces if total exceeds 21
            while (totalValue > GameConstants.Blackjack.BlackjackValue && aces > 0)
            {
                totalValue -= 10;
                aces--;
            }
        }

        return totalValue;
    }

    /// <summary>
    /// Determines if a hand is a blackjack (21 with exactly 2 cards).
    /// </summary>
    public static bool IsBlackjack(IReadOnlyCollection<BlackjackCard> hand)
    {
        return hand.Count == GameConstants.Blackjack.MinimumCardsForBlackjack && 
               CalculateHandValue(hand) == GameConstants.Blackjack.BlackjackValue;
    }

    /// <summary>
    /// Determines if a hand has busted (exceeds 21).
    /// </summary>
    public static bool IsBusted(IEnumerable<BlackjackCard> hand)
    {
        return CalculateHandValue(hand) > GameConstants.Blackjack.BlackjackValue;
    }

    /// <summary>
    /// Determines the game outcome for a player based on their hand and the dealer's hand.
    /// </summary>
    public GameOutcome DetermineOutcome(
        IReadOnlyCollection<BlackjackCard> playerHand,
        IReadOnlyCollection<BlackjackCard> dealerHand)
    {
        var playerValue = CalculateHandValue(playerHand);
        var dealerValue = CalculateHandValue(dealerHand);

        // Player busted
        if (playerValue > GameConstants.Blackjack.BlackjackValue)
        {
            return GameOutcome.Lose;
        }

        // Player has blackjack
        if (IsBlackjack(playerHand))
        {
            if (IsBlackjack(dealerHand))
            {
                return GameOutcome.Push;
            }
            return GameOutcome.Blackjack;
        }

        // Dealer busted
        if (dealerValue > GameConstants.Blackjack.BlackjackValue)
        {
            return GameOutcome.Win;
        }

        // Compare values
        if (playerValue > dealerValue)
        {
            return GameOutcome.Win;
        }

        if (playerValue == dealerValue)
        {
            return GameOutcome.Push;
        }

        return GameOutcome.Lose;
    }

    /// <summary>
    /// Calculates the payout amount for a player based on their stake and outcome.
    /// </summary>
    public long CalculatePayout(long stake, GameOutcome outcome)
    {
        return outcome switch
        {
            GameOutcome.Blackjack => Convert.ToInt64(Math.Round(stake * config.PayoutBlackjack)),
            GameOutcome.Win => Convert.ToInt64(Math.Round(stake * config.PayoutWin)),
            GameOutcome.Push => stake, // Return bet amount
            GameOutcome.Lose => 0,
            _ => 0
        };
    }

    /// <summary>
    /// Calculates the net profit (payout - stake) for a player.
    /// </summary>
    public long CalculateNetProfit(long stake, GameOutcome outcome)
    {
        var payout = CalculatePayout(stake, outcome);
        return payout - stake;
    }
}
