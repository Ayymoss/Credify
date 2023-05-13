using Credify.Models;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class BuyCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public BuyCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceManager persistenceManager, CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "credifybuy";
        Alias = "crbuy";
        Description = credifyConfig.Translations.CommandBuyDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Item ID",
                Required = true
            }
        };
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Shop.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ShopDisabled);
            return;
        }

        var itemArg = gameEvent.Data;
        if (!int.TryParse(itemArg, out var itemId))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return;
        }

        var clientItems = await _persistenceManager.GetClientShopItems(gameEvent.Origin);
        var serverItems = _credifyConfig.Shop.Items.Where(x => x.CanBeBought).ToList();

        // Check if item exists
        if (serverItems.FirstOrDefault(x => x.Id == itemId) is not { } serverItem)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ItemDoesNotExist);
            return;
        }

        // Check if client has enough credits
        var initialClientCredits = await _persistenceManager.GetClientCredits(gameEvent.Origin);
        if (initialClientCredits < serverItem.Cost)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        // Check if client too many of the item
        var clientItem = clientItems.FirstOrDefault(x => x.Id == itemId);
        if (clientItem?.Amount >= serverItem.MaxPurchaseAmount)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.TooManyOfItem);
            return;
        }

        // Buy item
        if (clientItem is null)
        {
            clientItem = new ClientShopItem
            {
                Id = itemId,
                Amount = 1
            };
            clientItems.Add(clientItem);
        }
        else
        {
            clientItem.Amount++;
        }

        await _persistenceManager.WriteRecentBoughtItems(new ClientShopContext
        {
            Id = clientItem.Id,
            Amount = clientItem.Amount,
            ClientId = gameEvent.Origin.ClientId,
            ClientName = gameEvent.Origin.CleanedName,
            Bought = DateTimeOffset.UtcNow
        });

        _persistenceManager.StatisticsState.CreditsSpent += (ulong)serverItem.Cost;
        await _persistenceManager.AlterClientCredits(-serverItem.Cost, client: gameEvent.Origin);
        await _persistenceManager.WriteClientShopAsync(gameEvent.Origin, clientItems);
        gameEvent.Origin.Tell(_credifyConfig.Translations.BoughtItem
            .FormatExt(serverItem.Name, $"{serverItem.Cost:N0}"));
    }
}
