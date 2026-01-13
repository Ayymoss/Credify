using Credify.Chat.Active.Games.Poker;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class JoinPokerCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceService _persistenceService;
    private readonly PokerManager _poker;

    public JoinPokerCommand(CommandConfiguration config, ITranslationLookup translationLookup, 
        CredifyConfiguration credifyConfig, PersistenceService persistenceService, PokerManager poker) 
        : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _persistenceService = persistenceService;
        _poker = poker;
        Name = "credifypoker";
        Alias = "crpk";
        Description = "Join or leave the poker table (Texas Hold'em)";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Poker.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Poker.Disabled);
            return;
        }

        var funds = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        
        if (!_poker.IsPlayerPlaying(gameEvent.Origin))
        {
            if (funds < _credifyConfig.Poker.MinimumBuyIn)
            {
                gameEvent.Origin.Tell(_credifyConfig.Translations.Poker.InsufficientCredits
                    .FormatExt(_credifyConfig.Poker.MinimumBuyIn.ToString("N0")));
                return;
            }

            await _poker.JoinGameAsync(gameEvent.Origin, _credifyConfig.Poker.MinimumBuyIn);
            return;
        }

        await _poker.LeaveGameAsync(gameEvent.Origin);
        gameEvent.Origin.Tell(_credifyConfig.Translations.Poker.LeaveGame);
    }
}
