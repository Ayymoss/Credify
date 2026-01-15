using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class JoinRouletteCommand : Command
{
    private readonly GameJoinCommandHelper<RouletteManager> _helper;
    private readonly CredifyConfiguration _credifyConfig;

    public JoinRouletteCommand(CommandConfiguration config, ITranslationLookup translationLookup, 
        CredifyConfiguration credifyConfig, PersistenceService persistenceService, RouletteManager roulette,
        ActiveGameTracker gameTracker) 
        : base(config, translationLookup)
    {
        _helper = new GameJoinCommandHelper<RouletteManager>(roulette, credifyConfig, persistenceService, gameTracker);
        _credifyConfig = credifyConfig;
        Name = "credifyroulette";
        Alias = "crrl";
        Description = credifyConfig.Translations.Core.CommandRouletteDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await _helper.ExecuteAsync(
            gameEvent,
            isGameEnabled: _credifyConfig.Roulette.IsEnabled,
            minimumCredits: GameConstants.MinimumCredits,
            disabledMessage: _credifyConfig.Translations.Roulette.Disabled,
            insufficientCreditsMessage: _credifyConfig.Translations.Core.InsufficientCredits);
    }
}
