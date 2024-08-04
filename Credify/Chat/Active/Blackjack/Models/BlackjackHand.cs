namespace Credify.Chat.Active.Blackjack.Models;

public class BlackjackHand
{
    public List<BlackjackCard> Cards { get; set; } = [];
    public BlackjackEnums.PlayerState State { get; set; }
    public BlackjackEnums.GameOutcome Outcome { get; set; }
}

public class BlackJackPlayer(bool queued)
{
    public long? Stake { get; set; }
    public long? Payout { get; set; }
    public bool Queued { get; set; } = queued;
}
