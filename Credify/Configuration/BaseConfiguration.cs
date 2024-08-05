namespace Credify.Configuration;

public class BaseConfiguration
{
    public TimeSpan AdvertisementIntervalMinutes { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan LotteryFrequency { get; set; } = TimeSpan.FromDays(30);
    public TimeSpan LotteryFrequencyAtTime { get; set; } = new(15, 0, 0);
    public int MaxGiveCredits { get; set; } = 5000;
}
