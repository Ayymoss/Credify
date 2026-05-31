using Credify.Commands.Attributes;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Shop")]
public class ShopCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;

    public ShopCommand(CommandConfiguration config, ITranslationLookup layout, CredifyConfiguration credifyConfig) :
        base(config, layout)
    {
        _credifyConfig = credifyConfig;
        Name = "credifyshop";
        Description = credifyConfig.Translations.Shop.Description;
        Alias = "crshop";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Shop.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Shop.Disabled);
            return;
        }

        var headerMessage = new List<string>
        {
            _credifyConfig.Translations.Shop.ServerHeader
        };

        var shopItems = _credifyConfig.Shop.Items
            .Where(x => x.CanBeBought)
            .Select(shopItem => _credifyConfig.Translations.Shop.ItemFormat
                .FormatExt(shopItem.Id, shopItem.Cost.ToString("N0"), shopItem.Name)).ToList();

        shopItems.Add(_credifyConfig.Translations.Shop.PurchaseItemFormat);
        var shopMessages = headerMessage.Concat(shopItems);
        await gameEvent.Origin.TellAsync(shopMessages);
    }
}
