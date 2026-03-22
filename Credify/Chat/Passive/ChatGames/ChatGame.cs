using Credify.Chat.Passive.ChatGames.Models;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames;

public abstract class ChatGame
{
    public GameState GameState { get; set; }
    protected GameStateInfo GameInfo { get; set; } = new();
    protected readonly SemaphoreSlim MessageReceivedLock = new(1, 1);

    public abstract Task StartAsync();

    /// <summary>
    /// Handle a chat message with timing data for fair reaction calculation.
    /// </summary>
    public abstract Task HandleChatMessageAsync(EFClient client, string message, DateTime eventTime);

    /// <summary>
    /// Maximum sane reaction time in seconds. Any calculated value beyond this is clamped.
    /// </summary>
    private const double MaxReactionTimeSeconds = 120.0;

    /// <summary>
    /// Calculates fair reaction time in seconds, compensated for both send and receive latency.
    /// Raw reaction = true reaction + RCON send delay (broadcast reaching server) + receive delay (answer reaching us).
    /// Uses IW4MAdmin's per-server LatencyMetrics (EMA-smoothed) to subtract both legs.
    /// </summary>
    protected double CalculateReactionTime(EFClient client, DateTime answerEventTime)
    {
        var rawReactionSeconds = (answerEventTime - GameInfo.BroadcastTime).TotalSeconds;

        var metrics = client.CurrentServer.LatencyMetrics;
        var latencyOffsetSeconds = 0.0;

        if (metrics?.GameLogPipelineMs is { } logLatency)
        {
            // GSC companion — precise receive measurement + estimated send (half RCON RTT)
            latencyOffsetSeconds = logLatency / 1000.0;
            
            // Adding send latency back so slower servers are fairly accounted for.
            if (metrics.RconRoundTripMs is { } rtt)
            {
                latencyOffsetSeconds += rtt / 2000.0;
            }
        }
        else if (metrics?.RconRoundTripMs is { } rtt)
        {
            // No GSC companion — full RTT covers both send and receive legs
            latencyOffsetSeconds = rtt / 1000.0;
        }

        var adjustedReaction = rawReactionSeconds - latencyOffsetSeconds;
        return Math.Clamp(adjustedReaction, 0, MaxReactionTimeSeconds);
    }

    /// <summary>
    /// Calculates payout using non-linear decay (sqrt by default).
    /// </summary>
    protected static long CalculatePayout(double reactionTimeSeconds, double timeoutSeconds, int maxPayout, double decayExponent)
    {
        var timeRemainingRatio = Math.Max(0, (timeoutSeconds - reactionTimeSeconds) / timeoutSeconds);
        var decayMultiplier = Math.Pow(timeRemainingRatio, decayExponent);
        var payout = (long)Math.Round(maxPayout * decayMultiplier);
        return Math.Max(100, payout); // Minimum payout
    }
}
