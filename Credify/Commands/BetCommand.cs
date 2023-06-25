using Credify.ChatGames.Blackjack;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class BetCommand : Command
{
    private readonly BlackjackMeta _blackjackMeta;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;

    public BetCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        BlackjackMeta blackjackMeta, CredifyConfiguration credifyConfig, PersistenceManager persistenceManager) : base(
        config, translationLookup)
    {
        _blackjackMeta = blackjackMeta;
        _credifyConfig = credifyConfig;
        _persistenceManager = persistenceManager;
        Name = "credifybet";
        Alias = "crbet";
        Description = credifyConfig.Translations.CommandGambleCreditsDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Blackjack.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.BlackjackDisabled);
            return;
        }

        var funds = await _persistenceManager.GetClientCredits(gameEvent.Origin);
        if (funds < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        var player = gameEvent.Origin;
        if (!_blackjackMeta.IsPlayerPlaying(player))
        {
            await _blackjackMeta.JoinGame(gameEvent.Origin);

            if (_credifyConfig.Blackjack.JoinAnnouncements)
            {
                foreach (var server in gameEvent.Origin.CurrentServer.Manager.GetServers())
                {
                    if (server.ConnectedClients.Count is 0) continue;
                    server.Broadcast($"{_credifyConfig.Translations.BlackjackTitle} " +
                                     $"{_credifyConfig.Translations.BlackjackJoinAnnouncement.FormatExt(gameEvent.Origin.CleanedName, _blackjackMeta.GetPlayerCount() - 1)}");
                }
            }
            else
            {
                gameEvent.Origin.Tell($"{_credifyConfig.Translations.BlackjackTitle} " +
                                      $"{_credifyConfig.Translations.BlackjackJoin}");
            }

            return;
        }

        await _blackjackMeta.LeaveGame(gameEvent.Origin);
        gameEvent.Origin.Tell(_credifyConfig.Translations.BlackjackLeave);
    }
}
