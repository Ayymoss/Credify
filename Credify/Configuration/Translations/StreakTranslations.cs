namespace Credify.Configuration.Translations;

/// <summary>
/// Kill-streak rewards and the automatic streak-bounty announcements (distinct from the
/// player-placed bounty contracts in <see cref="BountyContractTranslations"/>).
/// </summary>
public class StreakTranslations
{
    // @formatter:off
    public string StreakReward { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{streak}} (Color::White)kill streak! (Color::Green)+${{reward}}";
    public string StreakAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{name}} (Color::White)is on a (Color::Red){{streak}} (Color::White)kill streak!";
    public string BountyPlaced { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Red)BOUNTY: (Color::Green)${{amount}} (Color::White)on (Color::Accent){{name}}(Color::White)! Kill them to claim!";
    public string BountyClaimed { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{killer}} (Color::White)claimed the (Color::Green)${{amount}} (Color::White)bounty on (Color::Accent){{victim}}(Color::White)!";
    // @formatter:on
}
