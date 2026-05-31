namespace Credify.Chat.Active.Games.Minefield.Enums;

/// <summary>
/// Per-player session state for Minefield. Each player progresses through these
/// independently (sessions are isolated, unlike the shared-round games).
/// </summary>
public enum SessionState
{
    /// <summary>Waiting for the player to type their stake.</summary>
    AwaitingStake,

    /// <summary>Stake set; waiting for the player to choose how many mines to arm.</summary>
    AwaitingMines,

    /// <summary>Field is armed; player is clearing tiles (dig) or may cash out.</summary>
    Digging
}
