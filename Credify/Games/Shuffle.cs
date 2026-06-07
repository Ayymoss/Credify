using System.Security.Cryptography;

namespace Credify.Games;

/// <summary>
/// Cryptographically-seeded Fisher–Yates shuffle, shared by every Credify game (cards, minefield tiles, …)
/// so shuffling is implemented — and audited for fairness — in exactly one place. Outcomes can't be
/// predicted client-side. Replaces the per-game shuffles that used to live in each chat/web game
/// (including the old <c>OrderBy(Guid.NewGuid())</c> deck shuffle, which is not a uniform shuffle).
/// </summary>
public static class Shuffle
{
    /// <summary>Shuffles <paramref name="items"/> in place, uniformly, using a crypto RNG.</summary>
    public static void InPlace<T>(IList<T> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
