namespace Credify.Configuration;

public class DuelConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public int MinStake { get; set; } = 100;
    public int MaxStake { get; set; } = 100_000;

    /// <summary>Kills on the opponent required to win the duel.</summary>
    public int KillsToWin { get; set; } = 5;

    /// <summary>How long a challenge stays open before it expires.</summary>
    public TimeSpan ChallengeTimeout { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Max duel length; if neither hits KillsToWin in time, both stakes are refunded.</summary>
    public TimeSpan MaxDuration { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Percent of the pot taken by the house on a win (0 = winner takes all).</summary>
    public double FeePercent { get; set; } = 0;

    /// <summary>Broadcast the result to the whole server on a win.</summary>
    public bool AnnounceResult { get; set; } = true;
}
