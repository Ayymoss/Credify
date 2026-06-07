namespace Credify.Games.ThreeCardPoker;

/// <summary>The settled result of a three-card hand, with the payout breakdown the web renders.</summary>
public sealed record ThreeCardOutcome(
    bool DealerQualified,
    string Result, // "Win" | "Lose" | "Push" | "Fold"
    long AntePlayReturn,
    long AnteBonus,
    long PairPlusReturn,
    long TotalReturn,
    long TotalStaked,
    long Net);
