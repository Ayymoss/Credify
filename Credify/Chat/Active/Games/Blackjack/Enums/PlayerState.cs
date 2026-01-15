namespace Credify.Chat.Active.Games.Blackjack.Enums;

/// <summary>
/// State of a player during a game round.
/// </summary>
public enum PlayerState
{
    Playing,
    Stand,
    Busted,
    PlayingSplitHand // Currently playing the second (split) hand
}
