namespace Credify.ChatGames.Blackjack.Enums;

public class StateEnums
{
    public enum PlayerState 
    {
        Playing,
        Stand,
        Busted
    }

    public enum GameState
    {
        WaitingForPlayers,
        RequestPlayerStakes,
        DealCards,
        RequestPlayerDecisions,
        DealerPlays,
        Payout
    }
    
    public enum PlayerChoice 
    {
        Hit,
        Stand,
        Cards
    }

    public enum GameOutcome
    {
        Blackjack,
        Win,
        Lose,
        Push
    }
}
