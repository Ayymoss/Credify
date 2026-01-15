using Credify.Chat.Passive.ChatGames.Models;
using Credify.Chat.Passive.ChatGames.Games;
using Credify.Configuration;
using Credify.Services;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames;

public class PassiveManager(
    CredifyConfiguration credifyConfig,
    ILogger<PassiveManager> logger,
    PersistenceService persistenceService,
    ChatUtils chatUtils)
{
    private ChatGame? _currentGame;

    public async Task InitGameAsync()
    {
        try
        {
            var gameTypes = new List<Type>();

            if (credifyConfig.ChatGame.EnabledPassiveGames.IsTriviaEnabled) gameTypes.Add(typeof(TriviaGame));
            if (credifyConfig.ChatGame.EnabledPassiveGames.IsCountdownEnabled) gameTypes.Add(typeof(CountdownGame));
            if (credifyConfig.ChatGame.EnabledPassiveGames.IsMathTestEnabled) gameTypes.Add(typeof(MathTestGame));
            if (credifyConfig.ChatGame.EnabledPassiveGames.IsTypingTestEnabled) gameTypes.Add(typeof(TypingTestGame));
            if (credifyConfig.ChatGame.EnabledPassiveGames.IsCompleteTheWordEnabled) gameTypes.Add(typeof(CompleteTheWordGame));
            if (credifyConfig.ChatGame.EnabledPassiveGames.IsAcronymEnabled) gameTypes.Add(typeof(AcronymGame));

            if (gameTypes.Count is 0) return;

            var selectedGameType = gameTypes[Random.Shared.Next(gameTypes.Count)];
            _currentGame = (ChatGame)Activator.CreateInstance(selectedGameType, credifyConfig, persistenceService, chatUtils)!;
            if (_currentGame is null)
            {
                logger.LogError("Couldn't get a new game instance");
                return;
            }

            await _currentGame.StartAsync();
        }
        catch (Exception e)
        {
            logger.LogError("Game exception caused us here; {Error}", e);
        }
    }

    public async Task HandleChatAsync(EFClient client, string message, long? gameTime, DateTime eventTime)
    {
        // Accept answers during Started or Closing (grace period) states
        if (_currentGame?.GameState is not (GameState.Started or GameState.Closing)) return;
        await _currentGame.HandleChatMessageAsync(client, message, gameTime, eventTime);
    }
}
