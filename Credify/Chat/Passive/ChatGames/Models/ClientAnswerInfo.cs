using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Passive.ChatGames.Models;

public class ClientAnswerInfo
{
    public bool Winner { get; set; }
    public EFClient Client { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public DateTimeOffset Answered { get; set; }
    public long Payout { get; set; }
    
    /// <summary>
    /// Calculated fair reaction time in seconds, compensated for server log pipeline latency.
    /// </summary>
    public double ReactionTimeSeconds { get; set; }
}
