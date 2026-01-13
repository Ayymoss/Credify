using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Outside;

/// <summary>
/// Constructor for a column bet.
/// Constrains the column to be between 1 and 3.
/// </summary>
/// <param name="stake"></param>
/// <param name="column"></param>
public class ColumnBet(int stake, Column column) : OutsideBaseBet(OutsideBet.Column, stake)
{
    public Column Column { get; } = column;
    public override int Payout { get; } = stake * 3;

    public override bool HasWon(SpinResult spinResult)
    {
        return Column switch
        {
            Column.First => spinResult.Number % 3 == 1,
            Column.Second => spinResult.Number % 3 == 2,
            _ => spinResult.Number % 3 == 0
        };
    }
}
