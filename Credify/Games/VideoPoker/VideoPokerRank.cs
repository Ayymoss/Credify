namespace Credify.Games.VideoPoker;

/// <summary>
/// The paying hands of Jacks-or-Better video poker, in ascending strength. <see cref="None"/> and a
/// low pair (below jacks) pay nothing — the defining quirk of the game.
/// </summary>
public enum VideoPokerRank
{
    None = 0,
    JacksOrBetter,
    TwoPair,
    ThreeOfAKind,
    Straight,
    Flush,
    FullHouse,
    FourOfAKind,
    StraightFlush,
    RoyalFlush
}
