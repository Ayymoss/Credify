namespace Credify.Chat.Passive.ChatGames.Models;

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
    /// Wall-clock time when the broadcast was sent to all servers.
    /// Used with per-server LatencyMetrics to calculate fair reaction times.
    /// </summary>
    public DateTime BroadcastTime { get; set; }
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
