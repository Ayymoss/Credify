using Credify.Configuration;
using Credify.Constants;
using Credify.Chat.Feature.Bounty;
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
    ServerTimeTracker serverTimeTracker,
    CredifyConfiguration config)
{
    public async Task HandleAsync(ClientKillEvent clientEvent, CancellationToken token)
    {
        // Update server time tracker for fair timing calculation
        if (clientEvent.Owner is not null)
        {
            serverTimeTracker.UpdateFromEvent(
                clientEvent.Owner.EndPoint,
                clientEvent.GameTime,
                clientEvent.Time);
        }
        
        await persistenceService.OnKill(clientEvent.Client);
        await questManager.HandleKillAsync(clientEvent);
        
        // Handle streak tracking and bounties (scale auto-bounty by server player count)
        var serverPlayerCount = clientEvent.Owner?.ConnectedClients.Count ?? 2;
        var streakResult = await streakTracker.OnKillAsync(clientEvent.Client, clientEvent.Victim, serverPlayerCount);
        
        // Announce streak reward to killer
        if (streakResult.HasStreakReward)
        {
            var rewardMsg = config.Translations.Core.StreakReward.FormatExt(
                PluginConstants.PluginName, streakResult.CurrentStreak, streakResult.StreakReward.ToString("N0"));
            clientEvent.Client.Tell(rewardMsg);
        }
        
        // Announce streak to server
        if (streakResult.ShouldAnnounceStreak)
        {
            var announceMsg = config.Translations.Core.StreakAnnouncement.FormatExt(
                PluginConstants.PluginName, clientEvent.Client.CleanedName, streakResult.CurrentStreak);
            clientEvent.Owner?.Broadcast(announceMsg);
        }
        
        // Announce bounty placed
        if (streakResult.ShouldAnnounceBounty)
        {
            var bountyMsg = config.Translations.Core.BountyPlaced.FormatExt(
                PluginConstants.PluginName, streakResult.BountyPlaced.ToString("N0"), clientEvent.Client.CleanedName);
            clientEvent.Owner?.Broadcast(bountyMsg);
        }
        
        // Announce bounty claimed
        if (streakResult.ShouldAnnounceBountyClaimed && streakResult.BountyVictim is not null)
        {
            var claimedMsg = config.Translations.Core.BountyClaimed.FormatExt(
                PluginConstants.PluginName, clientEvent.Client.CleanedName, 
                streakResult.BountyClaimed.ToString("N0"), streakResult.BountyVictim.CleanedName);
            clientEvent.Owner?.Broadcast(claimedMsg);
        }
        
        // Reset victim's streak on death
        if (clientEvent.Victim is not null)
        {
            streakTracker.OnDeath(clientEvent.Victim);
            
            // Handle player-placed bounty contracts
            var contractResult = await bountyContractManager.ClaimBountiesAsync(clientEvent.Client, clientEvent.Victim);
            if (contractResult.Success && contractResult.TotalClaimed > 0 && config.BountyContract.AnnounceClaim)
            {
                var contractMsg = config.Translations.Core.BountyContractClaimed.FormatExt(
                    PluginConstants.PluginName, clientEvent.Client.CleanedName,
                    contractResult.TotalClaimed.ToString("N0"), clientEvent.Victim.CleanedName);
                clientEvent.Owner?.Broadcast(contractMsg);
            }
        }
    }
}
