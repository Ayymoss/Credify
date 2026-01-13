using System.Collections.Concurrent;
using Credify.Configuration;
using Credify.Services;
using Credify.Chat.Active.Core;
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
