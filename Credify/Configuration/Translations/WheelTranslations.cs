namespace Credify.Configuration.Translations;

public class WheelTranslations
{
    // @formatter:off
    public string Description { get; set; } = "Spin the Wheel of Fortune";
    public string Disabled { get; set; } = "(Color::Yellow)Wheel of Fortune is disabled";
    public string Win { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! (Color::Green)Won ${{profit}}! (Color::White)Balance: (Color::Accent)${{balance}}";
    public string BreakEven { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! Break even! Balance: (Color::Accent)${{balance}}";
    public string PartialLoss { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! Lost ${{loss}}. Balance: (Color::Accent)${{balance}}";
    public string Spinning { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Cyan)Spinning the wheel...";
    public string Slowing { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Yellow)The wheel slows down...";
    public string Stopping { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Green)The wheel stops!";
    public string TwoXCash { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! (Color::Green)2X CASH! Won ${{profit}}! (Color::White)Balance: (Color::Accent)${{balance}}";
    public string Cooldown { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Yellow)You can only spin once per day! Next spin available in (Color::Accent){{timeUntilReset}}";
    public string ResetSuccess { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Green)Wheel cooldown reset for (Color::Accent){{targetName}}";
    public string ResetTarget { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Green)Your wheel cooldown was reset by (Color::Accent){{originName}}";
    public string BroadcastWin { get; set; } = "[(Color::Pink)WHEEL (Color::Grey)!crwof(Color::White)] (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)((Color::Accent){{segment}} (Color::White)@ (Color::Accent){{percentage}}pct(Color::White)!)";
    public string BroadcastLoss { get; set; } = "[(Color::Pink)WHEEL (Color::Grey)!crwof(Color::White)] (Color::Accent){{name}} (Color::White)lost (Color::Red)${{amount}} (Color::White)((Color::Accent){{segment}} (Color::White)@ (Color::Accent){{percentage}}pct(Color::White)!)";
    // @formatter:on
}
