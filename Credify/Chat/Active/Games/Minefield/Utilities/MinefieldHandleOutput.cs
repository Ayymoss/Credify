using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Minefield.Models;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Minefield.Utilities;

/// <summary>
/// Minefield-specific output handler inheriting from BaseGameOutputHandler.
/// </summary>
public class MinefieldHandleOutput(MinefieldTranslations translations, GamePlayerCommunication communication)
    : BaseGameOutputHandler<MinefieldPlayer>(communication)
{
    private readonly GamePlayerCommunication _communication = communication;
    protected override EFClient GetClient(MinefieldPlayer player) => player.Client;

    protected override string GetPrefix(bool longPrefix) =>
        longPrefix ? translations.Title : translations.TitleShort;

    /// <summary>
    /// Tells a player by EFClient reference (for cases where MinefieldPlayer isn't available).
    /// </summary>
    public async Task TellClientAsync(EFClient client, IEnumerable<string> messages, bool longPrefix = false)
    {
        await _communication.TellPlayerAsync(client, GetPrefix(longPrefix), messages);
    }
}
