using System.Text;
using SharedLibraryCore.Interfaces;
using System;

namespace Credify.ChatGames;

public class ChatUtils
{
    private readonly CredifyConfiguration _credifyConfig;
    private IManager? _manager;


    public ChatUtils(CredifyConfiguration credifyConfig)
    {
        _credifyConfig = credifyConfig;
    }

    public void SetManager(IManager manager) => _manager = manager;

    public async Task BroadcastToAllServers(string[] messages)
    {
        if (_manager is null) return;

        foreach (var server in _manager.GetServers())
        {
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
            "CountdownGame" => _credifyConfig.Translations.ChatGameFriendlyCountdownGame,
            "MathTestGame" => _credifyConfig.Translations.ChatGameFriendlyMathTestGame,
            "TypingTestGame" => _credifyConfig.Translations.ChatGameFriendlyTypingTestGame,
            "TriviaGame" => _credifyConfig.Translations.ChatGameFriendlyTriviaGame,
            _ => throw new ArgumentOutOfRangeException(gameName, "Invalid game name")
        };
    }

    public string DecodeBase64(string base64String)
    {
        var bytes = Convert.FromBase64String(base64String);
        return Encoding.UTF8.GetString(bytes);
    }
    
    
}
