namespace Credify.ChatGames.Models;

public class GameStateInfo
{
    public string GameName { get; set; } = null!;
    public string Question { get; set; } = null!;
    public string Answer { get; set; } = null!;
    public List<string> IncorrectAnswers { get; set; } = new();
    public List<string> AllAnswers { get; set; } = new();
    public List<ClientAnswerInfo> Players { get; set; } = new();
    public DateTimeOffset Started { get; set; }
}

public enum GameState
{
    Started,
    Ended
}
