namespace Credify.Configuration.Translations;

/// <summary>
/// Player-facing economy strings: balance checks, leaderboard, global statistics and
/// player-to-player payments.
/// </summary>
public class EconomyTranslations
{
    // @formatter:off
    public string UserCredits { get; set; } = "You have (Color::Accent)${{userCredits}} (Color::White)credits ((Color::Accent)!crhelp(Color::White))";
    public string NoOneHasCreditsForTop { get; set; } = "No one has any credits for top";
    public string TopCreditsTitle { get; set; } = "(Color::Accent)--Top Credits--";
    public string CommandTopCreditsDescription { get; set; } = "List top 5 players with most credits.";
    public string TopPlayerEntry { get; set; } = "[(Color::Accent)#{{rank}} (Color::White)@ (Color::Green)${{credits}}(Color::White)] {{name}}";
    public string StatsHeader { get; set; } = "(Color::Accent)--Global Credit Statistics--";
    public string StatsTotalEarnedCredits { get; set; } = "Total Earned: (Color::Accent)${{creditsEarned}} (Color::White)credits";
    public string StatsTotalSpentCredits { get; set; } = "Total Spent: (Color::Accent)${{creditsSpent}} (Color::White)credits";
    public string StatsTotalWonCredits { get; set; } = "Total Won: (Color::Accent)${{creditsPaid}} (Color::White)credits";
    public string StatsBankCredits { get; set; } = "Bank: (Color::Accent)${{bankCredits}} (Color::White)credits";
    public string CommandStatisticsDescription { get; set; } = "Check your credits.";
    public string CommandCheckCreditsDescription { get; set; } = "Check your credits.";
    public string TargetCredits { get; set; } = "{{targetName}} (Color::White)has (Color::Accent)${{targetCredits}} (Color::White)credits";
    public string OriginCredits { get; set; } = "You have (Color::Accent)${{originCredits}} (Color::White)credits";
    public string ServerBankCredits { get; set; } = "The server bank has (Color::Accent)${{bankCredits}} (Color::White)credits";
    public string PaySent { get; set; } = "(Color::Accent)${{amount}} credits sent to (Color::Accent){{targetName}}";
    public string PayReceived { get; set; } = "(Color::Accent)${{amount}} credits received from (Color::Accent){{targetName}}";
    public string CannotTargetConsole { get; set; } = "(Color::Yellow)Cannot target console";
    public string CannotTargetSelf { get; set; } = "(Color::Yellow)Cannot target self";
    public string CommandPayCreditsDescription { get; set; } = "Pay credits to another player";
    public string ErrorFindingTargetUser { get; set; } = "(Color::Yellow)Error trying to find user";
    // @formatter:on
}
