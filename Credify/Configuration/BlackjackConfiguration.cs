namespace Credify.Configuration;

public class BlackjackConfiguration
{
    public bool IsEnabled { get; set; } = true;
    public bool JoinAnnouncements { get; set; } = true;
    public double PayoutBlackjack { get; set; } = 2.5d;
    public double PayoutDealerBust { get; set; } = 2d;
    public double PayoutWin { get; set; } = 2d;
    public double PayoutInsurance { get; set; } = 3d; // 2:1 payout + original bet back
    public bool AllowDoubleAfterSplit { get; set; } = true;
    public TimeSpan TimeoutForPlayerAction { get; set; } = TimeSpan.FromSeconds(30);
}

