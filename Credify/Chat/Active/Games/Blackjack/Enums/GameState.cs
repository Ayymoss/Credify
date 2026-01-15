namespace Credify.Chat.Active.Games.Blackjack.Enums;

/// <summary>
/// State of the blackjack game flow.
/// </summary>
public enum GameState
{
    WaitingForPlayers,
    SettingUpGame,
    RequestPlayerStakes,
    DealCards,
    OfferingInsurance, // When dealer shows Ace
    RequestPlayerDecisions,
    DealerPlays,
    Payout
}

