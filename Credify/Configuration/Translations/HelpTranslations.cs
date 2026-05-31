namespace Credify.Configuration.Translations;

public class HelpTranslations
{
    // @formatter:off
    public string CommandHelpDescription { get; set; } = "Shows Credify user commands";
    public string HelpHeader { get; set; } = "(Color::Accent)--Credify Commands--";
    public string HelpGamble { get; set; } = "[(Color::Yellow)!crbj/!crrl(Color::White)] Gamble your credits";
    public string HelpStatistics { get; set; } = "[(Color::Yellow)!crstats(Color::White)] Check the global credit statistics";
    public string HelpTopCredits { get; set; } = "[(Color::Yellow)!crtop(Color::White)] Check the top credit holders";
    public string HelpRaffle { get; set; } = "[(Color::Yellow)!crraf(Color::White)] Buy a raffle ticket! (Color::Yellow)!crsr (Color::White)to see players!";
    public string HelpPayCredits { get; set; } = "[(Color::Yellow)!crpay(Color::White)] Pay credits to another player";
    public string HelpShop { get; set; } = "[(Color::Yellow)!crshop(Color::White)] Shop for items with your credits";
    public string HelpShopInventory { get; set; } = "[(Color::Yellow)!crinv(Color::White)] Check your bought shop items";
    public string HelpShopBuy { get; set; } = "[(Color::Yellow)!crbuy(Color::White)] Buy a shop item";
    public string HelpAvailableCategories { get; set; } = "(Color::Yellow)Available categories:";
    public string HelpCategoryUsage { get; set; } = "(Color::White)Use (Color::Accent)!crhelp <category> (Color::White)to see commands in a category";
    public string HelpCategoryHeader { get; set; } = "(Color::Accent)--{{category}} Commands--";
    public string HelpUnknownCategory { get; set; } = "(Color::Yellow)Unknown category: (Color::Accent){{category}}";
    // @formatter:on
}
