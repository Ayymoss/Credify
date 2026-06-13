namespace Credify.Configuration;

public class BaccaratConfiguration
{
    public bool IsEnabled { get; set; } = true;

    public int MinBet { get; set; } = 10;

    /// <summary>Maximum bet (0 = unlimited).</summary>
    public int MaxBet { get; set; } = 50_000;

    // Payouts are gross "for 1" returns (a winning bet returns bet * payout; Player/Banker bets push
    // on a tie, returning the stake).
    //
    // Tuned to FAIR ODDS — zero house edge — from the standard 8-deck win probabilities:
    //   Player 44.625% · Banker 45.860% · Tie 9.516%   (Player/Banker pushes on a tie)
    // Real baccarat takes its edge from a 5% banker commission and an 8:1 tie; here instead each bet
    // pays its true odds, so every bet is break-even in expectation:
    //   Player win  : lose/win = .45860/.44625 = 1.0277 profit  → 2.0277 gross  (~1.03 : 1)
    //   Banker win  : .44625/.45860 = 0.9731 profit             → 1.9731 gross  (~0.97 : 1, no commission)
    //   Tie         : (1-.09516)/.09516 = 9.5089 profit         → 10.5089 gross (~9.51 : 1)
    // For an authentic house-edge game instead, set Player/Banker to 2.0 and apply a banker commission,
    // and Tie to 9.0 — but that breaks the 1:1 economy.
    public double PlayerPayout { get; set; } = 2.0277;
    public double BankerPayout { get; set; } = 1.9731;
    public double TiePayout { get; set; } = 10.5089;
}
