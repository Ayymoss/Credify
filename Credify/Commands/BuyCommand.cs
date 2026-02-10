using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Models;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Shop")]
public class BuyCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;

    public BuyCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        Name = "credifybuy";
        Alias = "crbuy";
        Description = credifyConfig.Translations.Core.CommandBuyDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Item ID",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Shop.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ShopDisabled);
            return;
        }

        var itemArg = gameEvent.Data;
        if (!int.TryParse(itemArg, out var itemId))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingSecondArgument);
            return;
        }

        var clientItems = await _persistenceService.GetClientShopItemsAsync(gameEvent.Origin);
        var serverItems = _credifyConfig.Shop.Items.Where(x => x.CanBeBought).ToList();

        // Check if item exists
        if (serverItems.FirstOrDefault(x => x.Id == itemId) is not { } serverItem)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ItemDoesNotExist);
            return;
        }

        // Check if client has enough credits
        var initialClientCredits = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        if (initialClientCredits < serverItem.Cost)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        // Check if client too many of the item
        var clientItem = clientItems.FirstOrDefault(x => x.Id == itemId);
        if (clientItem?.Amount >= serverItem.MaxPurchaseAmount)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.TooManyOfItem);
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

        await _persistenceService.WriteRecentBoughtItemsAsync(new ClientShopContext
        {
            Id = clientItem.Id,
            Amount = clientItem.Amount,
            ClientId = gameEvent.Origin.ClientId,
            ClientName = gameEvent.Origin.CleanedName,
            Bought = DateTimeOffset.UtcNow
        });

        await _persistenceService.RemoveCreditsAsync(gameEvent.Origin, serverItem.Cost);
        await _persistenceService.WriteClientShopAsync(gameEvent.Origin, clientItems);
        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BoughtItem
            .FormatExt(serverItem.Name, serverItem.Cost.ToString("N0")));
    }
}
