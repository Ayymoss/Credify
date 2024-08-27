using System.Text;
using Credify.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Chat.Passive.ChatGames;

public class ChatUtils(CredifyConfiguration credifyConfig)
{
    private IManager? _manager;

    public void SetManager(IManager manager) => _manager = manager;

    public async Task BroadcastToAllServers(string[] messages)
    {
        if (_manager is null) return;

        foreach (var server in _manager.GetServers())
        {
            if (server.ConnectedClients.Count is 0) continue;
            await server.BroadcastAsync(messages);
        }
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
            _ => throw new ArgumentOutOfRangeException(gameName, "Invalid game name")
        };
    }

    public static string DecodeBase64(string base64String)
    {
        var bytes = Convert.FromBase64String(base64String);
        return Encoding.UTF8.GetString(bytes);
    }
}
