namespace Credify.Chat.Active.Games.Roulette;

/// <summary>
/// Serializable, frontend-agnostic view of the live Roulette table for the webfront to render. Built by
/// <c>Table.GetSnapshot()</c> from the same running game the in-game chat players are at.
/// </summary>
public sealed record RouletteSnapshot
{
    /// <summary>"WaitingForPlayers" | "Betting" | "Spinning" | "Resolving".</summary>
    public string Phase { get; init; } = "WaitingForPlayers";

    /// <summary>Seconds left in the betting window (0 outside the betting phase).</summary>
    public double SecondsRemaining { get; init; }

    public IReadOnlyList<RoulettePlayerView> Players { get; init; } = [];

    /// <summary>The most recent spin (null before the first spin of the session).</summary>
    public RouletteSpinView? LastSpin { get; init; }

    /// <summary>Recent spins, newest first, for the "history" strip.</summary>
    public IReadOnlyList<RouletteSpinView> History { get; init; } = [];
}

/// <summary>A seat at the table as the web should render it. A player may hold several bets in a round.</summary>
public sealed record RoulettePlayerView(
    int ClientId,
    string Name,
    IReadOnlyList<RouletteBetView> Bets,
    long TotalStake,
    bool HasBet,
    string Outcome, // "None" | "Pending" | "Won" | "Lost"
    long Net);

/// <summary>A single placed bet: what it's on and for how much.</summary>
public sealed record RouletteBetView(string Label, long Stake);

/// <summary>A wheel result. Colour is "Red" | "Black" | "Green".</summary>
public sealed record RouletteSpinView(int Number, string Display, string Colour);
