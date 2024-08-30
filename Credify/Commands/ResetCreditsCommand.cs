using Credify.Configuration;
using Credify.Services;
using Data.Abstractions;
using Data.Context;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class ResetCreditsCommand : Command
{
    private readonly IDatabaseContextFactory _context;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceService _persistenceService;

    public ResetCreditsCommand(CommandConfiguration config, IDatabaseContextFactory context,
        ITranslationLookup translationLookup, CredifyConfiguration credifyConfig, PersistenceService persistenceService)
        : base(config, translationLookup)
    {
        _context = context;
        _credifyConfig = credifyConfig;
        _persistenceService = persistenceService;
        Name = "credifyresetcredits";
        Description = credifyConfig.Translations.Core.CommandResetCreditsDescription;
        Alias = "crreset";
        Permission = EFClient.Permission.Owner;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "IW4MAdminConfiguration -> Id",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var configId = gameEvent.Origin.CurrentServer.Manager.GetApplicationSettings().Configuration().Id;
        if (!gameEvent.Data.Equals(configId))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.PassIdAsArgument);
            return;
        }

        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ResettingCreditsInit);
        await using var context = _context.CreateContext();
        await ResetMetaItems(context, Plugin.CreditsAmount, _credifyConfig.Translations.Core.ResettingCredits, gameEvent);
        await ResetMetaItems(context, Plugin.RaffleKey, _credifyConfig.Translations.Core.ResettingRaffleTickets,
            gameEvent);
        await ResetMetaItems(context, Plugin.ShopKey, _credifyConfig.Translations.Core.ResettingShopItems, gameEvent);
        await context.SaveChangesAsync();

        ResetOnlinePlayersAdditional(gameEvent.Origin.CurrentServer.Manager);
        await ResetAndWriteStats(gameEvent);
        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ResetCreditsComplete);
    }

    private static void ResetOnlinePlayersAdditional(IManager manager)
    {
        var clients = manager.GetActiveClients();
        foreach (var client in clients)
        {
            client.SetAdditionalProperty(Plugin.CreditsAmount, 0L);
        }
    }

    // TODO: This should be in persistence? 
    private static async Task ResetMetaItems(DatabaseContext context, string key, string translation, GameEvent gameEvent)
    {
        var items = await context.EFMeta.Where(m => m.Key == key).ToListAsync();
        context.EFMeta.RemoveRange(items);
        var message = translation.FormatExt(items.Count.ToString("N0"));
        gameEvent.Origin.Tell(message);
    }

    private async Task ResetAndWriteStats(GameEvent gameEvent)
    {
        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ResettingTopStats);
        _persistenceService.ResetTop();
        await _persistenceService.WriteTopScoreAsync();

        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ResettingStatistics);
        _persistenceService.ResetStatistics();
        await _persistenceService.WriteStatisticsAsync();

        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ResettingBank);
        _persistenceService.ResetBank();
        await _persistenceService.WriteBankCreditsAsync();
    }
}
