namespace Credify.Configuration.Translations;

public class SlotsTranslations
{
    // @formatter:off
    public string CommandSlotsDescription { get; set; } = "Spin the slot machine";
    public string SlotsDisabled { get; set; } = "(Color::Yellow)Slots is disabled";
    public string SlotsWin { get; set; } = "[(Color::Pink)SLOTS(Color::White)] {{reels}} - (Color::Green)You won ${{profit}}! (Color::White)Balance: (Color::Accent)${{balance}}";
    public string SlotsLose { get; set; } = "[(Color::Pink)SLOTS(Color::White)] {{reels}} - (Color::Red)No match! (Color::White)Lost ${{bet}}. Balance: (Color::Accent)${{balance}}";
    public string SlotsJackpot { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)JACKPOT! (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)on slots!";
    // @formatter:on
}
