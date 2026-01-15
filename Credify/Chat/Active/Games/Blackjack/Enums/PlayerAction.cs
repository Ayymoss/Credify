namespace Credify.Chat.Active.Games.Blackjack.Enums;

/// <summary>
/// Available actions a player can take during their turn.
/// </summary>
public enum PlayerAction
{
    Hit,
    Stand,
    Cards, // Show current cards
    Double,
    Split,
    Insurance
}
