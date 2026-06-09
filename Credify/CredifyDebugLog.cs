using Microsoft.Extensions.Logging;

namespace Credify;

/// <summary>
/// Lightweight, greppable debug logging for the live casino games (Poker / Roulette / Blackjack),
/// added to trace webfront game-flow issues with multiple concurrent players.
///
/// The game cores (PokerTable, Table, BlackjackGame) are constructed by hand inside DI factories /
/// managers, not resolved with their own <see cref="ILogger"/>, so they can't take a logger via the
/// constructor. This static facade bridges that: <see cref="CredifyDebugLogService"/> is built by DI
/// at plugin construction and assigns the host's configured <see cref="ILogger"/> to <see cref="Logger"/>.
/// (The static <c>Serilog.Log</c> is a no-op silent logger inside the plugin bundle, which is why it
/// produced nothing.)
///
/// Writes at <b>Information</b> level on purpose: the host's minimum level filters Debug out, so Debug
/// lines never reach the collected log. Every line is tagged "[CredifyDbg/{game}]" so the whole
/// multiplayer trace can be grepped out and the feature stripped later (delete this class +
/// <see cref="CredifyDebugLogService"/> + the call sites once the flow is sound).
///
/// Lives in the root <c>Credify</c> namespace so every <c>Credify.*</c> type can call it without a
/// using directive (C# name lookup walks the enclosing namespaces).
/// </summary>
public static class CredifyDebugLog
{
    /// <summary>
    /// The host logger, bound once at startup by <see cref="CredifyDebugLogService"/>. Null until then,
    /// so any early call is a safe no-op rather than an NRE.
    /// </summary>
    public static ILogger? Logger { get; set; }

    /// <summary>
    /// Emit a single debug line. <paramref name="game"/> is the table tag (Poker/Roulette/Blackjack),
    /// <paramref name="message"/> should lead with a short UPPER-CASE event token (STATE / TURN / ACTION /
    /// RECV / JOIN …) and always include the player's <c>CleanedName</c> where one is involved, so it is
    /// clear who did what. The logging framework stamps the timestamp.
    /// </summary>
    public static void Log(string game, string message) =>
        Logger?.LogInformation("[CredifyDbg/{Game}] {Message}", game, message);
}
