using Credify.Games.Cards;

namespace Credify.Chat.Active.Games.Poker;

/// <summary>
/// Serializable, frontend-agnostic view of the live Poker table for the webfront. Built per-viewer by
/// <c>PokerManager.GetSnapshot(viewerClientId)</c> from the same running multiplayer game the in-game chat
/// players are at — so web and chat sit at one table. Hole cards are REDACTED: a seat's cards are only filled
/// in for the viewer themselves, or for everyone (non-folded) at showdown. Holds shared <see cref="Card"/>
/// objects (Poker's own card maps 1:1).
/// </summary>
public sealed record PokerSnapshot
{
    /// <summary>"WaitingForPlayers" | "BetweenHands" | "PreFlop" | "Flop" | "Turn" | "River" | "Showdown".</summary>
    public string Phase { get; init; } = "WaitingForPlayers";

    public long Pot { get; init; }
    public long CurrentBet { get; init; }
    public IReadOnlyList<Card> Community { get; init; } = [];

    /// <summary>ClientId of the seat whose turn it is to act (null between turns).</summary>
    public int? ActiveClientId { get; init; }

    /// <summary>Seconds left on the active player's turn timer (0 when no one is on the clock).</summary>
    public double SecondsRemaining { get; init; }

    public IReadOnlyList<PokerSeatView> Seats { get; init; } = [];

    public long BigBlind { get; init; }
    public long MinBuyIn { get; init; }
    public long MaxBuyIn { get; init; }

    /// <summary>Non-null only when it is the VIEWER's turn — the actions they may take.</summary>
    public PokerActionsView? MyActions { get; init; }
}

public sealed record PokerSeatView
{
    public int ClientId { get; init; }
    public string Name { get; init; } = "";
    public long Chips { get; init; }
    public long CurrentBet { get; init; }
    public bool IsDealer { get; init; }
    public bool IsSmallBlind { get; init; }
    public bool IsBigBlind { get; init; }
    public bool IsFolded { get; init; }
    public bool IsAllIn { get; init; }
    public bool IsActiveTurn { get; init; }

    /// <summary>The seat's hole cards — only populated for the viewer, or all non-folded seats at showdown.</summary>
    public IReadOnlyList<Card> HoleCards { get; init; } = [];

    /// <summary>True if the seat holds hidden hole cards (rendered face-down to this viewer).</summary>
    public bool HasHiddenCards { get; init; }

    /// <summary>Made-hand name, shown at showdown for revealed seats.</summary>
    public string? HandName { get; init; }

    public string LastAction { get; init; } = "";
}

/// <summary>The acting viewer's available moves (amounts are TOTAL bet sizes, not increments).</summary>
public sealed record PokerActionsView
{
    public bool CanFold { get; init; }
    public bool CanCheck { get; init; }
    public bool CanCall { get; init; }
    public bool CanRaise { get; init; }
    public bool CanAllIn { get; init; }
    public long CallAmount { get; init; }
    public long MinRaise { get; init; }
    public long MaxRaise { get; init; }
    public long MyChips { get; init; }
}
