using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Abstract base class for game output handlers using template method pattern.
/// Derived classes only need to implement GetClient and GetPrefix to get full communication functionality.
/// </summary>
/// <typeparam name="TPlayer">The player type used by the game</typeparam>
public abstract class BaseGameOutputHandler<TPlayer>(GamePlayerCommunication communication) : IGameOutputHandler<TPlayer>
{
    /// <summary>
    /// Extracts the EFClient from the game-specific player type.
    /// </summary>
    protected abstract EFClient GetClient(TPlayer player);

    /// <summary>
    /// Gets the appropriate prefix string based on whether long or short prefix is requested.
    /// </summary>
    protected abstract string GetPrefix(bool longPrefix);

    /// <inheritdoc />
    public virtual async Task TellPlayerAsync(TPlayer player, IEnumerable<string> messages, bool longPrefix = false)
    {
        await communication.TellPlayerAsync(GetClient(player), GetPrefix(longPrefix), messages);
    }

    /// <inheritdoc />
    public virtual async Task TellPlayersAsync(IEnumerable<TPlayer> players, IEnumerable<string> messages)
    {
        await communication.TellPlayersAsync(players.Select(GetClient), messages);
    }

    /// <inheritdoc />
    public virtual async Task BroadcastToServerAsync(TPlayer player, IEnumerable<string> messages)
    {
        await communication.BroadcastToServerAsync(GetClient(player), messages);
    }

    /// <inheritdoc />
    public virtual async Task BroadcastToAllServersAsync(TPlayer player, IEnumerable<string> messages)
    {
        await communication.BroadcastToAllServersAsync(GetClient(player), messages);
    }

    /// <summary>
    /// Synchronous single-message convenience method with inline prefix.
    /// </summary>
    public virtual void Tell(TPlayer player, string message, bool longPrefix = false)
    {
        var prefix = GetPrefix(longPrefix);
        GetClient(player).Tell($"{prefix} {message}");
    }
}
