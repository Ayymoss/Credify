using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands.Base;

/// <summary>
/// Base class for single-shot gambling commands (coin flip, RPS, slots, ...).
/// Centralizes the stake-resolution sequence every one of them repeated: expand
/// "all" to the player's balance, parse, enforce min/max bet, and check funds.
/// </summary>
public abstract class GambleCommandBase : Command
{
    protected readonly PersistenceService Persistence;
    protected readonly CredifyConfiguration CredifyConfig;

    protected GambleCommandBase(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistence, CredifyConfiguration credifyConfig)
        : base(config, translationLookup)
    {
        Persistence = persistence;
        CredifyConfig = credifyConfig;
    }

    /// <summary>
    /// Resolves and validates a stake from raw chat input. Expands "all" to the player's
    /// balance, parses it, then enforces the minimum bet, an optional maximum
    /// (<paramref name="maxBet"/> &lt;= 0 means no cap), and available funds. On any failure
    /// the appropriate message is sent to the player and <c>null</c> is returned.
    /// </summary>
    protected async Task<long?> TryResolveStakeAsync(GameEvent gameEvent, string stakeArg,
        long minBet, long maxBet, string parseErrorMessage)
    {
        var balance = await Persistence.GetClientCreditsAsync(gameEvent.Origin);

        if (stakeArg.Equals("all", StringComparison.OrdinalIgnoreCase))
            stakeArg = balance.ToString();

        if (!long.TryParse(stakeArg, out var stake))
        {
            gameEvent.Origin.Tell(parseErrorMessage);
            return null;
        }

        if (stake < minBet)
        {
            gameEvent.Origin.Tell(CredifyConfig.Translations.Gambling.MinimumAmount);
            return null;
        }

        if (maxBet > 0 && stake > maxBet)
        {
            gameEvent.Origin.Tell(CredifyConfig.Translations.Gambling.MaximumAmount.FormatExt(maxBet.ToString("N0")));
            return null;
        }

        if (!PersistenceService.AvailableFunds(gameEvent.Origin, stake))
        {
            gameEvent.Origin.Tell(CredifyConfig.Translations.Core.InsufficientCredits);
            return null;
        }

        return stake;
    }
}
