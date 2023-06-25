using Credify.Models;

namespace Credify;

public class CredifyConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public BaseConfiguration Core { get; set; } = new();
    public ChatGameConfiguration ChatGame { get; set; } = new();
    public BlackjackConfiguration Blackjack { get; set; } = new();
    public Shop Shop { get; set; } = new();
    public Translations Translations { get; set; } = new();
}

public class BaseConfiguration
{
    public int MinimumPlayersRequiredForPlayerAndTeamBets { get; set; } = 6;
    public double BankTax { get; set; } = 0.05;
    public TimeSpan AdvertisementIntervalMinutes { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan TeamPlayerBetWindow { get; set; } = TimeSpan.FromMinutes(2);
    public TimeSpan LotteryFrequency { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan LotteryFrequencyAtTime { get; set; } = new(15, 0, 0);
}

public class BlackjackConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public bool JoinAnnouncements { get; set; } = true;
    public double PayoutBlackjack { get; set; } = 2.5d;
    public double PayoutDealerBust { get; set; } = 2d;
    public double PayoutWin { get; set; } = 2d;
    public TimeSpan TimeoutForPlayerActions { get; set; } = TimeSpan.FromSeconds(30);
}

public class ChatGameConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public TimeSpan Frequency { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan CountdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MathTestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan TriviaTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TypingTestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxPayout { get; set; } = 1_000;
    public int TypingTestTextLength { get; set; } = 10;
    public TriviaToggle EnabledTriviaGames { get; set; } = new();
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

public class TriviaToggle
{
    public bool IsTriviaEnabled { get; set; } = true;
    public bool IsCountdownEnabled { get; set; } = true;
    public bool IsMathTestEnabled { get; set; } = true;
    public bool IsTypingTestEnabled { get; set; } = true;
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
    public string PassIdAsArgument { get; set; } = "(Color::Yellow)Pass the 'Id' from IW4MAdminConfiguration as an argument";
    public string ResettingCreditsInit { get; set; } = "(Color::Accent)--Credit Reset--";
    public string ResettingCredits { get; set; } = "(Color::Yellow)Resetting credits... (Color::White){{count}} players reset";
    public string ResettingLotteryTickets { get; set; } = "(Color::Yellow)Resetting lottery tickets... (Color::White){{count}} players reset";
    public string ResettingShopItems { get; set; } = "(Color::Yellow)Resetting shop items... (Color::White){{count}} players reset";
    public string ResettingTopStats { get; set; } = "(Color::Yellow)Resetting top stats...";
    public string ResettingStatistics { get; set; } = "(Color::Yellow)Resetting statistics...";
    public string ResettingBank { get; set; } = "(Color::Yellow)Resetting server bank...";
    public string ResetCreditsComplete { get; set; } = "(Color::Accent)--Credit Reset Complete--";
    public string CommandResetCreditsDescription { get; set; } = "Resets the credits globally";
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
    public string CommandGambleCreditsDescription { get; set; } = "Join Blackjack";
    public string MinimumAmount { get; set; } = "(Color::Yellow)Minimum amount is 10";
    public string GambleWon { get; set; } = "You won (Color::Accent)${{wonAmount}} (Color::White)((Color::Yellow)${{taxed}} (Color::White)taxed) credits! New balance (Color::Accent)${{newBalance}}";
    public string GambleLost { get; set; } = "You lost (Color::Accent)${{lostAmount}} (Color::White)((Color::Yellow)${{taxed}} (Color::White)taxed) credits. New balance (Color::Accent)${{newBalance}}! (Color::Green)Try again! You could win big!";
    public string GambleDraw { get; set; } = "You only lost (Color::White)((Color::Yellow)${{taxed}} (Color::White)taxed) credits. New balance (Color::Accent)${{newBalance}}! (Color::Green)Better luck next time!";
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
    public string CommandRockPaperScissorsDescription { get; set; } = "Play rock paper scissors";
    public string BadRpsArgument { get; set; } = "(Color::Yellow)Invalid argument. (Color::White)Use (Color::Accent)!crrps <rock|paper|scissors> <stake>";
    public string RpsWonAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{winnerName}} (Color::White)won (Color::Green)${{amount}}. (Color::White)Try your luck with (Color::Accent)!crrps";

    #region Chat Games

    public string ChatGameFriendlyTypingTestGame { get; set; } = "Fast Fingers";
    public string ChatGameFriendlyMathTestGame { get; set; } = "Quick Maffs";
    public string ChatGameFriendlyCountdownGame { get; set; } = "Countdown";
    public string ChatGameFriendlyTriviaGame { get; set; } = "Trivia";
    public string ChatGameGenericNoAnswer { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)Times up! No one answered! (Color::White)The answer was (Color::Accent){{question}}";
    public string ChatGameTypingTestNoAnswer { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)Times up! No one answered!";
    public string ChatGameTypingTestWinnerBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)with a time of (Color::Accent){{time}} (Color::White)seconds!";
    public string ChatGameMathTestWinnerBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)with a time of (Color::Accent){{time}} (Color::White)seconds! (Color::White)The answer was (Color::Accent){{question}}";
    public string ChatGameReactionTell { get; set; }= "You won (Color::Green)${{amount}}(Color::White). New balance (Color::Green)${{balance}}";
    public string ChatGameReactionBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] [(Color::Accent){{name}}(Color::White)] (Color::White)First to Type! {{question}}";
    public string ChatGameTriviaBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] [(Color::Accent){{name}}(Color::White)] (Color::Yellow){{question}}";
    public string ChatGameTriviaWinBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{count}} (Color::White)winner(s) with (Color::Green)${{amount}} (Color::White)paid out! The answer was (Color::Accent){{question}}";
    public string ChatGameAlreadyAnswered { get; set; }= "(Color::Yellow)You already answered!";
    public string ChatGameAnswerAccepted { get; set; } = "(Color::White)Your answer of (Color::Accent){{answer}} (Color::White)has been accepted! (Color::Yellow)Please wait for results";
    public string ChatGameAnswerAcceptedDefinition { get; set; } = "Definition of (Color::Accent){{word}}(Color::White), (Color::Yellow){{definition}}";
    public string ChatGameTriviaNoWinner { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)No one answered correctly! (Color::White)The answer was (Color::Accent){{question}}";
    public string ChatGameCountdownWordNotFound { get; set; } = "(Color::Yellow){{word}} (Color::White)was not found in the dictionary";
    public string ChatGameCountdownWinBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{count}} (Color::White)winner(s) with (Color::Green)${{amount}} (Color::White)paid out! Accepted answers were (Color::Accent){{words}}";
    public string ChatGameCountdownBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] [(Color::Accent){{name}}(Color::White)] (Color::Yellow)Find the best word in these letters, (Color::Accent){{question}}";
    
    #endregion
    
    #region Blackjack
    
    public string BlackjackTitle { get; set; } = "[(Color::Pink)Blackjack(Color::White)]";
    public string BlackjackTitleShort { get; set; } = "[(Color::Pink)BJ(Color::White)]";
    public string BlackjackJoin { get; set; } = "(Color::Yellow)You have joined the game! (Color::White)(!crbet to leave)";
    public string BlackjackLeave { get; set; } = "(Color::Yellow)You have left the game! (Color::White)(!crbet to join)";
    public string BlackjackStartingGame { get; set; } = "(Color::Accent)Starting a new game with {{count}} player(s)";
    public string BlackjackPlaceBets { get; set; } = "(Color::Yellow)Type the amount of credits you'd like to bet. (Color::White)You have (Color::Green)${{credits}} (Color::White)available";
    public string BlackjackBetTimeout { get; set; } = "(Color::Yellow)You took too long to bet. (Color::White)You have been removed from the game";
    public string BlackjackDealerInitialCard { get; set; } = "Dealer's up-card: (Color::Accent){{card}}";
    public string BlackjackDealerCards { get; set; } = "Dealer's cards [(Color::Yellow){{total}}(Color::White)]: (Color::Accent){{cards}}";
    public string BlackjackPlayerCards { get; set; } = "Your cards [(Color::Yellow){{total}}(Color::White)]: (Color::Accent){{cards}}";
    public string BlackjackBlackjackConfirmation { get; set; } = "(Color::Accent)You have blackjack! (Color::White)Standing...";
    public string BlackjackAnnouncement { get; set; } = "(Color::Accent){{name}} (Color::White)has (Color::Purple)blackjack(Color::White), winning (Color::Green)${{amount}}(Color::White)! (Color::Accent)Play with (Color::Yellow)!crbet";
    public string BlackjackJoinAnnouncement { get; set; } = "(Color::Accent){{name}} (Color::White)has joined blackjack with {{count}} other(s)! (Color::Accent)Play with (Color::Yellow)!crbet";
    public string BlackjackPlayersDeciding { get; set; } = "(Color::Yellow)Waiting for {{count}} player(s) to decide...";
    public string BlackJackPlayerDecision { get; set; } = "Type: (Color::Accent)[H]it (Color::White)to hit. (Color::Accent)[S]tand (Color::White)to stand. (Color::Accent)[C]ards (Color::White)to see your cards";
    public string BlackJackPlayerBustConfirmation { get; set; } = "(Color::Yellow)You busted!";
    public string BlackjackBlackjackPush { get; set; } = "(Color::Yellow)Blackjack Push! (Color::White)You get your bet back!";
    public string BlackjackDealerBust { get; set; } = "(Color::Accent)Dealer busted with {{houseValue}}! (Color::White)You won!";
    public string BlackjackWin { get; set; } = "(Color::Accent)You won with {{playerValue}}!";
    public string BlackjackLose { get; set; } = "(Color::Yellow)You lost with {{playerValue}}!";
    public string BlackjackPush { get; set; } = "(Color::Yellow)Push! (Color::White)You get your bet back!";
    public string BlackjackPayout { get; set; } = "(Color::White)You won (Color::Green)${{amount}} (Color::White)with a bet of (Color::Green)${{bet}}";
    public string BlackjackNewDeckShuffled { get; set; } = "(Color::Accent)New deck shuffled!";
    public string BlackjackAcceptedBet { get; set; } = "(Color::Yellow)Accepted bet of (Color::Green)${{amount}}";
    public string BlackjackWaitingForBets { get; set; } = "(Color::Yellow)Waiting for {{count}} player(s) to bet...";
    public string BlackjackPlayerBust { get; set; } = "(Color::Red)Bust! (Color::White)[(Color::Yellow){{total}}(Color::White)]: {{cards}}";
    public string BlackjackPlayerHit { get; set; } = "(Color::Accent)Hit! (Color::White)[(Color::Yellow){{total}}(Color::White)]: {{cards}}";
    public string BlackjackPlayerStand { get; set; }= "(Color::Green)Stand! (Color::White)[(Color::Yellow){{total}}(Color::White)]";
    public string BlackjackDisabled { get; set; } = "(Color::Yellow)Blackjack is disabled";
    public string BlackjackQueued { get; set; } = "(Color::Yellow)You have been queued for the next game";
    public string BlackjackInsufficientFunds { get; set; } = "(Color::Yellow)You have been removed. (Color::White)You do not have enough credits to play";
    public string BlackjackOutcomeBlackjack { get; set; } = "(Color::Pink)BJ";
    public string BlackjackOutcomeWin { get; set; } = "(Color::Green)W";
    public string BlackjackOutcomeLose { get; set; } = "(Color::Red)L";
    public string BlackjackOutcomePush { get; set; } = "(Color::Yellow)P";
    public string? BlackjackPlayerOutcomeMessage { get; set; }= "(Color::White)[{{outcome}}(Color::White)] (Color::Accent){{name}} (Color::White)((Color::Yellow){{total}}(Color::White))";

    #endregion

    // @formatter:on
}
