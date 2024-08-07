using Credify.Chat.Active.Blackjack.Models;
using Credify.Chat.Passive.Games;
using Credify.Configuration;
using Credify.Services;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive;

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

    public async Task HandleChatEventAsync(EFClient client, string message)
    {
        if (_currentGame?.GameState is not GameState.Started) return;
        await _currentGame.HandleChatMessageAsync(client, message);
    }
}
