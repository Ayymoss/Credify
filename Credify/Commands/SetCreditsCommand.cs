using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class SetCreditsCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public SetCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceManager persistenceManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "credifysetcredits";
        Description = credifyConfig.Translations.CommandSetCreditsDescription;
        Alias = "crset";
        Permission = EFClient.Permission.Owner;
        RequiresTarget = true;
        Arguments = new[]
        {
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
        };
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var amount = gameEvent.Data;

        if (!long.TryParse(amount, out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return;
        }

        gameEvent.Target.SetAdditionalProperty(Plugin.CreditsKey, Math.Abs(argAmount));
        gameEvent.Origin.Tell(_credifyConfig.Translations.SetCreditsForTarget
            .FormatExt(gameEvent.Target.Name, $"{Math.Abs(argAmount):N0}"));
        if (gameEvent.Origin.ClientId != gameEvent.Target.ClientId)
            gameEvent.Target.Tell(_credifyConfig.Translations.CreditsSetByOrigin
                .FormatExt(gameEvent.Origin.Name, $"{Math.Abs(argAmount):N0}"));
        _persistenceManager.OrderTop(gameEvent.Target, Math.Abs(argAmount));
        await _persistenceManager.WriteClientCredits(gameEvent.Target, argAmount);
    }
}
