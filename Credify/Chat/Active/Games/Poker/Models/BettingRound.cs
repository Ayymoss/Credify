namespace Credify.Chat.Active.Games.Poker.Models;

/// <summary>
/// Tracks the state of a betting round.
/// </summary>
public class BettingRound
{
    public long CurrentBet { get; set; }
    public long Pot { get; set; }
    public int PlayersActed { get; set; }
    public bool IsComplete { get; set; }
    
    public void Reset()
    {
        CurrentBet = 0;
        Pot = 0;
        PlayersActed = 0;
        IsComplete = false;
    }
}
