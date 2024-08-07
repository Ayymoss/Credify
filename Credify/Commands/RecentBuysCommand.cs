using Credify.Configuration;
using Credify.Services;
using Data.Models.Client;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class RecentBuysCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;

    public RecentBuysCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        Name = "credifyrecentbuys";
        Alias = "crrb";
        Description = credifyConfig.Translations.Core.CommandRecentBuysDescription;
        Permission = EFClient.Permission.Administrator;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var serverItems = _credifyConfig.Shop.Items;
        var recentBuys = await _persistenceService.ReadRecentBoughtItemsAsync();

        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.RecentBuysTitle);

        var output = recentBuys.OrderByDescending(entry => entry.Bought)
            .Select((buyer, index) => _credifyConfig.Translations.Core.RecentBoughtItemEntry
                .FormatExt(index + 1, buyer.ClientName, buyer.ClientId, serverItems.First(x => x.Id == buyer.Id).Name,
                    buyer.Bought.Humanize()));

        await gameEvent.Origin.TellAsync(output);
    }
}
