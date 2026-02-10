namespace Credify.Chat.Active.Games.Roulette.Enums;

/// <summary>
/// State of the Roulette game flow.
/// </summary>
public enum RouletteGameState
{
    WaitingForPlayers,
    CollectingBets,      // Players enter their stake amounts
    AwaitingBetCategory, // Players choose Inside or Outside bet
    AwaitingBetDetails,  // Players specify bet numbers/options
    SpinningWheel,       // Wheel is spinning (no input accepted)
    ResolvingBets        // Calculating payouts
}
