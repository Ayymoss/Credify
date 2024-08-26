namespace Credify.Configuration.Translations;

public class QuestsTranslations
{ 
    // @formatter:off
    public string PermanentHeader { get; set; } = "(Color::Accent)--Permanent Quests--";
    public string DailyHeader { get; set; } = "(Color::Accent)--Daily Quests--";
    public string NoQuests { get; set; } = "(Color::Yellow)You have no active quests."; 
    public string CompletedQuest { get; set; } = "[(Color::Pink)Quest(Color::White)] (Color::Accent){{name}} (Color::White)completed (Color::Accent){{quest}} (Color::White)and was rewarded (Color::Green)${{reward}}(Color::White)!";
    public string Completed { get; set; } = "(Color::Green)Completed";
    public string Quest { get; set; } = "{{questName}} (Color::Accent)({{progressFormat}}(Color::Accent))";
    public string ProgressFormat { get; set; } = "(Color::Yellow){{progress}}(Color::Accent)/(Color::Green){{total}}";
    // @formatter:on
}
