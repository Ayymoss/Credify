using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Configuration.Translations;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Blackjack.Utilities;

/// <summary>
/// Blackjack-specific output handler inheriting from BaseGameOutputHandler.
/// </summary>
public class BlackjackHandleOutput(BlackjackTranslations translations, GamePlayerCommunication communication)
    : BaseGameOutputHandler<BlackjackPlayer>(communication)
{
    private readonly GamePlayerCommunication _communication = communication;
    protected override EFClient GetClient(BlackjackPlayer player) => player.Client;

    protected override string GetPrefix(bool longPrefix) =>
        longPrefix ? translations.Title : translations.TitleShort;

    /// <summary>
    /// Tells a player by EFClient reference (for cases where BlackjackPlayer isn't available).
    /// This remains a game-specific convenience and is intentionally not part of IGameOutputHandler.
    /// </summary>
    public async Task TellClientAsync(EFClient client, IEnumerable<string> messages, bool longPrefix = false)
    {
        await _communication.TellPlayerAsync(client, GetPrefix(longPrefix), messages);
    }
}
