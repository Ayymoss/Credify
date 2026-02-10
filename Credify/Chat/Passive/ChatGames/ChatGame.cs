using Credify.Chat.Passive.ChatGames.Models;
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
    /// Calculates fair reaction time in seconds based on per-server timing using model-based GameTime estimates.
    /// Uses EMA-based GameTime progression model for sub-second precision, independent of RCON lag.
    /// </summary>
    protected double CalculateReactionTime(long serverEndpoint, long? answerGameTime, DateTime answerEventTime, ServerTimeTracker serverTimeTracker)
    {
        if (!GameInfo.ServerBroadcastTimes.TryGetValue(serverEndpoint, out var broadcastTime))
        {
            // Server wasn't tracked at broadcast time, use fallback
            var fallbackTime = (answerEventTime - GameInfo.Started.DateTime).TotalSeconds;
            return fallbackTime;
        }
        
        // Use model-based fractional GameTime estimates for sub-second precision
        // Get fractional estimates for both broadcast and answer times
        var broadcastGameTimeFractional = serverTimeTracker.EstimateGameTimeAt(serverEndpoint, broadcastTime.EventTime);
        var answerGameTimeFractional = answerGameTime.HasValue 
            ? serverTimeTracker.EstimateGameTimeAt(serverEndpoint, answerEventTime)
            : null;
        
        // Prefer model-based GameTime if available for both answer and broadcast
        if (broadcastGameTimeFractional.HasValue && answerGameTimeFractional.HasValue)
        {
            // Use fractional GameTime difference (model-based, accounts for RCON lag)
            var gameTimeDiff = answerGameTimeFractional.Value - broadcastGameTimeFractional.Value;
            
            // Check for negative or suspicious values
            if (gameTimeDiff < 0 || gameTimeDiff < 0.01)
            {
                // Fall through to DateTime comparison
            }
            else
            {
                return gameTimeDiff;
            }
        }
        
        // Fallback: Try whole-second GameTime comparison if fractional estimates unavailable
        if (answerGameTime.HasValue && broadcastTime.GameTime.HasValue)
        {
            var gameTimeDiffWhole = answerGameTime.Value - broadcastTime.GameTime.Value;
            var realTimeDiff = (answerEventTime - broadcastTime.EventTime).TotalSeconds;
            
            if (gameTimeDiffWhole < 0 || (gameTimeDiffWhole == 0 && realTimeDiff < 0.01))
            {
                // Fall through to DateTime comparison
            }
            else
            {
                // Use real-time difference for sub-second precision when GameTime difference is 0
                return realTimeDiff;
            }
        }
        
        // Fallback to DateTime comparison if GameTime not available or suspicious
        var dateTimeDiff = (answerEventTime - broadcastTime.EventTime).TotalSeconds;
        return dateTimeDiff;
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
