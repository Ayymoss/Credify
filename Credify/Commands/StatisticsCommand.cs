using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Services;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Credits")]
public class StatisticsCommand : Command
{
    private readonly CredifyCache _cache;
    private readonly CredifyConfiguration _credifyConfig;

    public StatisticsCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyCache cache,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _cache = cache;
        _credifyConfig = credifyConfig;
        Name = "credifystats";
        Description = credifyConfig.Translations.Core.CommandStatisticsDescription;
        Alias = "crstats";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await gameEvent.Origin.TellAsync(
        [
            // @formatter:off
            _credifyConfig.Translations.Core.StatsHeader,
            _credifyConfig.Translations.Core.StatsBankCredits.FormatExt(_cache.BankCredits.ToString("N0")),
            _credifyConfig.Translations.Core.StatsTotalEarnedCredits.FormatExt(_cache.StatisticsState.CreditsEarned.ToString("N0")),
            _credifyConfig.Translations.Core.StatsTotalSpentCredits.FormatExt(_cache.StatisticsState.CreditsSpent.ToString("N0")),
            _credifyConfig.Translations.Core.StatsTotalWonCredits.FormatExt(_cache.StatisticsState.CreditsWon.ToString("N0")),
            // @formatter:on
        ]);
    }
}
