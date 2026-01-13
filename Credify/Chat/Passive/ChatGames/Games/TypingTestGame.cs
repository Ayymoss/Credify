using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System.Text;
using Credify.Chat.Active.Games.Blackjack.Models;

namespace Credify.Chat.Passive.ChatGames.Games;

public class TypingTestGame(CredifyConfiguration credifyConfig, PersistenceService persistenceService, ChatUtils chatUtils) : ChatGame
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
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.TypingTestTimeout, TimeoutReached, CancellationToken.None);
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

            // Confirm answer was recorded
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

        GameState = GameState.Closing;
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
            var message = credifyConfig.Translations.Passive.TypingTestNoAnswer.FormatExt(PluginConstants.PluginName);
            await chatUtils.BroadcastToAllServers([message]);
            return;
        }

        // Sort by fair reaction time to determine winner
        var sortedPlayers = GameInfo.Players.OrderBy(p => p.ReactionTimeSeconds).ToList();
        var winner = sortedPlayers.First();

        // Calculate and apply payouts based on fair reaction time
        var timeoutSeconds = credifyConfig.ChatGame.TypingTestTimeout.TotalSeconds;
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
        var broadcastMessage = credifyConfig.Translations.Passive.TypingTestWinnerBroadcast.FormatExt(
            PluginConstants.PluginName,
            winner.Client.CleanedName,
            winner.Payout.ToString("N0"),
            $"{winner.ReactionTimeSeconds:N2}");
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
