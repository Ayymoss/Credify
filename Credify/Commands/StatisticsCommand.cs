using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class StatisticsCommand : Command
{
    private readonly BetLogic _betLogic;
    private readonly CredifyConfiguration _credifyConfig;

    public StatisticsCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetLogic betLogic,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _betLogic = betLogic;
        _credifyConfig = credifyConfig;
        Name = "creditstats";
        Description = credifyConfig.Translations.CommandStatisticsDescription;
        Alias = "statscr";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await gameEvent.Origin.TellAsync(new[]
        {
            _credifyConfig.Translations.CreditStatisticsTitle,
            _credifyConfig.Translations.TotalEarnedCredits.FormatExt(_betLogic.StatisticsState.CreditsEarned),
            _credifyConfig.Translations.TotalSpentCredits.FormatExt(_betLogic.StatisticsState.CreditsSpent),
            _credifyConfig.Translations.TotalWonCredits.FormatExt(_betLogic.StatisticsState.CreditsPaid)
        });
    }
}
