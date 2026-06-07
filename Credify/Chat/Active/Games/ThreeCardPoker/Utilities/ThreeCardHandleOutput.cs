using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.ThreeCardPoker.Models;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.ThreeCardPoker.Utilities;

/// <summary>Three-Card Poker output handler.</summary>
public class ThreeCardHandleOutput(ThreeCardTranslations translations, GamePlayerCommunication communication)
    : BaseGameOutputHandler<ThreeCardChatPlayer>(communication)
{
    protected override EFClient GetClient(ThreeCardChatPlayer player) => player.Client;
    protected override string GetPrefix(bool longPrefix) => longPrefix ? translations.Title : translations.TitleShort;
}
