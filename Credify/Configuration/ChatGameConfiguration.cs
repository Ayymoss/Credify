namespace Credify.Configuration;

public class ChatGameConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public TimeSpan Frequency { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan CountdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MathTestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public TimeSpan TriviaTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TypingTestTimeout { get; set; } = TimeSpan.FromSeconds(10);
    public int MaxPayout { get; set; } = 1_000;
    public int TypingTestTextLength { get; set; } = 10;
    public TriviaToggle EnabledTriviaGames { get; set; } = new();
}
