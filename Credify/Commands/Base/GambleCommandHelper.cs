using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;

namespace Credify.Commands.Base;

/// <summary>
/// Shared stake-resolution logic for single-shot gambling commands (coin flip, RPS,
/// slots, ...). Used via composition rather than inheritance: a Command-derived base
/// would be picked up by IW4MAdmin's command discovery and fail to register (it can't
/// instantiate an abstract command). This mirrors <see cref="GameJoinCommandHelper{TManager}"/>.
/// </summary>
public class GambleCommandHelper(PersistenceService persistence, CredifyConfiguration credifyConfig)
{
    /// <summary>
    /// Resolves and validates a stake from raw chat input. Expands "all" to the player's
    /// balance, parses it, then enforces the minimum bet, an optional maximum
    /// (<paramref name="maxBet"/> &lt;= 0 means no cap), and available funds. On any failure
    /// the appropriate message is sent to the player and <c>null</c> is returned.
    /// </summary>
    public async Task<long?> TryResolveStakeAsync(GameEvent gameEvent, string stakeArg,
        long minBet, long maxBet, string parseErrorMessage)
    {
        var balance = await persistence.GetClientCreditsAsync(gameEvent.Origin);

        if (stakeArg.Equals("all", StringComparison.OrdinalIgnoreCase))
            stakeArg = balance.ToString();

        if (!long.TryParse(stakeArg, out var stake))
        {
            gameEvent.Origin.Tell(parseErrorMessage);
            return null;
        }

        if (stake < minBet)
        {
            gameEvent.Origin.Tell(credifyConfig.Translations.Gambling.MinimumAmount);
            return null;
        }

        if (maxBet > 0 && stake > maxBet)
        {
            gameEvent.Origin.Tell(credifyConfig.Translations.Gambling.MaximumAmount.FormatExt(maxBet.ToString("N0")));
            return null;
        }

        if (!PersistenceService.AvailableFunds(gameEvent.Origin, stake))
        {
            gameEvent.Origin.Tell(credifyConfig.Translations.Core.InsufficientCredits);
            return null;
        }

        return stake;
    }
}
