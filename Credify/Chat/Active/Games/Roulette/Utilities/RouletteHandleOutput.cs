using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Models;
using Credify.Configuration;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Roulette.Utilities;

/// <summary>
/// Roulette-specific output handler inheriting from BaseGameOutputHandler.
/// </summary>
public class RouletteHandleOutput(TranslationsRoot translations, GamePlayerCommunication communication)
    : BaseGameOutputHandler<Player>(communication)
{
    protected override EFClient GetClient(Player player) => player.Client;

    protected override string GetPrefix(bool longPrefix) =>
        longPrefix ? translations.Roulette.LongPrefix("").TrimEnd() : translations.Roulette.Prefix("").TrimEnd();

    /// <summary>
    /// Override Tell to use Roulette's prefix formatting which includes the message.
    /// </summary>
    public override void Tell(Player player, string message, bool longPrefix = false)
    {
        var prefixedMessage = longPrefix 
            ? translations.Roulette.LongPrefix(message) 
            : translations.Roulette.Prefix(message);
        player.Client.Tell(prefixedMessage);
    }

    // Convenience aliases for backward compatibility with Table.cs
    public Task TellAsync(List<Player> players, List<string> messages) =>
        TellPlayersAsync(players, messages);

    public Task TellAllServerAsync(Player player, List<string> messages) =>
        BroadcastToServerAsync(player, messages);

    public Task TellAllServersAsync(Player player, List<string> messages) =>
        BroadcastToAllServersAsync(player, messages);
}

