using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.CasinoHoldem;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class JoinCasinoHoldemCommand : Command
{
    private readonly GameJoinCommandHelper<CasinoHoldemGame> _helper;
    private readonly CredifyConfiguration _credifyConfig;

    public JoinCasinoHoldemCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        CasinoHoldemGame game, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, ActiveGameTracker gameTracker)
        : base(config, translationLookup)
    {
        _helper = new GameJoinCommandHelper<CasinoHoldemGame>(game, credifyConfig, persistenceService, gameTracker);
        _credifyConfig = credifyConfig;
        Name = "credifyholdem";
        Alias = "crholdem";
        Description = credifyConfig.Translations.CasinoHoldem.Description;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await _helper.ExecuteAsync(
            gameEvent,
            isGameEnabled: true,
            minimumCredits: GameConstants.MinimumCredits,
            disabledMessage: _credifyConfig.Translations.CasinoHoldem.Disabled,
            insufficientCreditsMessage: _credifyConfig.Translations.Core.InsufficientCredits,
            handleLeaveSuccessAsync: async ge => { ge.Origin.Tell(_credifyConfig.Translations.CasinoHoldem.Leave); });
    }
}
