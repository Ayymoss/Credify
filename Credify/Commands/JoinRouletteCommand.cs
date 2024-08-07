using Credify.Chat.Active.Roulette;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class JoinRouletteCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceService _persistenceService;
    private readonly RouletteManager _roulette;

    public JoinRouletteCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, RouletteManager roulette) : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _persistenceService = persistenceService;
        _roulette = roulette;
        Name = "credifyroulette";
        Alias = "crrl";
        Description = credifyConfig.Translations.Core.CommandRoulette;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Roulette.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Roulette.Disabled);
            return;
        }

        var funds = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        if (funds < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        if (!_roulette.IsPlayerInGame(gameEvent.Origin))
        {
            await _roulette.AddPlayerAsync(gameEvent.Origin);
            return;
        }

        _roulette.RemovePlayer(gameEvent.Origin);
    }
}
