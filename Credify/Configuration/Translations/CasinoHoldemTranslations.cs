namespace Credify.Configuration.Translations;

public class CasinoHoldemTranslations
{
    // @formatter:off
    public string Title { get; set; } = "[(Color::Pink)Casino Hold'em(Color::White)]";
    public string TitleShort { get; set; } = "[(Color::Pink)Hold'em(Color::White)]";
    public string Description { get; set; } = "Play Casino Hold'em against the house";
    public string Disabled { get; set; } = "(Color::Yellow)Casino Hold'em is disabled";

    public string EnterAnte { get; set; } = "Enter your (Color::Accent)ante(Color::White). You have (Color::Accent)${{credits}}(Color::White).";
    public string InvalidAnte { get; set; } = "(Color::Red)Invalid ante. (Color::White)Enter an amount.";

    public string Deal { get; set; } = "You: {{hole}} (Color::White)| Board: {{flop}} (Color::Yellow)({{name}})";
    public string Decision { get; set; } = "(Color::Green)call(Color::White) for 2x your ante ({{call}}), or (Color::Red)fold(Color::White).";
    public string NotEnoughToCall { get; set; } = "(Color::Red)Not enough to call (Color::White)- type (Color::Red)fold(Color::White).";

    public string Reveal { get; set; } = "Dealer: {{hole}} (Color::White)| Board: {{board}} (Color::Yellow)({{name}}){{qualify}}";
    public string DidNotQualify { get; set; } = " (Color::Gray)- didn't qualify";

    public string Win { get; set; } = "(Color::Green)You win! (Color::White)Net (Color::Green)+${{net}}(Color::White). Balance: (Color::Accent)${{balance}}";
    public string Lose { get; set; } = "(Color::Red)Dealer wins. (Color::White)Net (Color::Red)${{net}}(Color::White). Balance: (Color::Accent)${{balance}}";
    public string Push { get; set; } = "(Color::Yellow)Push. (Color::White)Net ${{net}}. Balance: (Color::Accent)${{balance}}";
    public string Fold { get; set; } = "You folded. Net (Color::Red)${{net}}(Color::White). Balance: (Color::Accent)${{balance}}";

    public string NextHand { get; set; } = "Enter your next (Color::Accent)ante(Color::White), or type (Color::Accent)!crholdem(Color::White) to leave.";
    public string Leave { get; set; } = "You left the Casino Hold'em table.";
    // @formatter:on
}
