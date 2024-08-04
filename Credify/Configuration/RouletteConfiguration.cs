namespace Credify.Configuration;

public class RouletteConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public bool JoinAnnouncements { get; set; } = true;
    public TimeSpan TimeoutForPlayerAction { get; set; } = TimeSpan.FromSeconds(10);
}
