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
        var playersInHand = players.Where(p => !p.IsFolded).ToList();
        
        // If everyone folded but one (or zero), round is complete
        if (playersInHand.Count <= 1) return true;

        // Calculate the maximum bet currently on the table
        var maxBet = playersInHand.Max(p => p.CurrentBet);

        // Check if anyone still needs to act
        // A player needs to act if:
        // 1. They are NOT All-In
        // 2. They have Chips > 0
        // 3. They haven't acted OR their bet is less than the max bet
        var playersNeedingToAct = playersInHand.Where(p => 
            !p.IsAllIn && 
            p.Chips > 0 && 
            (!p.HasActedThisRound || p.CurrentBet < maxBet)
        ).ToList();

        // If anyone needs to act, round is NOT complete
        if (playersNeedingToAct.Any()) return false;

        return true;
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

    /// <summary>
    /// Returns uncalled bets to players (e.g., when a player goes all-in for more than others can match).
    /// </summary>
    public List<(PokerPlayer Player, long Amount)> ReturnUncalledBets(List<PokerPlayer> players)
    {
        var refunds = new List<(PokerPlayer, long)>();
        var activePlayers = players.Where(p => !p.IsFolded).ToList();
        
        if (activePlayers.Count <= 1) return refunds;

        // Group players by their current bet amount
        var bets = activePlayers
            .Select(p => p.CurrentBet)
            .OrderByDescending(b => b)
            .Distinct()
            .ToList();

        if (bets.Count <= 1) return refunds; // All bets equal

        // Highest bet
        var maxBet = bets[0];
        // Second highest bet (the cap for the highest bettor)
        var cap = bets[1];

        var maxBettors = activePlayers.Where(p => p.CurrentBet == maxBet).ToList();
        
        if (maxBettors.Count == 1)
        {
            var player = maxBettors[0];
            var refund = maxBet - cap;
            
            if (refund > 0)
            {
                player.CurrentBet -= refund;
                player.Chips += refund;
                player.TotalInvestedThisHand -= refund;
                refunds.Add((player, refund));
            }
        }

        return refunds;
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
