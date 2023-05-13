using Credify.Models;

namespace Credify;

public class CredifyConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public BaseConfiguration Core { get; set; } = new();
    public Shop Shop { get; set; } = new();
    public Translations Translations { get; set; } = new();
}

public class BaseConfiguration
{
    public int MinimumPlayersRequiredForPlayerAndTeamBets { get; set; } = 6;
    public double BankTax { get; set; } = 0.05;
    public TimeSpan CredifyAdvertisementIntervalMinutes { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan CreditsTeamPlayerBetWindow { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan LotteryFrequency { get; set; } = TimeSpan.FromDays(5);
    public TimeSpan LotteryFrequencyAtTime { get; set; } = new(15, 0, 0);
}

public class Shop
{
    public bool IsEnabled { get; set; } = false;

    public List<ServerShopItem> Items { get; set; } = new()
    {
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
    };
}

public class Translations
{
    // @formatter:off
    public string CommandBetPlayerDescription { get; set; } = "Bet on a player's win.";
    public string AdvertisementMessage { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Gamble your Credits today! (Color::Accent)!crbet(Color::White), (Color::Accent)!crhelp";
    public string AdvertisementLotto { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Join the Lotto! (Color::Accent)!crlotto(Color::White), (Color::Accent)!crsl";
    public string AdvertisementShop { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Buy items from the shop! (Color::Accent)!crshop(Color::White)";
    public string MinimumPlayersNeeded { get; set; } = "(Color::Yellow){{minimumPlayers}} players minimum are needed to bet";
    public string InsufficientCredits { get; set; } = "(Color::Yellow)Insufficient credits";
    public string BetRemovedDueToTargetLeaving { get; set; } = "(Color::Red)Your bet was removed due to {{name}} leaving";
    public string BetRemovedDueToInsufficientCredits { get; set; } = "(Color::Red)Bet was removed due to you no longer having available credits";
    public string BetLostOnTarget { get; set; } = "Your bet (Color::Red)lost (Color::Accent)${{initAmount}} (Color::White)credits on {{target}}";
    public string BetWonOnTarget { get; set; } = "Your bet (Color::Green)won (Color::Accent)${{payout}} (Color::White)credits on {{target}}";
    public string ClaimableBetsAvailable { get; set; } = "(Color::Yellow)You have claimable bets. (Color::White)Type (Color::Accent)!crcb (Color::White)to claim them";
    public string BetCreatedOnTarget { get; set; } = "Bet on {{target}} (Color::White)for (Color::Accent)${{amount}} (Color::White)created";
    public string NoRankedPlayersOnTeam { get; set; } = "(Color::Yellow)No one on the team is ranked";
    public string TargetPlayerNeedsToBeRanked { get; set; } = "(Color::Yellow){{targetName}} (Color::Yellow)needs to be ranked to set a bet";
    public string UserCredits { get; set; } = "You have (Color::Accent)${{userCredits}} (Color::White)credits";
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
    public string CommandSetCreditsDescription { get; set; } = "Set Credits";
    public string ErrorParsingSecondArgument { get; set; } = "(Color::Red)Error trying to parse second argument";
    public string SetCreditsForTarget { get; set; } = "Set credits for {{targetName}} (Color::White)to (Color::Accent)${{absAmount}}(Color::White)";
    public string CreditsSetByOrigin { get; set; } = "{{originName}} (Color::White)set your credits to (Color::Accent)${{absAmount}}(Color::White)";
    public string CommandListAllOpenBetsDescription { get; set; } = "Lists all open bets";
    public string NoOpenBets { get; set; } = "(Color::Yellow)There are no open bets";
    public string OpenBetsTitle { get; set; } = "(Color::Accent)--Open Bets--";
    public string Allies { get; set; } = "(Color::Blue)Allies";
    public string Axis { get; set; } = "(Color::Blue)Axis";
    public string BetTargetPlayer { get; set; } = "(Color::Red){{targetPlayerCleanedName}}";
    public string BetEntry { get; set; } = "#(Color::Accent){{index}} (Color::White)- (Color::Green){{originCleanedName}} (Color::White)- {{target}} (Color::White)- (Color::Accent)${{initAmount}}";
    public string CommandGambleCreditsDescription { get; set; } = "Gamble Credits";
    public string MinimumAmount { get; set; } = "(Color::Yellow)Minimum amount is 10";
    public string GambleWon { get; set; } = "You won (Color::Accent)${{wonAmount}} (Color::White)((Color::Yellow)${{taxed}} (Color::White)taxed) credits! New balance (Color::Accent)${{newBalance}}";
    public string GambleLost { get; set; } = "You lost (Color::Accent)${{lostAmount}} (Color::White)((Color::Yellow)${{taxed}} (Color::White)taxed) credits. New balance (Color::Accent)${{newBalance}}! (Color::Green)Try again! You could win big!";
    public string GambleDraw { get; set; } = "You only lost (Color::White)((Color::Yellow)${{taxed}} (Color::White)taxed) credits. New balance (Color::Accent)${{newBalance}}! (Color::Green)Better luck next time!";
    public string GambleWonJackpotAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow){{originName}} (Color::White)won the jackpot of (Color::Accent)${{wonAmount}}! (Color::Yellow){{command}} (Color::White)to play!";
    public string GambleWonAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow){{originName}} (Color::White)won (Color::Accent)${{wonAmount}}! (Color::Yellow){{command}} (Color::White)to play!";
    public string CommandCheckCreditsDescription { get; set; } = "Check your credits.";
    public string TargetCredits { get; set; } = "{{targetName}} (Color::White)has (Color::Accent)${{targetCredits}} (Color::White)credits";
    public string OriginCredits { get; set; } = "You have (Color::Accent)${{originCredits}} (Color::White)credits";
    public string ServerBankCredits { get; set; } = "The server bank has (Color::Accent)${{bankCredits}} (Color::White)credits";
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
    public string BetWindowRestriction { get; set; } = "(Color::Yellow)Bets only accepted during first {{betWindowHumanized}}";
    public string ErrorFindingTargetUser { get; set; } = "(Color::Yellow)Error trying to find user";
    public string CommandHelpDescription { get; set; } = "Shows Credify user commands";
    public string HelpHeader { get; set; } = "(Color::Accent)--Credify Commands--";
    public string HelpBetPlayer { get; set; } = "[(Color::Yellow)!crbp(Color::White)] Bet on a player to win";
    public string HelpBetTeam { get; set; } = "[(Color::Yellow)!crbt(Color::White)] Bet on a team to win";
    public string HelpClaimBets { get; set; } = "[(Color::Yellow)!crcb(Color::White)] Claim your completed bets";
    public string HelpHelp { get; set; } = "[(Color::Yellow)!crhelp(Color::White)] Shows Credify user commands";
    public string HelpGamble { get; set; } = "[(Color::Yellow)!crbet(Color::White)] Gamble your credits";
    public string HelpStatistics { get; set; } = "[(Color::Yellow)!crstats(Color::White)] Check the global credit statistics";
    public string HelpTopCredits { get; set; } = "[(Color::Yellow)!crtop(Color::White)] Check the top credit holders";
    public string HelpLotto { get; set; } = "[(Color::Yellow)!crlotto(Color::White)] Buy lotto tickets! (1cr = 10 tickets)";
    public string HelpPayCredits { get; set; } = "[(Color::Yellow)!crpay(Color::White)] Pay credits to another player";
    public string HelpOpenBets { get; set; } = "[(Color::Yellow)!crob(Color::White)] List all open bets";
    public string HelpShop { get; set; } = "[(Color::Yellow)!crshop(Color::White)] Shop for items with your credits";
    public string HelpShopInventory { get; set; } = "[(Color::Yellow)!crinv(Color::White)] Check your bought shop items";
    public string HelpShopBuy { get; set; } = "[(Color::Yellow)!crbuy(Color::White)] Buy a shop item";
    public string PaySent { get; set; } = "(Color::Accent)${{amount}} (Color::White)((Color::Yellow)${{taxed}} (Color::White)taxed) credits sent to (Color::Accent){{targetName}}";
    public string PayReceived { get; set; } = "(Color::Accent)${{amount}} credits received from (Color::Accent){{targetName}}";
    public string CannotTargetConsole { get; set; } = "(Color::Yellow)Cannot target console";
    public string CannotTargetSelf { get; set; } = "(Color::Yellow)Cannot target self";
    public string CommandPayCreditsDescription { get; set; } = "Pay credits to another player";
    public string AnnounceLottoWinner { get; set; } = "(Color::Accent){{cleanedName}} (Color::White)won (Color::Green)${{bankCredits}} (Color::White)from the lottery with a (Color::Accent){{winPct}}(Color::White)pct chance!";
    public string BoughtLottoTickets { get; set; } = "(Color::Accent){{ticketCount}} (Color::White)lotto tickets bought for (Color::Accent)${{amount}} (Color::White)credits. You have a total of {{totalTickets}} tickets";
    public string CommandLottoDescription { get; set; } = "Buy lotto tickets! ($1 = 10 tickets)";
    public string TicketHolder { get; set; } = "[(Color::Accent)#{{index}} (Color::White)@ (Color::Green){{tickets}}(Color::White)] {{name}}";
    public string ShowLottoHeader { get; set; } = "(Color::Accent)--Ticket Holders--";
    public string LottoNextDraw { get; set; } = "Next draw in (Color::Accent){{nextDrawHumanized}}";
    public string NoTicketHolders { get; set; } = "(Color::Yellow)No ticket holders. (Color::White)Buy some tickets! (Color::Accent)!crlotto";
    public string NoTicketHoldersContinued { get; set; } = "Bank Amount: (Color::Green)${{bankCredits}} (Color::White)- Next Draw: (Color::Accent){{nextDraw}}";
    public string CommandShowLottoDescription { get; set; } = "Shows the current lotto holders";
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
    public string LastWinner { get; set; } = "Last winner(Color::Accent) {{name}} (@{{clientId}}) (Color::White)won (Color::Green)${{winTotal}}(Color::White)!";
    public string CommandRecentBuysDescription { get; set; } = "Shows the recent shop buys";
    public string RecentBuysTitle { get; set; } = "(Color::Accent)--Recent Shop Buys--";
    public string RecentBoughtItemEntry { get; set; } = "[{{index}}](Color::Accent) {{name}} (@{{clientId}}) (Color::White)bought (Color::Accent){{item}} (Color::White){{when}}";
    public string NoLastWinner { get; set; } = "(Color::Accent)Good luck!";
    // @formatter:on
}
