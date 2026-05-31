namespace Credify.Configuration.Translations;

public class CrashTranslations
{
    // @formatter:off
    public string Title { get; set; } = "[(Color::Pink)Crash(Color::White)]";
    public string TitleShort { get; set; } = "[(Color::Pink)CR(Color::White)]";
    public string Description { get; set; } = "Bet, ride the rocket, and cash out before it crashes";
    public string Disabled { get; set; } = "(Color::Yellow)Crash is disabled";
    public string Join { get; set; } = "(Color::Yellow)Joined Crash! (Color::White)Place a bet for the next launch.";
    public string Leave { get; set; } = "(Color::Yellow)You have left Crash. (Color::White)(!crcrash to play)";

    public string EnterStake { get; set; } = "(Color::Yellow)Place your bet for launch! (Color::White)You have (Color::Green)${{credits}}";
    public string BetSyntax { get; set; } = "(Color::White)Type an amount (e.g. (Color::Accent)500(Color::White)) - then (Color::Accent)CASH (Color::White)mid-flight to bank";
    public string BetAccepted { get; set; } = "(Color::Accent)Bet placed: (Color::Green)${{stake}}(Color::White). Waiting for launch...";
    public string AlreadyBet { get; set; } = "(Color::Yellow)You've already bet this round.";
    public string TimeWarning { get; set; } = "(Color::Red)Launching in 5s - bet now!";

    public string Launched { get; set; } = "(Color::Cyan)Rocket launched! (Color::White)Type (Color::Accent)CASH (Color::White)to bank your multiplier!";
    public string Tick { get; set; } = "(Color::Accent)x{{mult}} (Color::White)- type (Color::Accent)CASH";
    public string CashedOut { get; set; } = "(Color::Green)Cashed at x{{mult}}! (Color::White)Won (Color::Green)${{payout}} (Color::White)((Color::Green)+{{profit}}(Color::White))";
    public string Crashed { get; set; } = "(Color::Red)CRASHED at x{{mult}}!";
    public string Lost { get; set; } = "(Color::Red)Crashed at x{{mult}}! (Color::White)Lost (Color::Red)${{stake}}";
    public string NoBets { get; set; } = "(Color::Yellow)No bets placed - no launch this round.";
    // @formatter:on
}
