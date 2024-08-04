namespace Credify.Configuration.Translations;

public class PassiveTranslations
{
    // @formatter:off
    public string FriendlyTypingTestGame { get; set; } = "Fast Fingers";
    public string FriendlyMathTestGame { get; set; } = "Quick Maffs";
    public string FriendlyCountdownGame { get; set; } = "Countdown";
    public string FriendlyTriviaGame { get; set; } = "Trivia";
    public string GenericNoAnswer { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)Times up! No one answered! (Color::White)The answer was (Color::Accent){{question}}";
    public string TypingTestNoAnswer { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)Times up! No one answered!";
    public string TypingTestWinnerBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)with a time of (Color::Accent){{time}} (Color::White)seconds!";
    public string MathTestWinnerBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{name}} (Color::White)won (Color::Green)${{amount}} (Color::White)with a time of (Color::Accent){{time}} (Color::White)seconds! (Color::White)The answer was (Color::Accent){{question}}";
    public string ReactionTell { get; set; }= "You won (Color::Green)${{amount}}(Color::White). New balance (Color::Green)${{balance}}";
    public string ReactionBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] [(Color::Accent){{name}}(Color::White)] (Color::White)First to Type! {{question}}";
    public string TriviaBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] [(Color::Accent){{name}}(Color::White)] (Color::Yellow){{question}}";
    public string TriviaWinBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{count}} (Color::White)winner(s) with (Color::Green)${{amount}} (Color::White)paid out! The answer was (Color::Accent){{question}}";
    public string AlreadyAnswered { get; set; }= "(Color::Yellow)You already answered!";
    public string AnswerAccepted { get; set; } = "(Color::White)Your answer of (Color::Accent){{answer}} (Color::White)has been accepted! (Color::Yellow)Please wait for results";
    public string AnswerAcceptedDefinition { get; set; } = "Definition of (Color::Accent){{word}}(Color::White), (Color::Yellow){{definition}}";
    public string TriviaNoWinner { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Yellow)No one answered correctly! (Color::White)The answer was (Color::Accent){{question}}";
    public string CountdownWordNotFound { get; set; } = "(Color::Yellow){{word}} (Color::White)was not found in the dictionary";
    public string CountdownWinBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] (Color::Accent){{count}} (Color::White)winner(s) with (Color::Green)${{amount}} (Color::White)paid out! Accepted answers were (Color::Accent){{words}}";
    public string CountdownBroadcast { get; set; } = "[(Color::Pink){{pluginName}}(Color::White)] [(Color::Accent){{name}}(Color::White)] (Color::Yellow)Find the best word in these letters, (Color::Accent){{question}}";
    // @formatter:on
}
