using Credify.Chat.Active.Blackjack;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class JoinBlackjackCommand : Command
{
    private readonly BlackjackManager _blackjackManager;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceService _persistenceService;

    public JoinBlackjackCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        BlackjackManager blackjackManager, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService) : base(config, translationLookup)
    {
        _blackjackManager = blackjackManager;
        _credifyConfig = credifyConfig;
        _persistenceService = persistenceService;
        Name = "credifyblackjack";
        Alias = "crbj";
        Description = credifyConfig.Translations.Core.CommandBlackjack;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Blackjack.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Blackjack.Disabled);
            return;
        }

        var funds = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        if (funds < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        var player = gameEvent.Origin;
        if (!_blackjackManager.IsPlayerPlaying(player))
        {
            await _blackjackManager.JoinGameAsync(gameEvent.Origin);

            if (_credifyConfig.Blackjack.JoinAnnouncements)
            {
                foreach (var server in gameEvent.Origin.CurrentServer.Manager.GetServers())
                {
                    if (server.ConnectedClients.Count is 0) continue;
                    server.Broadcast($"{_credifyConfig.Translations.Blackjack.Title} " +
                                     $"{_credifyConfig.Translations.Blackjack.JoinAnnouncement.FormatExt(gameEvent.Origin.CleanedName, _blackjackManager.GetPlayerCount() - 1)}");
                }
            }
            else
            {
                gameEvent.Origin.Tell($"{_credifyConfig.Translations.Blackjack.Title} " +
                                      $"{_credifyConfig.Translations.Blackjack.Join}");
            }

            return;
        }

        await _blackjackManager.LeaveGameAsync(gameEvent.Origin);
        gameEvent.Origin.Tell(_credifyConfig.Translations.Blackjack.Leave);
    }
}
