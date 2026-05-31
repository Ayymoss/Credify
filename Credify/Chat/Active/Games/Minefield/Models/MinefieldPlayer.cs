using Credify.Chat.Active.Games.Minefield.Enums;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Minefield.Models;

/// <summary>
/// Represents a single isolated Minefield session for one player.
/// Holds the shuffled field, progress, and the per-dig idle timer.
/// </summary>
public class MinefieldPlayer
{
    public required EFClient Client { get; init; }
    public SessionState State { get; set; } = SessionState.AwaitingStake;

    /// <summary>Credits wagered. Debited when the field is armed.</summary>
    public long Stake { get; set; }

    /// <summary>Number of mines armed in the field.</summary>
    public int MineCount { get; set; }

    /// <summary>
    /// The field: true = mine. Length is the total tile count. Shuffled at arm time.
    /// Tiles are revealed sequentially (hidden tiles carry no information, so the
    /// order the player "picks" is irrelevant — this is mathematically identical to
    /// letting them choose coordinates).
    /// </summary>
    public bool[] Field { get; set; } = [];

    /// <summary>Count of safe tiles cleared so far.</summary>
    public int DugCount { get; set; }

    /// <summary>Cancels the in-flight idle/auto-cash timer when the player acts.</summary>
    public CancellationTokenSource? IdleToken { get; set; }

    /// <summary>Total safe tiles in the field (= total - mines).</summary>
    public int SafeTotal => Field.Length - MineCount;

    /// <summary>True when every safe tile has been cleared (nothing left to dig).</summary>
    public bool IsFieldCleared => DugCount >= SafeTotal;

    public void CancelIdleTimer()
    {
        IdleToken?.Cancel();
        IdleToken?.Dispose();
        IdleToken = null;
    }
}
