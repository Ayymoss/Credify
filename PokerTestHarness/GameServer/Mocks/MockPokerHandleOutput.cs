using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Configuration;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace GameServer.Mocks;

/// <summary>
/// Mock output handler that routes all messages through the test harness callback.
/// Directly implements IGameOutputHandler to bypass GamePlayerCommunication and EFClient.Tell().
/// </summary>
public class MockPokerHandleOutput : IGameOutputHandler<PokerPlayer>
{
    private readonly Action<EFClient, IEnumerable<string>> _onMessage;
    private readonly PokerTranslations _pokerTrans;

    public MockPokerHandleOutput(TranslationsRoot translations, Action<EFClient, IEnumerable<string>> onMessage)
    {
        _pokerTrans = translations.Poker;
        _onMessage = onMessage;
    }

    public Task TellPlayerAsync(PokerPlayer player, IEnumerable<string> messages, bool longPrefix = false)
    {
        var prefix = longPrefix ? _pokerTrans.Title : _pokerTrans.TitleShort;
        var formattedMessages = messages.Select(m => $"{prefix} {m}").ToList();
        _onMessage(player.Client, formattedMessages);
        return Task.CompletedTask;
    }

    public Task TellPlayersAsync(IEnumerable<PokerPlayer> players, IEnumerable<string> messages)
    {
        var prefix = _pokerTrans.TitleShort;
        var formattedMessages = messages.Select(m => $"{prefix} {m}").ToList();
        foreach (var player in players)
        {
            _onMessage(player.Client, formattedMessages);
        }
        return Task.CompletedTask;
    }

    public Task BroadcastToServerAsync(PokerPlayer player, IEnumerable<string> messages)
    {
        // For testing, just send to the player themselves
        return TellPlayerAsync(player, messages, false);
    }

    public Task BroadcastToAllServersAsync(PokerPlayer player, IEnumerable<string> messages)
    {
        // For testing, just send to the player themselves
        return TellPlayerAsync(player, messages, false);
    }

    public void Tell(PokerPlayer player, string message, bool longPrefix = false)
    {
        TellPlayerAsync(player, [message], longPrefix).Wait();
    }
}
