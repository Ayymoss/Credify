namespace Credify.Configuration.Translations;

public class DuelTranslations
{
    // @formatter:off
    public string Description { get; set; } = "Challenge a player to a kill-race duel for credits";
    public string AcceptDescription { get; set; } = "Accept a pending duel challenge";
    public string Disabled { get; set; } = "(Color::Yellow)Duels are disabled";
    public string CannotSelf { get; set; } = "(Color::Yellow)You can't duel yourself.";
    public string Busy { get; set; } = "(Color::Yellow){{name}} is already in a duel.";
    public string AlreadyChallenged { get; set; } = "(Color::Yellow){{name}} already has a pending challenge.";
    public string Challenged { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::Accent){{challenger}} (Color::White)challenges you: first to (Color::Accent){{kills}} (Color::White)kills for (Color::Green)${{amount}}(Color::White)! Type (Color::Accent)!crduelaccept";
    public string ChallengeSent { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::White)Challenge sent to (Color::Accent){{target}} (Color::White)for (Color::Green)${{amount}}(Color::White). Waiting for them to accept...";
    public string TargetCantAfford { get; set; } = "(Color::Yellow){{name}} doesn't have enough credits to cover that stake.";
    public string NoPending { get; set; } = "(Color::Yellow)You have no pending duel challenge.";
    public string Started { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::Accent){{a}} (Color::White)vs (Color::Accent){{b}} (Color::White)- first to (Color::Accent){{kills}} (Color::White)kills wins (Color::Green)${{pot}}(Color::White)! GO!";
    public string Score { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::Accent){{a}} {{scoreA}} (Color::White)- (Color::Accent){{scoreB}} {{b}}";
    public string Won { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::Green)You won the duel! (Color::White)+(Color::Green)${{payout}}";
    public string Lost { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::Red)You lost the duel to (Color::Accent){{winner}}(Color::White).";
    public string AnnounceResult { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{winner}} (Color::White)beat (Color::Accent){{loser}} (Color::White)in a duel for (Color::Green)${{pot}}(Color::White)! (Color::Accent)!crduel";
    public string ChallengeExpired { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::Yellow)Duel challenge expired - no one accepted.";
    public string Expired { get; set; } = "[(Color::Pink)Duel(Color::White)] (Color::Yellow)Duel expired - your stake was refunded.";
    // @formatter:on
}
