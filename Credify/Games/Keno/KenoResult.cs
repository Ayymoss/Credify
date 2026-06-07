using System.Collections.Generic;

namespace Credify.Games.Keno;

/// <summary>The pure result of a keno round: the 20 drawn numbers, which of the player's picks hit, and the payout multiplier.</summary>
public sealed record KenoResult(IReadOnlyList<int> Drawn, IReadOnlyList<int> Hits, int Spots, double Multiplier);
