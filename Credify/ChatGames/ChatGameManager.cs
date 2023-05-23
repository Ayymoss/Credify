using Credify.ChatGames.Games;
using Credify.ChatGames.Models;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames;

public class ChatGameManager
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly ILogger<ChatGameManager> _logger;
    private readonly PersistenceManager _persistenceManager;
    private readonly ChatUtils _chatUtils;
    private ChatGame? _currentGame;

    public ChatGameManager(CredifyConfiguration credifyConfig, ILogger<ChatGameManager> logger, PersistenceManager persistenceManager,
        ChatUtils chatUtils)
    {
        _credifyConfig = credifyConfig;
        _logger = logger;
        _persistenceManager = persistenceManager;
        _chatUtils = chatUtils;
    }

    public async Task StartGame()
    {
        // Some game is causing this to exit with an exception, so we'll just catch it and try again
        try
        {
            var gameTypes = new List<Type>();

            if (_credifyConfig.ChatGame.EnabledTriviaGames.IsTriviaEnabled) gameTypes.Add(typeof(TriviaGame));
            if (_credifyConfig.ChatGame.EnabledTriviaGames.IsCountdownEnabled) gameTypes.Add(typeof(CountdownGame));
            if (_credifyConfig.ChatGame.EnabledTriviaGames.IsMathTestEnabled) gameTypes.Add(typeof(MathTestGame));
            if (_credifyConfig.ChatGame.EnabledTriviaGames.IsTypingTestEnabled) gameTypes.Add(typeof(TypingTestGame));

            if (!gameTypes.Any()) return;

            var selectedGameType = gameTypes[Random.Shared.Next(gameTypes.Count)];
            selectedGameType = gameTypes[1];

            _currentGame = (ChatGame)Activator.CreateInstance(selectedGameType, _credifyConfig, _persistenceManager, _chatUtils)!;
            if (_currentGame is null)
            {
                _logger.LogError("Couldn't get a new game instance");
                return;
            }

            await _currentGame.Start();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }

    public async Task HandleChatEvent(EFClient client, string message)
    {
        if (_currentGame?.GameState is not GameState.Started) return;
        await _currentGame.HandleChatMessage(client, message);
    }
}
