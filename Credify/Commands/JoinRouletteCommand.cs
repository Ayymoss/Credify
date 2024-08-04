using Credify.Chat.Active.Roulette;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class JoinRouletteCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;
    private readonly TableManager _roulette;

    public JoinRouletteCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyConfiguration credifyConfig,
        PersistenceManager persistenceManager, TableManager roulette) : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _persistenceManager = persistenceManager;
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

        var funds = await _persistenceManager.GetClientCreditsAsync(gameEvent.Origin);
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
