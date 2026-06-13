using System.Collections.Generic;
using System.Threading.Tasks;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Games;
using Credify.Games.Baccarat;
using Credify.Games.Cards;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// Server-authoritative Punto Banco. One call settles a whole coup: debit, deal &amp; resolve by the
/// fixed tableau (<see cref="BaccaratRules"/>), then pay the chosen bet at its configured (fair) odds.
/// The player's only input is which of Player / Banker / Tie they backed — there are no in-hand
/// decisions in baccarat, so nothing else can be influenced.
/// </summary>
public class BaccaratService(PersistenceService persistence, CredifyConfiguration config)
{
    private const int ShoeDecks = 8; // standard baccarat shoe; the fair payouts are tuned to its odds

    public bool IsEnabled => config.Baccarat.IsEnabled;
    public long MinBet => config.Baccarat.MinBet;
    public long MaxBet => config.Baccarat.MaxBet;

    public async Task<BaccaratReceipt> PlayAsync(EFClient client, long bet, BaccaratBet choice)
    {
        await persistence.RemoveCreditsAsync(client, bet);

        var shoe = BuildShoe();
        var index = 0;
        var hand = BaccaratRules.Resolve(() => shoe[index++]);

        var winnings = (long)(bet * GrossMultiplier(choice, hand.Result, config.Baccarat));

        long newBalance;
        if (winnings > 0)
        {
            newBalance = await persistence.AddCreditsAsync(client, winnings);
            if (winnings > bet) ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, winnings); // real win, not a push
        }
        else
        {
            newBalance = await persistence.GetClientCreditsAsync(client);
        }

        return new BaccaratReceipt(hand, choice, bet, winnings, winnings - bet, newBalance);
    }

    // gross "for 1" return. Player/Banker bets push (return the stake) on a tie; the tie bet loses
    // on any decisive result.
    private static double GrossMultiplier(BaccaratBet choice, BaccaratResult result, BaccaratConfiguration cfg)
    {
        if (result == BaccaratResult.Tie)
            return choice == BaccaratBet.Tie ? cfg.TiePayout : 1.0; // tie wins the tie bet; P/B push

        if (choice == BaccaratBet.Tie) return 0; // tie bet loses on a decisive coup

        var won = (choice == BaccaratBet.Player && result == BaccaratResult.PlayerWin)
                  || (choice == BaccaratBet.Banker && result == BaccaratResult.BankerWin);
        if (!won) return 0;
        return choice == BaccaratBet.Player ? cfg.PlayerPayout : cfg.BankerPayout;
    }

    private static List<Card> BuildShoe()
    {
        var shoe = new List<Card>(52 * ShoeDecks);
        for (var d = 0; d < ShoeDecks; d++) shoe.AddRange(CardDeck.BuildStandardDeck());
        Shuffle.InPlace(shoe);
        return shoe;
    }
}

/// <summary>The settled coup plus the payout breakdown for the chosen bet.</summary>
public sealed record BaccaratReceipt(
    BaccaratHand Hand, BaccaratBet Choice, long Bet, long Winnings, long Profit, long NewBalance);
