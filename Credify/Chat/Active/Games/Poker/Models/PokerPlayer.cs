using Credify.Chat.Active.Games.Poker.Enums;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Poker.Models;

public class PokerPlayer
{
    public EFClient Client { get; init; }
    public long Chips { get; set; }
    public List<PokerCard> HoleCards { get; set; } = [];
    public long CurrentBet { get; set; }
    public long TotalInvestedThisHand { get; set; }
    public PlayerAction? LastAction { get; set; }
    public bool IsFolded { get; set; }
    public bool IsAllIn { get; set; }
    public int Position { get; set; } // Seat position at table
    public bool IsDealer { get; set; }
    public bool IsSmallBlind { get; set; }
    public bool IsBigBlind { get; set; }
    public bool HasActedThisRound { get; set; }

    public PokerPlayer(EFClient client, long chips)
    {
        Client = client;
        Chips = chips;
    }

    public void ResetForNewHand()
    {
        HoleCards.Clear();
        CurrentBet = 0;
        TotalInvestedThisHand = 0;
        LastAction = null;
        IsFolded = false;
        IsAllIn = false;
        HasActedThisRound = false;
    }

    public void ResetForNewRound()
    {
        CurrentBet = 0;
        LastAction = null;
        HasActedThisRound = false;
    }

    public bool IsActive() => !IsFolded && !IsAllIn && Chips > 0;
}
