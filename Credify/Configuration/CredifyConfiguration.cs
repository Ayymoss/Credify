namespace Credify.Configuration;

public class CredifyConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public BaseConfiguration Core { get; set; } = new();
    public ChatGameConfiguration ChatGame { get; set; } = new();
    public BlackjackConfiguration Blackjack { get; set; } = new();
    public RouletteConfiguration Roulette { get; set; } = new();
    public Shop Shop { get; set; } = new();
    public QuestConfiguration Quest { get; set; } = new();
    public StreakConfiguration Streak { get; set; } = new();
    public BountyConfiguration Bounty { get; set; } = new();
    public SlotsConfiguration Slots { get; set; } = new();
    public WheelConfiguration Wheel { get; set; } = new();
    public BountyContractConfiguration BountyContract { get; set; } = new();
    public PokerConfiguration Poker { get; set; } = new();
    public TranslationsRoot Translations { get; set; } = new();
}
