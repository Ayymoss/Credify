using Credify.Chat.Passive.ChatGames.Models;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames.Games;

/// <summary>
/// A word completion game where players fill in missing letters.
/// Uses a configurable word bank from ChatGameConfiguration.FillInBlankWords.
/// </summary>
public class CompleteTheWordGame(CredifyConfiguration credifyConfig, PersistenceService persistenceService, ChatUtils chatUtils)
    : ChatGame
{
    private const double HidePercentage = 0.4; // Hide 40% of letters
    
    public override async Task StartAsync()
    {
        GameState = GameState.Started;

        GameInfo = new GameStateInfo
        {
            GameName = chatUtils.GameNameToFriendly(GetType().Name),
            Started = DateTimeOffset.UtcNow
        };

        GenerateQuestion();

        var message = credifyConfig.Translations.Passive.CompleteWordBroadcast.FormatExt(
            PluginConstants.PluginName, 
            GameInfo.GameName, 
            GameInfo.Question);

        // Store per-server broadcast times for fair timing calculation
        GameInfo.ServerBroadcastTimes = await chatUtils.BroadcastToAllServers([message]);

        // Schedule timeout, which will trigger grace period before final calculation
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.CompleteWordTimeout, TimeoutReached, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message, long? gameTime, DateTime eventTime)
    {
        // Accept answers during Started or Closing (grace period) states
        if (GameState is not (GameState.Started or GameState.Closing)) return;
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId))
        {
            client.Tell(credifyConfig.Translations.Passive.AlreadyAnswered);
            return;
        }

        // Check if answer matches (case-insensitive)
        var isCorrect = message.Equals(GameInfo.Answer, StringComparison.OrdinalIgnoreCase);
        if (!isCorrect) return;

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
                ServerEndpoint = serverEndpoint
            };

            GameInfo.Players.Add(player);
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
            var message = credifyConfig.Translations.Passive.GenericNoAnswer.FormatExt(PluginConstants.PluginName, GameInfo.Answer);
            await chatUtils.BroadcastToAllServers([message]);
            return;
        }

        // Sort by fair reaction time
        var sortedPlayers = GameInfo.Players.OrderBy(p => p.ReactionTimeSeconds).ToList();
        var winner = sortedPlayers.First();

        // Calculate and apply payouts based on fair reaction time
        var timeoutSeconds = credifyConfig.ChatGame.CompleteWordTimeout.TotalSeconds;
        foreach (var player in sortedPlayers)
        {
            player.Payout = CalculatePayout(
                player.ReactionTimeSeconds,
                timeoutSeconds,
                credifyConfig.ChatGame.MaxPayout,
                credifyConfig.ChatGame.PayoutDecayExponent);

            await persistenceService.AddCreditsAsync(player.Client, player.Payout);
        }

        // Announce winner
        var broadcastMessage = credifyConfig.Translations.Passive.CompleteWordWinnerBroadcast.FormatExt(
            PluginConstants.PluginName,
            winner.Client.CleanedName,
            winner.Payout.ToString("N0"),
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
            
            // Tell non-winners their time offset
            if (player != winner)
            {
                var timeOffset = player.ReactionTimeSeconds - winner.ReactionTimeSeconds;
                var offsetMessage = credifyConfig.Translations.Passive.ReactionTimeOffset
                    .FormatExt($"{timeOffset:F3}");
                player.Client.Tell(offsetMessage);
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
        GameInfo.Question = CreateMaskedWord(word);
    }

    private static string CreateMaskedWord(string word)
    {
        var chars = word.ToCharArray();
        var indicesToHide = new List<int>();
        
        // Determine how many letters to hide (HidePercentage of word length)
        var hideCount = Math.Max(2, (int)(word.Length * HidePercentage));
        
        // Randomly select indices to hide
        while (indicesToHide.Count < hideCount)
        {
            var index = Random.Shared.Next(word.Length);
            if (!indicesToHide.Contains(index))
            {
                indicesToHide.Add(index);
            }
        }
        
        // Replace selected indices with underscores
        foreach (var index in indicesToHide)
        {
            chars[index] = '_';
        }
        
        // Add spaces between each character so underscores don't blend together in-game chat
        return string.Join(" ", chars);
    }
}
