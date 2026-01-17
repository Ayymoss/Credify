namespace Credify.Chat.Active.Games.Roulette.Enums;

/// <summary>
/// Tracks where a player is in the betting input flow.
/// </summary>
public enum PlayerInputState
{
    WaitingForStake,    // Needs to enter bet amount
    WaitingForCategory, // Needs to choose Inside/Outside
    WaitingForDetails,  // Needs to specify bet details
    Complete,           // Bet is fully placed
    TimedOut            // Missed the betting window
}
