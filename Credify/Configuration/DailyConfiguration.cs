namespace Credify.Configuration;

public class DailyConfiguration
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>Reward for a day-1 claim.</summary>
    public long BaseReward { get; set; } = 1_000;

    /// <summary>Added per consecutive day, up to <see cref="MaxStreakDays"/>.</summary>
    public long StreakBonus { get; set; } = 500;

    /// <summary>Streak length at which the reward stops scaling (e.g. 7 = caps after a week).</summary>
    public int MaxStreakDays { get; set; } = 7;
}
