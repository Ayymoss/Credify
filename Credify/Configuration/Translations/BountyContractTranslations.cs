namespace Credify.Configuration.Translations;

public class BountyContractTranslations
{
    // @formatter:off
    public string CommandPlaceBountyDescription { get; set; } = "Place a bounty on a player";
    public string CommandListBountiesDescription { get; set; } = "List all active bounties";
    public string BountyContractDisabled { get; set; } = "(Color::Yellow)Bounty contracts are disabled";
    public string BountyContractPlaced { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] You placed a (Color::Green)${{amount}} (Color::White)bounty on (Color::Accent){{target}}(Color::White). Fee: (Color::Yellow)${{fee}}";
    public string BountyContractTargeted { get; set; } = "[(Color::Pink)BOUNTY(Color::White)] (Color::Red)WARNING: (Color::White)There's a (Color::Green)${{amount}} (Color::White)bounty on your head from (Color::Accent){{placer}}(Color::White)!";
    public string BountyContractAnnouncement { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Red)BOUNTY CONTRACT: (Color::Green)${{amount}} (Color::White)on (Color::Accent){{target}}(Color::White)!";
    public string BountyContractClaimed { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{killer}} (Color::White)collected (Color::Green)${{amount}} (Color::White)in bounties on (Color::Accent){{victim}}(Color::White)!";
    public string NoBountiesActive { get; set; } = "(Color::Yellow)No active bounties";
    public string BountiesHeader { get; set; } = "(Color::Accent)--Active Bounties--";
    public string BountyListEntry { get; set; } = "[(Color::Accent)#{{rank}}(Color::White)] (Color::Green)${{amount}} (Color::White)on (Color::Accent){{target}}";
    public string BountiesMoreCount { get; set; } = "(Color::Yellow)...and {{count}} more bounties";
    // @formatter:on
}
