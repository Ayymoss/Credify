using Credify.Games.Cards;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// Serializable, frontend-agnostic view of the live Blackjack table for the webfront. Built by
/// <c>BlackjackGame.GetSnapshot()</c> from the same running multiplayer game the in-game chat players are at,
/// so web and chat share one table and one dealer. Holds shared <see cref="Card"/> objects directly (consumed
/// in-process by the Blazor component, not serialized over the wire).
/// </summary>
public sealed record BlackjackSnapshot
{
    /// <summary>"Waiting" | "Betting" | "Dealing" | "Insurance" | "Decisions" | "DealerPlaying" | "Payout".</summary>
    public string Phase { get; init; } = "Waiting";

    /// <summary>Seconds left in the current player window (0 outside a timed window).</summary>
    public double SecondsRemaining { get; init; }

    /// <summary>Dealer's visible cards (just the up-card until the hole is revealed).</summary>
    public IReadOnlyList<Card> DealerCards { get; init; } = [];

    /// <summary>True while the dealer still has a face-down hole card to show.</summary>
    public bool DealerHasHole { get; init; }

    /// <summary>Dealer total: up-card value while hidden, full hand once revealed.</summary>
    public int DealerValue { get; init; }

    public IReadOnlyList<BlackjackSeatView> Seats { get; init; } = [];
}

/// <summary>A seat at the table as the web should render it.</summary>
public sealed record BlackjackSeatView
{
    public int ClientId { get; init; }
    public string Name { get; init; } = "";
    public long? Stake { get; init; }
    public IReadOnlyList<Card> Cards { get; init; } = [];
    public int Value { get; init; }
    public bool IsSoft { get; init; }
    public bool IsBlackjack { get; init; }
    public bool Busted { get; init; }

    /// <summary>"Waiting" | "SittingOut" | "Betting" | "Playing" | "PlayingSplit" | "Stand" | "Busted".</summary>
    public string State { get; init; } = "";

    /// <summary>"" | "Win" | "Lose" | "Push" | "Blackjack" — only set once the round is settled.</summary>
    public string Outcome { get; init; } = "";
    public long Net { get; init; }

    public bool HasSplit { get; init; }
    public IReadOnlyList<Card> SplitCards { get; init; } = [];
    public int SplitValue { get; init; }
    public string SplitOutcome { get; init; } = "";

    public bool HasInsurance { get; init; }

    // affordances for the web controls (only meaningful for the local player in the right phase)
    public bool CanDouble { get; init; }
    public bool CanSplit { get; init; }
    public bool InsuranceEligible { get; init; }
}
