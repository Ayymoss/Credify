using System.Collections.Concurrent;
using Credify.Configuration;
using Credify.Services;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Base class for active games providing common player management and communication functionality.
/// </summary>
/// <typeparam name="TPlayer">The player data type used by the game</typeparam>
public abstract class BaseActiveGame<TPlayer>(
    PersistenceService persistenceService,
    CredifyConfiguration config,
    GamePlayerCommunication communication)
    : IActiveGame
{
    protected readonly ConcurrentDictionary<EFClient, TPlayer> Players = new();
    protected readonly PersistenceService PersistenceService = persistenceService;
    protected readonly CredifyConfiguration Config = config;
    protected readonly GamePlayerCommunication Communication = communication;

    /// <summary>
    /// Serializes chat-input/state mutations within a game. Every active game needs this,
    /// so it lives here rather than being re-declared per game.
    /// </summary>
    private readonly SemaphoreSlim _chatLock = new(1, 1);

    /// <summary>
    /// Runs <paramref name="action"/> while holding the game's chat lock, guaranteeing the
    /// lock is released exactly once. Replaces the hand-rolled WaitAsync/try-finally that
    /// each game used to duplicate.
    /// </summary>
    protected async Task ExecuteUnderChatLockAsync(Func<Task> action)
    {
        await _chatLock.WaitAsync();
        try
        {
            await action();
        }
        finally
        {
            _chatLock.Release();
        }
    }

    /// <summary>
    /// Validates that a player has sufficient credits to participate.
    /// </summary>
    protected async Task<bool> ValidatePlayerCreditsAsync(EFClient player, long minimumCredits = GameConstants.MinimumCredits)
    {
        var credits = await PersistenceService.GetClientCreditsAsync(player);
        return credits >= minimumCredits;
    }

    /// <summary>
    /// Gets the current number of players in the game.
    /// </summary>
    public virtual int GetPlayerCount() => Players.Count;

    /// <summary>
    /// Checks if a player is currently in the game.
    /// </summary>
    public virtual bool IsPlayerPlaying(EFClient player) => Players.ContainsKey(player);

    /// <summary>
    /// Adds a player to the game.
    /// </summary>
    protected virtual void AddPlayer(EFClient player, TPlayer playerData)
    {
        Players.TryAdd(player, playerData);
    }

    /// <summary>
    /// Removes a player from the game.
    /// </summary>
    protected virtual bool RemovePlayer(EFClient player)
    {
        return Players.TryRemove(player, out _);
    }

    /// <summary>
    /// Gets player data if they exist in the game.
    /// </summary>
    protected virtual bool TryGetPlayer(EFClient player, out TPlayer? playerData)
    {
        return Players.TryGetValue(player, out playerData);
    }

    // Abstract methods that must be implemented by derived classes
    public abstract Task JoinGameAsync(EFClient player);
    public abstract Task LeaveGameAsync(EFClient player);
    public abstract Task HandleChatAsync(EFClient player, string message);
}
