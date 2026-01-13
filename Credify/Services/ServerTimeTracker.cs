using System.Collections.Concurrent;

namespace Credify.Services;

/// <summary>
/// Tracks the last known game time per server for fair timing calculations.
/// Updated from game events to maintain accurate per-server timestamps.
/// </summary>
public class ServerTimeTracker
{
    private readonly ConcurrentDictionary<long, TimeTrackingInfo> _serverTimes = new();

    /// <summary>
    /// Updates the tracking info for a server from a game event.
    /// Should be called on events that have timing data (kills, messages, etc.)
    /// </summary>
    public void UpdateFromEvent(long serverEndpoint, long? gameTime, DateTime eventTime)
    {
        _serverTimes[serverEndpoint] = new TimeTrackingInfo(gameTime, eventTime);
    }

    /// <summary>
    /// Gets the last known timing info for a server.
    /// Returns current time with no GameTime if server hasn't been seen.
    /// </summary>
    public TimeTrackingInfo GetLastKnownTime(long serverEndpoint)
    {
        return _serverTimes.TryGetValue(serverEndpoint, out var info)
            ? info
            : new TimeTrackingInfo(null, DateTime.UtcNow);
    }
}

/// <summary>
/// Represents timing information for a server at a point in time.
/// </summary>
/// <param name="GameTime">Game engine time in seconds (null if unavailable)</param>
/// <param name="EventTime">IW4MAdmin event processing time (always available)</param>
public record TimeTrackingInfo(long? GameTime, DateTime EventTime);
