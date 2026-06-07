using System.Collections.Generic;
using System.Threading;
using Credify.Chat.Active.Games.ThreeCardPoker.Enums;
using Credify.Games.Cards;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.ThreeCardPoker.Models;

/// <summary>Per-player Three-Card Poker session state (the active game holds one of these per seated client).</summary>
public class ThreeCardChatPlayer
{
    public required EFClient Client { get; init; }
    public ThreeCardSessionState State { get; set; } = ThreeCardSessionState.AwaitingAnte;

    public long Ante { get; set; }
    public long PairPlus { get; set; }
    public List<Card> PlayerCards { get; } = [];
    public List<Card> DealerCards { get; } = [];

    public CancellationTokenSource? IdleToken { get; set; }

    public void CancelIdleTimer()
    {
        IdleToken?.Cancel();
        IdleToken?.Dispose();
        IdleToken = null;
    }
}
