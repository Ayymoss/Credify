namespace Credify.Configuration;

public class ChatGameConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public TimeSpan Frequency { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan CountdownTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan MathTestTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan TriviaTimeout { get; set; } = TimeSpan.FromSeconds(30);
    public TimeSpan TypingTestTimeout { get; set; } = TimeSpan.FromSeconds(15);
    public TimeSpan CompleteWordTimeout { get; set; } = TimeSpan.FromSeconds(20);
    public TimeSpan AcronymTimeout { get; set; } = TimeSpan.FromSeconds(25);
    public int MaxPayout { get; set; } = 10_000;
    public int TypingTestTextLength { get; set; } = 10;

    /// <summary>
    /// Grace period after timeout before calculating winners.
    /// Allows time for late RCON messages to arrive.
    /// </summary>
    public TimeSpan EndGracePeriod { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Decay exponent for payout calculation. 
    /// 0.5 = square root (default, options-style theta decay)
    /// 1.0 = linear decay
    /// Lower values = more generous to slower answers
    /// </summary>
    public double PayoutDecayExponent { get; set; } = 0.5;

    public PassiveToggle EnabledPassiveGames { get; set; } = new();
}
