using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Configuration;
using Credify.Configuration.Translations;

namespace Credify.Chat.Active.Games.Poker.Utilities;

/// <summary>
/// Poker-specific output handler that wraps GamePlayerCommunication with poker-specific formatting.
/// </summary>
public class PokerHandleOutput(TranslationsRoot translations, GamePlayerCommunication communication)
{
    private readonly PokerTranslations _pokerTrans = translations.Poker;

    /// <summary>
    /// Sends a message to a single player.
    /// </summary>
    public async Task TellPlayerAsync(PokerPlayer player, IEnumerable<string> messages, bool longPrefix = false)
    {
        var prefix = longPrefix ? _pokerTrans.Title : _pokerTrans.TitleShort;
        await communication.TellPlayerAsync(player.Client, prefix, messages);
    }

    /// <summary>
    /// Sends a message to multiple players.
    /// </summary>
    public async Task TellPlayersAsync(List<PokerPlayer> players, IEnumerable<string> messages)
    {
        await communication.TellPlayersAsync(players.Select(p => p.Client), messages);
    }
}
