using Credify.Chat.Active.Games.Blackjack;
using Credify.Chat.Active.Games.Poker;
using Credify.Chat.Active.Games.Roulette;
using Credify.Chat.Feature.Bounty;
using Credify.Services;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Database.Models;

namespace Credify.EventHandlers;

/// <summary>
/// Handles client state disposed events, cleaning up client data and game sessions.
/// </summary>
public class ClientStateDisposedEventHandler(
    PersistenceService persistenceService,
    BlackjackManager blackjack,
    RouletteManager rouletteManager,
    PokerManager pokerManager,
    StreakTracker streakTracker,
    BountyContractManager bountyContractManager)
{
    public async Task HandleAsync(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        await Task.WhenAll(
            persistenceService.WriteClientQuestsAsync(clientEvent.Client),
            persistenceService.WriteClientCreditsAsync(clientEvent.Client),
            persistenceService.WriteStatisticsAsync(),
            persistenceService.WriteTopScoreAsync(),
            persistenceService.WriteBankCreditsAsync(),
            blackjack.LeaveGameAsync(clientEvent.Client),
            rouletteManager.LeaveGameAsync(clientEvent.Client),
            pokerManager.LeaveGameAsync(clientEvent.Client)
        );
        
        streakTracker.OnDisconnect(clientEvent.Client);
        bountyContractManager.OnPlayerDisconnect(clientEvent.Client);
    }
}
