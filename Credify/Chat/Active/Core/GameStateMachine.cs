namespace Credify.Chat.Active.Core;

/// <summary>
/// Typed current-state holder for games that track a single game-wide state. Provides the
/// current state, state queries, and an unconditional transition.
///
/// Note: this intentionally does NOT validate transitions. Games guard their own flow with
/// IsInState checks at each step (e.g. "if (!IsInState(RequestPlayerStakes)) return;"), and
/// they also need recovery transitions (resetting back to WaitingForPlayers after a
/// disconnect/timeout) that a strict transition table would reject. A validated FSM was
/// tried and bypassed everywhere, so it was removed rather than left as dead code.
/// </summary>
/// <typeparam name="TState">The state enum type</typeparam>
public abstract class GameStateMachine<TState> where TState : struct, Enum
{
    /// <summary>
    /// Gets the current state.
    /// </summary>
    public TState CurrentState { get; private set; }

    /// <summary>
    /// Initializes the state machine with the initial state.
    /// </summary>
    protected GameStateMachine(TState initialState)
    {
        CurrentState = initialState;
    }

    /// <summary>
    /// Transitions to a new state.
    /// </summary>
    public void TransitionTo(TState newState) => CurrentState = newState;

    /// <summary>
    /// Checks if the current state matches the given state.
    /// </summary>
    public bool IsInState(TState state) => EqualityComparer<TState>.Default.Equals(CurrentState, state);

    /// <summary>
    /// Checks if the current state is one of the given states.
    /// </summary>
    public bool IsInAnyState(params TState[] states) => states.Contains(CurrentState);
}
