using System.Threading;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Base class for active games that run continuously in a loop (e.g., Poker, Roulette).
/// Provides common loop structure with player waiting and minimum player checks.
/// </summary>
/// <typeparam name="TPlayer">The player data type used by the game</typeparam>
public abstract class BaseContinuousGame<TPlayer>(
    PersistenceService persistenceService,
    CredifyConfiguration config,
    GamePlayerCommunication communication)
    : BaseActiveGame<TPlayer>(persistenceService, config, communication)
{
    /// <summary>
    /// Event that signals when players are available.
    /// </summary>
    protected readonly ManualResetEventSlim HasPlayers = new(false);

    /// <summary>
    /// Gets the minimum number of players required to start a game round.
    /// </summary>
    protected abstract int GetMinimumPlayers();

    /// <summary>
    /// Executes a single game round. Implement this with game-specific logic.
    /// </summary>
    protected abstract Task ExecuteGameRoundAsync(CancellationToken token);

    /// <summary>
    /// Optional delay between game rounds. Override to customize.
    /// </summary>
    protected virtual TimeSpan GetDelayBetweenRounds() => TimeSpan.FromSeconds(3);

    /// <summary>
    /// Optional delay when waiting for minimum players. Override to customize.
    /// </summary>
    protected virtual TimeSpan GetDelayWaitingForPlayers() => TimeSpan.FromSeconds(1);

    /// <summary>
    /// Main game loop - runs continuously until cancellation is requested.
    /// </summary>
    public async Task GameLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            HasPlayers.Wait(token);

            if (Players.Count < GetMinimumPlayers())
            {
                await Task.Delay(GetDelayWaitingForPlayers(), token);
                continue;
            }

            await ExecuteGameRoundAsync(token);

            // Brief pause between rounds if we still have enough players
            if (Players.Count >= GetMinimumPlayers())
            {
                await Task.Delay(GetDelayBetweenRounds(), token);
            }
        }
    }

    /// <summary>
    /// Signals that players are available. Call this when a player joins.
    /// </summary>
    protected void SignalPlayersAvailable()
    {
        if (!HasPlayers.IsSet)
        {
            HasPlayers.Set();
        }
    }

    /// <summary>
    /// Resets the players signal. Call this when no players remain.
    /// </summary>
    protected void ResetPlayersSignal()
    {
        if (Players.IsEmpty)
        {
            HasPlayers.Reset();
        }
    }

    /// <summary>
    /// Call this after adding a player to signal that players are available.
    /// Derived classes should call this in their JoinGameAsync implementation.
    /// </summary>
    protected void OnPlayerJoined()
    {
        SignalPlayersAvailable();
    }

    /// <summary>
    /// Call this after removing a player to reset the signal if no players remain.
    /// Derived classes should call this in their LeaveGameAsync implementation.
    /// </summary>
    protected void OnPlayerLeft()
    {
        ResetPlayersSignal();
    }
}
