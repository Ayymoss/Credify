namespace Credify.Models;

public record StatisticsState
{
    public ulong CreditsSpent { get; set; }
    public ulong CreditsEarned { get; set; }
    public ulong CreditsWon { get; set; }
}
