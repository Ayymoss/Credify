namespace Credify.Configuration.Translations;

public class MinefieldTranslations
{
    // @formatter:off
    public string Description { get; set; } = "Play Minefield - clear tiles for rising payouts, avoid the mines";
    public string Title { get; set; } = "[(Color::Pink)Minefield(Color::White)]";
    public string TitleShort { get; set; } = "[(Color::Pink)Mine(Color::White)]";
    public string Disabled { get; set; } = "(Color::Yellow)Minefield is disabled";
    public string Leave { get; set; } = "(Color::Yellow)You have left Minefield. (Color::White)(!crmine to play)";

    // Setup Q/A
    public string EnterStake { get; set; } = "(Color::Yellow)Enter your stake. (Color::White)You have (Color::Green)${{credits}}(Color::White). (!crmine to quit)";
    public string EnterMines { get; set; } = "(Color::Yellow)Enter mine count (Color::White)((Color::Accent)1-{{max}}(Color::White), more mines = bigger payouts):";
    public string InvalidStake { get; set; } = "(Color::Yellow)Enter a whole number of credits to stake.";
    public string InvalidMines { get; set; } = "(Color::Yellow)Enter a mine count between (Color::Accent)1(Color::Yellow) and (Color::Accent){{max}}(Color::Yellow).";

    // Armed / status. Lines are kept compact so the visual bar fits on its own line
    // without blowing past the limited chat height.
    public string Armed { get; set; } = "(Color::Accent)Armed: (Color::White){{mines}} mines / {{total}} tiles, stake (Color::Green)${{stake}}";
    public string DigPrompt { get; set; } = "(Color::Accent)Type DIG (Color::White)to clear a tile, (Color::Green)CASH (Color::White)to collect.";
    public string Status { get; set; } = "(Color::Accent)x{{mult}} (Color::White)| cash (Color::Green)${{payout}}";
    // The visual field bar - its own line. {{bar}} is built in code ('#' cleared, '.' left).
    public string ProgressBar { get; set; } = "(Color::White)[{{bar}}(Color::White)] (Color::Accent){{dug}}(Color::White)/{{safe}}";
    // Command hint - its own line every turn so players always see their options.
    public string Commands { get; set; } = "(Color::White)Cmds: (Color::Accent)DIG (Color::Green)CASH (Color::Yellow)STATUS";

    // Outcomes. "pct" is spelled out because the "%" symbol is not renderable in chat.
    // On a bust, {{odds}} is the mine risk of the tile that was hit (per-turn chance).
    // On a cash/clear, {{odds}} is the survival probability of the whole run (chance of
    // clearing that many tiles) - the lower it is, the rarer the run.
    public string Safe { get; set; } = "(Color::Green)SAFE (Color::White)| x{{mult}} | cash (Color::Green)${{payout}}";
    public string Boom { get; set; } = "(Color::Red)BOOM! (Color::White)Hit a mine after {{dug}} tile(s). Lost (Color::Red)${{stake}}(Color::White). That tile had a (Color::Accent){{odds}} pct (Color::White)mine chance.";
    public string Cashed { get; set; } = "(Color::Green)Collected ${{payout}} (Color::White)((Color::Green)+{{profit}}(Color::White) profit) | {{dug}} tiles, {{mines}} mines | (Color::Accent){{odds}} pct (Color::White)chance of clearing this far.";
    public string Cleared { get; set; } = "(Color::Accent)Field cleared! (Color::White)All safe tiles found - max payout.";
    public string AutoCash { get; set; } = "(Color::Yellow)Auto-collected at x{{mult}} (Color::White)(idle or left). Paid (Color::Green)${{payout}} (Color::White)| (Color::Accent){{odds}} pct (Color::White)chance of clearing this far.";

    // Broadcasts. {{odds}} = survival probability of the run (lower = rarer feat).
    public string BroadcastWin { get; set; } = "{{title}} (Color::Accent){{name}} (Color::White)cleared {{dug}} tiles past {{mines}} mines for (Color::Green)${{payout}}(Color::White) - only a (Color::Accent){{odds}} pct (Color::White)chance of getting that far! (Color::Accent)!crmine";
    public string BroadcastFullClear { get; set; } = "{{title}} (Color::Pink)FULL CLEAR! (Color::Accent){{name}} (Color::White)swept every safe tile around {{mines}} mines for (Color::Green)${{payout}}(Color::White) - a (Color::Accent){{odds}} pct (Color::White)run! (Color::Accent)!crmine";
    // @formatter:on
}
