using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Blackjack.Rewrite;

public class Player(EFClient client)
{
    public EFClient Client { get; } = client;
    public List<Hand> Hands { get; set; } = [];
}
