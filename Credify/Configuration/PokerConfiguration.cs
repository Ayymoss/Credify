namespace Credify.Configuration;

public class PokerConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public int MinPlayers { get; set; } = 2;
    public int MaxPlayers { get; set; } = 9;
    public long SmallBlind { get; set; } = 100;
    public long BigBlind { get; set; } = 200;
    public TimeSpan TimeoutForPlayerAction { get; set; } = TimeSpan.FromSeconds(30);
    public long MinimumBuyIn { get; set; } = 1000;
    public long MaximumBuyIn { get; set; } = 100000;
}
