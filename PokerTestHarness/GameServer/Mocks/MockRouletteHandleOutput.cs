using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Configuration;
using SharedLibraryCore.Database.Models;
using Player = Credify.Chat.Active.Games.Roulette.Models.Player;

namespace GameServer.Mocks;

/// <summary>
/// Mock output handler that routes all messages through the test harness callback.
/// Directly implements IGameOutputHandler to bypass GamePlayerCommunication and EFClient.Tell().
/// </summary>
public class MockRouletteHandleOutput : IGameOutputHandler<Player>
{
    private readonly Action<EFClient, IEnumerable<string>> _onMessage;
    private readonly TranslationsRoot _translations;

    public MockRouletteHandleOutput(TranslationsRoot translations, Action<EFClient, IEnumerable<string>> onMessage)
    {
        _translations = translations;
        _onMessage = onMessage;
    }

    public Task TellPlayerAsync(Player player, IEnumerable<string> messages, bool longPrefix = false)
    {
        var prefix = longPrefix ? _translations.Roulette.LongPrefix("").TrimEnd() : _translations.Roulette.Prefix("").TrimEnd();
        var formattedMessages = messages.Select(m => $"{prefix} {m}").ToList();
        _onMessage(player.Client, formattedMessages);
        return Task.CompletedTask;
    }

    public Task TellPlayersAsync(IEnumerable<Player> players, IEnumerable<string> messages)
    {
        var prefix = _translations.Roulette.Prefix("").TrimEnd();
        var formattedMessages = messages.Select(m => $"{prefix} {m}").ToList();
        foreach (var player in players)
        {
            _onMessage(player.Client, formattedMessages);
        }
        return Task.CompletedTask;
    }

    public Task BroadcastToServerAsync(Player player, IEnumerable<string> messages)
    {
        // For testing, just send to the player themselves
        return TellPlayerAsync(player, messages, false);
    }

    public Task BroadcastToAllServersAsync(Player player, IEnumerable<string> messages)
    {
        // For testing, just send to the player themselves
        return TellPlayerAsync(player, messages, false);
    }

    public void Tell(Player player, string message, bool longPrefix = false)
    {
        TellPlayerAsync(player, [message], longPrefix).Wait();
    }
}
