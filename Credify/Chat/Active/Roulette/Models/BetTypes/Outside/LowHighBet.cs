using Credify.Chat.Active.Roulette.Enums;

namespace Credify.Chat.Active.Roulette.Models.BetTypes.Outside;

/// <summary>
/// Represents a bet on low or high numbers.
/// </summary>
/// <param name="stake"></param>
/// <param name="range"></param>
public class LowHighBet(int stake, LowHigh range) : OutsideBaseBet(OutsideBet.LowHigh, stake)
{
    public LowHigh Range { get; } = range;
    public override int Payout { get; } = stake * 2;

    public override bool HasWon(SpinResult spinResult)
    {
        if (Range is LowHigh.Low)
        {
            return spinResult.Number is >= 1 and <= 18;
        }

        return spinResult.Number is >= 19 and <= 36;
    }
}
