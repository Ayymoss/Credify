using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class JoinBlackjackCommand : BaseGameJoinCommand<BlackjackManager>
{
    public JoinBlackjackCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        BlackjackManager blackjackManager, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService) 
        : base(config, translationLookup, blackjackManager, credifyConfig, persistenceService)
    {
        Name = "credifyblackjack";
        Alias = "crbj";
        Description = credifyConfig.Translations.Core.CommandBlackjack;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    protected override bool IsGameEnabled => CredifyConfig.Blackjack.IsEnabled;
    protected override long MinimumCredits => GameConstants.MinimumCredits;
    protected override string DisabledMessage => CredifyConfig.Translations.Blackjack.Disabled;

    protected override async Task HandleJoinSuccessAsync(GameEvent gameEvent)
    {
        if (CredifyConfig.Blackjack.JoinAnnouncements)
        {
            foreach (var server in gameEvent.Origin.CurrentServer.Manager.GetServers())
            {
                if (server.ConnectedClients.Count is 0) continue;
                server.Broadcast($"{CredifyConfig.Translations.Blackjack.Title} " +
                                 $"{CredifyConfig.Translations.Blackjack.JoinAnnouncement.FormatExt(gameEvent.Origin.CleanedName, GameManager.GetPlayerCount() - 1)}");
            }
        }
        else
        {
            gameEvent.Origin.Tell($"{CredifyConfig.Translations.Blackjack.Title} " +
                                  $"{CredifyConfig.Translations.Blackjack.Join}");
        }
    }

    protected override async Task HandleLeaveSuccessAsync(GameEvent gameEvent)
    {
        gameEvent.Origin.Tell(CredifyConfig.Translations.Blackjack.Leave);
    }
}
