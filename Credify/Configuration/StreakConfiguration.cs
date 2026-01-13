namespace Credify.Configuration;

public class StreakConfiguration
{
    public bool IsEnabled { get; set; } = true;
    
    /// <summary>
    /// Kill streak thresholds and their credit rewards.
    /// Key = kills needed, Value = credits awarded
    /// </summary>
    public Dictionary<int, int> KillStreakRewards { get; set; } = new()
    {
        [10] = 200,
        [15] = 350,
        [20] = 550,
        [25] = 800,
        [30] = 1100
    };
    
    /// <summary>
    /// Whether to announce kill streaks to the server
    /// </summary>
    public bool AnnounceKillStreaks { get; set; } = true;
    
    /// <summary>
    /// Minimum streak to announce (to avoid spam)
    /// </summary>
    public int MinimumStreakToAnnounce { get; set; } = 15;
}
