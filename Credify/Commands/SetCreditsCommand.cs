using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

[CommandCategory("Admin")]
public class SetCreditsCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;

    public SetCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        Name = "credifysetcredits";
        Description = credifyConfig.Translations.Core.CommandSetCreditsDescription;
        Alias = "crset";
        Permission = EFClient.Permission.Owner;
        RequiresTarget = true;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Player",
                Required = true
            },
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var amount = gameEvent.Data;

        if (!long.TryParse(amount, out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingSecondArgument);
            return;
        }

        gameEvent.Target.SetAdditionalProperty(PluginConstants.CreditsAmount, Math.Abs(argAmount));
        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.SetCreditsForTarget
            .FormatExt(gameEvent.Target.Name, Math.Abs(argAmount).ToString("N0")));
        if (gameEvent.Origin.ClientId != gameEvent.Target.ClientId)
            gameEvent.Target.Tell(_credifyConfig.Translations.Core.CreditsSetByOrigin
                .FormatExt(gameEvent.Origin.Name, Math.Abs(argAmount).ToString("N0")));
        _persistenceService.OrderTop(gameEvent.Target, Math.Abs(argAmount));
        await _persistenceService.WriteClientCreditsAsync(gameEvent.Target, argAmount);
    }
}
