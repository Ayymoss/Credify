using SharedLibraryCore.Database.Models;

namespace Credify.Models;

public record BetData
{
    public EFClient Origin { get; init; } = null!;
    public EFClient? TargetPlayer { get; init; }
    public long Server { get; init; }
    public string? TargetTeam { get; init; }
    public int TeamRankAverage { get; init; }
    public int TargetPlayerRank { get; init; }
    public int TotalRanked { get; init; }
    public long InitAmount { get; init; }
    public long PayOut { get; set; }
    public string? Message { get; set; }
    public bool TargetWon { get; set; }
    public bool BetCompleted { get; set; }
}
