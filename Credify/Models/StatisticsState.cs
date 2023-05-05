namespace Credify.Models;

public record StatisticsState
{
    public long CreditsSpent { get; set; }
    public long CreditsEarned { get; set; }
    public long CreditsWon { get; set; }
}
