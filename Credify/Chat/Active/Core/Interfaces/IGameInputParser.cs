namespace Credify.Chat.Active.Core.Interfaces;

/// <summary>
/// Interface for parsing chat messages into game-specific actions.
/// Implement this for simple synchronous parsing (e.g., hit/stand, fold/raise).
/// </summary>
/// <typeparam name="TResult">The parsed action type</typeparam>
public interface IGameInputParser<TResult>
{
    /// <summary>
    /// Parses a chat message into a game action.
    /// </summary>
    /// <param name="message">Raw chat message from player</param>
    /// <returns>Parse result with action or error</returns>
    ParseResult<TResult> Parse(string message);
}
