namespace Credify.Configuration.Translations;

public class HelpTranslations
{
    // @formatter:off
    public string Description { get; set; } = "Shows Credify user commands";
    public string Header { get; set; } = "(Color::Accent)--Credify Commands--";
    public string Gamble { get; set; } = "[(Color::Yellow)!crbj/!crrl(Color::White)] Gamble your credits";
    public string Statistics { get; set; } = "[(Color::Yellow)!crstats(Color::White)] Check the global credit statistics";
    public string TopCredits { get; set; } = "[(Color::Yellow)!crtop(Color::White)] Check the top credit holders";
    public string Raffle { get; set; } = "[(Color::Yellow)!crraf(Color::White)] Buy a raffle ticket! (Color::Yellow)!crsr (Color::White)to see players!";
    public string PayCredits { get; set; } = "[(Color::Yellow)!crpay(Color::White)] Pay credits to another player";
    public string Shop { get; set; } = "[(Color::Yellow)!crshop(Color::White)] Shop for items with your credits";
    public string ShopInventory { get; set; } = "[(Color::Yellow)!crinv(Color::White)] Check your bought shop items";
    public string ShopBuy { get; set; } = "[(Color::Yellow)!crbuy(Color::White)] Buy a shop item";
    public string AvailableCategories { get; set; } = "(Color::Yellow)Available categories:";
    public string CategoryUsage { get; set; } = "(Color::White)Use (Color::Accent)!crhelp <category> (Color::White)to see commands in a category";
    public string CategoryHeader { get; set; } = "(Color::Accent)--{{category}} Commands--";
    public string UnknownCategory { get; set; } = "(Color::Yellow)Unknown category: (Color::Accent){{category}}";
    // @formatter:on
}
