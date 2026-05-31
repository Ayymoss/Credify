using Credify.Chat.Passive.ChatGames.Models;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames.Games;

/// <summary>
/// Players race to unscramble a word whose letters have been shuffled.
/// Reuses the gaming/CoD word bank from ChatGameConfiguration.FillInBlankWords.
/// </summary>
public class UnscrambleGame(CredifyConfiguration credifyConfig, PersistenceService persistenceService, ChatUtils chatUtils)
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

        var message = credifyConfig.Translations.Passive.UnscrambleBroadcast.FormatExt(
            PluginConstants.PluginName,
            GameInfo.GameName,
            GameInfo.Question);

        GameInfo.BroadcastTime = await chatUtils.BroadcastToAllServers([message]);

        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.UnscrambleTimeout, TimeoutReached, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message, DateTime eventTime)
    {
        if (GameState is not (GameState.Started or GameState.Closing)) return;
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId))
        {
            client.Tell(credifyConfig.Translations.Passive.AlreadyAnswered);
            return;
        }

        if (!message.Equals(GameInfo.Answer, StringComparison.OrdinalIgnoreCase)) return;

        try
        {
            await MessageReceivedLock.WaitAsync();

            var reactionTimeSeconds = CalculateReactionTime(client, eventTime);

            GameInfo.Players.Add(new ClientAnswerInfo
            {
                Winner = true,
                Client = client,
                Answer = message,
                Answered = DateTimeOffset.UtcNow,
                ReactionTimeSeconds = reactionTimeSeconds
            });
            client.Tell(credifyConfig.Translations.Passive.AnswerRecorded);
        }
        finally
        {
            if (MessageReceivedLock.CurrentCount is 0) MessageReceivedLock.Release();
        }
    }

    private async Task TimeoutReached(CancellationToken token)
    {
        if (GameState is not GameState.Started) return;

        GameState = GameState.Closing;
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.EndGracePeriod, FinalizeResults, CancellationToken.None);
    }

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

        var sortedPlayers = GameInfo.Players.OrderBy(p => p.ReactionTimeSeconds).ToList();
        var winner = sortedPlayers.First();

        var timeoutSeconds = credifyConfig.ChatGame.UnscrambleTimeout.TotalSeconds;
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

        var broadcastMessage = credifyConfig.Translations.Passive.UnscrambleWinnerBroadcast.FormatExt(
            PluginConstants.PluginName,
            winner.Client.CleanedName,
            winner.Payout.ToString("N0"),
            $"{winner.ReactionTimeSeconds:N2}",
            GameInfo.Answer);
        await chatUtils.BroadcastToAllServers([broadcastMessage]);

        foreach (var player in sortedPlayers)
        {
            var balance = await persistenceService.GetClientCreditsAsync(player.Client);
            var userMessage = credifyConfig.Translations.Passive.ReactionTell
                .FormatExt(player.Payout.ToString("N0"), balance.ToString("N0"));
            if (!player.Client.IsIngame) continue;
            player.Client.Tell(userMessage);

            if (player != winner)
            {
                var timeOffset = player.ReactionTimeSeconds - winner.ReactionTimeSeconds;
                player.Client.Tell(credifyConfig.Translations.Passive.ReactionTimeOffset.FormatExt($"{timeOffset:F3}"));
            }
        }
    }

    private void GenerateQuestion()
    {
        var wordList = credifyConfig.ChatGame.FillInBlankWords;
        if (wordList.Count == 0)
        {
            throw new InvalidOperationException("No words configured in ChatGameConfiguration.FillInBlankWords");
        }

        var word = wordList[Random.Shared.Next(wordList.Count)].ToUpperInvariant();
        GameInfo.Answer = word;
        GameInfo.Question = Scramble(word);
    }

    /// <summary>
    /// Shuffles a word's letters (spaced for chat legibility), retrying so the scramble
    /// isn't identical to the original.
    /// </summary>
    private static string Scramble(string word)
    {
        if (word.Length <= 1) return word;

        var chars = word.ToCharArray();
        for (var attempt = 0; attempt < 10; attempt++)
        {
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = Random.Shared.Next(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            if (new string(chars) != word) break;
        }

        return string.Join(" ", chars);
    }
}
