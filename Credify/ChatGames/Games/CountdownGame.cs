using Credify.ChatGames.Models;
using Credify.Helpers;
using Credify.Models.ApiModels;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.ChatGames.Games;

public class CountdownGame : ChatGame
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;
    private readonly ChatUtils _chatUtils;

    public CountdownGame(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager, ChatUtils chatUtils)
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

        var message = _credifyConfig.Translations.ChatGameCountdownBroadcast.FormatExt(Plugin.PluginName, GameInfo.GameName,
            GameInfo.Question);
        await _chatUtils.BroadcastToAllServers(new[] {message});

        Utilities.ExecuteAfterDelay(_credifyConfig.ChatGame.TriviaTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessage(EFClient client, string message)
    {
        if (message.Length is < 2 or > 9) return;
        if (!IsValid(message.ToUpper(), GameInfo.Question)) return;

        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId))
        {
            client.Tell(_credifyConfig.Translations.ChatGameAlreadyAnswered);
            return;
        }

        var http = new HttpClient();
        var response = await http.GetAsync($"https://api.dictionaryapi.dev/api/v2/entries/en/{message}");
        if (!response.IsSuccessStatusCode)
        {
            client.Tell(_credifyConfig.Translations.ChatGameCountdownWordNotFound.FormatExt(message.ToUpper()));
            return;
        }

        var result = await response.DeserializeHttpResponseContentAsync<List<DictionaryApi>>();
        if (result is null or {Count: 0}) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (_credifyConfig.ChatGame.CountdownTimeout - reactionTime).TotalSeconds /
                                        _credifyConfig.ChatGame.CountdownTimeout.TotalSeconds;
            var initialPayout = _credifyConfig.ChatGame.MaxPayout * remainingAsPercentage;
            var lengthMultiplier = 1 + (message.Length - 2) * (3.0 / 7.0);
            var payout = Convert.ToInt64(Math.Round(initialPayout * lengthMultiplier));
            if (payout < 10) payout = 10;

            await _persistenceManager.AlterClientCreditsAsync(payout, client: client);

            var player = new ClientAnswerInfo
            {
                Winner = true,
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                Payout = payout,
            };

            GameInfo.Players.Add(player);
            var definition = result.First().Meanings.First().Definitions.First().Definition.ToLower();
            var messages = new[]
            {
                _credifyConfig.Translations.ChatGameAnswerAccepted.FormatExt(message.ToLower().Titleize()),
                _credifyConfig.Translations.ChatGameAnswerAcceptedDefinition.FormatExt(message.ToLower().Titleize(), definition)
            };
            await client.TellAsync(messages);
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
            var message =
                _credifyConfig.Translations.ChatGameTypingTestNoAnswer.FormatExt(Plugin.PluginName);
            await _chatUtils.BroadcastToAllServers(new[] {message});
            return;
        }

        var players = GameInfo.Players.Where(x => x.Winner).ToList();
        var uniqueAnswers = players.Select(x => x.Answer.ToLower().Titleize()).Distinct().ToList();
        var winnerCount = players.Count;
        var totalPayout = players.Sum(x => x.Payout);
        var broadcastMessage = _credifyConfig.Translations.ChatGameCountdownWinBroadcast.FormatExt(Plugin.PluginName,
            winnerCount, $"{totalPayout:N0}", string.Join(", ", uniqueAnswers));
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

    private void GenerateQuestion()
    {
        const string countdown = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var vowels = new[] {'A', 'E', 'I', 'O', 'U'};
        var consonants = countdown.Except(vowels).ToList();
        var letters = new List<char>();
        var random = new Random();
        var vowelCount = random.Next(3, 6);
        var consonantCount = 9 - vowelCount;

        for (var i = 0; i < vowelCount; i++)
        {
            var index = random.Next(vowels.Length);
            letters.Add(vowels[index]);
        }

        for (var i = 0; i < consonantCount; i++)
        {
            var index = random.Next(consonants.Count);
            letters.Add(consonants[index]);
        }

        for (var i = letters.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (letters[i], letters[j]) = (letters[j], letters[i]);
        }

        var shuffledLettersString = new string(letters.ToArray());
        GameInfo.Question = shuffledLettersString;
        GameInfo.Answer = string.Empty;
    }

    public static bool IsValid(string attempt, string generated)
    {
        var attemptCounts = attempt.GroupBy(c => c)
            .ToDictionary(group => group.Key, group => group.Count());
        var generatedCounts = generated.GroupBy(c => c)
            .ToDictionary(group => group.Key, group => group.Count());

        return attemptCounts.All(ac =>
            generatedCounts.ContainsKey(ac.Key) &&
            generatedCounts[ac.Key] >= ac.Value);
    }
}
