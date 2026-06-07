using System;
using System.Security.Cryptography;

namespace Credify.Games.Plinko;

/// <summary>
/// Pure Plinko ball physics: drops a ball through <c>rows</c> peg rows, choosing left/right with a fair 50/50
/// at each peg. No I/O, no economy — the web page settles a drop through here so the odds can never drift from
/// the payout math. By default each bounce uses a crypto RNG so the landing bucket can't be predicted
/// client-side; the choice source is injectable for deterministic tests.
/// </summary>
public static class PlinkoBoard
{
    /// <param name="rows">Number of peg rows. Produces <c>rows + 1</c> buckets.</param>
    /// <param name="next">
    /// Returns an int in [0, maxExclusive). Defaults to a crypto RNG. The ball goes right when
    /// <c>next(2) == 1</c>.
    /// </param>
    public static PlinkoDrop Drop(int rows, Func<int, int>? next = null)
    {
        if (rows < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(rows), "A Plinko board needs at least one peg row.");
        }

        next ??= RandomNumberGenerator.GetInt32;

        var path = new bool[rows];
        var bucket = 0;
        for (var i = 0; i < rows; i++)
        {
            var right = next(2) == 1;
            path[i] = right;
            if (right)
            {
                bucket++;
            }
        }

        return new PlinkoDrop(path, bucket);
    }
}
