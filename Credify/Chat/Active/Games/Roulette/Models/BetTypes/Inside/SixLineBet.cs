using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;

/// <summary>
/// Represents a bet on a six line.
/// </summary>
/// <param name="stake"></param>
/// <param name="num1"></param>
/// <param name="num2"></param>
/// <param name="num3"></param>
/// <param name="num4"></param>
/// <param name="num5"></param>
/// <param name="num6"></param>
public class SixLineBet(int stake, int num1, int num2, int num3, int num4, int num5, int num6) : InsideBaseBet(InsideBet.SixLine, stake)
{
    public int[] Numbers { get; } = [num1, num2, num3, num4, num5, num6];
    public override int Payout { get; } = stake * 6;

    public override bool HasWon(SpinResult spinResult) => Numbers.Contains(spinResult.Number);
}
