namespace Credify.Configuration.Translations;

public class ShopTranslations
{
    // @formatter:off
    public string Description { get; set; } = "Shows the shop";
    public string BuyDescription { get; set; } = "Buy an item from the shop";
    public string InventoryDescription { get; set; } = "Shows your inventory";
    public string RecentBuysDescription { get; set; } = "Shows the recent shop buys";
    public string ItemFormat { get; set; } = "[(Color::Accent){{id}} (Color::White)@ (Color::Green)${{price}}(Color::White)] (Color::Yellow){{name}}";
    public string ItemFormatClient { get; set; } = "[(Color::Green){{count}}x (Color::White)of (Color::Accent){{id}}(Color::White)] (Color::Yellow){{name}}";
    public string PurchaseItemFormat { get; set; } = "You can buy an item with (Color::Green)!crbuy <ID>";
    public string ClientHeader { get; set; } = "(Color::Accent)--{{name}} - Shop Items--";
    public string ServerHeader { get; set; } = "(Color::Accent)--Shop Items--";
    public string ItemDoesNotExist { get; set; } = "(Color::Yellow)Item does not exist";
    public string TooManyOfItem { get; set; } = "(Color::Yellow)You already have too many of this item";
    public string BoughtItem { get; set; } = "You bought (Color::Accent){{name}} (Color::White)for (Color::Green)${{price}}";
    public string Disabled { get; set; } = "(Color::Yellow)Shop is disabled. Ask the server owner to enable it";
    public string RecentBuysTitle { get; set; } = "(Color::Accent)--Recent Shop Buys--";
    public string RecentBoughtItemEntry { get; set; } = "[{{index}}](Color::Accent) {{name}} (@{{clientId}}) (Color::White)bought (Color::Accent){{item}} (Color::White){{when}}";
    // @formatter:on
}
