using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames.Games;

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
        var message = credifyConfig.Translations.Passive.ReactionBroadcast.FormatExt(PluginConstants.PluginName, GameInfo.GameName,
            GameInfo.Question);
        
        // Store per-server broadcast times for fair timing calculation
        GameInfo.ServerBroadcastTimes = await chatUtils.BroadcastToAllServers([message]);
        
        // Schedule timeout, which will trigger grace period before final calculation
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.MathTestTimeout, TimeoutReached, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message, long? gameTime, DateTime eventTime)
    {
        // Accept answers during Started or Closing (grace period) states
        if (GameState is not (GameState.Started or GameState.Closing)) return;
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId)) return;
        if (!message.Equals(GameInfo.Answer)) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            // Calculate fair reaction time based on per-server timing
            var serverEndpoint = client.CurrentServer.EndPoint;
            var reactionTimeSeconds = CalculateReactionTime(serverEndpoint, gameTime, eventTime);
            
            // Record the answer - payout calculated and applied at end
            var player = new ClientAnswerInfo
            {
                Winner = true,
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                ReactionTimeSeconds = reactionTimeSeconds,
                ServerEndpoint = serverEndpoint
            };

            GameInfo.Players.Add(player);
            
            // Confirm answer was recorded (don't reveal if they won yet)
            client.Tell(credifyConfig.Translations.Passive.AnswerRecorded);
        }
        finally
        {
            if (MessageReceivedLock.CurrentCount is 0) MessageReceivedLock.Release();
        }
    }

    /// <summary>
    /// Called when timeout is reached. Enters grace period to allow late RCON messages.
    /// </summary>
    private async Task TimeoutReached(CancellationToken token)
    {
        if (GameState is not GameState.Started) return;
        
        // Enter grace period - still accept answers but schedule final calculation
        GameState = GameState.Closing;
        
        // Wait for grace period to allow late RCON messages to arrive
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.EndGracePeriod, FinalizeResults, CancellationToken.None);
    }

    /// <summary>
    /// Called after grace period. Calculates winners and announces results.
    /// </summary>
    private async Task FinalizeResults(CancellationToken token)
    {
        if (GameState is not GameState.Closing) return;

        GameState = GameState.Ended;
        
        if (GameInfo.Players.Count is 0)
        {
            var message = credifyConfig.Translations.Passive.GenericNoAnswer.FormatExt(PluginConstants.PluginName, GameInfo.Answer);
            await chatUtils.BroadcastToAllServers([message]);
            return;
        }

        // Sort by fair reaction time to determine winner
        var sortedPlayers = GameInfo.Players.OrderBy(p => p.ReactionTimeSeconds).ToList();
        var winner = sortedPlayers.First();
        
        // Calculate and apply payouts based on fair reaction time
        var timeoutSeconds = credifyConfig.ChatGame.MathTestTimeout.TotalSeconds;
        foreach (var player in sortedPlayers)
        {
            player.Payout = CalculatePayout(
                player.ReactionTimeSeconds,
                timeoutSeconds,
                credifyConfig.ChatGame.MaxPayout,
                credifyConfig.ChatGame.PayoutDecayExponent);
            
            await persistenceService.AddCreditsAsync(player.Client, player.Payout);
            ICredifyEventService.RaiseEvent(ObjectiveType.Trivia, player.Client);
        }

        // Announce winner
        var broadcastMessage = credifyConfig.Translations.Passive.MathTestWinnerBroadcast.FormatExt(
            PluginConstants.PluginName,
            winner.Client.CleanedName, 
            winner.Payout.ToString("N0"),
            $"{winner.ReactionTimeSeconds:N2}",
            GameInfo.Answer);
        await chatUtils.BroadcastToAllServers([broadcastMessage]);

        // Tell each player their result
        foreach (var player in sortedPlayers)
        {
            var balance = await persistenceService.GetClientCreditsAsync(player.Client);
            var userMessage = credifyConfig.Translations.Passive.ReactionTell
                .FormatExt(player.Payout.ToString("N0"), balance.ToString("N0"));
            if (!player.Client.IsIngame) continue;
            player.Client.Tell(userMessage);
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
