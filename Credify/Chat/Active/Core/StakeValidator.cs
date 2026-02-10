using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Core;

/// <summary>
/// Shared stake validation logic for games that accept bets.
/// Handles common patterns: parsing, minimum bet checks, credit validation.
/// </summary>
public class StakeValidator(PersistenceService persistenceService, long minimumBet = GameConstants.MinimumCredits)
{
    /// <summary>
    /// Validates a stake amount against player's available credits.
    /// </summary>
    /// <param name="message">Raw chat message (should be a number)</param>
    /// <param name="player">The player placing the bet</param>
    /// <param name="insufficientFundsMessage">Error message for insufficient funds</param>
    /// <param name="minimumBetMessage">Error message for below minimum bet</param>
    /// <param name="invalidInputMessage">Error message for non-numeric input</param>
    /// <returns>Validated stake amount or error</returns>
    public async Task<ParseResult<long>> ValidateStakeAsync(
        string message,
        EFClient player,
        string insufficientFundsMessage,
        string minimumBetMessage,
        string invalidInputMessage)
    {
        if (!long.TryParse(message, out var stake))
        {
            return ParseResult<long>.Error(invalidInputMessage);
        }

        if (stake < minimumBet)
        {
            return ParseResult<long>.Error(minimumBetMessage);
        }

        var playerCredits = await persistenceService.GetClientCreditsAsync(player);
        if (stake > playerCredits)
        {
            return ParseResult<long>.Error(insufficientFundsMessage);
        }

        return ParseResult<long>.Success(stake);
    }

    /// <summary>
    /// Synchronous validation when credits are already known.
    /// </summary>
    public ParseResult<long> ValidateStake(
        string message,
        long playerCredits,
        string insufficientFundsMessage,
        string minimumBetMessage,
        string invalidInputMessage)
    {
        if (!long.TryParse(message, out var stake))
        {
            return ParseResult<long>.Error(invalidInputMessage);
        }

        if (stake < minimumBet)
        {
            return ParseResult<long>.Error(minimumBetMessage);
        }

        if (stake > playerCredits)
        {
            return ParseResult<long>.Error(insufficientFundsMessage);
        }

        return ParseResult<long>.Success(stake);
    }
}
