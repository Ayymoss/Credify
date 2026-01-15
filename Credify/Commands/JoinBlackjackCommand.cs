using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class JoinBlackjackCommand : Command
{
    private readonly GameJoinCommandHelper<BlackjackManager> _helper;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly BlackjackManager _blackjackManager;

    public JoinBlackjackCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        BlackjackManager blackjackManager, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService, ActiveGameTracker gameTracker) 
        : base(config, translationLookup)
    {
        _helper = new GameJoinCommandHelper<BlackjackManager>(blackjackManager, credifyConfig, persistenceService, gameTracker);
        _credifyConfig = credifyConfig;
        _blackjackManager = blackjackManager;
        Name = "credifyblackjack";
        Alias = "crbj";
        Description = credifyConfig.Translations.Core.CommandBlackjackDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await _helper.ExecuteAsync(
            gameEvent,
            isGameEnabled: _credifyConfig.Blackjack.IsEnabled,
            minimumCredits: GameConstants.MinimumCredits,
            disabledMessage: _credifyConfig.Translations.Blackjack.Disabled,
            insufficientCreditsMessage: _credifyConfig.Translations.Core.InsufficientCredits,
            handleJoinSuccessAsync: async (ge) =>
            {
                if (_credifyConfig.Blackjack.JoinAnnouncements)
                {
                    foreach (var server in ge.Origin.CurrentServer.Manager.GetServers())
                    {
                        if (server.ConnectedClients.Count is 0) continue;
                        server.Broadcast($"{_credifyConfig.Translations.Blackjack.Title} " +
                                         $"{_credifyConfig.Translations.Blackjack.JoinAnnouncement.FormatExt(ge.Origin.CleanedName, _blackjackManager.GetPlayerCount() - 1)}");
                    }
                }
                else
                {
                    ge.Origin.Tell($"{_credifyConfig.Translations.Blackjack.Title} " +
                                  $"{_credifyConfig.Translations.Blackjack.Join}");
                }
            },
            handleLeaveSuccessAsync: async (ge) =>
            {
                ge.Origin.Tell(_credifyConfig.Translations.Blackjack.Leave);
            });
    }
}
