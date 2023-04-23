using Data.Abstractions;
using Data.Models.Client;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class TopCreditsCommand : Command
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly BetLogic _betLogic;
    private readonly CredifyConfiguration _credifyConfig;

    public TopCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        IDatabaseContextFactory contextFactory, BetLogic betLogic, CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _contextFactory = contextFactory;
        _betLogic = betLogic;
        _credifyConfig = credifyConfig;
        Name = "topcredits";
        Alias = "tcr";
        Description = credifyConfig.Translations.CommandTopCreditsDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // If user requests top and there are no entries.
        if (!_betLogic.TopCredits.Any())
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.NoOneHasCreditsForTop);
            return;
        }

        gameEvent.Origin.Tell(_credifyConfig.Translations.TopCreditsTitle);

        // Get top credits, format for returning.
        await using var context = _contextFactory.CreateContext(false);
        var names = await context.Clients
            .Where(client => _betLogic.TopCredits.Select(credit => credit.ClientId).Contains(client.ClientId))
            .Select(client => new {client.ClientId, client.CurrentAlias.Name})
            .ToDictionaryAsync(selector => selector.ClientId, selector => selector.Name);

        var output = _betLogic.TopCredits.OrderByDescending(entry => entry.Credits).Select((creditEntry, index) =>
            _credifyConfig.Translations.TopPlayerEntry.FormatExt(index + 1, names[creditEntry.ClientId], $"{creditEntry.Credits:N0}"));

        await gameEvent.Origin.TellAsync(output);
    }
}
