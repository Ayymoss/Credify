using System.Text;
using Credify.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Chat.Passive.ChatGames;

public class ChatUtils(CredifyConfiguration credifyConfig)
{
    private IManager? _manager;

    public void SetManager(IManager manager) => _manager = manager;

    /// <summary>
    /// Broadcasts messages to all servers in parallel and returns the broadcast wall-clock time.
    /// Latency compensation is handled per-server at answer time via LatencyMetrics.
    /// </summary>
    public async Task<DateTime> BroadcastToAllServers(string[] messages)
    {
        var broadcastTime = DateTime.UtcNow;

        if (_manager is null) return broadcastTime;

        var servers = _manager.GetServers()
            .Where(s => s.ConnectedClients.Count > 0)
            .ToList();

        var broadcastTasks = servers.Select(server => server.BroadcastAsync(messages));
        await Task.WhenAll(broadcastTasks);

        return broadcastTime;
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
