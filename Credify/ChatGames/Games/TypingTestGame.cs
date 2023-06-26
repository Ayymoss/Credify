using System.Text;
using Credify.ChatGames.Models;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Games;

public class TypingTestGame : ChatGame
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;
    private readonly ChatUtils _chatUtils;

    public TypingTestGame(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager, ChatUtils chatUtils)
    {
        _credifyConfig = credifyConfig;
        _persistenceManager = persistenceManager;
        _chatUtils = chatUtils;
    }

    public override async Task Start()
    {
        GameState = GameState.Started;

        GameInfo = new GameStateInfo
        {
            GameName = _chatUtils.GameNameToFriendly(GetType().Name),
            Started = DateTimeOffset.UtcNow
        };

        GenerateQuestion();
        var message = _credifyConfig.Translations.ChatGameReactionBroadcast.FormatExt(Plugin.PluginName, GameInfo.GameName,
            GameInfo.Question);
        await _chatUtils.BroadcastToAllServers(new[] {message});
        Utilities.ExecuteAfterDelay(_credifyConfig.ChatGame.TypingTestTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessage(EFClient client, string message)
    {
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId)) return;
        if (!message.Equals(GameInfo.Answer)) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (_credifyConfig.ChatGame.TypingTestTimeout - reactionTime).TotalSeconds /
                                        _credifyConfig.ChatGame.TypingTestTimeout.TotalSeconds;
            var payout = Convert.ToInt64(Math.Round(_credifyConfig.ChatGame.MaxPayout * remainingAsPercentage));
            if (payout < 10) payout = 10;
            await _persistenceManager.AlterClientCreditsAsync(payout, client: client);

            var winner = new ClientAnswerInfo
            {
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                Payout = payout,
            };

            GameInfo.Players.Add(winner);
            await End(CancellationToken.None);
        }
        finally
        {
            if (MessageReceivedLock.CurrentCount is 0) MessageReceivedLock.Release();
        }
    }

    private async Task End(CancellationToken token)
    {
        if (GameState is not GameState.Started) return;

        GameState = GameState.Ended;
        if (GameInfo.Players.Count is 0)
        {
            var message = _credifyConfig.Translations.ChatGameTypingTestNoAnswer.FormatExt(Plugin.PluginName);
            await _chatUtils.BroadcastToAllServers(new[] {message});
            return;
        }

        _persistenceManager.StatisticsState.CreditsWon += (ulong)GameInfo.Players.Sum(x => x.Payout);

        var winnerClient = GameInfo.Players.First();
        var broadcastMessage = _credifyConfig.Translations.ChatGameTypingTestWinnerBroadcast.FormatExt(Plugin.PluginName,
            winnerClient.Client.CleanedName, $"{winnerClient.Payout:N0}", $"{(winnerClient.Answered - GameInfo.Started).TotalSeconds:N2}");
        await _chatUtils.BroadcastToAllServers(new[] {broadcastMessage});

        foreach (var winner in GameInfo.Players)
        {
            var balance = await _persistenceManager.GetClientCreditsAsync(winner.Client);
            var userMessage = _credifyConfig.Translations.ChatGameReactionTell
                .FormatExt($"{winnerClient.Payout:N0}", $"{balance:N0}");
            if (!winner.Client.IsIngame) continue;
            winner.Client.Tell(userMessage);
        }
    }

    private void GenerateQuestion()
    {
        var messageLength = _credifyConfig.ChatGame.TypingTestTextLength;
        const string charSet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz123456789";
        var answer = new StringBuilder();

        for (var i = 0; i < messageLength; i++)
        {
            answer.Append(charSet[Random.Shared.Next(charSet.Length)]);
        }

        GameInfo.Question = answer.ToString();
        GameInfo.Answer = answer.ToString();
    }
}
