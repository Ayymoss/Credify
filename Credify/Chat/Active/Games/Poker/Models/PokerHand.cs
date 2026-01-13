using Credify.Chat.Active.Games.Poker.Enums;

namespace Credify.Chat.Active.Games.Poker.Models;

/// <summary>
/// Represents a evaluated poker hand with ranking information.
/// </summary>
public class PokerHand
{
    public HandRank Rank { get; init; }
    public List<PokerCard> Cards { get; init; } = [];
    public List<int> Kickers { get; init; } = []; // For tie-breaking
    
    /// <summary>
    /// Compares this hand to another hand.
    /// Returns: -1 if this hand is worse, 0 if equal, 1 if better
    /// </summary>
    public int CompareTo(PokerHand other)
    {
        // Compare by rank first
        var rankComparison = Rank.CompareTo(other.Rank);
        if (rankComparison != 0) return rankComparison;
        
        // Same rank - compare kickers
        for (int i = 0; i < Math.Min(Kickers.Count, other.Kickers.Count); i++)
        {
            var kickerComparison = Kickers[i].CompareTo(other.Kickers[i]);
            if (kickerComparison != 0) return kickerComparison;
        }
        
        return 0; // Hands are equal
    }
    
    public bool IsEqualTo(PokerHand other) => CompareTo(other) == 0;
}
