using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class JoinRouletteCommand : BaseGameJoinCommand<RouletteManager>
{
    public JoinRouletteCommand(CommandConfiguration config, ITranslationLookup translationLookup, 
        CredifyConfiguration credifyConfig, PersistenceService persistenceService, RouletteManager roulette) 
        : base(config, translationLookup, roulette, credifyConfig, persistenceService)
    {
        Name = "credifyroulette";
        Alias = "crrl";
        Description = credifyConfig.Translations.Core.CommandRoulette;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    protected override bool IsGameEnabled => CredifyConfig.Roulette.IsEnabled;
    protected override long MinimumCredits => GameConstants.MinimumCredits;
    protected override string DisabledMessage => CredifyConfig.Translations.Roulette.Disabled;
}
