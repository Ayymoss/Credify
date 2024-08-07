using Credify.Chat.Active.Blackjack.Models;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.Games;

public class MathTestGame(CredifyConfiguration credifyConfig, PersistenceService persistenceService, ChatUtils chatUtils)
    : ChatGame
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
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.MathTestTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message)
    {
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId)) return;
        if (!message.Equals(GameInfo.Answer)) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (credifyConfig.ChatGame.MathTestTimeout - reactionTime).TotalSeconds /
                                        credifyConfig.ChatGame.MathTestTimeout.TotalSeconds;
            var payout = Convert.ToInt64(Math.Round(credifyConfig.ChatGame.MaxPayout * remainingAsPercentage));
            if (payout < 10) payout = 10;
            await persistenceService.AddCreditsAsync(client, payout);

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
            var message = credifyConfig.Translations.Passive.GenericNoAnswer.FormatExt(Plugin.PluginName, GameInfo.Answer);
            await chatUtils.BroadcastToAllServers([message]);

            return;
        }

        var winnerClient = GameInfo.Players.First();
        var broadcastMessage = credifyConfig.Translations.Passive.MathTestWinnerBroadcast.FormatExt(Plugin.PluginName,
            winnerClient.Client.CleanedName, winnerClient.Payout.ToString("N0"),
            $"{(winnerClient.Answered - GameInfo.Started).TotalSeconds:N2}",
            GameInfo.Answer);
        await chatUtils.BroadcastToAllServers([broadcastMessage]);

        foreach (var winner in GameInfo.Players)
        {
            var balance = await persistenceService.GetClientCreditsAsync(winner.Client);
            var userMessage = credifyConfig.Translations.Passive.ReactionTell
                .FormatExt(winnerClient.Payout.ToString("N0"), balance.ToString("N0"));
            if (!winner.Client.IsIngame) continue;
            winner.Client.Tell(userMessage);
        }
    }

    private void GenerateQuestion()
    {
        var game = Random.Shared.Next(0, 7);
        var firstNum = Random.Shared.Next(1, 50);
        var secondNum = Random.Shared.Next(1, 50);
        var mathOperator = string.Empty;
        var answer = 0;

        switch (game)
        {
            case 0:
            case 1:
            case 2:
                answer = firstNum + secondNum;
                mathOperator = "+";
                break;
            case 3:
            case 4:
                firstNum = secondNum + Random.Shared.Next(0, 10);
                answer = firstNum - secondNum;
                mathOperator = "-";
                break;
            case 5:
                answer = firstNum * secondNum;
                mathOperator = "*";
                break;
            case 6:
                firstNum = secondNum + Random.Shared.Next(0, 100);
                answer = firstNum % secondNum;
                mathOperator = "mod";
                break;
        }

        GameInfo.Question = $"{firstNum} {mathOperator} {secondNum}";
        GameInfo.Answer = answer.ToString();
    }
}
