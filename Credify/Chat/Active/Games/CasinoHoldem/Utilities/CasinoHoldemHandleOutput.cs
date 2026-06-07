using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.CasinoHoldem.Models;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.CasinoHoldem.Utilities;

/// <summary>Casino Hold'em output handler.</summary>
public class CasinoHoldemHandleOutput(CasinoHoldemTranslations translations, GamePlayerCommunication communication)
    : BaseGameOutputHandler<CasinoHoldemChatPlayer>(communication)
{
    protected override EFClient GetClient(CasinoHoldemChatPlayer player) => player.Client;
    protected override string GetPrefix(bool longPrefix) => longPrefix ? translations.Title : translations.TitleShort;
}
