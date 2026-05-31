namespace Credify.Configuration.Translations;

public class AchievementsTranslations
{
    // @formatter:off
    public string Description { get; set; } = "View your achievements and titles";
    public string Disabled { get; set; } = "(Color::Yellow)Achievements are disabled";
    public string Unlocked { get; set; } = "[(Color::Pink)Achievement(Color::White)] (Color::Accent){{name}} (Color::White)earned the title (Color::Accent){{title}}(Color::White)! (Color::Green)+${{reward}}";
    public string Header { get; set; } = "(Color::Accent)--Achievements-- (Color::White){{unlocked}}/{{total}} unlocked";
    public string UnlockedLabel { get; set; } = "(Color::Green)Earned:";
    public string InProgressLabel { get; set; } = "(Color::Yellow)Next:";
    public string ProgressEntry { get; set; } = "(Color::White){{name}} (Color::Accent){{progress}}(Color::White)/{{threshold}}";
    public string TitleGreeting { get; set; } = "(Color::White)Welcome back, (Color::Accent){{title}} {{name}}(Color::White)!";
    // @formatter:on
}
