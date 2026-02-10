using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;

/// <summary>
/// Represents a bet on two adjacent numbers.
/// </summary>
/// <param name="stake"></param>
/// <param name="num1"></param>
/// <param name="num2"></param>
public class SplitBet(int stake, int num1, int num2) : InsideBaseBet(InsideBet.Split, stake)
{
    public int[] Numbers { get; } = [num1, num2];
    public override int Payout { get; } = stake * 18;

    public override bool HasWon(SpinResult spinResult) => Numbers.Contains(spinResult.Number);
}
