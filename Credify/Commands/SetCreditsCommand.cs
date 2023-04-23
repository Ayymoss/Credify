using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class SetCreditsCommand : Command
{
    private readonly BetLogic _betLogic;
    private readonly CredifyConfiguration _credifyConfig;

    public SetCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetLogic betLogic,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _betLogic = betLogic;
        _credifyConfig = credifyConfig;
        Name = "setcredits";
        Description = credifyConfig.Translations.CommandSetCreditsDescription;
        Alias = "scr";
        Permission = EFClient.Permission.Owner;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Player",
                Required = true
            },
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        };
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        var argStr = gameEvent.Data.Split(" ");

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return Task.CompletedTask;
        }

        gameEvent.Target = gameEvent.Owner.GetClientByName(argStr[0]).FirstOrDefault();

        if (gameEvent.Target == null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorFindingUser);
            return Task.CompletedTask;
        }

        // Check if target isn't null - Set credits, sort, and tell the origin and target.
        if (gameEvent.Target == null) return Task.CompletedTask;

        gameEvent.Target.SetAdditionalProperty(Plugin.CreditsKey, Math.Abs(argAmount));
        gameEvent.Origin.Tell(
            _credifyConfig.Translations.SetCreditsForTarget.FormatExt(gameEvent.Target.Name, $"{Math.Abs(argAmount):N0}"));
        if (gameEvent.Origin.ClientId != gameEvent.Target.ClientId)
            gameEvent.Target.Tell(
                _credifyConfig.Translations.CreditsSetByOrigin.FormatExt(gameEvent.Origin.Name, $"{Math.Abs(argAmount):N0}"));
        _betLogic.OrderTop(gameEvent.Target, Math.Abs(argAmount));

        return Task.CompletedTask;
    }
}
