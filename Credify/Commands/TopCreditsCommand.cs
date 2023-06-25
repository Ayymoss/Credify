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
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public TopCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        IDatabaseContextFactory contextFactory, PersistenceManager persistenceManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _contextFactory = contextFactory;
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "credifytop";
        Alias = "crtop";
        Description = credifyConfig.Translations.CommandTopCreditsDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        // If user requests top and there are no entries.
        if (!_persistenceManager.TopCredits.Any())
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.NoOneHasCreditsForTop);
            return;
        }

        gameEvent.Origin.Tell(_credifyConfig.Translations.TopCreditsTitle);

        await using var context = _contextFactory.CreateContext(false);
        var names = await context.Clients
            .Where(client => _persistenceManager.TopCredits
                .Select(credit => credit.ClientId)
                .Contains(client.ClientId))
            .Select(client => new {client.ClientId, client.CurrentAlias.Name})
            .ToDictionaryAsync(selector => selector.ClientId, selector => selector.Name);

        var output = _persistenceManager.TopCredits
            .OrderByDescending(entry => entry.Credits)
            .Select((creditEntry, index) => _credifyConfig.Translations.TopPlayerEntry
                .FormatExt(index + 1, $"{creditEntry.Credits:N0}", names[creditEntry.ClientId]));

        await gameEvent.Origin.TellAsync(output);
    }
}
