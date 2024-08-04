using System.Text;
using Credify.Chat.Active.Blackjack.Models;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.Games;

public class TypingTestGame(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager, ChatUtils chatUtils) : ChatGame
{
    public override async Task StartAsync()
    {
        GameState = GameState.Started;

        GameInfo = new GameStateInfo
        {
            GameName = chatUtils.GameNameToFriendly(GetType().Name),
            Started = DateTimeOffset.UtcNow
        };

        GenerateQuestion();
        var message = credifyConfig.Translations.Passive.ReactionBroadcast.FormatExt(Plugin.PluginName, GameInfo.GameName,
            GameInfo.Question);
        await chatUtils.BroadcastToAllServers([message]);
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.TypingTestTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message)
    {
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId)) return;
        if (!message.Equals(GameInfo.Answer)) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (credifyConfig.ChatGame.TypingTestTimeout - reactionTime).TotalSeconds /
                                        credifyConfig.ChatGame.TypingTestTimeout.TotalSeconds;
            var payout = Convert.ToInt64(Math.Round(credifyConfig.ChatGame.MaxPayout * remainingAsPercentage));
            if (payout < 10) payout = 10;

            await persistenceManager.AddCreditsAsync(client, payout);

            var winner = new ClientAnswerInfo
            {
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                Payout = payout
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
            var message = credifyConfig.Translations.Passive.TypingTestNoAnswer.FormatExt(Plugin.PluginName);
            await chatUtils.BroadcastToAllServers(new[] { message });
            return;
        }

        var winnerClient = GameInfo.Players.First();
        var broadcastMessage = credifyConfig.Translations.Passive.TypingTestWinnerBroadcast.FormatExt(Plugin.PluginName,
            winnerClient.Client.CleanedName, winnerClient.Payout.ToString("N0"),
            $"{(winnerClient.Answered - GameInfo.Started).TotalSeconds:N2}");
        await chatUtils.BroadcastToAllServers([broadcastMessage]);

        foreach (var winner in GameInfo.Players)
        {
            var balance = await persistenceManager.GetClientCreditsAsync(winner.Client);
            var userMessage = credifyConfig.Translations.Passive.ReactionTell
                .FormatExt(winnerClient.Payout.ToString("N0"), balance.ToString("N0"));
            if (!winner.Client.IsIngame) continue;
            winner.Client.Tell(userMessage);
        }
    }

    private void GenerateQuestion()
    {
        var messageLength = credifyConfig.ChatGame.TypingTestTextLength;
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
