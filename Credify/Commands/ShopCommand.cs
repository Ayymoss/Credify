using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class ShopCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;

    public ShopCommand(CommandConfiguration config, ITranslationLookup layout, CredifyConfiguration credifyConfig) :
        base(config, layout)
    {
        _credifyConfig = credifyConfig;
        Name = "credifyshop";
        Description = credifyConfig.Translations.CommandShopDescription;
        Alias = "crshop";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Shop.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ShopDisabled);
            return;
        }

        var headerMessage = new List<string>
        {
            _credifyConfig.Translations.ShopServerHeader
        };

        var shopItems = _credifyConfig.Shop.Items
            .Where(x => x.CanBeBought)
            .Select(shopItem => _credifyConfig.Translations.ShopItemFormat
                .FormatExt(shopItem.Id, $"{shopItem.Cost:N0}", shopItem.Name)).ToList();

        shopItems.Add(_credifyConfig.Translations.PurchaseShopItemFormat);
        var shopMessages = headerMessage.Concat(shopItems);
        await gameEvent.Origin.TellAsync(shopMessages);
    }
}
