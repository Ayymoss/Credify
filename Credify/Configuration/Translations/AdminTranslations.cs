namespace Credify.Configuration.Translations;

public class AdminTranslations
{
    // @formatter:off
    public string ResetCreditsDescription { get; set; } = "Resets the credits globally";
    public string SetCreditsDescription { get; set; } = "Set Credits";
    public string PassIdAsArgument { get; set; } = "(Color::Yellow)Pass the 'Id' from IW4MAdminConfiguration as an argument";
    public string ResettingCreditsInit { get; set; } = "(Color::Accent)--Credit Reset--";
    public string ResettingCredits { get; set; } = "(Color::Yellow)Resetting credits... (Color::White){{count}} players reset";
    public string ResettingRaffleTickets { get; set; } = "(Color::Yellow)Resetting raffle tickets... (Color::White){{count}} players reset";
    public string ResettingShopItems { get; set; } = "(Color::Yellow)Resetting shop items... (Color::White){{count}} players reset";
    public string ResettingTopStats { get; set; } = "(Color::Yellow)Resetting top stats...";
    public string ResettingStatistics { get; set; } = "(Color::Yellow)Resetting statistics...";
    public string ResettingBank { get; set; } = "(Color::Yellow)Resetting server bank...";
    public string ResetCreditsComplete { get; set; } = "(Color::Accent)--Credit Reset Complete--";
    public string SetCreditsForTarget { get; set; } = "Set credits for {{targetName}} (Color::White)to (Color::Accent)${{absAmount}}(Color::White)";
    public string CreditsSetByOrigin { get; set; } = "{{originName}} (Color::White)set your credits to (Color::Accent)${{absAmount}}(Color::White)";
    // @formatter:on
}
