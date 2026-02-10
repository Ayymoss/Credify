using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;

/// <summary>
/// Represents a corner bet in roulette.
/// </summary>
/// <param name="stake"></param>
/// <param name="num1"></param>
/// <param name="num2"></param>
/// <param name="num3"></param>
/// <param name="num4"></param>
public class CornerBet(int stake, int num1, int num2, int num3, int num4) : InsideBaseBet(InsideBet.Corner, stake)
{
    public int[] Numbers { get; } = [num1, num2, num3, num4];
    public override int Payout { get; } = stake * 9;

    public override bool HasWon(SpinResult spinResult) => Numbers.Contains(spinResult.Number);
}
