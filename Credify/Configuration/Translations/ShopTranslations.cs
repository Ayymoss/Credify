namespace Credify.Configuration.Translations;

public class ShopTranslations
{
    // @formatter:off
    public string ShopItemFormat { get; set; } = "[(Color::Accent){{id}} (Color::White)@ (Color::Green)${{price}}(Color::White)] (Color::Yellow){{name}}";
    public string ShopItemFormatClient { get; set; } = "[(Color::Green){{count}}x (Color::White)of (Color::Accent){{id}}(Color::White)] (Color::Yellow){{name}}";
    public string CommandShopDescription { get; set; } = "Shows the shop";
    public string PurchaseShopItemFormat { get; set; } = "You can buy an item with (Color::Green)!crbuy <ID>";
    public string ShopClientHeader { get; set; } = "(Color::Accent)--{{name}} - Shop Items--";
    public string ShopServerHeader { get; set; } = "(Color::Accent)--Shop Items--";
    public string ItemDoesNotExist { get; set; } = "(Color::Yellow)Item does not exist";
    public string TooManyOfItem { get; set; } = "(Color::Yellow)You already have too many of this item";
    public string CommandBuyDescription { get; set; } = "Buy an item from the shop";
    public string CommandInventoryDescription { get; set; } = "Shows your inventory";
    public string BoughtItem { get; set; } = "You bought (Color::Accent){{name}} (Color::White)for (Color::Green)${{price}}";
    public string ShopDisabled { get; set; } = "(Color::Yellow)Shop is disabled. Ask the server owner to enable it";
    public string CommandRecentBuysDescription { get; set; } = "Shows the recent shop buys";
    public string RecentBuysTitle { get; set; } = "(Color::Accent)--Recent Shop Buys--";
    public string RecentBoughtItemEntry { get; set; } = "[{{index}}](Color::Accent) {{name}} (@{{clientId}}) (Color::White)bought (Color::Accent){{item}} (Color::White){{when}}";
    // @formatter:on
}
