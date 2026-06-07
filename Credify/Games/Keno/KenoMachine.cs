using System;
using System.Collections.Generic;
using System.Linq;

namespace Credify.Games.Keno;

/// <summary>
/// Pure keno draw: pulls <see cref="KenoPayouts.DrawCount"/> distinct numbers from the 1..<see cref="KenoPayouts.PoolSize"/>
/// pool (partial Fisher–Yates), counts hits against the player's picks, and looks up the multiplier. RNG is
/// injectable so the web page, a crypto source, or a seeded test can all drive the same draw.
/// </summary>
public static class KenoMachine
{
    /// <param name="next">Returns an int in [0, maxExclusive). Defaults to <see cref="Random.Shared"/>.</param>
    public static KenoResult Draw(IReadOnlyCollection<int> picks, Func<int, int>? next = null)
    {
        next ??= Random.Shared.Next;

        var pool = Enumerable.Range(1, KenoPayouts.PoolSize).ToArray();
        // partial Fisher–Yates: the first DrawCount entries become the draw
        for (var i = 0; i < KenoPayouts.DrawCount; i++)
        {
            var j = i + next(pool.Length - i);
            (pool[i], pool[j]) = (pool[j], pool[i]);
        }

        var drawn = pool.Take(KenoPayouts.DrawCount).ToList();
        var drawnSet = drawn.ToHashSet();
        var hits = picks.Where(drawnSet.Contains).ToList();

        return new KenoResult(drawn, hits, picks.Count, KenoPayouts.Multiplier(picks.Count, hits.Count));
    }
}
