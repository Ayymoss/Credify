namespace Credify.Chat.Active.Core;

/// <summary>
/// Shared constants used across active games.
/// </summary>
public static class GameConstants
{
    /// <summary>
    /// Minimum credits required to participate in games.
    /// </summary>
    public const long MinimumCredits = 10;

    /// <summary>
    /// Blackjack-specific constants.
    /// </summary>
    public static class Blackjack
    {
        public const int BlackjackValue = 21;
        public const int DealerStandValue = 17;
        public const int MinimumCardsForBlackjack = 2;
    }

    /// <summary>
    /// Common timeout values (in seconds) used across games.
    /// </summary>
    public static class Timeouts
    {
        public const int DefaultGameStartDelay = 2;
        public const int DefaultPayoutDelay = 2;
    }
}
