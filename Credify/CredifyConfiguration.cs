namespace Credify;

public class CredifyConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public int MinimumPlayersRequiredForPlayerAndTeamBets { get; set; } = 6;
    public TimeSpan CredifyAdvertisementIntervalMinutes { get; set; } = TimeSpan.FromMinutes(5);
    public int GambleMultiplier { get; set; } = 9;
    public Translations Translations { get; set; } = new();
}

public class Translations
{
    public string CommandBetPlayerDescription { get; set; } = "Bet on a player's win.";
    public string AdvertisementMessage { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Gamble your Credits today! (Color::Accent)!crhelp";
    public string MinimumPlayersNeeded { get; set; } = "(Color::Yellow){{minimumPlayers}} players minimum are needed to bet";
    public string InsufficientCredits { get; set; } = "(Color::Yellow)Insufficient credits";
    public string BetRemovedDueToTargetLeaving { get; set; } = "(Color::Red)Your bet was removed due to {{name}} leaving";
    public string BetRemovedDueToInsufficientCredits { get; set; } = "(Color::Red)Bet was removed due to you no longer having available credits";
    public string BetLostOnTarget { get; set; } = "Your bet (Color::Red)lost (Color::Cyan){{initAmount}} (Color::White)credits on {{target}}";
    public string BetWonOnTarget { get; set; } = "Your bet (Color::Green)won (Color::Cyan){{payout}} (Color::White)credits on {{target}}";
    public string ClaimableBetsAvailable { get; set; } = "(Color::Yellow)You have claimable bets. (Color::White)Type (Color::Cyan)!cb (Color::White)to claim them";
    public string BetCreatedOnTarget { get; set; } = "Bet on {{target}} (Color::White)for (Color::Cyan){{amount}} (Color::White)created";
    public string NoRankedPlayersOnTeam { get; set; } = "(Color::Yellow)No one on the team is ranked";
    public string TargetPlayerNeedsToBeRanked { get; set; } = "(Color::Yellow){{targetName}} (Color::Yellow)needs to be ranked to set a bet";
    public string UserCredits { get; set; } = "You have (Color::Cyan){{userCredits}} (Color::White)credits";
    public string NoOneHasCreditsForTop { get; set; } = "No one has any credits for top";
    public string TopCreditsTitle { get; set; } = "(Color::Cyan)--Top Credits--";
    public string CommandTopCreditsDescription { get; set; } = "List top 5 players with most credits.";
    public string TopPlayerEntry { get; set; } = "#{{index}} {{name}} (Color::White)- {{credits}}";
    public string CreditStatisticsTitle { get; set; } = "(Color::Cyan)--Credit Statistics--";
    public string TotalEarnedCredits { get; set; } = "Total Earned: (Color::Cyan){{creditsEarned}} (Color::White)credits";
    public string TotalSpentCredits { get; set; } = "Total Spent: (Color::Cyan){{creditsSpent}} (Color::White)credits";
    public string TotalWonCredits { get; set; } = "Total Won: (Color::Cyan){{creditsPaid}} (Color::White)credits";
    public string CommandStatisticsDescription { get; set; } = "Check your credits.";
    public string CommandSetCreditsDescription { get; set; } = "Set Credits";
    public string ErrorParsingFirstArgument { get; set; } = "(Color::Yellow)Error trying to parse first argument";
    public string ErrorParsingSecondArgument { get; set; } = "(Color::Red)Error trying to parse second argument";
    public string ErrorFindingUser { get; set; } = "(Color::Red)Error trying to find user";
    public string SetCreditsForTarget { get; set; } = "Set credits for {{targetName}} (Color::White)to (Color::Cyan){{absAmount}}(Color::White)";
    public string CreditsSetByOrigin { get; set; } = "{{originName}} (Color::White)set your credits to (Color::Cyan){{absAmount}}(Color::White)";
    public string ListAllOpenBets { get; set; } = "Lists all open bets";
    public string NoOpenBets { get; set; } = "(Color::Yellow)There are no open bets";
    public string OpenBetsTitle { get; set; } = "(Color::Cyan)--Open Bets--";
    public string Allies { get; set; } = "(Color::Blue)Allies";
    public string Axis { get; set; } = "(Color::Blue)Axis";
    public string BetTargetPlayer { get; set; } = "(Color::Red){{targetPlayerCleanedName}}";
    public string BetEntry { get; set; } = "#(Color::Cyan){{index}} (Color::White)- (Color::Green){{originCleanedName}} (Color::White)- {{target}} (Color::White)- (Color::Cyan){{initAmount}}";
    public string CommandGambleCreditsDescription { get; set; } = "Gamble Credits";
    public string AcceptedRange { get; set; } = "(Color::Yellow)Accepted range is 1 to 10";
    public string MinimumAmount { get; set; } = "(Color::Yellow)Minimum amount is 1";
    public string GambleWon { get; set; } = "You won (Color::Cyan){{wonAmount}} (Color::White)tokens!";
    public string GambleWonAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow){{originName}} (Color::White)won (Color::Cyan){{wonAmount}} (Color::White)tokens!";
    public string GambleWonInstructions { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] You can too! (Color::Yellow){{command}} (Color::White)to play!";
    public string GambleLost { get; set; } = "You lost (Color::Cyan){{lostAmount}} (Color::White)credits";
    public string GambleChoices { get; set; } = "You chose (Color::Cyan){{userChoice}}(Color::White), the number was (Color::Cyan){{randNum}}(Color::White)";
    public string CommandCheckCreditsDescription { get; set; } = "Check your credits.";
    public string TargetCredits { get; set; } = "{{targetName}} (Color::White)has (Color::Cyan){{targetCredits}} (Color::White)credits";
    public string OriginCredits { get; set; } = "You have (Color::Cyan){{originCredits}} (Color::White)credits";
    public string CommandClaimCompletedBetsDescription { get; set; } = "Claims your completed bets";
    public string NoCompletedBetsToClaim { get; set; } = "(Color::Yellow)You have no completed bets to claim";
    public string CommandCancelOpenBetsDescription { get; set; } = "Cancel your open bets";
    public string BetsOnlyAcceptedDuringWindow { get; set; } = "(Color::Yellow)Bets are only accepted during first {{betWindowHumanized}}";
    public string NoBetsToCancel { get; set; } = "(Color::Yellow)You have no bets to cancel";
    public string BetsCancelled { get; set; } = "You bets ({{cancelledBets}}) have been cancelled";
    public string CommandBetOnTeamWinDescription { get; set; } = "Bet on a Team's Win";
    public string UnknownTeam { get; set; } = "(Color::Yellow)Unknown Team";
    public string YourTeam { get; set; } = "Your Team: {{originTeam}}";
    public string OtherTeams { get; set; } = "Other Teams: {{teamList}}";
    public string ErrorParsingAmount { get; set; } = "(Color::Yellow)Error trying to parse amount";
    public string MinimumAmountIsOne { get; set; } = "(Color::Yellow)Minimum amount is 1";
    public string InsufficientCreditsForBet { get; set; } = "(Color::Yellow)Insufficient credits";
    public string BetWindowRestriction { get; set; } = "(Color::Yellow)Bets only accepted during first {{betWindowHumanized}}";
    public string ErrorFindingTargetUser { get; set; } = "(Color::Yellow)Error trying to find user";
    public string CommandHelpDescription { get; set; } = "Shows Credify user commands";
    public string HelpHeader { get; set; } = "(Color::Cyan)--Credify Commands--";
    public string HelpBetPlayer { get; set; } = "[(Color::Yellow)!betp(Color::White)] Bet on a player to win";
    public string HelpBetTeam { get; set; } = "[(Color::Yellow)!bett(Color::White)] Bet on a team to win";
    public string HelpClaimBets { get; set; } = "[(Color::Yellow)!cb(Color::White)] Claim your completed bets";
    public string HelpHelp { get; set; } = "[(Color::Yellow)!help(Color::White)] Shows Credify user commands";
    public string HelpGamble { get; set; } =  "[(Color::Yellow)!gmb(Color::White)] Gamble your credits";
    public string HelpStatistics { get; set; } = "[(Color::Yellow)!stats(Color::White)] Check the global credit statistics";
    public string HelpTopCredits { get; set; } = "[(Color::Yellow)!top(Color::White)] Check the top credit holders";
}
