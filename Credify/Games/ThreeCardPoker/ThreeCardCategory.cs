namespace Credify.Games.ThreeCardPoker;

/// <summary>
/// Three-card hand ranks, ascending. Note this differs from normal poker: with only three cards a straight
/// is rarer than a flush, so a straight outranks a flush.
/// </summary>
public enum ThreeCardCategory
{
    HighCard,
    Pair,
    Flush,
    Straight,
    ThreeOfAKind,
    StraightFlush
}
