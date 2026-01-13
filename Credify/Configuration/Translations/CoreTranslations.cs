namespace Credify.Configuration.Translations;

public class CoreTranslations
{
    // @formatter:off
    public string AdvertisementMessage { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Gamble your Credits today! (Color::Accent)!crbj(Color::White), (Color::Accent)!crrl(Color::White), (Color::Accent)!crhelp";
    public string AdvertisementRaffle { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Join the Raffle! (Color::Accent)!crraf(Color::White), (Color::Accent)!crsr";
    public string AdvertisementShop { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] Buy items from the shop! (Color::Accent)!crshop(Color::White)";
    public string InsufficientCredits { get; set; } = "(Color::Yellow)Insufficient credits";
    public string PassIdAsArgument { get; set; } = "(Color::Yellow)Pass the 'Id' from IW4MAdminConfiguration as an argument";
    public string ResettingCreditsInit { get; set; } = "(Color::Accent)--Credit Reset--";
    public string ResettingCredits { get; set; } = "(Color::Yellow)Resetting credits... (Color::White){{count}} players reset";
    public string ResettingRaffleTickets { get; set; } = "(Color::Yellow)Resetting raffle tickets... (Color::White){{count}} players reset";
    public string ResettingShopItems { get; set; } = "(Color::Yellow)Resetting shop items... (Color::White){{count}} players reset";
    public string ResettingTopStats { get; set; } = "(Color::Yellow)Resetting top stats...";
    public string ResettingStatistics { get; set; } = "(Color::Yellow)Resetting statistics...";
    public string ResettingBank { get; set; } = "(Color::Yellow)Resetting server bank...";
    public string ResetCreditsComplete { get; set; } = "(Color::Accent)--Credit Reset Complete--";
    public string CommandResetCreditsDescription { get; set; } = "Resets the credits globally";
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
    public string CommandSetCreditsDescription { get; set; } = "Set Credits";
    public string ErrorParsingArgument { get; set; } = "(Color::Red)Error trying to parse argument";
    public string ErrorParsingSecondArgument { get; set; } = "(Color::Red)Error trying to parse second argument";
    public string SetCreditsForTarget { get; set; } = "Set credits for {{targetName}} (Color::White)to (Color::Accent)${{absAmount}}(Color::White)";
    public string CreditsSetByOrigin { get; set; } = "{{originName}} (Color::White)set your credits to (Color::Accent)${{absAmount}}(Color::White)";
    public string MinimumAmount { get; set; } = "(Color::Yellow)Minimum amount is 10";
    public string GambleWon { get; set; } = "You won (Color::Accent)${{wonAmount}} credits! New balance (Color::Accent)${{newBalance}}";
    public string GambleLost { get; set; } = "You lost (Color::Accent)${{lostAmount}} credits. New balance (Color::Accent)${{newBalance}}! (Color::Green)Try again! You could win big!";
    public string GambleDraw { get; set; } = "You drew! (Color::Accent)${{amount}} credits returned. Balance (Color::Accent)${{newBalance}}";
    public string CommandCheckCreditsDescription { get; set; } = "Check your credits.";
    public string TargetCredits { get; set; } = "{{targetName}} (Color::White)has (Color::Accent)${{targetCredits}} (Color::White)credits";
    public string OriginCredits { get; set; } = "You have (Color::Accent)${{originCredits}} (Color::White)credits";
    public string ServerBankCredits { get; set; } = "The server bank has (Color::Accent)${{bankCredits}} (Color::White)credits";
    public string ErrorFindingTargetUser { get; set; } = "(Color::Yellow)Error trying to find user";
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
    public string PaySent { get; set; } = "(Color::Accent)${{amount}} credits sent to (Color::Accent){{targetName}}";
    public string PayReceived { get; set; } = "(Color::Accent)${{amount}} credits received from (Color::Accent){{targetName}}";
    public string CannotTargetConsole { get; set; } = "(Color::Yellow)Cannot target console";
    public string CannotTargetSelf { get; set; } = "(Color::Yellow)Cannot target self";
    public string CommandPayCreditsDescription { get; set; } = "Pay credits to another player";
    public string CommandShowRaffleDescription { get; set; } = "Shows the current raffle holders";
    public string CommandRaffleDescription { get; set; } = "Buy your raffle ticket!";
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
    public string CommandRecentBuysDescription { get; set; } = "Shows the recent shop buys";
    public string RecentBuysTitle { get; set; } = "(Color::Accent)--Recent Shop Buys--";
    public string RecentBoughtItemEntry { get; set; } = "[{{index}}](Color::Accent) {{name}} (@{{clientId}}) (Color::White)bought (Color::Accent){{item}} (Color::White){{when}}";
    public string CommandRockPaperScissorsDescription { get; set; } = "Play rock paper scissors";
    public string CommandCoinFlipDescription { get; set; } = "Bet your money on a coin flip";
    public string BadRpsArgument { get; set; } = "(Color::Yellow)Invalid argument. (Color::White)Use (Color::Accent)!crrps <rock|paper|scissors> <bet>";
    public string BadCfArgument { get; set; } = "(Color::Yellow)Invalid argument. (Color::White)Use (Color::Accent)!crcf <h|t> <bet>";
    public string MaximumAmount { get; set; } = "(Color::Yellow)Maximum amount is (Color::Green){{maxAmount}}";
    public string CommandBlackjack { get; set; } ="Join Blackjack";
    public string CommandRoulette { get; set; } = "Join Roulette";
    public string CommandQuestDescription { get; set; } = "Shows the quests";
    
    // Streak & Bounty
    public string StreakReward { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{streak}} (Color::White)kill streak! (Color::Green)+${{reward}}";
    public string StreakAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{name}} (Color::White)is on a (Color::Red){{streak}} (Color::White)kill streak!";
    public string BountyPlaced { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Red)BOUNTY: (Color::Green)${{amount}} (Color::White)on (Color::Accent){{name}}(Color::White)! Kill them to claim!";
    public string BountyClaimed { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{killer}} (Color::White)claimed the (Color::Green)${{amount}} (Color::White)bounty on (Color::Accent){{victim}}(Color::White)!";
    
    // Slots
    public string CommandSlotsDescription { get; set; } = "Spin the slot machine";
    public string SlotsDisabled { get; set; } = "(Color::Yellow)Slots is disabled";
    public string SlotsWin { get; set; } = "[(Color::Pink)SLOTS(Color::White)] {{reels}} - (Color::Green)You won ${{profit}}! (Color::White)Balance: (Color::Accent)${{balance}}";
    public string SlotsLose { get; set; } = "[(Color::Pink)SLOTS(Color::White)] {{reels}} - (Color::Red)No match! (Color::White)Lost ${{bet}}. Balance: (Color::Accent)${{balance}}";
    public string SlotsJackpot { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)JACKPOT! (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)on slots!";
    
    // Wheel of Fortune
    public string CommandWheelDescription { get; set; } = "Spin the Wheel of Fortune";
    public string WheelDisabled { get; set; } = "(Color::Yellow)Wheel of Fortune is disabled";
    public string WheelWin { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}} (Color::White)- (Color::Green)Won ${{profit}}! (Color::White)Balance: (Color::Accent)${{balance}}";
    public string WheelBreakEven { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}} (Color::White)- Break even! Balance: (Color::Accent)${{balance}}";
    public string WheelPartialLoss { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}} (Color::White)- Lost ${{loss}}. Balance: (Color::Accent)${{balance}}";
    public string WheelBankrupt { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Red)BANKRUPT! (Color::White)Lost ${{bet}}. Balance: (Color::Accent)${{balance}}";
    public string WheelJackpot { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)JACKPOT! (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)on the wheel!";
    
    // Bounty Contracts
    public string CommandPlaceBountyDescription { get; set; } = "Place a bounty on a player";
    public string CommandListBountiesDescription { get; set; } = "List all active bounties";
    public string BountyContractDisabled { get; set; } = "(Color::Yellow)Bounty contracts are disabled";
    public string BountyContractPlaced { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] You placed a (Color::Green)${{amount}} (Color::White)bounty on (Color::Accent){{target}}(Color::White). Fee: (Color::Yellow)${{fee}}";
    public string BountyContractTargeted { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] (Color::Red)WARNING: (Color::White)There's a (Color::Green)${{amount}} (Color::White)bounty on your head from (Color::Accent){{placer}}(Color::White)!";
    public string BountyContractAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Red)BOUNTY CONTRACT: (Color::Green)${{amount}} (Color::White)on (Color::Accent){{target}}(Color::White)!";
    public string BountyContractClaimed { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{killer}} (Color::White)collected (Color::Green)${{amount}} (Color::White)in bounties on (Color::Accent){{victim}}(Color::White)!";
    public string NoBountiesActive { get; set; } = "(Color::Yellow)No active bounties";
    public string BountiesHeader { get; set; } = "(Color::Accent)--Active Bounties--";
    public string BountyListEntry { get; set; } = "[(Color::Accent)#{{rank}}(Color::White)] (Color::Green)${{amount}} (Color::White)on (Color::Accent){{target}}";
    public string BountiesMoreCount { get; set; } = "(Color::Yellow)...and {{count}} more bounties";
    public string AlreadyInAnotherGame { get; set; } = "(Color::Yellow)You are already in {{gameName}}. Leave that game first.";

    // @formatter:on
}
