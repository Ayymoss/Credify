using Credify.ChatGames.Models;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Games;

public class MathTestGame : ChatGame
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;
    private readonly ChatUtils _chatUtils;

    public MathTestGame(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager, ChatUtils chatUtils)
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
        Utilities.ExecuteAfterDelay(_credifyConfig.ChatGame.MathTestTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessage(EFClient client, string message)
    {
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId)) return;
        if (!message.Equals(GameInfo.Answer)) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (_credifyConfig.ChatGame.MathTestTimeout - reactionTime).TotalSeconds /
                                        _credifyConfig.ChatGame.MathTestTimeout.TotalSeconds;
            var payout = Convert.ToInt64(Math.Round(_credifyConfig.ChatGame.MaxPayout * remainingAsPercentage));
            if (payout < 10) payout = 10;
            await _persistenceManager.AlterClientCredits(payout, client: client);

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
            var message = _credifyConfig.Translations.ChatGameGenericNoAnswer.FormatExt(Plugin.PluginName, GameInfo.Answer);
            await _chatUtils.BroadcastToAllServers(new[] {message});

            return;
        }

        _persistenceManager.StatisticsState.CreditsWon += (ulong)GameInfo.Players.Sum(x => x.Payout);

        var winnerClient = GameInfo.Players.First();
        var broadcastMessage = _credifyConfig.Translations.ChatGameMathTestWinnerBroadcast.FormatExt(Plugin.PluginName,
            winnerClient.Client.CleanedName, $"{winnerClient.Payout:N0}", $"{(winnerClient.Answered - GameInfo.Started).TotalSeconds:N2}",
            GameInfo.Answer);
        await _chatUtils.BroadcastToAllServers(new[] {broadcastMessage});

        foreach (var winner in GameInfo.Players)
        {
            var balance = await _persistenceManager.GetClientCredits(winner.Client);
            var userMessage = _credifyConfig.Translations.ChatGameReactionTell
                .FormatExt($"{winnerClient.Payout:N0}", $"{balance:N0}");
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
