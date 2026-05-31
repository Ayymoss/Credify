namespace Credify.Chat.Feature.Achievements.Models;

/// <summary>
/// A client's persisted achievement state: cumulative totals per objective and the set of
/// unlocked achievement ids.
/// </summary>
public class AchievementProgress
{
    /// <summary>Cumulative total per objective, keyed by (int)ObjectiveType.</summary>
    public Dictionary<int, long> Totals { get; set; } = new();

    /// <summary>Ids of achievements the client has already unlocked.</summary>
    public List<string> Unlocked { get; set; } = [];
}
