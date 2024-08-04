using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Blackjack.Models;

public class ClientAnswerInfo
{
    public bool Winner { get; set; }
    public EFClient Client { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public DateTimeOffset Answered { get; set; }
    public long Payout { get; set; }
}
