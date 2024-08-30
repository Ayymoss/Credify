using Credify.Configuration;
using Credify.Services;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;

namespace Credify.Commands;

public class CreditCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceService _persistenceService;
    private readonly CredifyCache _cache;
    private readonly ClientService _clientService;

    public CreditCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, CredifyCache cache, ClientService clientService) :
        base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _persistenceService = persistenceService;
        _cache = cache;
        _clientService = clientService;
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
        var argPlayer = gameEvent.Data;

        // Handle ClientID
        if (!string.IsNullOrWhiteSpace(argPlayer) && argPlayer[0] is '@' && int.TryParse(argPlayer[1..], out var clientId))
        {
            gameEvent.Target = await _clientService.Get(clientId);
        }
        else
        {
            gameEvent.Target = gameEvent.Owner.GetClientByName(argPlayer).FirstOrDefault();
        }

        // Handle unknown target
        if (argPlayer.Length is not 0 && gameEvent.Target is null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorFindingTargetUser);
            return;
        }

        // Return target's credits
        long credits;
        if (gameEvent.Target is not null)
        {
            credits = await _persistenceService.GetClientCreditsAsync(gameEvent.Target);
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.TargetCredits.FormatExt(gameEvent.Target.Name, credits.ToString("N0")));
            return;
        }

        // If no target specified
        credits = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        await gameEvent.Origin.TellAsync(
        [
            _credifyConfig.Translations.Core.OriginCredits.FormatExt(credits.ToString("N0")),
            _credifyConfig.Translations.Core.ServerBankCredits.FormatExt(_cache.BankCredits.ToString("N0"))
        ]);
    }
}
