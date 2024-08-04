namespace Credify.Chat.Active.Blackjack;

public abstract class BlackjackEnums
{
    public enum PlayerState 
    {
        Playing,
        Stand,
        Busted
    }

    public enum GameState
    {
        SettingUpGame,
        WaitingForPlayers,
        RequestPlayerStakes,
        DealCards,
        RequestPlayerDecisions,
        DealerPlays,
        Payout,
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
