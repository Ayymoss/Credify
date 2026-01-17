using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;

/// <summary>
/// American Roulette special bet on 0, 00, 1, 2, 3 (Top Line / Five Number Bet).
/// Payout is 6:1 (pays 7x stake). House edge is ~7.89%.
/// </summary>
public class TopLineBet(int stake) : InsideBaseBet(InsideBet.TopLine, stake)
{
    // Top Line covers: 0, 00 (37), 1, 2, 3
    private static readonly int[] CoveredNumbers = [0, 1, 2, 3, RouletteConstants.DoubleZero];
    
    public override int Payout { get; } = stake * 7; // 6:1 payout

    public override bool HasWon(SpinResult spinResult) => 
        CoveredNumbers.Contains(spinResult.Number);
}
