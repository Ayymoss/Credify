namespace Credify.Configuration.Translations;

/// <summary>
/// Strings shared by the simple single-shot gambling commands (coin flip, RPS) plus the
/// common bet-validation messages used across gambling commands.
/// </summary>
public class GamblingTranslations
{
    // @formatter:off
    public string GambleWon { get; set; } = "You won (Color::Accent)${{wonAmount}} credits! New balance (Color::Accent)${{newBalance}}";
    public string GambleLost { get; set; } = "You lost (Color::Accent)${{lostAmount}} credits. New balance (Color::Accent)${{newBalance}}! (Color::Green)Try again! You could win big!";
    public string GambleDraw { get; set; } = "You drew! (Color::Accent)${{amount}} credits returned. Balance (Color::Accent)${{newBalance}}";
    public string MinimumAmount { get; set; } = "(Color::Yellow)Minimum amount is 10";
    public string MaximumAmount { get; set; } = "(Color::Yellow)Maximum amount is (Color::Green){{maxAmount}}";
    public string BadRpsArgument { get; set; } = "(Color::Yellow)Invalid argument. (Color::White)Use (Color::Accent)!crrps <rock|paper|scissors> <bet>";
    public string BadCfArgument { get; set; } = "(Color::Yellow)Invalid argument. (Color::White)Use (Color::Accent)!crcf <h|t> <bet>";
    public string CommandRockPaperScissorsDescription { get; set; } = "Play rock paper scissors";
    public string CommandCoinFlipDescription { get; set; } = "Bet your money on a coin flip";
    // @formatter:on
}
