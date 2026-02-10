using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Outside;

/// <summary>
/// Represents a bet on a dozen (1-12, 13-24, 25-36)
/// </summary>
/// <param name="stake"></param>
/// <param name="dozen"></param>
public class DozenBet(int stake, Dozen dozen) : OutsideBaseBet(OutsideBet.Dozen, stake)
{
    public Dozen Dozen { get; } = dozen;
    public override int Payout { get; } = stake * 3;

    public override bool HasWon(SpinResult spinResult)
    {
        return Dozen switch
        {
            Dozen.First => spinResult.Number is >= 1 and <= 12,
            Dozen.Second => spinResult.Number is >= 13 and <= 24,
            _ => spinResult.Number is >= 25 and <= 36
        };
    }
}
