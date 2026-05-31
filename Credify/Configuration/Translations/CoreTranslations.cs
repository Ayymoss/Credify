namespace Credify.Configuration.Translations;

/// <summary>
/// Cross-cutting strings used across many features. Feature-specific text lives in the
/// per-feature translation classes (Shop, Economy, Wheel, Slots, ...).
/// </summary>
public class CoreTranslations
{
    // @formatter:off
    // Advertisement lines are rotated one-per-interval by ScheduleService (not all at once),
    // so each stays focused on a theme. Keep them short and end gambling/feature lines with
    // a pointer to !crhelp rather than listing every command.
    public string AdvertisementMessage { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Bet your credits! Blackjack (Color::Accent)!crbj(Color::White), Minefield (Color::Accent)!crmine(Color::White), Poker (Color::Accent)!crpk(Color::White), Roulette (Color::Accent)!crrl(Color::White), Slots (Color::Accent)!crslots";
    public string AdvertisementQuickBets { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Fancy a quick flutter? Coin flip (Color::Accent)!crcf(Color::White), Rock-Paper-Scissors (Color::Accent)!crrps(Color::White), Wheel of Fortune (Color::Accent)!crwof";
    public string AdvertisementRaffle { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Earn more: daily quests (Color::Accent)!crq(Color::White), the raffle (Color::Accent)!crraf(Color::White), and climb the leaderboard (Color::Accent)!crtop";
    public string AdvertisementShop { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Spend your credits: shop (Color::Accent)!crshop(Color::White), gift players (Color::Accent)!crpay(Color::White), place a bounty (Color::Accent)!crbounty";
    public string AdvertisementProfile { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Check your balance (Color::Accent)!cr(Color::White), stats (Color::Accent)!crstats(Color::White), inventory (Color::Accent)!crinv(Color::White). Full list: (Color::Accent)!crhelp";

    public string InsufficientCredits { get; set; } = "(Color::Yellow)Insufficient credits";
    public string AlreadyInAnotherGame { get; set; } = "(Color::Yellow)You are already in {{gameName}}. Leave that game first.";
    public string ErrorParsingArgument { get; set; } = "(Color::Red)Error trying to parse argument";
    public string ErrorParsingSecondArgument { get; set; } = "(Color::Red)Error trying to parse second argument";
    // @formatter:on
}
