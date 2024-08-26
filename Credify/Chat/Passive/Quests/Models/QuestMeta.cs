namespace Credify.Chat.Passive.Quests.Models;

public class QuestMeta
{
    public required int QuestId { get; set; }
    public required int Progress { get; set; }
    public required bool Completed { get; set; }
    public int? CompletedDay { get; set; }
}
