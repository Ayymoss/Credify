namespace Credify.Configuration.Translations;

public class RouletteTranslations
{
    // @formatter:off
    private string PrefixLong { get; set; } = "[(Color::Green)Roulette(Color::White)]";
    private string PrefixShort { get; set; } = "[(Color::Green)RL(Color::White)]";
    public string BallStopped { get; set; } = "The ball landed on (Color::Accent){{number}} (Color::White)({{color}})";
    public string Won { get; set; } = "You won (Color::Green)${{amount}}!";
    public string Lost { get; set; } = "You lost (Color::Red)${{amount}}";
    public string SpinningWheel { get; set; } = "(Color::Cyan)Spinning the wheel";
    public string BetTimeout { get; set; } = "(Color::Red)You took too long to bet. (Color::White)You have been removed from the game";
    public string JoinDuringActiveMessage { get; set; } = "(Color::Yellow)There's an on-going game, please wait...";
    public string LeaveMessage { get; set; } = "(Color::Yellow)You have left the game! (Color::White)(!crrl to join)";
    public string InputTimeout { get; set;} = "(Color::Red)You took too long to respond.";
    public string InsideBetSelected { get; set; } = "(Color::Blue)You've selected Inside Bet";
    public string InsidePickNumbers { get; set; } = "(Color::Yellow)Pick your number(s) (0-36)";
    public string InsideBetOptions { get; set; } = "(Color::Yellow)Select for (Color::White)Straight Up, Split, Street, Corner, Six Line"; 
    public string OutsideBetSelected { get; set; } = "(Color::Purple)You've selected Outside Bet";
    public string OutsideSelectBet { get; set; } = "(Color::Yellow)Select your bet";
    public string OutsideBetOptions { get; set; } = "(Color::Yellow)Acceptable Inputs: (Color::White)Red, Black, Odd, Even, Low, High, (Color::Cyan)D1, D2, D3, (Color::Pink)C1, C2, C3";
    public string InvalidRangeOrDuplicateNumbers { get; set; } = "(Color::Red)Invalid range or duplicate numbers";
    public string InvalidNumberOfArguments { get; set; } = "(Color::Red)Invalid number of arguments";
    public string HowMuchToBet { get; set; } = "(Color::Yellow)How much do you want to bet? (Color::White)You have (Color::Green)${{credits}} (Color::White)available";
    public string InvalidBetInput { get; set; } = "(Color::Red)Invalid bet input";
    public string InvalidBetCategory { get; set; } = "(Color::Red)Invalid bet category";
    public string InvalidBetType { get; set; } = "(Color::Red)Invalid bet type";
    public string InnerOrOutsideBet { get; set; } = "(Color::Yellow)Inside or Outside bet?";
    public string InnerOrOutsideBetAcceptableInputs { get; set; } = "(Color::Yellow)Acceptable Inputs: (Color::White)(O)utside, (I)nside";
    public string Broke { get; set; } = "(Color::Red)You are broke! (Color::White)You have been removed from the game";
    public string Disabled { get; set; } = "(Color::Red)Roulette is disabled";
    public string HouseWin { get; set; } = "(Color::Yellow){{playerName}} just won (Color::Green)${{payout}} credits (Color::Yellow)in (Color::Pink)Roulette !crrl (Color::Yellow)betting on (Color::Accent){{number}}!";
    public string PlayerStartedRoulette { get; set; } = "(Color::Yellow){{playerName}} has joined Roulette! (Color::White)Place your bets! (Color::Accent)!crrl";
    public string BetAccepted { get; set; } = "(Color::Accent)Input accepted! (Color::White)Please wait for other players...";
    public string MinimumBet { get; set; } = "(Color::Red)Minimum bet is $10";
    // @formatter:on

    public string Prefix(string message) => $"{PrefixShort} {message}";
    public string LongPrefix(string message) => $"{PrefixLong} {message}";
}
