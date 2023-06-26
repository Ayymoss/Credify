using Credify.ChatGames.Models;
using Credify.Helpers;
using Credify.Models.ApiModels;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Games;

public class TriviaGame : ChatGame
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;
    private readonly ChatUtils _chatUtils;

    public TriviaGame(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager, ChatUtils chatUtils)
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
            _credifyConfig.Translations.ChatGameTriviaBroadcast.FormatExt(Plugin.PluginName, GameInfo.GameName, GameInfo.Question)
        };

        var combined = message.Concat(formattedAnswers).ToArray();
        await _chatUtils.BroadcastToAllServers(combined);

        Utilities.ExecuteAfterDelay(_credifyConfig.ChatGame.TriviaTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessage(EFClient client, string message)
    {
        if (!GameInfo.AllAnswers.Any()) return;

        var success = int.TryParse(message, out var answerIndex);
        if (success)
        {
            if (answerIndex < 1 || answerIndex > GameInfo.AllAnswers.Count) return;
            message = GameInfo.AllAnswers[answerIndex - 1];
        }

        if (!GameInfo.AllAnswers.Contains(message)) return;

        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId))
        {
            client.Tell(_credifyConfig.Translations.ChatGameAlreadyAnswered);
            return;
        }

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (_credifyConfig.ChatGame.TriviaTimeout - reactionTime).TotalSeconds /
                                        _credifyConfig.ChatGame.TriviaTimeout.TotalSeconds;
            var payout = Convert.ToInt64(Math.Round(_credifyConfig.ChatGame.MaxPayout * remainingAsPercentage));
            if (payout < 10) payout = 10;
            var winner = false;
            if (message.Equals(GameInfo.Answer, StringComparison.OrdinalIgnoreCase))
            {
                await _persistenceManager.AlterClientCreditsAsync(payout, client: client);
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
            client.Tell(_credifyConfig.Translations.ChatGameAnswerAccepted.FormatExt(message.Titleize()));
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
        if (!GameInfo.Players.Any())
        {
            var message = _credifyConfig.Translations.ChatGameGenericNoAnswer.FormatExt(Plugin.PluginName, GameInfo.Answer);
            await _chatUtils.BroadcastToAllServers(new[] {message});
            return;
        }

        var players = GameInfo.Players.Where(x => x.Winner).ToList();

        if (!players.Any())
        {
            var message = _credifyConfig.Translations.ChatGameTriviaNoWinner.FormatExt(Plugin.PluginName, GameInfo.Answer);
            await _chatUtils.BroadcastToAllServers(new[] {message});
            return;
        }

        var winnerCount = players.Count;
        var totalPayout = players.Sum(x => x.Payout);
        var broadcastMessage = _credifyConfig.Translations.ChatGameTriviaWinBroadcast.FormatExt(Plugin.PluginName,
            winnerCount, $"{totalPayout:N0}", GameInfo.Answer);
        await _chatUtils.BroadcastToAllServers(new[] {broadcastMessage});

        foreach (var winner in players)
        {
            var balance = await _persistenceManager.GetClientCreditsAsync(winner.Client);
            var userMessage = _credifyConfig.Translations.ChatGameReactionTell
                .FormatExt($"{winner.Payout:N0}", $"{balance:N0}");
            if (!winner.Client.IsIngame) continue;
            winner.Client.Tell(userMessage);
        }

        _persistenceManager.StatisticsState.CreditsWon += (ulong)totalPayout;
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

            GameInfo.Question = _chatUtils.DecodeBase64(question.Question).Replace("\"", "'");
            GameInfo.Answer = _chatUtils.DecodeBase64(question.CorrectAnswer);

            foreach (var incorrectAnswer in question.IncorrectAnswers)
            {
                GameInfo.IncorrectAnswers.Add(_chatUtils.DecodeBase64(incorrectAnswer));
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
    }
}
