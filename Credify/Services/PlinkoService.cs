using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Games.Plinko;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// The single source of truth for a Plinko drop: stake debit, the pure <see cref="PlinkoBoard"/> drop, payout
/// credit and the objective event. Mirrors <see cref="SlotsService"/> so the economy is settled atomically and
/// consistently (debit the stake, pay stake × multiplier, the house bank is never touched — wins are "invented"
/// and losses aren't drained from it, exactly like the other web games). Stake validation stays with the caller.
/// </summary>
public class PlinkoService(PersistenceService persistence, CredifyConfiguration config)
{
    private readonly PlinkoPayoutCalculator _calculator = new(config.Plinko);

    public bool IsEnabled => config.Plinko.IsEnabled;
    public long MinBet => config.Plinko.MinBet;
    public long MaxBet => config.Plinko.MaxBet;
    public IReadOnlyList<int> RowPresets => config.Plinko.RowPresets;
    public int DefaultRows => config.Plinko.DefaultRows;

    /// <summary>The multiplier curve for a board, for the page to render the bucket strip.</summary>
    public IReadOnlyList<double> Multipliers(int rows, PlinkoRisk risk) => _calculator.Multipliers(rows, risk);

    /// <summary>
    /// Debits <paramref name="bet"/>, drops a ball through a <paramref name="rows"/>-row board, credits the
    /// bucket payout and raises the objective. The caller must have already validated affordability and that
    /// <paramref name="rows"/> is an allowed preset.
    /// </summary>
    public async Task<PlinkoDropReceipt> DropAsync(EFClient client, long bet, int rows, PlinkoRisk risk)
    {
        await persistence.RemoveCreditsAsync(client, bet);

        var drop = PlinkoBoard.Drop(rows);
        var multiplier = _calculator.MultiplierForBucket(rows, risk, drop.Bucket);
        var payout = (long)Math.Floor(bet * multiplier);

        long newBalance;
        if (payout > 0)
        {
            newBalance = await persistence.AddCreditsAsync(client, payout);
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, payout);
        }
        else
        {
            newBalance = await persistence.GetClientCreditsAsync(client);
        }

        return new PlinkoDropReceipt(drop, rows, risk, multiplier, bet, payout, payout - bet, newBalance);
    }
}

/// <summary>The settled outcome of a drop, with everything the page needs to animate and render it.</summary>
public sealed record PlinkoDropReceipt(
    PlinkoDrop Drop,
    int Rows,
    PlinkoRisk Risk,
    double Multiplier,
    long Bet,
    long Payout,
    long Profit,
    long NewBalance);
