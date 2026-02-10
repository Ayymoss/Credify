using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Outside;

/// <summary>
/// Constructor for a column bet.
/// Columns are 1-34, 2-35, 3-36. 0 and 00 are not part of any column (house wins).
/// </summary>
/// <param name="stake"></param>
/// <param name="column"></param>
public class ColumnBet(int stake, Column column) : OutsideBaseBet(OutsideBet.Column, stake)
{
    public Column Column { get; } = column;
    public override int Payout { get; } = stake * 3;

    public override bool HasWon(SpinResult spinResult)
    {
        // 0 and 00 are not part of any column - house wins
        if (RouletteConstants.IsZero(spinResult.Number)) return false;
        
        return Column switch
        {
            Column.First => spinResult.Number % 3 == 1,  // 1, 4, 7, 10, ...
            Column.Second => spinResult.Number % 3 == 2, // 2, 5, 8, 11, ...
            _ => spinResult.Number % 3 == 0              // 3, 6, 9, 12, ...
        };
    }
}
