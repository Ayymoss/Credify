using Credify.Services;

namespace Credify.Chat.Active.Games.Blackjack.Models;

public class GameStateInfo
{
    public string GameName { get; set; } = null!;
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public List<string> IncorrectAnswers { get; set; } = [];
    public List<string> AllAnswers { get; set; } = [];
    public List<ClientAnswerInfo> Players { get; set; } = [];
    public DateTimeOffset Started { get; set; }
    
    /// <summary>
    /// Per-server broadcast times for fair reaction time calculation.
    /// Key is server endpoint, value is timing info at broadcast.
    /// </summary>
    public Dictionary<long, TimeTrackingInfo> ServerBroadcastTimes { get; set; } = new();
}

public enum GameState
{
    Started,
    /// <summary>
    /// Grace period after timeout - still accepting answers but timer has ended
    /// </summary>
    Closing,
    Ended
}
