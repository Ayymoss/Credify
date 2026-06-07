namespace Credify.Games.Live;

/// <summary>
/// A live game that the webfront can observe. The game raises <see cref="StateChanged"/> whenever its
/// state mutates (a bet placed, a phase change, a spin resolved …); a Blazor component subscribes, calls
/// <see cref="GetSnapshot"/> and re-renders. The snapshot is a serializable, frontend-agnostic view of the
/// table — the same running game instance the in-game chat players are at, so web and chat share one table.
/// This is the read/observe half of the web seam; structured web ACTIONS are game-specific methods that run
/// under the game's existing chat lock (so web and chat input are serialized together).
/// </summary>
public interface IWebObservableGame<out TSnapshot>
{
    /// <summary>An immutable snapshot of the current table state for rendering.</summary>
    TSnapshot GetSnapshot();

    /// <summary>Raised (on a background/game thread) whenever the table state changes.</summary>
    event Action? StateChanged;
}
