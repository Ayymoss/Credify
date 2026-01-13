using Credify.Chat.Active.Games.Blackjack;
using Credify.Chat.Active.Games.Poker;
using Credify.Chat.Passive.ChatGames;
using Credify.Chat.Passive.Quests;
using Credify.Services;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Database.Models;

namespace Credify.EventHandlers;

/// <summary>
/// Handles client message events and routes them to appropriate game handlers.
/// </summary>
public class ClientMessagedEventHandler(
    PassiveManager passiveManager,
    BlackjackManager blackjack,
    QuestManager questManager,
    PokerManager pokerManager,
    ServerTimeTracker serverTimeTracker)
{
    public async Task HandleAsync(ClientMessageEvent messageEvent, CancellationToken token)
    {
        // Update server time tracker for fair timing calculation
        if (messageEvent.Owner is not null)
        {
            serverTimeTracker.UpdateFromEvent(
                messageEvent.Owner.EndPoint,
                messageEvent.GameTime,
                messageEvent.Time);
        }
        
        // Handle chat messages in parallel where order doesn't matter
        await Task.WhenAll(
            passiveManager.HandleChatAsync(
                messageEvent.Client, 
                messageEvent.Message,
                messageEvent.GameTime,
                messageEvent.Time),
            blackjack.HandleChatAsync(messageEvent.Client, messageEvent.Message),
            questManager.HandleChatAsync(messageEvent.Client, messageEvent.Message),
            pokerManager.HandleChatAsync(messageEvent.Client, messageEvent.Message)
        );
    }
}
