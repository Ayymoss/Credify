using System.Text;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Interfaces;

namespace Credify.Chat.Passive.ChatGames;

public class ChatUtils(CredifyConfiguration credifyConfig, ServerTimeTracker serverTimeTracker)
{
    private IManager? _manager;

    public void SetManager(IManager manager) => _manager = manager;
    
    public ServerTimeTracker GetServerTimeTracker() => serverTimeTracker;

    /// <summary>
    /// Broadcasts messages to all servers in parallel and returns per-server timing info for fair reaction calculation.
    /// </summary>
    public async Task<Dictionary<long, TimeTrackingInfo>> BroadcastToAllServers(string[] messages)
    {
        var broadcastTimes = new Dictionary<long, TimeTrackingInfo>();
        
        if (_manager is null) return broadcastTimes;

        var servers = _manager.GetServers()
            .Where(s => s.ConnectedClients.Count > 0)
            .ToList();
        
        // Capture the broadcast time using DateTime (broadcasts don't generate GameTime events)
        // Estimate GameTime at broadcast time based on last known GameTime
        var broadcastEventTime = DateTime.UtcNow;
        
        // Record timing for all servers - estimate GameTime at broadcast time using model
        foreach (var server in servers)
        {
            // Estimate what GameTime would be at broadcast time (returns double? for fractional precision)
            var estimatedGameTime = serverTimeTracker.EstimateGameTimeAt(server.EndPoint, broadcastEventTime);
            
            // Convert double? to long? for TimeTrackingInfo (round to nearest whole second for storage)
            long? estimatedGameTimeWhole = estimatedGameTime.HasValue ? (long)Math.Round(estimatedGameTime.Value) : null;
            var timeInfo = new TimeTrackingInfo(estimatedGameTimeWhole, broadcastEventTime);
            broadcastTimes[server.EndPoint] = timeInfo;
        }
        
        // Broadcast to all servers in parallel
        var broadcastTasks = servers.Select(server => server.BroadcastAsync(messages));
        await Task.WhenAll(broadcastTasks);
        
        return broadcastTimes;
    }

    public static List<T> Shuffle<T>(List<T> list)
    {
        var n = list.Count;
        while (n > 1)
        {
            var k = Random.Shared.Next(n--);
            (list[n], list[k]) = (list[k], list[n]);
        }

        return list;
    }

    public string GameNameToFriendly(string gameName)
    {
        return gameName switch
        {
            "CountdownGame" => credifyConfig.Translations.Passive.FriendlyCountdownGame,
            "MathTestGame" => credifyConfig.Translations.Passive.FriendlyMathTestGame,
            "TypingTestGame" => credifyConfig.Translations.Passive.FriendlyTypingTestGame,
            "TriviaGame" => credifyConfig.Translations.Passive.FriendlyTriviaGame,
            "AcronymGame" => credifyConfig.Translations.Passive.FriendlyAcronymGame,
            "CompleteTheWordGame" => credifyConfig.Translations.Passive.FriendlyCompleteWordGame,
            _ => throw new ArgumentOutOfRangeException(gameName, "Invalid game name")
        };
    }

    public static string DecodeBase64(string base64String)
    {
        var bytes = Convert.FromBase64String(base64String);
        return Encoding.UTF8.GetString(bytes);
    }
}
