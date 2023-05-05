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
        Name = "credifytats";
        Description = credifyConfig.Translations.CommandStatisticsDescription;
        Alias = "crstats";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await gameEvent.Origin.TellAsync(new[]
        {
            _credifyConfig.Translations.StatsHeader,
            _credifyConfig.Translations.StatsBankCredits.FormatExt($"{_persistenceManager.BankCredits:N0}"),
            _credifyConfig.Translations.StatsTotalEarnedCredits.FormatExt($"{_persistenceManager.StatisticsState.CreditsEarned:N0}"),
            _credifyConfig.Translations.StatsTotalSpentCredits.FormatExt($"{_persistenceManager.StatisticsState.CreditsSpent:N0}"),
            _credifyConfig.Translations.StatsTotalWonCredits.FormatExt($"{_persistenceManager.StatisticsState.CreditsWon:N0}")
        });
    }
}
