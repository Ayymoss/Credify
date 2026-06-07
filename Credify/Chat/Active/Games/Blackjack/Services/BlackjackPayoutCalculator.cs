using Credify.Configuration;
using Credify.Games.Blackjack;
using Credify.Games.Cards;

namespace Credify.Chat.Active.Games.Blackjack.Services;

/// <summary>
/// Thin chat-side adapter over the shared <see cref="BlackjackRules"/> / <see cref="BlackjackPayouts"/> core.
/// Kept so the chat game's existing call sites stay unchanged while the actual rules live in one shared place
/// (used by the webfront too). No game logic lives here anymore — it only forwards.
/// </summary>
public class BlackjackPayoutCalculator(BlackjackConfiguration config)
{
    public static int CalculateHandValue(IEnumerable<Card> hand) => BlackjackRules.HandValue(hand);

    public static bool IsBlackjack(IReadOnlyCollection<Card> hand) => BlackjackRules.IsBlackjack(hand);

    public static bool IsBusted(IEnumerable<Card> hand) => BlackjackRules.IsBusted(hand);

    public GameOutcome DetermineOutcome(
        IReadOnlyCollection<Card> playerHand,
        IReadOnlyCollection<Card> dealerHand) => BlackjackRules.DetermineOutcome(playerHand, dealerHand);

    public long CalculatePayout(long stake, GameOutcome outcome) => BlackjackPayouts.Payout(stake, outcome, config);

    public long CalculateNetProfit(long stake, GameOutcome outcome) => BlackjackPayouts.NetProfit(stake, outcome, config);
}
