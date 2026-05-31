using Credify.Configuration;
using Credify.Constants;
using Credify.Chat.Feature.Achievements;
using Credify.Chat.Feature.Bounty;
using Credify.Chat.Feature.Duel;
using Credify.Chat.Passive.Quests;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Database.Models;

namespace Credify.EventHandlers;

/// <summary>
/// Handles client kill events including streaks, bounties, and quests.
/// </summary>
public class ClientKilledEventHandler(
    PersistenceService persistenceService,
    QuestManager questManager,
    StreakTracker streakTracker,
    BountyContractManager bountyContractManager,
    AchievementManager achievementManager,
    DuelManager duelManager,
    CredifyConfiguration config)
{
    public async Task HandleAsync(ClientKillEvent clientEvent, CancellationToken token)
    {
        await persistenceService.OnKill(clientEvent.Client);
        await questManager.HandleKillAsync(clientEvent);
        await achievementManager.HandleKill(clientEvent.Client);
        
        // Handle streak tracking and bounties (scale auto-bounty by server player count)
        var serverPlayerCount = clientEvent.Owner?.ConnectedClients.Count ?? 2;
        var streakResult = await streakTracker.OnKillAsync(clientEvent.Client, clientEvent.Victim, serverPlayerCount);
        
        // Announce streak reward to killer
        if (streakResult.HasStreakReward)
        {
            var rewardMsg = config.Translations.Streak.Reward.FormatExt(
                PluginConstants.PluginName, streakResult.CurrentStreak, streakResult.StreakReward.ToString("N0"));
            clientEvent.Client.Tell(rewardMsg);
        }
        
        // Announce streak to server
        if (streakResult.ShouldAnnounceStreak)
        {
            var announceMsg = config.Translations.Streak.Announcement.FormatExt(
                PluginConstants.PluginName, clientEvent.Client.CleanedName, streakResult.CurrentStreak);
            clientEvent.Owner?.Broadcast(announceMsg);
        }
        
        // Announce bounty placed
        if (streakResult.ShouldAnnounceBounty)
        {
            var bountyMsg = config.Translations.Streak.BountyPlaced.FormatExt(
                PluginConstants.PluginName, streakResult.BountyPlaced.ToString("N0"), clientEvent.Client.CleanedName);
            clientEvent.Owner?.Broadcast(bountyMsg);
        }
        
        // Announce bounty claimed
        if (streakResult.ShouldAnnounceBountyClaimed && streakResult.BountyVictim is not null)
        {
            var claimedMsg = config.Translations.Streak.BountyClaimed.FormatExt(
                PluginConstants.PluginName, clientEvent.Client.CleanedName, 
                streakResult.BountyClaimed.ToString("N0"), streakResult.BountyVictim.CleanedName);
            clientEvent.Owner?.Broadcast(claimedMsg);
        }
        
        // Reset victim's streak on death
        if (clientEvent.Victim is not null)
        {
            streakTracker.OnDeath(clientEvent.Victim);
            await duelManager.HandleKillAsync(clientEvent.Client, clientEvent.Victim);

            // Handle player-placed bounty contracts
            var contractResult = await bountyContractManager.ClaimBountiesAsync(clientEvent.Client, clientEvent.Victim);
            if (contractResult.Success && contractResult.TotalClaimed > 0)
            {
                // Always give the killer a direct confirmation (a broadcast is easy to miss).
                clientEvent.Client.Tell(config.Translations.BountyContract.ClaimedDirect.FormatExt(
                    contractResult.TotalClaimed.ToString("N0"), clientEvent.Victim.CleanedName));

                if (config.BountyContract.AnnounceClaim)
                {
                    var contractMsg = config.Translations.BountyContract.Claimed.FormatExt(
                        PluginConstants.PluginName, clientEvent.Client.CleanedName,
                        contractResult.TotalClaimed.ToString("N0"), clientEvent.Victim.CleanedName);
                    clientEvent.Owner?.Broadcast(contractMsg);
                }
            }
        }
    }
}
