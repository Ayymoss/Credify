using Credify.ChatGames.Blackjack;
using Credify.ChatGames.Blackjack.Enums;

namespace Credify.ChatGames.Models;

public class BlackjackHand
{
    public List<BlackjackCard> Cards { get; set; } = new();
    public StateEnums.PlayerState State { get; set; }
    public StateEnums.GameOutcome Outcome { get; set; }
}

public class BlackJackPlayer
{
    public long? Stake { get; set; }
    public long? Payout { get; set; }
    public bool Queued { get; set; }
}
