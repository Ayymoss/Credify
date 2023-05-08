using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class InventoryCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;

    public InventoryCommand(CommandConfiguration config, ITranslationLookup layout, CredifyConfiguration credifyConfig,
        PersistenceManager persistenceManager)
        : base(config,
            layout)
    {
        _credifyConfig = credifyConfig;
        _persistenceManager = persistenceManager;
        Name = "credifyinventory";
        Description = credifyConfig.Translations.CommandInventoryDescription;
        Alias = "crinv";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Player",
                Required = false
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

        var argPlayer = gameEvent.Data;
        if (gameEvent.Data.Length != 0 && gameEvent.Target == null)
        {
            gameEvent.Target = gameEvent.Owner.GetClientByName(argPlayer).FirstOrDefault();

            if (gameEvent.Target is null)
            {
                gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorFindingTargetUser);
                return;
            }
        }

        var client = gameEvent.Target ?? gameEvent.Origin;
        var shopItems = await _persistenceManager.GetClientShopItems(client);
        var headerMessage = new List<string>
        {
            _credifyConfig.Translations.ShopClientHeader.FormatExt(client.CleanedName)
        };

        var serverItems = _credifyConfig.Shop.Items.Where(x => x.CanBeBought).ToList();
        var userShopMessages = shopItems
            .Select(shopItem =>
            {
                var shopItemName = serverItems
                    .FirstOrDefault(x => x.Id == shopItem.Id)?.Name ?? "Unknown Item";
                return _credifyConfig.Translations.ShopItemFormatClient
                    .FormatExt($"{shopItem.Amount:N0}", shopItem.Id, shopItemName);
            }).ToList();

        userShopMessages.Add(_credifyConfig.Translations.HelpShop);
        var shopMessages = headerMessage.Concat(userShopMessages);
        await gameEvent.Origin.TellAsync(shopMessages);
    }
}
