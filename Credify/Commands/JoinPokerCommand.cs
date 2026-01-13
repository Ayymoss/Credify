using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;

namespace Credify.Commands;

public class JoinPokerCommand : Command
{
    private readonly GameJoinCommandHelper<PokerManager> _helper;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PokerManager _poker;

    public JoinPokerCommand(CommandConfiguration config, ITranslationLookup translationLookup, 
        CredifyConfiguration credifyConfig, PersistenceService persistenceService, PokerManager poker,
        ActiveGameTracker gameTracker) 
        : base(config, translationLookup)
    {
        _helper = new GameJoinCommandHelper<PokerManager>(poker, credifyConfig, persistenceService, gameTracker);
        _credifyConfig = credifyConfig;
        _poker = poker;
        Name = "credifypoker";
        Alias = "crpk";
        Description = "Join or leave the poker table (Texas Hold'em)";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var minimumBuyIn = _credifyConfig.Poker.MinimumBuyIn;
        await _helper.ExecuteAsync(
            gameEvent,
            isGameEnabled: _credifyConfig.Poker.IsEnabled,
            minimumCredits: minimumBuyIn,
            disabledMessage: _credifyConfig.Translations.Poker.Disabled,
            insufficientCreditsMessage: _credifyConfig.Translations.Poker.InsufficientCredits.FormatExt(minimumBuyIn.ToString("N0")),
            customJoinAsync: async (player) => await _poker.JoinGameAsync(player, minimumBuyIn),
            handleLeaveSuccessAsync: async (ge) =>
            {
                ge.Origin.Tell(_credifyConfig.Translations.Poker.LeaveGame);
            });
    }
}
