using Credify.Configuration.Translations;

namespace Credify.Configuration;

public class TranslationsRoot
{
    public CoreTranslations Core { get; set; } = new();
    public RaffleTranslations Raffle { get; set; } = new();
    public PassiveTranslations Passive { get; set; } = new();
    public BlackjackTranslations Blackjack { get; set; } = new();
    public RouletteTranslations Roulette { get; set; } = new();
    public QuestsTranslations Quests { get; set; } = new();
    public PokerTranslations Poker { get; set; } = new();
    public MinefieldTranslations Minefield { get; set; } = new();
    public AdminTranslations Admin { get; set; } = new();
    public EconomyTranslations Economy { get; set; } = new();
    public HelpTranslations Help { get; set; } = new();
    public ShopTranslations Shop { get; set; } = new();
    public GamblingTranslations Gambling { get; set; } = new();
    public SlotsTranslations Slots { get; set; } = new();
    public WheelTranslations Wheel { get; set; } = new();
    public StreakTranslations Streak { get; set; } = new();
    public BountyContractTranslations BountyContract { get; set; } = new();
}
