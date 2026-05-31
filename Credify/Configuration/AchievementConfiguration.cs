using Credify.Chat.Passive.Quests.Enums;

namespace Credify.Configuration;

public class AchievementConfiguration
{
    public bool IsEnabled { get; set; } = true;

    /// <summary>Broadcast a server-wide message when a player unlocks an achievement.</summary>
    public bool AnnounceUnlocks { get; set; } = true;

    /// <summary>
    /// Achievement definitions. Each tracks a cumulative total for its Objective and unlocks
    /// (granting its Name as a title + Reward credits) once Threshold is reached. Tiers that
    /// share an Objective share the same running total.
    /// </summary>
    public List<Achievement> Achievements { get; set; } =
    [
        // Kills
        new() { Id = "kills_soldier",  Name = "Soldier",      Objective = ObjectiveType.Kill, Threshold = 100,     Reward = 5_000,   Description = "100 kills" },
        new() { Id = "kills_veteran",  Name = "Veteran",      Objective = ObjectiveType.Kill, Threshold = 1_000,   Reward = 25_000,  Description = "1,000 kills" },
        new() { Id = "kills_warlord",  Name = "Warlord",      Objective = ObjectiveType.Kill, Threshold = 10_000,  Reward = 250_000, Description = "10,000 kills" },

        // Credits won (Baller)
        new() { Id = "won_rich",       Name = "Getting Rich", Objective = ObjectiveType.Baller, Threshold = 100_000,    Reward = 10_000,  Description = "Win 100k total" },
        new() { Id = "won_highroller", Name = "High Roller",  Objective = ObjectiveType.Baller, Threshold = 1_000_000,  Reward = 100_000, Description = "Win 1M total" },
        new() { Id = "won_whale",      Name = "Whale",        Objective = ObjectiveType.Baller, Threshold = 10_000_000, Reward = 500_000, Description = "Win 10M total" },

        // Credits spent
        new() { Id = "spent_spender",  Name = "Spender",      Objective = ObjectiveType.CreditsSpent, Threshold = 100_000,   Reward = 10_000,  Description = "Spend 100k total" },
        new() { Id = "spent_big",      Name = "Big Spender",  Objective = ObjectiveType.CreditsSpent, Threshold = 1_000_000, Reward = 75_000,  Description = "Spend 1M total" },

        // Games played
        new() { Id = "mine_sapper",    Name = "Sapper",       Objective = ObjectiveType.Minefield, Threshold = 50,  Reward = 15_000, Description = "Play 50 Minefield rounds" },
        new() { Id = "trivia_quiz",    Name = "Quizmaster",   Objective = ObjectiveType.Trivia,    Threshold = 50,  Reward = 15_000, Description = "Win 50 chat games" },
        new() { Id = "donate_phil",    Name = "Philanthropist", Objective = ObjectiveType.Donation, Threshold = 100_000, Reward = 20_000, Description = "Gift 100k to players" }
    ];
}

/// <summary>
/// A single achievement definition. <see cref="Name"/> doubles as the unlocked title.
/// </summary>
public class Achievement
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public ObjectiveType Objective { get; set; }
    public long Threshold { get; set; }
    public long Reward { get; set; }
    public bool Enabled { get; set; } = true;
}
