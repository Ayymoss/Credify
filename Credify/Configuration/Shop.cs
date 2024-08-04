using Credify.Models;

namespace Credify.Configuration;

public class Shop
{
    public bool IsEnabled { get; set; } = false;

    public List<ServerShopItem> Items { get; set; } =
    [
        new ServerShopItem
        {
            Id = 0,
            Name = "EXAMPLE Discord Role",
            Cost = 10_000,
            MaxPurchaseAmount = 1,
            CanBeBought = true
        },

        new ServerShopItem
        {
            Id = 1,
            Name = "EXAMPLE Free Month Minecraft Server",
            Cost = 1_000_000,
            MaxPurchaseAmount = 10,
            CanBeBought = true
        }
    ];
}
