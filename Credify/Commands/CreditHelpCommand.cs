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
        Description = credifyConfig.Translations.CommandHelpDescription;
        Alias = "crhelp";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        await gameEvent.Origin.TellAsync(new[]
        {
            _credifyConfig.Translations.HelpHeader,
            _credifyConfig.Translations.HelpHelp,
            _credifyConfig.Translations.HelpStatistics,
            _credifyConfig.Translations.HelpTopCredits,
            _credifyConfig.Translations.HelpPayCredits,
            _credifyConfig.Translations.HelpBetPlayer,
            _credifyConfig.Translations.HelpBetTeam,
            _credifyConfig.Translations.HelpOpenBets,
            _credifyConfig.Translations.HelpClaimBets,
            _credifyConfig.Translations.HelpShop,
            _credifyConfig.Translations.HelpShopInventory,
            _credifyConfig.Translations.HelpShopBuy,
            _credifyConfig.Translations.HelpLotto,
            _credifyConfig.Translations.HelpGamble
        });
    }
}
