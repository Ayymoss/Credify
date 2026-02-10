using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker.Enums;

namespace Credify.Chat.Active.Games.Poker;

/// <summary>
/// State machine for Poker game with transition validation.
/// </summary>
internal class PokerStateMachine() : GameStateMachine<PokerGameState>(PokerGameState.WaitingForPlayers)
{
    /// <summary>
    /// Validates state transitions to ensure game flow integrity.
    /// </summary>
    protected override bool IsValidTransition(PokerGameState from, PokerGameState to)
    {
        // Define valid state transitions
        return (from, to) switch
        {
            // Can start from waiting
            (PokerGameState.WaitingForPlayers, PokerGameState.BetweenHands) => true,
            
            // Between hands flow
            (PokerGameState.BetweenHands, PokerGameState.PreFlop) => true,
            (PokerGameState.BetweenHands, PokerGameState.WaitingForPlayers) => true, // Not enough players
            
            // Betting rounds flow (sequential)
            (PokerGameState.PreFlop, PokerGameState.Flop) => true,
            (PokerGameState.PreFlop, PokerGameState.Showdown) => true, // All but one folded
            
            (PokerGameState.Flop, PokerGameState.Turn) => true,
            (PokerGameState.Flop, PokerGameState.Showdown) => true, // All but one folded
            
            (PokerGameState.Turn, PokerGameState.River) => true,
            (PokerGameState.Turn, PokerGameState.Showdown) => true, // All but one folded
            
            (PokerGameState.River, PokerGameState.Showdown) => true,
            
            // Showdown flow
            (PokerGameState.Showdown, PokerGameState.BetweenHands) => true,
            (PokerGameState.Showdown, PokerGameState.WaitingForPlayers) => true, // Not enough players
            
            // Allow staying in same state
            _ when from == to => true,
            
            // Invalid transition
            _ => false
        };
    }
}
