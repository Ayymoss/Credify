namespace Credify.Chat.Active.Roulette.Models.BetTypes;

public abstract class BaseBet(int stake)
{
    public long Stake { get; } = stake;
    public abstract int Payout { get; }
    public abstract bool HasWon(SpinResult spinResult);
}
