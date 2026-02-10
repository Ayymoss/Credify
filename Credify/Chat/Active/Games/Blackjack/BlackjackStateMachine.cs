using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack.Enums;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// State machine for Blackjack game with transition validation.
/// </summary>
internal class BlackjackStateMachine : GameStateMachine<GameState>
{
    public BlackjackStateMachine() : base(GameState.WaitingForPlayers)
    {
    }

    /// <summary>
    /// Validates state transitions to ensure game flow integrity.
    /// </summary>
    protected override bool IsValidTransition(GameState from, GameState to)
    {
        // Define valid state transitions
        return (from, to) switch
        {
            // Can start from waiting
            (GameState.WaitingForPlayers, GameState.SettingUpGame) => true,
            
            // Setup flow
            (GameState.SettingUpGame, GameState.RequestPlayerStakes) => true,
            (GameState.RequestPlayerStakes, GameState.DealCards) => true,
            (GameState.RequestPlayerStakes, GameState.Payout) => true, // Early exit if no players
            
            // Deal cards flow
            (GameState.DealCards, GameState.OfferingInsurance) => true,
            (GameState.DealCards, GameState.RequestPlayerDecisions) => true,
            
            // Insurance flow
            (GameState.OfferingInsurance, GameState.RequestPlayerDecisions) => true,
            
            // Player decisions flow
            (GameState.RequestPlayerDecisions, GameState.DealerPlays) => true,
            
            // Dealer plays flow
            (GameState.DealerPlays, GameState.Payout) => true,
            
            // Payout flow
            (GameState.Payout, GameState.WaitingForPlayers) => true,
            
            // Allow staying in same state
            _ when from == to => true,
            
            // Invalid transition
            _ => false
        };
    }
}
