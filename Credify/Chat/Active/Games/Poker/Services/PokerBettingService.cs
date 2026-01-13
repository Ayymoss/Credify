using Credify.Chat.Active.Games.Poker.Models;

namespace Credify.Chat.Active.Games.Poker.Services;

/// <summary>
/// Service responsible for managing betting rounds, pots, and side pots.
/// </summary>
public class PokerBettingService
{
    private readonly long _smallBlind;
    private readonly long _bigBlind;

    public PokerBettingService(long smallBlind, long bigBlind)
    {
        _smallBlind = smallBlind;
        _bigBlind = bigBlind;
    }

    /// <summary>
    /// Posts the small blind for a player.
    /// </summary>
    public long PostSmallBlind(PokerPlayer player, BettingRound round)
    {
        var amount = Math.Min(_smallBlind, player.Chips);
        player.Chips -= amount;
        player.CurrentBet = amount;
        player.TotalInvestedThisHand += amount;
        round.CurrentBet = amount;
        return amount; // Return amount for pot tracking
    }

    /// <summary>
    /// Posts the big blind for a player.
    /// </summary>
    public long PostBigBlind(PokerPlayer player, BettingRound round)
    {
        var amount = Math.Min(_bigBlind, player.Chips);
        player.Chips -= amount;
        player.CurrentBet = amount;
        player.TotalInvestedThisHand += amount;
        round.CurrentBet = amount;
        return amount; // Return amount for pot tracking
    }

    /// <summary>
    /// Calculates the minimum raise amount.
    /// </summary>
    public long CalculateMinimumRaise(long currentBet, long previousRaise)
    {
        var raiseAmount = previousRaise > 0 ? previousRaise : _bigBlind;
        return currentBet + raiseAmount;
    }

    /// <summary>
    /// Checks if a betting round is complete (all active players have acted and bets are equal).
    /// </summary>
    public bool IsBettingRoundComplete(List<PokerPlayer> players, BettingRound round)
    {
        var activePlayers = players.Where(p => p.IsActive()).ToList();
        if (activePlayers.Count <= 1) return true;

        // Check if all active players have acted
        var playersNeedingToAct = activePlayers.Where(p => !p.HasActedThisRound || 
            (round.CurrentBet > 0 && p.CurrentBet < round.CurrentBet && p.Chips > 0)).ToList();

        if (playersNeedingToAct.Any()) return false;

        // If only one distinct bet amount (excluding all-in players who may have lower amounts), round is complete
        var nonAllInBets = activePlayers
            .Where(p => !p.IsAllIn && p.Chips > 0)
            .Select(p => p.CurrentBet)
            .Distinct()
            .ToList();

        return nonAllInBets.Count <= 1;
    }

    /// <summary>
    /// Calculates side pots for all-in scenarios.
    /// </summary>
    public List<SidePot> CalculateSidePots(List<PokerPlayer> players, long totalPot)
    {
        var sidePots = new List<SidePot>();
        var activePlayers = players.Where(p => !p.IsFolded && p.TotalInvestedThisHand > 0).ToList();
        
        if (activePlayers.Count == 0) return sidePots;

        var sortedInvestments = activePlayers
            .Select(p => p.TotalInvestedThisHand)
            .Distinct()
            .OrderBy(v => v)
            .ToList();

        long previousInvestment = 0;
        long remainingPot = totalPot;
        
        foreach (var investment in sortedInvestments)
        {
            // Calculate how much each eligible player contributed to this pot level
            var eligiblePlayers = activePlayers.Where(p => p.TotalInvestedThisHand >= investment).ToList();
            
            if (eligiblePlayers.Count == 0) continue;

            // Each eligible player contributed (investment - previousInvestment) to this side pot
            var contributionPerPlayer = investment - previousInvestment;
            var potAmount = contributionPerPlayer * eligiblePlayers.Count;

            if (potAmount > 0 && eligiblePlayers.Count > 0 && remainingPot >= potAmount)
            {
                sidePots.Add(new SidePot
                {
                    Amount = potAmount,
                    EligiblePlayers = eligiblePlayers
                });
                remainingPot -= potAmount;
            }

            previousInvestment = investment;
        }

        // If there's any remaining pot (due to rounding or edge cases), add it to the main pot
        if (remainingPot > 0 && sidePots.Count > 0)
        {
            sidePots[0].Amount += remainingPot;
        }
        else if (remainingPot > 0 && sidePots.Count == 0)
        {
            // All players folded, but we have a pot - shouldn't happen, but handle it
            sidePots.Add(new SidePot
            {
                Amount = remainingPot,
                EligiblePlayers = activePlayers
            });
        }

        return sidePots;
    }

    /// <summary>
    /// Distributes pots to winners.
    /// </summary>
    public void DistributePot(List<SidePot> sidePots, Dictionary<PokerPlayer, PokerHand> playerHands)
    {
        foreach (var sidePot in sidePots)
        {
            var eligibleWithHands = sidePot.EligiblePlayers
                .Where(p => playerHands.ContainsKey(p))
                .ToList();

            if (eligibleWithHands.Count == 0) continue;

            // Find best hand(s)
            var bestHand = eligibleWithHands
                .Select(p => playerHands[p])
                .MaxBy(h => h, Comparer<PokerHand>.Create((a, b) => a.CompareTo(b)));

            var winners = eligibleWithHands
                .Where(p => playerHands[p].IsEqualTo(bestHand!))
                .ToList();

            // Split pot among winners
            var amountPerWinner = sidePot.Amount / winners.Count;
            var remainder = sidePot.Amount % winners.Count;

            foreach (var winner in winners)
            {
                winner.Chips += amountPerWinner;
            }

            // Give remainder to first winner (or dealer if split)
            if (remainder > 0 && winners.Count > 0)
            {
                winners[0].Chips += remainder;
            }
        }
    }
}

/// <summary>
/// Represents a side pot created when players go all-in.
/// </summary>
public class SidePot
{
    public long Amount { get; set; }
    public List<PokerPlayer> EligiblePlayers { get; set; } = [];
}
