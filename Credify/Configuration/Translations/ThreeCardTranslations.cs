namespace Credify.Configuration.Translations;

public class ThreeCardTranslations
{
    // @formatter:off
    public string Title { get; set; } = "[(Color::Pink)Three-Card Poker(Color::White)]";
    public string TitleShort { get; set; } = "[(Color::Pink)3CP(Color::White)]";
    public string Description { get; set; } = "Play Three-Card Poker against the house";
    public string Disabled { get; set; } = "(Color::Yellow)Three-Card Poker is disabled";

    public string EnterAnte { get; set; } = "Enter your (Color::Accent)ante(Color::White). You have (Color::Accent)${{credits}}(Color::White). Add (Color::Accent)pp(Color::White) for a Pair Plus side bet, e.g. (Color::Accent)100 pp";
    public string InvalidAnte { get; set; } = "(Color::Red)Invalid ante. (Color::White)Enter an amount, optionally with (Color::Accent)pp(Color::White).";
    public string NotEnoughForPairPlus { get; set; } = "(Color::Red)Not enough credits for that ante + Pair Plus.";

    public string YourHand { get; set; } = "Your hand: {{cards}} (Color::Yellow)({{name}})";
    public string Decision { get; set; } = "(Color::Green)play(Color::White) to match your ante ({{ante}}), or (Color::Red)fold(Color::White) to forfeit it.";
    public string NotEnoughToPlay { get; set; } = "(Color::Red)Not enough to play (Color::White)- type (Color::Red)fold(Color::White).";

    public string DealerHand { get; set; } = "Dealer: {{cards}} (Color::Yellow)({{name}}){{qualify}}";
    public string DidNotQualify { get; set; } = " (Color::Gray)- didn't qualify";

    public string Win { get; set; } = "(Color::Green)You win! (Color::White)Net (Color::Green)+${{net}}(Color::White). Balance: (Color::Accent)${{balance}}";
    public string Lose { get; set; } = "(Color::Red)Dealer wins. (Color::White)Net (Color::Red)${{net}}(Color::White). Balance: (Color::Accent)${{balance}}";
    public string Push { get; set; } = "(Color::Yellow)Push. (Color::White)Net ${{net}}. Balance: (Color::Accent)${{balance}}";
    public string Fold { get; set; } = "You folded. Net (Color::Red)${{net}}(Color::White). Balance: (Color::Accent)${{balance}}";
    public string PairPlusHit { get; set; } = "(Color::Pink)Pair Plus(Color::White) paid (Color::Green)+${{amount}}(Color::White)!";

    public string NextHand { get; set; } = "Enter your next (Color::Accent)ante(Color::White), or type (Color::Accent)!crtcp(Color::White) to leave.";
    public string Leave { get; set; } = "You left the Three-Card Poker table.";
    // @formatter:on
}
