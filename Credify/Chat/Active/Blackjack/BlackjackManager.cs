﻿using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Blackjack;

public class BlackjackManager(CredifyConfiguration credifyConfig, PersistenceService persistenceService)
{
    private readonly BlackjackGame _game = new(persistenceService, credifyConfig);

    public async Task HandleChatEventAsync(EFClient player, string message) => await _game.HandleChatAsync(player, message);
    public async Task JoinGameAsync(EFClient player) => await _game.JoinGameAsync(player);
    public async Task LeaveGameAsync(EFClient player) => await _game.LeaveGameAsync(player);
    public bool IsPlayerPlaying(EFClient player) => _game.IsPlayerPlaying(player);
    public int GetPlayerCount() => _game.GetPlayerCount();
}
