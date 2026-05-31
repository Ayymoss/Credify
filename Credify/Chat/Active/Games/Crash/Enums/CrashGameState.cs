namespace Credify.Chat.Active.Games.Crash.Enums;

/// <summary>
/// Shared state for the Crash round (one rocket for all players in the round).
/// </summary>
public enum CrashGameState
{
    WaitingForPlayers,
    Betting,
    Flying,
    Resolving
}
