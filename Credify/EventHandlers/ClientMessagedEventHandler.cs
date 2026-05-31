using Credify.Chat.Active.Games.Blackjack;
using Credify.Chat.Active.Games.Minefield;
using Credify.Chat.Active.Games.Poker;
using Credify.Chat.Passive.ChatGames;
using Credify.Chat.Passive.Quests;
using SharedLibraryCore.Events.Game;

namespace Credify.EventHandlers;

/// <summary>
/// Handles client message events and routes them to appropriate game handlers.
/// </summary>
public class ClientMessagedEventHandler(
    PassiveManager passiveManager,
    BlackjackManager blackjack,
    QuestManager questManager,
    PokerManager pokerManager,
    MinefieldManager minefieldManager)
{
    public async Task HandleAsync(ClientMessageEvent messageEvent, CancellationToken token)
    {
        // Handle chat messages in parallel where order doesn't matter
        await Task.WhenAll(
            passiveManager.HandleChatAsync(
                messageEvent.Client,
                messageEvent.Message,
                messageEvent.Time),
            blackjack.HandleChatAsync(messageEvent.Client, messageEvent.Message),
            questManager.HandleChatAsync(messageEvent.Client, messageEvent.Message),
            pokerManager.HandleChatAsync(messageEvent.Client, messageEvent.Message),
            minefieldManager.HandleChatAsync(messageEvent.Client, messageEvent.Message)
        );
    }
}
