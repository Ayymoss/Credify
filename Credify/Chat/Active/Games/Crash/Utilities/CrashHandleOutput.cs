using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Crash.Models;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Crash.Utilities;

/// <summary>
/// Crash output handler. Like Minefield, all output goes to participants only (never a
/// server-wide broadcast), so non-players aren't spammed by the live tick stream.
/// </summary>
public class CrashHandleOutput(CrashTranslations translations, GamePlayerCommunication communication)
    : BaseGameOutputHandler<CrashPlayer>(communication)
{
    private readonly GamePlayerCommunication _communication = communication;
    protected override EFClient GetClient(CrashPlayer player) => player.Client;

    protected override string GetPrefix(bool longPrefix) =>
        longPrefix ? translations.Title : translations.TitleShort;

    public async Task TellClientAsync(EFClient client, IEnumerable<string> messages, bool longPrefix = false)
    {
        await _communication.TellPlayerAsync(client, GetPrefix(longPrefix), messages);
    }
}
