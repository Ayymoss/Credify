using Credify.Chat.Active.Games.Blackjack.Enums;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Blackjack.Models;

/// <summary>
/// Represents a player in a Blackjack game with hand, stake, state, and outcome.
/// Supports split hands, double-down, and insurance.
/// </summary>
public class BlackjackPlayer
{
    public required EFClient Client { get; init; }
    public List<BlackjackCard> Cards { get; set; } = [];
    public PlayerState State { get; set; } = PlayerState.Playing;
    public GameOutcome Outcome { get; set; }
    public long? Stake { get; set; }
    public long? Payout { get; set; }
    public bool Queued { get; set; }

    // Split hand properties
    public List<BlackjackCard> SplitCards { get; set; } = [];
    public long? SplitStake { get; set; }
    public GameOutcome SplitOutcome { get; set; }
    public long? SplitPayout { get; set; }
    public bool HasSplit { get; set; }
    public PlayerState SplitState { get; set; } = PlayerState.Playing;

    // Double-down and insurance
    public bool HasDoubled { get; set; }
    public bool HasInsurance { get; set; }
    public long InsuranceBet { get; set; }

    /// <summary>
    /// Checks if player can split (two cards of same rank).
    /// </summary>
    public bool CanSplit() => Cards.Count == 2 && !HasSplit && Cards[0].CardRank == Cards[1].CardRank;

    /// <summary>
    /// Checks if player can double down (initial two-card hand, not split).
    /// </summary>
    public bool CanDouble() => Cards.Count == 2 && !HasDoubled && !HasSplit;

    /// <summary>
    /// Resets player state for a new round while keeping them in the game.
    /// </summary>
    public void ResetForNewRound()
    {
        Cards.Clear();
        SplitCards.Clear();
        State = PlayerState.Playing;
        SplitState = PlayerState.Playing;
        Outcome = default;
        SplitOutcome = default;
        Stake = null;
        Payout = null;
        SplitStake = null;
        SplitPayout = null;
        HasSplit = false;
        HasDoubled = false;
        HasInsurance = false;
        InsuranceBet = 0;
        Queued = false;
    }
}

