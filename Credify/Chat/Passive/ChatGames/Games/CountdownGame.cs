using Credify.Chat.Passive.ChatGames.Models;
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

public class CountdownGame(CredifyConfiguration credifyConfig, PersistenceService persistenceService, ChatUtils chatUtils)
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

        var message = credifyConfig.Translations.Passive.CountdownBroadcast.FormatExt(PluginConstants.PluginName, GameInfo.GameName,
            GameInfo.Question);
        
        // Store per-server broadcast times for fair timing calculation
        GameInfo.ServerBroadcastTimes = await chatUtils.BroadcastToAllServers([message]);
        
        // Schedule timeout, which will trigger grace period before final calculation
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.CountdownTimeout, TimeoutReached, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message, long? gameTime, DateTime eventTime)
    {
        // Accept answers during Started or Closing (grace period) states
        if (GameState is not (GameState.Started or GameState.Closing)) return;
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

            // Calculate fair reaction time based on per-server timing
            var serverEndpoint = client.CurrentServer.EndPoint;
            var reactionTimeSeconds = CalculateReactionTime(serverEndpoint, gameTime, eventTime, chatUtils.GetServerTimeTracker());

            var player = new ClientAnswerInfo
            {
                Winner = true,
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                ReactionTimeSeconds = reactionTimeSeconds,
                ServerEndpoint = serverEndpoint,
                // Store word length for bonus calculation at end
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

        // Raise events for all participants
        foreach (var player in GameInfo.Players)
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Trivia, player.Client);
        }

        // Calculate and apply payouts based on fair reaction time + word length bonus
        var timeoutSeconds = credifyConfig.ChatGame.CountdownTimeout.TotalSeconds;
        var winners = GameInfo.Players.Where(x => x.Winner).ToList();
        
        foreach (var player in winners)
        {
            // Base payout from fair timing with non-linear decay
            var basePayout = CalculatePayout(
                player.ReactionTimeSeconds,
                timeoutSeconds,
                credifyConfig.ChatGame.MaxPayout,
                credifyConfig.ChatGame.PayoutDecayExponent);
            
            // Word length multiplier: longer words = more payout
            var lengthMultiplier = 1 + (player.Answer.Length - 2) * (3.0 / 7.0);
            player.Payout = (long)Math.Round(basePayout * lengthMultiplier);
            if (player.Payout < 10) player.Payout = 10;
            
            await persistenceService.AddCreditsAsync(player.Client, player.Payout);
        }

        var uniqueAnswers = winners.Select(x => x.Answer.ToLower().Titleize()).Distinct().ToList();
        var winnerCount = winners.Count;
        var totalPayout = winners.Sum(x => x.Payout);
        var broadcastMessage = credifyConfig.Translations.Passive.CountdownWinBroadcast.FormatExt(PluginConstants.PluginName,
            winnerCount, totalPayout.ToString("N0"), string.Join(", ", uniqueAnswers));
        await chatUtils.BroadcastToAllServers([broadcastMessage]);

        var fastestWinner = winners.OrderBy(w => w.ReactionTimeSeconds).First();
        foreach (var winner in winners)
        {
            var balance = await persistenceService.GetClientCreditsAsync(winner.Client);
            var userMessage = credifyConfig.Translations.Passive.ReactionTell
                .FormatExt(winner.Payout.ToString("N0"), balance.ToString("N0"));
            if (!winner.Client.IsIngame) continue;
            winner.Client.Tell(userMessage);
        }
        
        // Tell non-winners their time offset from fastest winner
        var losers = GameInfo.Players.Where(p => !p.Winner).ToList();
        foreach (var loser in losers)
        {
            if (!loser.Client.IsIngame) continue;
            var timeOffset = loser.ReactionTimeSeconds - fastestWinner.ReactionTimeSeconds;
            var offsetMessage = credifyConfig.Translations.Passive.ReactionTimeOffset
                .FormatExt($"{timeOffset:F3}");
            loser.Client.Tell(offsetMessage);
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
