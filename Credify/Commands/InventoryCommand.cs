using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Shop")]
public class InventoryCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceService _persistenceService;

    public InventoryCommand(CommandConfiguration config, ITranslationLookup layout, CredifyConfiguration credifyConfig,
        PersistenceService persistenceService) : base(config, layout)
    {
        _credifyConfig = credifyConfig;
        _persistenceService = persistenceService;
        Name = "credifyinventory";
        Description = credifyConfig.Translations.Shop.InventoryDescription;
        Alias = "crinv";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Player",
                Required = false
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Shop.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Shop.Disabled);
            return;
        }

        var argPlayer = gameEvent.Data;
        if (gameEvent.Data.Length is not 0 && gameEvent.Target is null)
        {
            gameEvent.Target = gameEvent.Owner.GetClientByName(argPlayer).FirstOrDefault();

            if (gameEvent.Target is null)
            {
                gameEvent.Origin.Tell(_credifyConfig.Translations.Economy.ErrorFindingTargetUser);
                return;
            }
        }

        var client = gameEvent.Target ?? gameEvent.Origin;
        var shopItems = await _persistenceService.GetClientShopItemsAsync(client);
        var headerMessage = new List<string>
        {
            _credifyConfig.Translations.Shop.ClientHeader.FormatExt(client.CleanedName)
        };

        var serverItems = _credifyConfig.Shop.Items.Where(x => x.CanBeBought).ToList();
        var userShopMessages = shopItems
            .Select(shopItem =>
            {
                var shopItemName = serverItems
                    .FirstOrDefault(x => x.Id == shopItem.Id)?.Name ?? "Unknown Item";
                return _credifyConfig.Translations.Shop.ItemFormatClient
                    .FormatExt(shopItem.Amount.ToString("N0"), shopItem.Id, shopItemName);
            }).ToList();

        userShopMessages.Add(_credifyConfig.Translations.Help.Shop);
        var shopMessages = headerMessage.Concat(userShopMessages);
        await gameEvent.Origin.TellAsync(shopMessages);
    }
}
