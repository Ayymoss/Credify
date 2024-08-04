using Credify.Chat.Active.Blackjack.Models;
using Credify.Configuration;
using Credify.Helpers;
using Credify.Models.ApiModels;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.Games;

public class CountdownGame(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager, ChatUtils chatUtils)
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

        var message = credifyConfig.Translations.Passive.CountdownBroadcast.FormatExt(Plugin.PluginName, GameInfo.GameName,
            GameInfo.Question);
        await chatUtils.BroadcastToAllServers(new[] { message });
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.TriviaTimeout, End, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message)
    {
        if (message.Length is < 2 or > 9) return;
        if (!IsValid(message.ToUpper(), GameInfo.Question)) return;

        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId))
        {
            client.Tell(credifyConfig.Translations.Passive.AlreadyAnswered);
            return;
        }

        var http = new HttpClient();
        var response = await http.GetAsync($"https://api.dictionaryapi.dev/api/v2/entries/en/{message}");
        if (!response.IsSuccessStatusCode)
        {
            client.Tell(credifyConfig.Translations.Passive.CountdownWordNotFound.FormatExt(message.ToUpper()));
            return;
        }

        var result = await response.DeserializeHttpResponseContentAsync<List<DictionaryApi>>();
        if (result is null or { Count: 0 }) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTime = DateTimeOffset.UtcNow - GameInfo.Started;
            var remainingAsPercentage = (credifyConfig.ChatGame.CountdownTimeout - reactionTime).TotalSeconds /
                                        credifyConfig.ChatGame.CountdownTimeout.TotalSeconds;
            var initialPayout = credifyConfig.ChatGame.MaxPayout * remainingAsPercentage;
            var lengthMultiplier = 1 + (message.Length - 2) * (3.0 / 7.0);
            var payout = Convert.ToInt64(Math.Round(initialPayout * lengthMultiplier));
            if (payout < 10) payout = 10;

            await persistenceManager.AddCreditsAsync(client, payout);

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
                credifyConfig.Translations.Passive.AnswerAccepted.FormatExt(message.ToLower().Titleize()),
                credifyConfig.Translations.Passive.AnswerAcceptedDefinition.FormatExt(message.ToLower().Titleize(), definition)
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
        if (GameInfo.Players.Count is 0)
        {
            var message =
                credifyConfig.Translations.Passive.TypingTestNoAnswer.FormatExt(Plugin.PluginName);
            await chatUtils.BroadcastToAllServers([message]);
            return;
        }

        var players = GameInfo.Players.Where(x => x.Winner).ToList();
        var uniqueAnswers = players.Select(x => x.Answer.ToLower().Titleize()).Distinct().ToList();
        var winnerCount = players.Count;
        var totalPayout = players.Sum(x => x.Payout);
        var broadcastMessage = credifyConfig.Translations.Passive.CountdownWinBroadcast.FormatExt(Plugin.PluginName,
            winnerCount, totalPayout.ToString("N0"), string.Join(", ", uniqueAnswers));
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

    private void GenerateQuestion()
    {
        const string countdown = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        char[] vowels = ['A', 'E', 'I', 'O', 'U'];
        var consonants = countdown.Except(vowels).ToList();
        var letters = new List<char>();
        var vowelCount = Random.Shared.Next(3, 6);
        var consonantCount = 9 - vowelCount;

        for (var i = 0; i < vowelCount; i++)
        {
            var index = Random.Shared.Next(vowels.Length);
            letters.Add(vowels[index]);
        }

        for (var i = 0; i < consonantCount; i++)
        {
            var index = Random.Shared.Next(consonants.Count);
            letters.Add(consonants[index]);
        }

        for (var i = letters.Count - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (letters[i], letters[j]) = (letters[j], letters[i]);
        }

        var shuffledLettersString = new string(letters.ToArray());
        GameInfo.Question = shuffledLettersString;
        GameInfo.Answer = string.Empty;
    }

    private static bool IsValid(string attempt, string generated)
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
