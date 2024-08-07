using Credify.Configuration;
using Credify.Services;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class CreditCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceService _persistenceService;
    private readonly CredifyCache _cache;

    public CreditCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, CredifyCache cache) :
        base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _persistenceService = persistenceService;
        _cache = cache;
        Name = "credify";
        Description = credifyConfig.Translations.Core.CommandCheckCreditsDescription;
        Alias = "cr";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Player",
                Required = false
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // Get argument from command.
        var argPlayer = gameEvent.Data;
        gameEvent.Target = gameEvent.Owner.GetClientByName(argPlayer).FirstOrDefault();

        // Check for valid target.
        if (gameEvent.Data.Length is not 0 && gameEvent.Target is null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorFindingTargetUser);
            return;
        }

        var credits = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);

        // Return player's credits
        if (gameEvent.Target != null)
        {
            credits = await _persistenceService.GetClientCreditsAsync(gameEvent.Target);
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.TargetCredits.FormatExt(gameEvent.Target.Name, credits.ToString("N0")));
            return;
        }

        // If no target specified
        await gameEvent.Origin.TellAsync(new[]
        {
            _credifyConfig.Translations.Core.OriginCredits.FormatExt(credits.ToString("N0")),
            _credifyConfig.Translations.Core.ServerBankCredits.FormatExt(_cache.BankCredits.ToString("N0"))
        });
    }
}
