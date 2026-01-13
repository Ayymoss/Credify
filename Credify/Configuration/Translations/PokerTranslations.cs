namespace Credify.Configuration.Translations;

public class PokerTranslations
{
    // @formatter:off
    public string Title { get; set; } = "[(Color::Pink)Poker(Color::White)]";
    public string TitleShort { get; set; } = "[(Color::Pink)PK(Color::White)]";
    public string Disabled { get; set; } = "(Color::Yellow)Poker is currently disabled";
    public string Prefix(string message) => $"{TitleShort} {message}";
    public string LongPrefix(string message) => $"{Title} {message}";
    
    // Join/Leave
    public string JoinGame { get; set; } = "(Color::Yellow)You have joined the poker table! (Color::White)Waiting for more players...";
    public string LeaveGame { get; set; } = "(Color::Yellow)You have left the poker table";
    public string PlayerJoined { get; set; } = "(Color::Accent){{name}} (Color::White)joined the poker table (Color::Accent){{count}} (Color::White)player(s)";
    public string InsufficientCredits { get; set; } = "(Color::Yellow)Insufficient credits to join. Minimum buy-in: (Color::Green)${{amount}}";
    public string InvalidBuyIn { get; set; } = "(Color::Yellow)Invalid buy-in amount. Must be between (Color::Green)${{min}} (Color::White)and (Color::Green)${{max}}";
    
    // Game States
    public string WaitingForPlayers { get; set; } = "(Color::Yellow)Waiting for more players... Need (Color::Accent){{count}} (Color::White)more";
    public string StartingHand { get; set; } = "(Color::Accent)Starting new hand... (Color::White)Players: (Color::Accent){{count}}";
    public string DealerButton { get; set; } = "Dealer: (Color::Accent){{name}}";
    public string SmallBlindPosted { get; set; } = "Small blind: (Color::Accent){{name}} (Color::White)posts (Color::Green)${{amount}}";
    public string BigBlindPosted { get; set; } = "Big blind: (Color::Accent){{name}} (Color::White)posts (Color::Green)${{amount}}";
    
    // Cards
    public string YourCards { get; set; } = "Your cards: (Color::Accent){{cards}}";
    public string CommunityCards { get; set; } = "Community cards: (Color::Accent){{cards}}";
    public string CardsDealt { get; set; } = "(Color::Accent)Hole cards dealt";
    public string FlopDealt { get; set; } = "(Color::Accent)Flop: (Color::White){{cards}}";
    public string TurnDealt { get; set; } = "(Color::Accent)Turn: (Color::White){{card}}";
    public string RiverDealt { get; set; } = "(Color::Accent)River: (Color::White){{card}}";
    
    // Betting Actions
    public string ActionPrompt { get; set; } = "(Color::Yellow)Your action: (Color::White){{options}}";
    public string Fold { get; set; } = "Fold (f)";
    public string Check { get; set; } = "Check (c)";
    public string Call { get; set; } = "Call {{amount}} (k)";
    public string Raise { get; set; } = "Raise {{min}}-{{max}} (r X)";
    public string AllIn { get; set; } = "All-in {{amount}} (a)";
    
    // Action Announcements
    public string PlayerFolded { get; set; } = "(Color::Accent){{name}} (Color::White)folded";
    public string PlayerChecked { get; set; } = "(Color::Accent){{name}} (Color::White)checked";
    public string PlayerCalled { get; set; } = "(Color::Accent){{name}} (Color::White)called (Color::Green)${{amount}}";
    public string PlayerRaised { get; set; } = "(Color::Accent){{name}} (Color::White)raised to (Color::Green)${{amount}}";
    public string PlayerAllIn { get; set; } = "(Color::Accent){{name}} (Color::White)went all-in with (Color::Green)${{amount}}";
    public string ActionTimeout { get; set; } = "(Color::Yellow)Action timeout - {{name}} (Color::White)automatically folded";
    
    // Betting Round
    public string BettingRoundStart { get; set; } = "(Color::Accent)-- {{round}} --";
    public string CurrentBet { get; set; } = "Current bet: (Color::Green)${{amount}}";
    public string PotSize { get; set; } = "Pot: (Color::Green)${{amount}}";
    public string BettingComplete { get; set; } = "(Color::Accent)Betting round complete";
    
    // Showdown
    public string Showdown { get; set; } = "(Color::Accent)-- Showdown --";
    public string PlayerShowsHand { get; set; } = "(Color::Accent){{name}} (Color::White)shows: (Color::Accent){{cards}} (Color::White)- (Color::Yellow){{hand}}";
    public string PlayerWins { get; set; } = "(Color::Accent){{name}} (Color::White)wins (Color::Green)${{amount}} (Color::White)with (Color::Yellow){{hand}}";
    public string SplitPot { get; set; } = "(Color::Accent){{names}} (Color::White)split the pot: (Color::Green)${{amount}} (Color::White)each";
    
    // Hand Rankings
    public string HandRoyalFlush { get; set; } = "Royal Flush";
    public string HandStraightFlush { get; set; } = "Straight Flush";
    public string HandFourOfAKind { get; set; } = "Four of a Kind";
    public string HandFullHouse { get; set; } = "Full House";
    public string HandFlush { get; set; } = "Flush";
    public string HandStraight { get; set; } = "Straight";
    public string HandThreeOfAKind { get; set; } = "Three of a Kind";
    public string HandTwoPair { get; set; } = "Two Pair";
    public string HandPair { get; set; } = "Pair";
    public string HandHighCard { get; set; } = "High Card";
    
    // Errors
    public string InvalidAction { get; set; } = "(Color::Yellow)Invalid action. Available: {{options}}";
    public string CannotCheck { get; set; } = "(Color::Yellow)Cannot check - must call or fold";
    public string InvalidRaise { get; set; } = "(Color::Yellow)Invalid raise amount. Minimum: (Color::Green)${{min}}";
    public string InsufficientChips { get; set; } = "(Color::Yellow)Insufficient chips";
    
    // Status
    public string YourChips { get; set; } = "Your chips: (Color::Green)${{amount}}";
    public string PlayerEliminated { get; set; } = "(Color::Accent){{name}} (Color::White)was eliminated";
    public string NextHandStarting { get; set; } = "(Color::Yellow)Next hand starting in (Color::Accent){{seconds}} (Color::White)seconds...";
    // @formatter:on
}
