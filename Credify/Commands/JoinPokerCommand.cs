using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;

namespace Credify.Commands;

public class JoinPokerCommand : BaseGameJoinCommand<PokerManager>
{
    public JoinPokerCommand(CommandConfiguration config, ITranslationLookup translationLookup, 
        CredifyConfiguration credifyConfig, PersistenceService persistenceService, PokerManager poker) 
        : base(config, translationLookup, poker, credifyConfig, persistenceService)
    {
        Name = "credifypoker";
        Alias = "crpk";
        Description = "Join or leave the poker table (Texas Hold'em)";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    protected override bool IsGameEnabled => CredifyConfig.Poker.IsEnabled;
    protected override long MinimumCredits => CredifyConfig.Poker.MinimumBuyIn;
    protected override string DisabledMessage => CredifyConfig.Translations.Poker.Disabled;
    protected override string InsufficientCreditsMessage => 
        CredifyConfig.Translations.Poker.InsufficientCredits.FormatExt(MinimumCredits.ToString("N0"));

    protected override Task JoinGameAsync(EFClient player)
    {
        // Poker requires a buy-in amount
        return GameManager.JoinGameAsync(player, MinimumCredits);
    }

    protected override Task HandleLeaveSuccessAsync(GameEvent gameEvent)
    {
        gameEvent.Origin.Tell(CredifyConfig.Translations.Poker.LeaveGame);
        return Task.CompletedTask;
    }
}
