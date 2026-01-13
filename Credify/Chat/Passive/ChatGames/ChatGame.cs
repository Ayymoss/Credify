using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Configuration;
using Credify.Services;
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
    public abstract Task HandleChatMessageAsync(EFClient client, string message, long? gameTime, DateTime eventTime);
    
    /// <summary>
    /// Calculates fair reaction time in seconds based on per-server timing.
    /// Uses GameTime if available on both broadcast and answer, falls back to DateTime.
    /// </summary>
    protected double CalculateReactionTime(long serverEndpoint, long? answerGameTime, DateTime answerEventTime)
    {
        if (!GameInfo.ServerBroadcastTimes.TryGetValue(serverEndpoint, out var broadcastTime))
        {
            // Server wasn't tracked at broadcast time, use fallback
            return (answerEventTime - GameInfo.Started.DateTime).TotalSeconds;
        }
        
        // Prefer GameTime if available on both sides (more accurate)
        if (answerGameTime.HasValue && broadcastTime.GameTime.HasValue)
        {
            return answerGameTime.Value - broadcastTime.GameTime.Value;
        }
        
        // Fallback to DateTime comparison
        return (answerEventTime - broadcastTime.EventTime).TotalSeconds;
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
