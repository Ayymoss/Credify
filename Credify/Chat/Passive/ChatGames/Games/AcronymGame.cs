using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames.Games;

/// <summary>
/// A game where players guess what gaming/CoD acronyms stand for.
/// Uses a curated list of gaming-specific acronyms.
/// </summary>
public class AcronymGame(CredifyConfiguration credifyConfig, PersistenceService persistenceService, ChatUtils chatUtils)
    : ChatGame
{
    // Curated list of gaming/CoD acronyms with their meanings
    private static readonly Dictionary<string, string[]> Acronyms = new()
    {
        // Game modes
        ["FFA"] = ["Free For All"],
        ["TDM"] = ["Team Deathmatch"],
        ["DOM"] = ["Domination"],
        ["S&D"] = ["Search and Destroy", "Search & Destroy", "SnD"],
        ["CTF"] = ["Capture The Flag"],
        ["HQ"] = ["Headquarters"],
        ["KC"] = ["Kill Confirmed"],
        ["HP"] = ["Hardpoint"],
        ["GW"] = ["Ground War"],
        
        // Gameplay terms
        ["ADS"] = ["Aim Down Sights", "Aim Down Sight"],
        ["TTK"] = ["Time To Kill"],
        ["KDR"] = ["Kill Death Ratio", "Kill/Death Ratio"],
        ["SPM"] = ["Score Per Minute"],
        ["OBJ"] = ["Objective"],
        ["UAV"] = ["Unmanned Aerial Vehicle"],
        ["EMP"] = ["Electromagnetic Pulse"],
        ["RPG"] = ["Rocket Propelled Grenade"],
        ["LMG"] = ["Light Machine Gun"],
        ["SMG"] = ["Submachine Gun"],
        ["DMR"] = ["Designated Marksman Rifle"],
        ["AR"] = ["Assault Rifle"],
        
        // Slang/Community
        ["GG"] = ["Good Game"],
        ["GGWP"] = ["Good Game Well Played"],
        ["AFK"] = ["Away From Keyboard"],
        ["BRB"] = ["Be Right Back"],
        ["NPC"] = ["Non Player Character", "Non-Player Character"],
        ["DLC"] = ["Downloadable Content"],
        ["OP"] = ["Overpowered"],
        ["MVP"] = ["Most Valuable Player"],
        ["KS"] = ["Killstreak"],
        ["WP"] = ["Well Played"],
        ["GL"] = ["Good Luck"],
        ["HF"] = ["Have Fun"],
        ["GLHF"] = ["Good Luck Have Fun"],
        ["POTG"] = ["Play Of The Game"],
        ["OHK"] = ["One Hit Kill"],
        ["QS"] = ["Quickscope"],
        ["NS"] = ["Noscope", "No Scope", "Nice Shot"],
        ["HC"] = ["Hardcore"],
        ["MP"] = ["Multiplayer"],
        ["SP"] = ["Single Player", "Singleplayer"],
        ["FPS"] = ["First Person Shooter", "Frames Per Second"],
        ["PVP"] = ["Player Versus Player", "Player vs Player"],
        ["PVE"] = ["Player Versus Environment", "Player vs Environment"],
        ["RCON"] = ["Remote Console", "Remote Control"],
        ["IW"] = ["Infinity Ward"],
        ["COD"] = ["Call of Duty"],
        ["MW"] = ["Modern Warfare"],
        ["BO"] = ["Black Ops"],
        ["WZ"] = ["Warzone"],
        ["BR"] = ["Battle Royale"]
    };

    public override async Task StartAsync()
    {
        GameState = GameState.Started;

        GameInfo = new GameStateInfo
        {
            GameName = chatUtils.GameNameToFriendly(GetType().Name),
            Started = DateTimeOffset.UtcNow
        };

        GenerateQuestion();

        var message = credifyConfig.Translations.Passive.AcronymBroadcast.FormatExt(
            PluginConstants.PluginName, 
            GameInfo.GameName, 
            GameInfo.Question);

        // Store per-server broadcast times for fair timing calculation
        GameInfo.ServerBroadcastTimes = await chatUtils.BroadcastToAllServers([message]);

        // Schedule timeout, which will trigger grace period before final calculation
        Utilities.ExecuteAfterDelay(credifyConfig.ChatGame.AcronymTimeout, TimeoutReached, CancellationToken.None);
    }

    public override async Task HandleChatMessageAsync(EFClient client, string message, long? gameTime, DateTime eventTime)
    {
        // Accept answers during Started or Closing (grace period) states
        if (GameState is not (GameState.Started or GameState.Closing)) return;
        
        // Check if player already answered
        if (GameInfo.Players.Any(x => x.Client.ClientId == client.ClientId))
        {
            client.Tell(credifyConfig.Translations.Passive.AlreadyAnswered);
            return;
        }

        // Check if answer is correct (case-insensitive, check against all valid answers)
        var isCorrect = GameInfo.AllAnswers.Any(a => a.Equals(message, StringComparison.OrdinalIgnoreCase));
        if (!isCorrect) return;

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
        var timeoutSeconds = credifyConfig.ChatGame.AcronymTimeout.TotalSeconds;
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
        var broadcastMessage = credifyConfig.Translations.Passive.AcronymWinnerBroadcast.FormatExt(
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
        }
    }

    private void GenerateQuestion()
    {
        // Select a random acronym
        var acronyms = Acronyms.Keys.ToArray();
        var selectedAcronym = acronyms[Random.Shared.Next(acronyms.Length)];
        var validAnswers = Acronyms[selectedAcronym];

        GameInfo.Question = selectedAcronym;
        GameInfo.Answer = validAnswers[0]; // Primary answer for display
        GameInfo.AllAnswers = validAnswers.ToList(); // All valid answers
    }
}
