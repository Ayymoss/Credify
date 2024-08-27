using Credify.Configuration;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class CreditHelpCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;

    public CreditHelpCommand(CommandConfiguration config, ITranslationLookup layout, CredifyConfiguration credifyConfig) : base(config,
        layout)
    {
        _credifyConfig = credifyConfig;
        Name = "credifyhelp";
        Description = credifyConfig.Translations.Core.CommandHelpDescription;
        Alias = "crhelp";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await gameEvent.Origin.TellAsync([
            _credifyConfig.Translations.Core.HelpHeader,
            _credifyConfig.Translations.Core.HelpStatistics,
            _credifyConfig.Translations.Core.HelpTopCredits,
            _credifyConfig.Translations.Core.HelpPayCredits,
            _credifyConfig.Translations.Core.HelpShop,
            _credifyConfig.Translations.Core.HelpShopInventory,
            _credifyConfig.Translations.Core.HelpShopBuy,
            _credifyConfig.Translations.Core.HelpRaffle,
            _credifyConfig.Translations.Core.HelpGamble
        ]);
    }
}
