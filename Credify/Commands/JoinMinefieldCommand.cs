using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Minefield;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class JoinMinefieldCommand : Command
{
    private readonly GameJoinCommandHelper<MinefieldManager> _helper;
    private readonly CredifyConfiguration _credifyConfig;

    public JoinMinefieldCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        MinefieldManager minefieldManager, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, ActiveGameTracker gameTracker)
        : base(config, translationLookup)
    {
        _helper = new GameJoinCommandHelper<MinefieldManager>(minefieldManager, credifyConfig, persistenceService, gameTracker);
        _credifyConfig = credifyConfig;
        Name = "credifyminefield";
        Alias = "crmine";
        Description = credifyConfig.Translations.Core.CommandMinefieldDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await _helper.ExecuteAsync(
            gameEvent,
            isGameEnabled: _credifyConfig.Minefield.IsEnabled,
            minimumCredits: GameConstants.MinimumCredits,
            disabledMessage: _credifyConfig.Translations.Minefield.Disabled,
            insufficientCreditsMessage: _credifyConfig.Translations.Core.InsufficientCredits,
            handleLeaveSuccessAsync: async ge =>
            {
                ge.Origin.Tell(_credifyConfig.Translations.Minefield.Leave);
            });
    }
}
