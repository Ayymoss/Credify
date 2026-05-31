namespace Credify.Configuration.Translations;

public class WheelTranslations
{
    // @formatter:off
    public string CommandWheelDescription { get; set; } = "Spin the Wheel of Fortune";
    public string WheelDisabled { get; set; } = "(Color::Yellow)Wheel of Fortune is disabled";
    public string WheelWin { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! (Color::Green)Won ${{profit}}! (Color::White)Balance: (Color::Accent)${{balance}}";
    public string WheelBreakEven { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! Break even! Balance: (Color::Accent)${{balance}}";
    public string WheelPartialLoss { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! Lost ${{loss}}. Balance: (Color::Accent)${{balance}}";
    public string WheelSpinning { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Cyan)Spinning the wheel...";
    public string WheelSlowing { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Yellow)The wheel slows down...";
    public string WheelStopping { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Green)The wheel stops!";
    public string WheelTwoXCash { get; set; } = "[(Color::Pink)WHEEL(Color::White)] Landed on (Color::Accent){{segment}}(Color::White)! (Color::Green)2X CASH! Won ${{profit}}! (Color::White)Balance: (Color::Accent)${{balance}}";
    public string WheelCooldown { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Yellow)You can only spin once per day! Next spin available in (Color::Accent){{timeUntilReset}}";
    public string WheelResetSuccess { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Green)Wheel cooldown reset for (Color::Accent){{targetName}}";
    public string WheelResetTarget { get; set; } = "[(Color::Pink)WHEEL(Color::White)] (Color::Green)Your wheel cooldown was reset by (Color::Accent){{originName}}";
    public string WheelBroadcastWin { get; set; } = "[(Color::Pink)WHEEL (Color::Grey)!crwof(Color::White)] (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)((Color::Accent){{segment}} (Color::White)@ (Color::Accent){{percentage}}pct(Color::White)!)";
    public string WheelBroadcastLoss { get; set; } = "[(Color::Pink)WHEEL (Color::Grey)!crwof(Color::White)] (Color::Accent){{name}} (Color::White)lost (Color::Red)${{amount}} (Color::White)((Color::Accent){{segment}} (Color::White)@ (Color::Accent){{percentage}}pct(Color::White)!)";
    // @formatter:on
}
