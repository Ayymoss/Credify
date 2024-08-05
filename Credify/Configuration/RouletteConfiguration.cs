namespace Credify.Configuration;

public class RouletteConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public bool AnnounceMaxPayoutWinners { get; set; } = true;
    public TimeSpan TimeoutForPlayerAction { get; set; } = TimeSpan.FromSeconds(20);
}
