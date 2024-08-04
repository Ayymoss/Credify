namespace Credify.Configuration.Translations;

public class BlackjackTranslations
{
    // @formatter:off
    public string Title { get; set; } = "[(Color::Pink)Blackjack(Color::White)]";
    public string TitleShort { get; set; } = "[(Color::Pink)BJ(Color::White)]";
    public string Join { get; set; } = "(Color::Yellow)You have joined the game! (Color::White)(!crbet to leave)";
    public string Leave { get; set; } = "(Color::Yellow)You have left the game! (Color::White)(!crbet to join)";
    public string StartingGame { get; set; } = "(Color::Accent)Starting a new game with {{count}} player(s)";
    public string PlaceBets { get; set; } = "(Color::Yellow)Type the amount of credits you'd like to bet. (Color::White)You have (Color::Green)${{credits}} (Color::White)available";
    public string BetTimeout { get; set; } = "(Color::Yellow)You took too long to bet. (Color::White)You have been removed from the game";
    public string DealerInitialCard { get; set; } = "Dealer's up-card: (Color::Accent){{card}}";
    public string DealerCards { get; set; } = "Dealer's cards [(Color::Yellow){{total}}(Color::White)]: (Color::Accent){{cards}}";
    public string PlayerCards { get; set; } = "Your cards [(Color::Yellow){{total}}(Color::White)]: (Color::Accent){{cards}}";
    public string BlackjackConfirmation { get; set; } = "(Color::Accent)You have blackjack! (Color::White)Standing...";
    public string Announcement { get; set; } = "(Color::Accent){{name}} (Color::White)has (Color::Purple)blackjack(Color::White), winning (Color::Green)${{amount}}(Color::White)! (Color::Accent)Play with (Color::Yellow)!crbet";
    public string JoinAnnouncement { get; set; } = "(Color::Accent){{name}} (Color::White)has joined blackjack with {{count}} other(s)! (Color::Accent)Play with (Color::Yellow)!crbet";
    public string PlayersDeciding { get; set; } = "(Color::Yellow)Waiting for {{count}} player(s) to decide...";
    public string PlayerDecision { get; set; } = "Type: (Color::Accent)[H]it (Color::White)to hit. (Color::Accent)[S]tand (Color::White)to stand. (Color::Accent)[C]ards (Color::White)to see your cards";
    public string PlayerBustConfirmation { get; set; } = "(Color::Yellow)You busted!";
    public string BlackjackPush { get; set; } = "(Color::Yellow)Blackjack Push! (Color::White)You get your bet back!";
    public string DealerBust { get; set; } = "(Color::Accent)Dealer busted with {{houseValue}}! (Color::White)You won!";
    public string Win { get; set; } = "(Color::Accent)You won with {{playerValue}}!";
    public string Lose { get; set; } = "(Color::Yellow)You lost with {{playerValue}}!";
    public string Push { get; set; } = "(Color::Yellow)Push! (Color::White)You get your bet back!";
    public string Payout { get; set; } = "(Color::White)You won (Color::Green)${{amount}} (Color::White)with a bet of (Color::Green)${{bet}}";
    public string NewDeckShuffled { get; set; } = "(Color::Accent)New deck shuffled!";
    public string AcceptedBet { get; set; } = "(Color::Yellow)Accepted bet of (Color::Green)${{amount}}";
    public string WaitingForBets { get; set; } = "(Color::Yellow)Waiting for {{count}} player(s) to bet...";
    public string PlayerBust { get; set; } = "(Color::Red)Bust! (Color::White)[(Color::Yellow){{total}}(Color::White)]: {{cards}}";
    public string PlayerHit { get; set; } = "(Color::Accent)Hit! (Color::White)[(Color::Yellow){{total}}(Color::White)]: {{cards}}";
    public string PlayerStand { get; set; }= "(Color::Green)Stand! (Color::White)[(Color::Yellow){{total}}(Color::White)]";
    public string Disabled { get; set; } = "(Color::Yellow)Blackjack is disabled";
    public string Queued { get; set; } = "(Color::Yellow)You have been queued for the next game";
    public string InsufficientFunds { get; set; } = "(Color::Yellow)You have been removed. (Color::White)You do not have enough credits to play";
    public string OutcomeBlackjack { get; set; } = "(Color::Pink)BJ";
    public string OutcomeWin { get; set; } = "(Color::Green)W";
    public string OutcomeLose { get; set; } = "(Color::Red)L";
    public string OutcomePush { get; set; } = "(Color::Yellow)P";
    public string PlayerOutcomeMessage { get; set; } = "(Color::White)[{{outcome}}(Color::White)] (Color::Accent){{name}} (Color::White)((Color::Yellow){{total}}(Color::White))";
    // @formatter:on
}
