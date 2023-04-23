using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class OpenBetsCommand : Command
{
    private readonly BetManager _betManager;
    private readonly CredifyConfiguration _credifyConfig;

    public OpenBetsCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetManager betManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _betManager = betManager;
        _credifyConfig = credifyConfig;
        Name = "openbets";
        Description = credifyConfig.Translations.ListAllOpenBets;
        Alias = "ob";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var openBets = _betManager.GetBetsList();
        var openBetsCount = openBets.Count(bet => !bet.BetCompleted);
        if (!openBets.Any() || openBetsCount == 0)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.NoOpenBets);
            return;
        }

        gameEvent.Origin.Tell(_credifyConfig.Translations.OpenBetsTitle);

        var target = string.Empty;
        var index = 0;
        var stringList = new List<string>();
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        foreach (var bet in openBets.Where(srv => srv.Server == clientServerId).Where(betComp => !betComp.BetCompleted))
        {
            if (_betManager.TeamStringToEnum(bet.TargetTeam) == EFClient.TeamType.Allies) target = _credifyConfig.Translations.Allies;
            if (_betManager.TeamStringToEnum(bet.TargetTeam) == EFClient.TeamType.Axis) target = _credifyConfig.Translations.Axis;
            if (bet.TargetPlayer != null) target = _credifyConfig.Translations.BetTargetPlayer.FormatExt(bet.TargetPlayer.CleanedName);

            stringList.Add(
                _credifyConfig.Translations.BetEntry.FormatExt(index + 1, bet.Origin.CleanedName, target, $"{bet.InitAmount:N0}"));
            index++;
        }

        await gameEvent.Origin.TellAsync(stringList);
    }
}
