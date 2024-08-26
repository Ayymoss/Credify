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
}
