using System.Collections.Concurrent;

namespace Credify.Services;

/// <summary>
/// Tracks the last known game time per server for fair timing calculations.
/// Updated from game events to maintain accurate per-server timestamps.
/// Builds a model of GameTime progression to enable sub-second precision estimates.
/// </summary>
public class ServerTimeTracker
{
    private readonly ConcurrentDictionary<long, TimeTrackingInfo> _serverTimes = new();
    private readonly ConcurrentDictionary<long, GameTimeModel> _serverModels = new();

    private const int SampleWindowSize = 10;
    private const int MinimumSamplesForModel = 2;
    private const double EmaAlpha = 0.3; // EMA smoothing factor (0.3 = more weight to recent values)

    /// <summary>
    /// Updates the tracking info for a server from a game event.
    /// Should be called on events that have timing data (kills, messages, joins, leaves, etc.)
    /// Detects GameTime resets (map rotation) when new GameTime less than previous GameTime.
    /// </summary>
    public void UpdateFromEvent(long serverEndpoint, long? gameTime, DateTime eventTime)
    {
        var previousInfo = _serverTimes.TryGetValue(serverEndpoint, out var prev) ? prev : null;

        // Detect GameTime reset (map rotation) - if new GameTime is less than previous, it reset
        var gameTimeReset = false;
        if (gameTime.HasValue && previousInfo?.GameTime.HasValue == true)
        {
            if (gameTime.Value < previousInfo.GameTime.Value)
            {
                gameTimeReset = true;
            }
        }

        // If GameTime reset, clear model and don't store GameTime (will use DateTime fallback)
        // Otherwise, store the GameTime normally and update the model
        if (gameTimeReset)
        {
            _serverTimes[serverEndpoint] = new TimeTrackingInfo(null, eventTime, null);
            _serverModels.TryRemove(serverEndpoint, out _); // Clear model on reset
        }
        else
        {
            _serverTimes[serverEndpoint] = new TimeTrackingInfo(gameTime, eventTime, gameTime.HasValue ? eventTime : null);

            // Update GameTime model if we have valid GameTime
            if (gameTime.HasValue)
            {
                UpdateGameTimeModel(serverEndpoint, gameTime.Value, eventTime, previousInfo);
            }
        }
    }

    /// <summary>
    /// Estimates what the GameTime would be at a given EventTime using the GameTime progression model.
    /// Uses EMA-based rate calculation for sub-second precision, independent of RCON lag.
    /// </summary>
    public double? EstimateGameTimeAt(long serverEndpoint, DateTime targetEventTime)
    {
        if (!_serverTimes.TryGetValue(serverEndpoint, out var info))
        {
            return null;
        }

        if (!info.GameTime.HasValue || !info.GameTimeRecordedAt.HasValue)
        {
            return null;
        }

        // Try to use model if we have enough samples
        if (_serverModels.TryGetValue(serverEndpoint, out var model) && model.SampleCount >= MinimumSamplesForModel)
        {
            // Use model-based estimation with EMA rate for fractional precision
            var elapsedEventTime = (targetEventTime - info.GameTimeRecordedAt.Value).TotalSeconds;
            var estimatedGameTime = info.GameTime.Value + (elapsedEventTime * model.EmaRate);

            return estimatedGameTime;
        }

        // Fallback to 1:1 assumption if model not available
        var timeSinceLastGameTime = (targetEventTime - info.GameTimeRecordedAt.Value).TotalSeconds;
        var simpleEstimatedGameTime = info.GameTime.Value + timeSinceLastGameTime;

        return simpleEstimatedGameTime;
    }

    /// <summary>
    /// Updates the GameTime progression model with a new sample.
    /// Calculates rate and updates EMA.
    /// </summary>
    private void UpdateGameTimeModel(long serverEndpoint, long gameTime, DateTime eventTime, TimeTrackingInfo? previousInfo)
    {
        var model = _serverModels.GetOrAdd(serverEndpoint, _ => new GameTimeModel());

        lock (model)
        {
            // Add new sample
            model.Samples.Enqueue((gameTime, eventTime));

            // Maintain window size
            while (model.Samples.Count > SampleWindowSize)
            {
                model.Samples.Dequeue();
            }

            // Calculate rate if we have at least 2 samples
            if (model.Samples.Count < 2) return;
            var samples = model.Samples.ToArray();
            var latest = samples[^1];
            var previous = samples[^2];

            var gameTimeDelta = latest.GameTime - previous.GameTime;
            var eventTimeDelta = (latest.EventTime - previous.EventTime).TotalSeconds;

            if (!(eventTimeDelta > 0)) return;
            var currentRate = gameTimeDelta / eventTimeDelta;

            // Update EMA: EMA = alpha * current + (1 - alpha) * previous_EMA
            if (model.SampleCount == 0)
            {
                model.EmaRate = currentRate;
            }
            else
            {
                model.EmaRate = (EmaAlpha * currentRate) + ((1 - EmaAlpha) * model.EmaRate);
            }

            model.SampleCount = model.Samples.Count;
        }
    }
}

/// <summary>
/// Represents timing information for a server at a point in time.
/// </summary>
/// <param name="GameTime">Game engine time in seconds (null if unavailable)</param>
/// <param name="EventTime">IW4MAdmin event processing time (always available)</param>
/// <param name="GameTimeRecordedAt">When the GameTime was last recorded (for extrapolation)</param>
public record TimeTrackingInfo(long? GameTime, DateTime EventTime, DateTime? GameTimeRecordedAt = null);

/// <summary>
/// Model of GameTime progression for a server, using EMA to track advancement rate.
/// </summary>
internal class GameTimeModel
{
    public Queue<(long GameTime, DateTime EventTime)> Samples { get; } = new();
    public double EmaRate { get; set; } = 1.0; // Default to 1:1 if no data
    public int SampleCount { get; set; } = 0;
}
