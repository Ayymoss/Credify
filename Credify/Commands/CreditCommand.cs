using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class CreditCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;

    public CreditCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        Name = "credits";
        Description = credifyConfig.Translations.CommandCheckCreditsDescription;
        Alias = "cr";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Player",
                Required = false
            }
        };
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        // Get argument from command.
        var argPlayer = gameEvent.Data;
        gameEvent.Target = gameEvent.Owner.GetClientByName(argPlayer).FirstOrDefault();

        // Check for valid target.
        if (gameEvent.Data.Length != 0 && gameEvent.Target == null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorFindingTargetUser);
            return Task.CompletedTask;
        }

        // Return player's credits
        if (gameEvent.Target != null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.TargetCredits.FormatExt(gameEvent.Target.Name,
                $"{gameEvent.Target.GetAdditionalProperty<int>(Plugin.CreditsKey):N0}"));
            return Task.CompletedTask;
        }

        // If no target specified
        gameEvent.Origin.Tell(_credifyConfig.Translations.OriginCredits
            .FormatExt($"{gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey):N0}"));
        return Task.CompletedTask;
    }
}
