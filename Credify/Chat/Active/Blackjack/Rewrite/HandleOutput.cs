using Credify.Configuration;

namespace Credify.Chat.Active.Blackjack.Rewrite;

public class HandleOutput(TranslationsRoot translationsRoot)
{
    public void MessagePlayer(Player player, string message) => player.Client.Tell($"{translationsRoot.Blackjack.TitleShort} {message}");
}
