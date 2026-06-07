using System.Collections.Generic;

namespace Credify.Games.Plinko;

/// <summary>
/// The pure result of a single ball drop: the left/right decision at each peg row (<c>true</c> = right) and
/// the bucket the ball settled in. The bucket index equals the number of right moves, so it ranges
/// 0..rows (rows + 1 buckets). The web frontend replays <see cref="Path"/> to animate the ball; the bucket
/// alone determines the payout.
/// </summary>
public sealed record PlinkoDrop(IReadOnlyList<bool> Path, int Bucket)
{
    public int Rows => Path.Count;
}
