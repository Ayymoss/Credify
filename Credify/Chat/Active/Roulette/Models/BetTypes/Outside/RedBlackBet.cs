using Credify.Chat.Active.Roulette.Enums;

namespace Credify.Chat.Active.Roulette.Models.BetTypes.Outside;

/// <summary>
/// Represents a bet on a red or black number.
/// </summary>
/// <param name="stake"></param>
/// <param name="colour"></param>
public class RedBlackBet(int stake, Colour colour) : OutsideBaseBet(OutsideBet.RedBlack, stake)
{
    public Colour Colour { get; } = colour;
    public override int Payout { get; } = stake * 2;

    public override bool HasWon(SpinResult spinResult) => spinResult.Colour == Colour;
}
