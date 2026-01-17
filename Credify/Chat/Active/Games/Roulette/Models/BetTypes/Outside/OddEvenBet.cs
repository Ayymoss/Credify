using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Outside;

/// <summary>
/// Represents a bet on an odd or even number.
/// 0 and 00 are neither odd nor even (house wins).
/// </summary>
/// <param name="stake"></param>
/// <param name="isEven"></param>
public class OddEvenBet(int stake, bool isEven) : OutsideBaseBet(OutsideBet.OddEven, stake)
{
    public bool IsEven { get; } = isEven;
    public override int Payout { get; } = stake * 2;

    public override bool HasWon(SpinResult spinResult)
    {
        // 0 and 00 are neither odd nor even - house wins
        if (RouletteConstants.IsZero(spinResult.Number)) return false;
        return spinResult.IsEven == IsEven;
    }
}
