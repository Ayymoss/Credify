using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;

/// <summary>
/// Represents a bet on a single number.
/// </summary>
/// <param name="stake"></param>
/// <param name="number"></param>
public class StraightUpBet(int stake, int number) : InsideBaseBet(InsideBet.StraightUp, stake)
{
    public int Number { get; } = number;
    public override int Payout { get; } = stake * 36;

    public override bool HasWon(SpinResult spinResult) => spinResult.Number == Number;
}
