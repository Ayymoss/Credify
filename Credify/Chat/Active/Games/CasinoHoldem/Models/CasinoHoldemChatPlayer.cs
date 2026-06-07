using System.Collections.Generic;
using System.Threading;
using Credify.Chat.Active.Games.CasinoHoldem.Enums;
using Credify.Games.Cards;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.CasinoHoldem.Models;

/// <summary>Per-player Casino Hold'em session state.</summary>
public class CasinoHoldemChatPlayer
{
    public required EFClient Client { get; init; }
    public CasinoHoldemSessionState State { get; set; } = CasinoHoldemSessionState.AwaitingAnte;

    public long Ante { get; set; }
    public List<Card> PlayerHole { get; } = [];
    public List<Card> DealerHole { get; } = [];
    public List<Card> Community { get; } = []; // 5 dealt up-front; only the flop is shown until the call

    public CancellationTokenSource? IdleToken { get; set; }

    public void CancelIdleTimer()
    {
        IdleToken?.Cancel();
        IdleToken?.Dispose();
        IdleToken = null;
    }
}
