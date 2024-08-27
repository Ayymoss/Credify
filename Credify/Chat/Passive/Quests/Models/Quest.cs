using Credify.Chat.Passive.Quests.Enums;

namespace Credify.Chat.Passive.Quests.Models;

public class Quest
{
    public required bool Enabled { get; set; }
    public required string Name { get; set; }
    public required int Reward { get; set; }
    public required ObjectiveType ObjectiveType { get; set; }
    public required int ObjectiveCount { get; set; }
    public required bool IsRepeatable { get; set; }
    public required bool IsPermanent { get; set; }
}
