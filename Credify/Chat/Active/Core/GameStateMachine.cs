namespace Credify.Chat.Active.Core;

/// <summary>
/// Simple state machine base class for game state management.
/// Provides state transition validation and state change events.
/// </summary>
/// <typeparam name="TState">The state enum type</typeparam>
public abstract class GameStateMachine<TState> where TState : struct, Enum
{
    private TState _currentState;

    /// <summary>
    /// Gets the current state.
    /// </summary>
    public TState CurrentState => _currentState;

    /// <summary>
    /// Initializes the state machine with the initial state.
    /// </summary>
    protected GameStateMachine(TState initialState)
    {
        _currentState = initialState;
    }

    /// <summary>
    /// Validates if a state transition is allowed. Override to implement custom validation.
    /// </summary>
    protected virtual bool IsValidTransition(TState from, TState to) => true;

    /// <summary>
    /// Called before a state transition. Override to perform actions before state changes.
    /// </summary>
    protected virtual void OnStateExiting(TState exitingState) { }

    /// <summary>
    /// Called after a state transition. Override to perform actions after state changes.
    /// </summary>
    protected virtual void OnStateEntered(TState enteredState) { }

    /// <summary>
    /// Transitions to a new state if the transition is valid.
    /// </summary>
    /// <returns>True if the transition was successful, false otherwise</returns>
    public bool TransitionTo(TState newState)
    {
        if (EqualityComparer<TState>.Default.Equals(_currentState, newState))
        {
            return true; // Already in this state
        }

        if (!IsValidTransition(_currentState, newState))
        {
            return false; // Invalid transition
        }

        OnStateExiting(_currentState);
        _currentState = newState;
        OnStateEntered(_currentState);
        return true;
    }

    /// <summary>
    /// Transitions to a new state without validation (use with caution).
    /// </summary>
    public void ForceTransitionTo(TState newState)
    {
        OnStateExiting(_currentState);
        _currentState = newState;
        OnStateEntered(_currentState);
    }

    /// <summary>
    /// Checks if the current state matches the given state.
    /// </summary>
    public bool IsInState(TState state) => EqualityComparer<TState>.Default.Equals(_currentState, state);

    /// <summary>
    /// Checks if the current state is one of the given states.
    /// </summary>
    public bool IsInAnyState(params TState[] states) => states.Contains(_currentState);
}
