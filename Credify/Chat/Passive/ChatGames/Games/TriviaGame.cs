using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.Helpers;
using Credify.Models.ApiModels;
using Credify.Services;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames.Games;

public class TriviaGame(CredifyConfiguration credifyConfig, PersistenceService persistenceService, ChatUtils chatUtils)
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

        await GenerateQuestion();

        if (string.IsNullOrEmpty(GameInfo.Question))
        {
            GameState = GameState.Ended;
            return;
        }

        var answers = GameInfo.IncorrectAnswers.ToList();
        answers.Add(GameInfo.Answer);
        answers = ChatUtils.Shuffle(answers);
        GameInfo.AllAnswers = answers;

        var formattedAnswers = answers.Select((answer, index) => $"(Color::Accent){index + 1}. (Color::White){answer}").ToArray();
        var message = new[]
        {
            credifyConfig.Translations.Passive.TriviaBroadcast.FormatExt(PluginConstants.PluginName, GameInfo.GameName, GameInfo.Question)
        };

        var combined = message.Concat(formattedAnswers).ToArray();
        
        // Store per-server broadcast times for fair timing calculation
        GameInfo.ServerBroadcastTimes = await chatUtils.BroadcastToAllServers(combined);

        // Schedule timeout, which will trigger grace period before final calculation
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.TriviaTimeout, TimeoutReached, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message, long? gameTime, DateTime eventTime)
    {
        // Accept answers during Started or Closing (grace period) states
        if (GameState is not (GameState.Started or GameState.Closing)) return;
        if (GameInfo.AllAnswers.Count is 0) return;

        var success = int.TryParse(message, out var answerIndex);
        if (success)
        {
            if (answerIndex < 1 || answerIndex > GameInfo.AllAnswers.Count) return;
            message = GameInfo.AllAnswers[answerIndex - 1];
        }

        if (!GameInfo.AllAnswers.Contains(message)) return;

        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId))
        {
            client.Tell(credifyConfig.Translations.Passive.AlreadyAnswered);
            return;
        }

        try
        {
            await MessageReceivedLock.WaitAsync();

            // Calculate fair reaction time based on per-server timing
            var serverEndpoint = client.CurrentServer.EndPoint;
            var reactionTimeSeconds = CalculateReactionTime(serverEndpoint, gameTime, eventTime);
            var isCorrect = message.Equals(GameInfo.Answer, StringComparison.OrdinalIgnoreCase);

            var player = new ClientAnswerInfo
            {
                Winner = isCorrect,
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                ReactionTimeSeconds = reactionTimeSeconds,
                ServerEndpoint = serverEndpoint
            };

            GameInfo.Players.Add(player);
            client.Tell(credifyConfig.Translations.Passive.AnswerAccepted.FormatExt(message.Titleize()));
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
            var message = credifyConfig.Translations.Passive.GenericNoAnswer.FormatExt(PluginConstants.PluginName, GameInfo.Answer);
            await chatUtils.BroadcastToAllServers([message]);
            return;
        }

        // Raise events for all participants
        foreach (var player in GameInfo.Players)
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Trivia, player.Client);
        }

        // Get winners sorted by fair reaction time
        var winners = GameInfo.Players.Where(x => x.Winner).OrderBy(x => x.ReactionTimeSeconds).ToList();
        
        if (winners.Count is 0)
        {
            var message = credifyConfig.Translations.Passive.TriviaNoWinner.FormatExt(PluginConstants.PluginName, GameInfo.Answer);
            await chatUtils.BroadcastToAllServers([message]);
            return;
        }

        // Calculate and apply payouts based on fair reaction time
        var timeoutSeconds = credifyConfig.ChatGame.TriviaTimeout.TotalSeconds;
        foreach (var winner in winners)
        {
            winner.Payout = CalculatePayout(
                winner.ReactionTimeSeconds,
                timeoutSeconds,
                credifyConfig.ChatGame.MaxPayout,
                credifyConfig.ChatGame.PayoutDecayExponent);
            
            await persistenceService.AddCreditsAsync(winner.Client, winner.Payout);
        }

        var winnerCount = winners.Count;
        var totalPayout = winners.Sum(x => x.Payout);
        var broadcastMessage = credifyConfig.Translations.Passive.TriviaWinBroadcast.FormatExt(PluginConstants.PluginName,
            winnerCount, totalPayout.ToString("N0"), GameInfo.Answer);
        await chatUtils.BroadcastToAllServers([broadcastMessage]);

        foreach (var winner in winners)
        {
            var balance = await persistenceService.GetClientCreditsAsync(winner.Client);
            var userMessage = credifyConfig.Translations.Passive.ReactionTell
                .FormatExt(winner.Payout.ToString("N0"), balance.ToString("N0"));
            if (!winner.Client.IsIngame) continue;
            winner.Client.Tell(userMessage);
        }
    }

    private async Task GenerateQuestion()
    {
        try
        {
            var http = new HttpClient();
            var response = await http.GetAsync("https://opentdb.com/api.php?amount=1&category=15&encode=base64");
            var content = await response.DeserializeHttpResponseContentAsync<Trivia>();
            if (content?.ResponseCode is not 0) return;
            var question = content.Results.First();

            GameInfo.Question = ChatUtils.DecodeBase64(question.Question).Replace("\"", "'");
            GameInfo.Answer = ChatUtils.DecodeBase64(question.CorrectAnswer);

            foreach (var incorrectAnswer in question.IncorrectAnswers)
            {
                GameInfo.IncorrectAnswers.Add(ChatUtils.DecodeBase64(incorrectAnswer));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
