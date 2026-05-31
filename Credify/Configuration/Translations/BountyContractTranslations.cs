namespace Credify.Configuration.Translations;

public class BountyContractTranslations
{
    // @formatter:off
    public string PlaceDescription { get; set; } = "Place a bounty on a player";
    public string ListDescription { get; set; } = "List all active bounties";
    public string Disabled { get; set; } = "(Color::Yellow)Bounty contracts are disabled";
    public string Placed { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] You placed a (Color::Green)${{amount}} (Color::White)bounty on (Color::Accent){{target}}(Color::White). Fee: (Color::Yellow)${{fee}}";
    public string Targeted { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] (Color::Red)WARNING: (Color::White)There's a (Color::Green)${{amount}} (Color::White)bounty on your head from (Color::Accent){{placer}}(Color::White)!";
    public string Announcement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Red)BOUNTY CONTRACT: (Color::Green)${{amount}} (Color::White)on (Color::Accent){{target}}(Color::White)!";
    public string Claimed { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{killer}} (Color::White)collected (Color::Green)${{amount}} (Color::White)in bounties on (Color::Accent){{victim}}(Color::White)!";
    public string NoneActive { get; set; } = "(Color::Yellow)No active bounties";
    public string Header { get; set; } = "(Color::Accent)--Active Bounties--";
    public string ListEntry { get; set; } = "[(Color::Accent)#{{rank}}(Color::White)] (Color::Green)${{amount}} (Color::White)on (Color::Accent){{target}} (Color::White)by (Color::Accent){{placer}}";
    public string MoreCount { get; set; } = "(Color::Yellow)...and {{count}} more bounties";
    public string OnYouWarning { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] (Color::Red)WARNING: (Color::White)there is a (Color::Green)${{amount}} (Color::White)bounty on you across (Color::Accent){{count}} (Color::White)contract(s)! Watch your back.";
    public string ClaimedDirect { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] (Color::Green)+${{amount}} (Color::White)claimed in bounties on (Color::Accent){{victim}}(Color::White)!";
    // @formatter:on
}
