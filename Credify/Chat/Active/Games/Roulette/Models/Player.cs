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
