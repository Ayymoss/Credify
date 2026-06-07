namespace Credify.Games.CasinoHoldem;

/// <summary>The settled result of a Casino Hold'em hand, with the payout breakdown the web renders.</summary>
public sealed record CasinoHoldemOutcome(
    bool DealerQualified,
    string Result, // "Win" | "Lose" | "Push" | "Fold"
    long AnteReturn,
    long CallReturn,
    long TotalReturn,
    long TotalStaked,
    long Net);
