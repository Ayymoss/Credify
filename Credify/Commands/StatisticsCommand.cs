using Credify.Configuration;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class StatisticsCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public StatisticsCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceManager persistenceManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
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
            _credifyConfig.Translations.Core.StatsBankCredits.FormatExt(_persistenceManager.BankCredits.ToString("N0")),
            _credifyConfig.Translations.Core.StatsTotalEarnedCredits.FormatExt(_persistenceManager.StatisticsState.CreditsEarned.ToString("N0")),
            _credifyConfig.Translations.Core.StatsTotalSpentCredits.FormatExt(_persistenceManager.StatisticsState.CreditsSpent.ToString("N0")),
            _credifyConfig.Translations.Core.StatsTotalWonCredits.FormatExt(_persistenceManager.StatisticsState.CreditsWon.ToString("N0")),
            // @formatter:on
        ]);
    }
}
