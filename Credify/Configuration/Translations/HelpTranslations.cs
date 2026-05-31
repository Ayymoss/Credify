namespace Credify.Configuration.Translations;

public class HelpTranslations
{
    // @formatter:off
    public string Description { get; set; } = "Shows Credify user commands";
    public string Header { get; set; } = "(Color::Accent)--Credify Commands--";
    public string Shop { get; set; } = "[(Color::Yellow)!crshop(Color::White)] Shop for items with your credits";
    public string AvailableCategories { get; set; } = "(Color::Yellow)Available categories:";
    public string CategoryUsage { get; set; } = "(Color::White)Use (Color::Accent)!crhelp <category> (Color::White)to see commands in a category";
    public string CategoryHeader { get; set; } = "(Color::Accent)--{{category}} Commands--";
    public string UnknownCategory { get; set; } = "(Color::Yellow)Unknown category: (Color::Accent){{category}}";
    // @formatter:on
}
