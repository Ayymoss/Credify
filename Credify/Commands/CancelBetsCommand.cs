using Data.Models.Client;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class CancelBetsCommand : Command
{
    private readonly BetManager _betManager;
    private readonly CredifyConfiguration _credifyConfig;

    public CancelBetsCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetManager betManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _betManager = betManager;
        _credifyConfig = credifyConfig;
        Name = "cancelbets";
        Description = credifyConfig.Translations.CommandCancelOpenBetsDescription;
        Alias = "cnclb";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_betManager.MaximumTimePassed(gameEvent.Origin))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.BetsOnlyAcceptedDuringWindow
                .FormatExt(_betManager.CreditsBetWindow.Humanize()));
            return Task.CompletedTask;
        }

        var cancelledBets = _betManager.CancelBets(gameEvent.Origin);
        if (cancelledBets == 0)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.NoBetsToCancel);
            return Task.CompletedTask;
        }

        gameEvent.Origin.Tell(_credifyConfig.Translations.BetsCancelled.FormatExt(cancelledBets));
        return Task.CompletedTask;
    }
}
