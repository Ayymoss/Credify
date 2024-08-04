using Credify.Chat.Active.Roulette.Models.BetTypes;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Roulette.Models;

public class Player(EFClient client)
{
    public EFClient Client { get; } = client;
    public BaseBet? Bet { get; private set; }

    public void CreateBet(BaseBet bet) => Bet = bet;
    public void ClearBet() => Bet = null;
}
