using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Configuration;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Poker.Utilities;

/// <summary>
/// Poker-specific output handler inheriting from BaseGameOutputHandler.
/// </summary>
public class PokerHandleOutput(TranslationsRoot translations, GamePlayerCommunication communication)
    : BaseGameOutputHandler<PokerPlayer>(communication)
{
    private readonly PokerTranslations _pokerTrans = translations.Poker;

    protected override EFClient GetClient(PokerPlayer player) => player.Client;

    protected override string GetPrefix(bool longPrefix) =>
        longPrefix ? _pokerTrans.Title : _pokerTrans.TitleShort;
}
