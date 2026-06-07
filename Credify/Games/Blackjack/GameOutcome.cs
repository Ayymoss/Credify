namespace Credify.Games.Blackjack;

/// <summary>
/// The result of a settled blackjack hand, from the player's point of view. Shared by the chat and web
/// games (and their payout/display layers) so outcome handling is defined once.
/// </summary>
public enum GameOutcome
{
    Blackjack,
    Win,
    Lose,
    Push
}
