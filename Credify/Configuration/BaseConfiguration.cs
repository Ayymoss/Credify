namespace Credify.Configuration;

public class BaseConfiguration
{
    public TimeSpan AdvertisementIntervalMinutes { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan RaffleFrequency { get; set; } = TimeSpan.FromDays(7);
    public TimeSpan RaffleDrawTime { get; set; } = new(15, 0, 0);
    public int MaxGiveCredits { get; set; } = 5000;
    public int RaffleCost { get; set; } = 250;
}
