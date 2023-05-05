using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class CreditCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;

    public CreditCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyConfiguration credifyConfig, PersistenceManager persistenceManager) :
        base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _persistenceManager = persistenceManager;
        Name = "credify";
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

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // Get argument from command.
        var argPlayer = gameEvent.Data;
        gameEvent.Target = gameEvent.Owner.GetClientByName(argPlayer).FirstOrDefault();

        // Check for valid target.
        if (gameEvent.Data.Length != 0 && gameEvent.Target == null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorFindingTargetUser);
            return;
        }

        var credits = await _persistenceManager.GetClientCredits(gameEvent.Origin);

        // Return player's credits
        if (gameEvent.Target != null)
        {
            credits = await _persistenceManager.GetClientCredits(gameEvent.Target);
            gameEvent.Origin.Tell(_credifyConfig.Translations.TargetCredits.FormatExt(gameEvent.Target.Name, $"{credits:N0}"));
            return;
        }

        // If no target specified
        await gameEvent.Origin.TellAsync(new[]
        {
            _credifyConfig.Translations.OriginCredits.FormatExt($"{credits:N0}"),
            _credifyConfig.Translations.ServerBankCredits.FormatExt($"{_persistenceManager.BankCredits:N0}")
        });

    }
}
