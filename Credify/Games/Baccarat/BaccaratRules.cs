using System;
using System.Collections.Generic;
using System.Linq;
using Credify.Games.Cards;

namespace Credify.Games.Baccarat;

/// <summary>
/// Punto Banco — the standard casino baccarat with no player decisions: both hands draw by a fixed
/// rule set (the "tableau"). Pure and deterministic given the draw order. Card values: Ace = 1,
/// 2–9 face, 10/J/Q/K = 0; a hand's total is the sum mod 10.
/// </summary>
public static class BaccaratRules
{
    public static int Value(Card card) => card.Rank switch
    {
        Rank.Ace => 1,
        Rank.Ten or Rank.Jack or Rank.Queen or Rank.King => 0,
        _ => (int)card.Rank // Two..Nine
    };

    public static int Total(IEnumerable<Card> cards) => cards.Sum(Value) % 10;

    /// <summary>
    /// Deals and resolves a full coup using <paramref name="draw"/> for cards. Applies the natural
    /// (8/9) short-circuit, the player's draw-on-0-to-5 rule, and the banker tableau that depends on
    /// the banker total and the player's third card.
    /// </summary>
    public static BaccaratHand Resolve(Func<Card> draw)
    {
        var player = new List<Card> { draw(), draw() };
        var banker = new List<Card> { draw(), draw() };

        var p = Total(player);
        var b = Total(banker);

        // a natural (8 or 9 on the first two) ends the coup immediately for both hands
        if (p < 8 && b < 8)
        {
            int? playerThird = null;
            if (p <= 5)
            {
                var card = draw();
                player.Add(card);
                playerThird = Value(card);
            }

            bool bankerDraws;
            if (playerThird is null)
            {
                // player stood (6 or 7) → banker plays like the player: draw on 0–5
                bankerDraws = b <= 5;
            }
            else
            {
                bankerDraws = b switch
                {
                    <= 2 => true,
                    3 => playerThird != 8,
                    4 => playerThird is >= 2 and <= 7,
                    5 => playerThird is >= 4 and <= 7,
                    6 => playerThird is 6 or 7,
                    _ => false // 7 stands
                };
            }

            if (bankerDraws) banker.Add(draw());
        }

        var playerTotal = Total(player);
        var bankerTotal = Total(banker);
        var result = playerTotal > bankerTotal ? BaccaratResult.PlayerWin
            : bankerTotal > playerTotal ? BaccaratResult.BankerWin
            : BaccaratResult.Tie;

        return new BaccaratHand(player, banker, playerTotal, bankerTotal, result);
    }
}

/// <summary>A settled coup: both hands, their final totals, and the result.</summary>
public sealed record BaccaratHand(
    IReadOnlyList<Card> Player, IReadOnlyList<Card> Banker, int PlayerTotal, int BankerTotal, BaccaratResult Result);
