using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Chat.Active.Games.Blackjack.Utilities;
using Credify.Configuration;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace GameServer.Mocks;

/// <summary>
/// Mock output handler that routes all messages through the test harness callback.
/// Extends BlackjackHandleOutput to satisfy BlackjackGame constructor requirements.
/// </summary>
public class MockBlackjackHandleOutput : BlackjackHandleOutput
{
    private readonly Action<EFClient, IEnumerable<string>> _onMessage;
    private readonly BlackjackTranslations _blackjackTrans;

    public MockBlackjackHandleOutput(TranslationsRoot translations, Action<EFClient, IEnumerable<string>> onMessage)
        : base(translations.Blackjack, new GamePlayerCommunication()) // Pass dummy communication
    {
        _blackjackTrans = translations.Blackjack;
        _onMessage = onMessage;
    }

    public override Task TellPlayerAsync(BlackjackPlayer player, IEnumerable<string> messages, bool longPrefix = false)
    {
        var prefix = longPrefix ? _blackjackTrans.Title : _blackjackTrans.TitleShort;
        var formattedMessages = messages.Select(m => $"{prefix} {m}").ToList();
        _onMessage(player.Client, formattedMessages);
        return Task.CompletedTask;
    }

    public override Task TellPlayersAsync(IEnumerable<BlackjackPlayer> players, IEnumerable<string> messages)
    {
        var prefix = _blackjackTrans.TitleShort;
        var formattedMessages = messages.Select(m => $"{prefix} {m}").ToList();
        foreach (var player in players)
        {
            _onMessage(player.Client, formattedMessages);
        }
        return Task.CompletedTask;
    }

    public override Task BroadcastToServerAsync(BlackjackPlayer player, IEnumerable<string> messages)
    {
        // For testing, just send to the player themselves
        return TellPlayerAsync(player, messages, false);
    }

    public override Task BroadcastToAllServersAsync(BlackjackPlayer player, IEnumerable<string> messages)
    {
        // For testing, just send to the player themselves
        return TellPlayerAsync(player, messages, false);
    }

    public override void Tell(BlackjackPlayer player, string message, bool longPrefix = false)
    {
        TellPlayerAsync(player, [message], longPrefix).Wait();
    }

    // Override TellClientAsync method that BlackjackGame uses
    public new async Task TellClientAsync(EFClient client, IEnumerable<string> messages, bool longPrefix = false)
    {
        var prefix = longPrefix ? _blackjackTrans.Title : _blackjackTrans.TitleShort;
        var formattedMessages = messages.Select(m => $"{prefix} {m}").ToList();
        _onMessage(client, formattedMessages);
        await Task.CompletedTask;
    }
}