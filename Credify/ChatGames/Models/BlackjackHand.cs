using Credify.ChatGames.Blackjack;

namespace Credify.ChatGames.Models;

public class BlackjackHand
{
    public List<BlackjackCard> Cards { get; set; } = new();
    public BlackjackEnums.PlayerState State { get; set; }
    public BlackjackEnums.GameOutcome Outcome { get; set; }
}

public class BlackJackPlayer
{
    public long? Stake { get; set; }
    public long? Payout { get; set; }
    public bool Queued { get; set; }
}
