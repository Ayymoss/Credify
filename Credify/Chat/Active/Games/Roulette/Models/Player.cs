using System.Collections.Generic;
using System.Linq;
using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Roulette.Models;

/// <summary>
/// Represents a player in a Roulette game with betting state tracking. A player may hold multiple bets
/// in a round (the web places several at once; chat places one via its guided flow).
/// </summary>
public class Player(EFClient client)
{
    public EFClient Client { get; } = client;

    private readonly List<PlacedBet> _bets = [];

    /// <summary>All bets placed this round.</summary>
    public IReadOnlyList<PlacedBet> Bets => _bets;
    public bool HasBet => _bets.Count > 0;
    public long TotalStake => _bets.Sum(placed => placed.Bet.Stake);

    // Betting flow state
    public PlayerInputState InputState { get; set; } = PlayerInputState.WaitingForStake;
    public long? PendingStake { get; set; }
    public BetCategory? SelectedCategory { get; set; }

    /// <summary>
    /// The last accepted single-line bet input ("&lt;stake&gt; &lt;bet&gt;"), kept across
    /// rounds so the player can repeat it with "same". Not cleared on round reset.
    /// </summary>
    public string? LastBetInput { get; set; }

    // ── webfront display state (ignored by chat; surfaced in the web snapshot) ──
    /// <summary>"None" | "Pending" (bet placed, awaiting spin) | "Won" | "Lost". Kept until the next bet.</summary>
    public string LastResult { get; set; } = "None";
    /// <summary>Net credits from the last settled round (+win / -stake), aggregated over all bets.</summary>
    public long LastNet { get; set; }

    public void AddBet(BaseBet bet, string label) => _bets.Add(new PlacedBet(bet, label));
    public void ClearBets() => _bets.Clear();

    /// <summary>Resets player state for a new betting round.</summary>
    public void ResetForNewRound()
    {
        _bets.Clear();
        InputState = PlayerInputState.WaitingForStake;
        PendingStake = null;
        SelectedCategory = null;
        // LastResult / LastNet intentionally kept so the web can show the previous outcome until the
        // player places a new bet.
    }
}

/// <summary>A placed bet plus the human label of what it was on (e.g. "red", "17", "1-12").</summary>
public sealed record PlacedBet(BaseBet Bet, string Label);
