using Credify.Constants;
using Credify.Models;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

/// <summary>
/// Service responsible for managing shop item persistence.
/// </summary>
public class ShopPersistenceService(
    IMetaServiceV2 metaService)
{
    /// <summary>
    /// Reads client shop items from persistent storage.
    /// </summary>
    private async Task<List<ClientShopItem>> ReadClientShopItemsAsync(EFClient client)
    {
        var shopItems = await metaService.GetPersistentMetaValue<List<ClientShopItem>>(PluginConstants.ShopKey, client.ClientId) ?? [];
        client.SetAdditionalProperty(PluginConstants.ShopKey, shopItems);
        return shopItems;
    }

    /// <summary>
    /// Gets client shop items, using cache if available.
    /// </summary>
    public async Task<List<ClientShopItem>> GetClientShopItemsAsync(EFClient client)
    {
        List<ClientShopItem> userCredits;
        if (client.IsIngame)
        {
            userCredits = client.GetAdditionalProperty<List<ClientShopItem>>(PluginConstants.ShopKey);
            return userCredits;
        }

        userCredits = await ReadClientShopItemsAsync(client);
        return userCredits;
    }

    /// <summary>
    /// Writes client shop items to persistent storage.
    /// </summary>
    public async Task WriteClientShopAsync(EFClient client, List<ClientShopItem> shopItems)
    {
        await metaService.SetPersistentMetaValue(PluginConstants.ShopKey, shopItems, client.ClientId);
    }

    /// <summary>
    /// Loads client shop items on join.
    /// </summary>
    public async Task LoadShopItemsOnJoinAsync(EFClient client)
    {
        await ReadClientShopItemsAsync(client);
    }

    /// <summary>
    /// Writes a recently bought item to persistent storage.
    /// </summary>
    public async Task WriteRecentBoughtItemsAsync(ClientShopContext item)
    {
        var items = await ReadRecentBoughtItemsAsync();
        var ordered = items.OrderByDescending(x => x.Bought)
            .Take(9)
            .ToList();

        ordered.Add(item);
        await metaService.SetPersistentMetaValue(PluginConstants.RecentBoughtItems, ordered);
    }

    /// <summary>
    /// Reads recently bought items from persistent storage.
    /// </summary>
    public async Task<IEnumerable<ClientShopContext>> ReadRecentBoughtItemsAsync()
    {
        return await metaService.GetPersistentMetaValue<List<ClientShopContext>>(PluginConstants.RecentBoughtItems) ?? [];
    }
}
