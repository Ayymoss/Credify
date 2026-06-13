using Credify.Games.VideoPoker;

namespace Credify.Configuration;

public class VideoPokerConfiguration
{
    public bool IsEnabled { get; set; } = true;

    public int MinBet { get; set; } = 10;

    /// <summary>Maximum bet (0 = unlimited).</summary>
    public int MaxBet { get; set; } = 50_000;

    // Paytable — gross "for 1" returns (a paying hand returns bet * multiplier; Jacks-or-Better
    // returns the bet, i.e. a push). These are the classic 9/6 "full pay" Jacks or Better values,
    // the most player-friendly standard machine: optimal-strategy RTP ≈ 99.54%.
    //
    // Video-poker RTP is *strategy-dependent* — the figure above assumes perfect hold decisions; real
    // players return less, so the house edge is in practice a touch higher. There is no integer Jacks-
    // or-Better paytable that lands exactly on 100% (9/6 = 99.54%, 10/6 ≈ 100.7%), so 9/6 is the
    // closest-to-fair standard. To pin it to an exact RTP, tune these with a strategy-EV calculator.
    public double RoyalFlush { get; set; } = 800; // flat (no max-coin bonus — single-bet machine)
    public double StraightFlush { get; set; } = 50;
    public double FourOfAKind { get; set; } = 25;
    public double FullHouse { get; set; } = 9;
    public double Flush { get; set; } = 6;
    public double Straight { get; set; } = 4;
    public double ThreeOfAKind { get; set; } = 3;
    public double TwoPair { get; set; } = 2;
    public double JacksOrBetter { get; set; } = 1; // pays even money = returns the bet (a push)

    /// <summary>Gross payout multiplier for a result rank (0 = no win).</summary>
    public double Multiplier(VideoPokerRank rank) => rank switch
    {
        VideoPokerRank.RoyalFlush => RoyalFlush,
        VideoPokerRank.StraightFlush => StraightFlush,
        VideoPokerRank.FourOfAKind => FourOfAKind,
        VideoPokerRank.FullHouse => FullHouse,
        VideoPokerRank.Flush => Flush,
        VideoPokerRank.Straight => Straight,
        VideoPokerRank.ThreeOfAKind => ThreeOfAKind,
        VideoPokerRank.TwoPair => TwoPair,
        VideoPokerRank.JacksOrBetter => JacksOrBetter,
        _ => 0
    };
}
