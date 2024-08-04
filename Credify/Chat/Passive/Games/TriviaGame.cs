using Credify.Chat.Active.Blackjack.Models;
using Credify.Configuration;
using Credify.Helpers;
using Credify.Models.ApiModels;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.Games;

public class TriviaGame(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager, ChatUtils chatUtils)
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
            credifyConfig.Translations.Passive.TriviaBroadcast.FormatExt(Plugin.PluginName, GameInfo.GameName, GameInfo.Question)
        };

        var combined = message.Concat(formattedAnswers).ToArray();
        await chatUtils.BroadcastToAllServers(combined);

        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.TriviaTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message)
    {
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

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (credifyConfig.ChatGame.TriviaTimeout - reactionTime).TotalSeconds /
                                        credifyConfig.ChatGame.TriviaTimeout.TotalSeconds;
            var payout = Convert.ToInt64(Math.Round(credifyConfig.ChatGame.MaxPayout * remainingAsPercentage));
            if (payout < 10) payout = 10;
            var winner = false;
            if (message.Equals(GameInfo.Answer, StringComparison.OrdinalIgnoreCase))
            {
                await persistenceManager.AddCreditsAsync(client, payout);
                winner = true;
            }

            var player = new ClientAnswerInfo
            {
                Winner = winner,
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                Payout = payout,
            };

            GameInfo.Players.Add(player);
            client.Tell(credifyConfig.Translations.Passive.AnswerAccepted.FormatExt(message.Titleize()));
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

        var players = GameInfo.Players.Where(x => x.Winner).ToList();

        if (players.Count is 0)
        {
            var message = credifyConfig.Translations.Passive.TriviaNoWinner.FormatExt(Plugin.PluginName, GameInfo.Answer);
            await chatUtils.BroadcastToAllServers([message]);
            return;
        }

        var winnerCount = players.Count;
        var totalPayout = players.Sum(x => x.Payout);
        var broadcastMessage = credifyConfig.Translations.Passive.TriviaWinBroadcast.FormatExt(Plugin.PluginName,
            winnerCount, totalPayout.ToString("N0"), GameInfo.Answer);
        await chatUtils.BroadcastToAllServers([broadcastMessage]);

        foreach (var winner in players)
        {
            var balance = await persistenceManager.GetClientCreditsAsync(winner.Client);
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
