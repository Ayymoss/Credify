using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;

/// <summary>
/// Represents a street bet in roulette.
/// </summary>
/// <param name="stake"></param>
/// <param name="num1"></param>
/// <param name="num2"></param>
/// <param name="num3"></param>
public class StreetBet(int stake, int num1, int num2, int num3) : InsideBaseBet(InsideBet.Street, stake)
{
    public int[] Numbers { get; } = [num1, num2, num3];
    public override int Payout { get; } = stake * 12;

    public override bool HasWon(SpinResult spinResult) => Numbers.Contains(spinResult.Number);
}
