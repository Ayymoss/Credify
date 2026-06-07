using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.ThreeCardPoker;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class JoinThreeCardPokerCommand : Command
{
    private readonly GameJoinCommandHelper<ThreeCardPokerGame> _helper;
    private readonly CredifyConfiguration _credifyConfig;

    public JoinThreeCardPokerCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        ThreeCardPokerGame game, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, ActiveGameTracker gameTracker)
        : base(config, translationLookup)
    {
        _helper = new GameJoinCommandHelper<ThreeCardPokerGame>(game, credifyConfig, persistenceService, gameTracker);
        _credifyConfig = credifyConfig;
        Name = "credifythreecardpoker";
        Alias = "crtcp";
        Description = credifyConfig.Translations.ThreeCard.Description;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await _helper.ExecuteAsync(
            gameEvent,
            isGameEnabled: true,
            minimumCredits: GameConstants.MinimumCredits,
            disabledMessage: _credifyConfig.Translations.ThreeCard.Disabled,
            insufficientCreditsMessage: _credifyConfig.Translations.Core.InsufficientCredits,
            handleLeaveSuccessAsync: async ge => { ge.Origin.Tell(_credifyConfig.Translations.ThreeCard.Leave); });
    }
}
