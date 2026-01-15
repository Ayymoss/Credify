using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Chat.Active.Games.Poker.Services;
using Credify.Chat.Active.Games.Poker.Utilities;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Poker;

/// <summary>
/// Manager for Poker game. Implements IActiveGame for consistency with other active games.
/// </summary>
public class PokerManager : IActiveGame
{
    private readonly PokerTable _table;
    private readonly CredifyConfiguration _config;

    public PokerManager(
        CredifyConfiguration config,
        TranslationsRoot translations,
        PersistenceService persistenceService,
        GamePlayerCommunication communication)
    {
        _config = config;
        var deckService = new PokerDeckService();
        var handEvaluator = new PokerHandEvaluator();
        var bettingService = new PokerBettingService(config.Poker.SmallBlind, config.Poker.BigBlind);
        var actionValidator = new PokerActionValidator(bettingService);
        var input = new PokerHandleInput(actionValidator, translations.Poker);
        var output = new PokerHandleOutput(translations, communication);

        _table = new PokerTable(
            config,
            translations,
            persistenceService,
            communication,
            input,
            input, // Pass concrete type for FormatAvailableActions
            output,
            deckService,
            handEvaluator,
            bettingService,
            actionValidator);
    }

    /// <summary>
    /// Starts the continuous poker game loop (runs in background).
    /// </summary>
    public async Task StartGameAsync(CancellationToken token) => await _table.GameLoopAsync(token);

    /// <summary>
    /// IActiveGame implementation - allows a player to join the game with a buy-in.
    /// </summary>
    public async Task JoinGameAsync(EFClient player)
    {
        await JoinGameAsync(player, null);
    }

    /// <summary>
    /// Allows a player to join with a specific buy-in amount.
    /// </summary>
    public async Task JoinGameAsync(EFClient player, long? buyInAmount)
    {
        // Use provided buy-in or default to minimum
        var buyIn = buyInAmount ?? _config.Poker.MinimumBuyIn;
        
        var pokerPlayer = new PokerPlayer(player, 0);
        await _table.PlayerJoinAsync(pokerPlayer, buyIn);
    }

    /// <summary>
    /// IActiveGame implementation - removes a player from the game.
    /// </summary>
    public Task LeaveGameAsync(EFClient player)
    {
        _table.PlayerLeave(player);
        return Task.CompletedTask;
    }

    /// <summary>
    /// IActiveGame implementation - handles chat messages from players during gameplay.
    /// </summary>
    public async Task HandleChatAsync(EFClient player, string message)
    {
        // Handle cards/river view commands before routing to table
        var trimmedMessage = message.Trim().ToLower();
        if (trimmedMessage is "cards" or "river")
        {
            await _table.ShowCardsAsync(player, trimmedMessage == "river");
            return;
        }

        await _table.HandleChatAsync(player, message);
    }

    /// <summary>
    /// IActiveGame implementation - checks if a player is in the game.
    /// </summary>
    public bool IsPlayerPlaying(EFClient player) => _table.IsPlayerPlaying(player);

    /// <summary>
    /// IActiveGame implementation - gets the current number of players.
    /// </summary>
    public int GetPlayerCount() => _table.GetPlayerCount();
}
