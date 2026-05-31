using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Roulette.Models;

/// <summary>
/// Represents a player in a Roulette game with betting state tracking.
/// </summary>
public class Player(EFClient client)
{
    public EFClient Client { get; } = client;
    public BaseBet? Bet { get; private set; }
    
    // Betting flow state
    public PlayerInputState InputState { get; set; } = PlayerInputState.WaitingForStake;
    public long? PendingStake { get; set; }
    public BetCategory? SelectedCategory { get; set; }

    /// <summary>
    /// The last accepted single-line bet input ("&lt;stake&gt; &lt;bet&gt;"), kept across
    /// rounds so the player can repeat it with "same". Not cleared on round reset.
    /// </summary>
    public string? LastBetInput { get; set; }

    public void CreateBet(BaseBet bet) => Bet = bet;
    public void ClearBet() => Bet = null;
    
    /// <summary>
    /// Resets player state for a new betting round.
    /// </summary>
    public void ResetForNewRound()
    {
        Bet = null;
        InputState = PlayerInputState.WaitingForStake;
        PendingStake = null;
        SelectedCategory = null;
    }
}
