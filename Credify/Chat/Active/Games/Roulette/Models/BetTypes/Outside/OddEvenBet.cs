using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Outside;

/// <summary>
/// Represents a bet on an odd or even number.
/// </summary>
/// <param name="stake"></param>
/// <param name="isEven"></param>
public class OddEvenBet(int stake, bool isEven) : OutsideBaseBet(OutsideBet.OddEven, stake)
{
    public bool IsEven { get; } = isEven;
    public override int Payout { get; } = stake * 2;

    public override bool HasWon(SpinResult spinResult) => spinResult.IsEven == IsEven;
}
