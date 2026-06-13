using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.Games.Slots;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// The single source of truth for a slots spin: stake debit, the pure <see cref="SlotMachine"/> draw,
/// payout credit, objective event, and the jackpot broadcast. Both the chat command and the web page
/// call <see cref="SpinAsync"/> so the odds, payouts and economy can never diverge between front-ends.
/// Stake validation stays with the caller (chat resolves "all"/limits and reports errors in chat).
/// </summary>
public class SlotsService(PersistenceService persistence, CredifyConfiguration config)
{
    public bool IsEnabled => config.Slots.IsEnabled;
    public long MinBet => config.Slots.MinBet;
    public long MaxBet => config.Slots.MaxBet;
    public bool GambleEnabled => config.Slots.GambleEnabled;
    public int GambleMaxSteps => config.Slots.GambleMaxSteps;

    // Server-tracked "amount on the table" for the gamble feature, keyed by client id. Set when a spin
    // wins, doubled/cleared by each gamble, removed on collect. The winnings are already credited to
    // the balance on the spin; gambling risks that amount on a fair 50/50, so this is purely the bound
    // on what the player is allowed to gamble next (never client-supplied).
    private readonly ConcurrentDictionary<int, (long Amount, int Steps)> _pendingGamble = new();

    /// <summary>
    /// Debits <paramref name="bet"/>, spins, credits any winnings, raises the objective, and (on a
    /// jackpot) broadcasts to the player's server if they are connected. The caller must have already
    /// validated that the player can afford the stake.
    /// </summary>
    public async Task<SlotSpinReceipt> SpinAsync(EFClient client, long bet)
    {
        await persistence.RemoveCreditsAsync(client, bet);

        var slots = config.Slots;
        var spin = SlotMachine.Spin(slots.Symbols, slots.TwoMatchMultiplier, slots.ThreeMatchMultiplier,
            slots.JackpotMultiplier);

        var winnings = (long)(bet * spin.Multiplier);

        long newBalance;
        if (winnings > 0)
        {
            newBalance = await persistence.AddCreditsAsync(client, winnings);
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, winnings);
            // these winnings become gamblable; a fresh spin always resets any prior gamble state
            _pendingGamble[client.ClientId] = (winnings, 0);
        }
        else
        {
            newBalance = await persistence.GetClientCreditsAsync(client);
            _pendingGamble.TryRemove(client.ClientId, out _);
        }

        if (spin.Outcome is SlotOutcome.Jackpot && client.CurrentServer is not null)
        {
            // web-only players (no server) simply skip the in-game announce
            try
            {
                var message = config.Translations.Slots.Jackpot.FormatExt(
                    PluginConstants.PluginName, client.CleanedName, winnings.ToString("N0"));
                // fire-and-forget, exactly as the chat command did (Broadcast returns a GameEvent)
                client.CurrentServer.Broadcast(message);
            }
            catch
            {
                // a failed broadcast must never void a settled spin
            }
        }

        return new SlotSpinReceipt(spin, bet, winnings, winnings - bet, newBalance);
    }

    /// <summary>
    /// Gamble the current "amount on the table" on a fair 50/50: pick red or black. A correct call
    /// doubles it, a wrong one loses it. Exactly even money, so this adds no house edge — the game
    /// stays 1:1. The staked amount is server-tracked (never trusted from the caller). Returns null
    /// when there is nothing to gamble or the feature is disabled.
    /// </summary>
    public async Task<SlotGambleReceipt?> GambleAsync(EFClient client, bool pickRed)
    {
        if (!config.Slots.GambleEnabled) return null;
        if (!_pendingGamble.TryGetValue(client.ClientId, out var pending) || pending.Amount <= 0) return null;

        var staked = pending.Amount; // already in the player's balance (the winnings on the table)
        var landedRed = Random.Shared.Next(2) == 0; // even money
        var won = landedRed == pickRed;
        var steps = pending.Steps + 1;

        // net settle: a win adds another `staked` (table now holds 2x); a loss removes it (table now 0).
        // 0.5*(+staked) + 0.5*(-staked) = 0 → no house edge.
        long newBalance, amountNow;
        if (won)
        {
            amountNow = staked * 2;
            newBalance = await persistence.AddCreditsAsync(client, staked);
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, client, staked);
        }
        else
        {
            amountNow = 0;
            newBalance = await persistence.RemoveCreditsAsync(client, staked);
        }

        var canContinue = won && steps < config.Slots.GambleMaxSteps;
        if (canContinue) _pendingGamble[client.ClientId] = (amountNow, steps);
        else _pendingGamble.TryRemove(client.ClientId, out _);

        return new SlotGambleReceipt(won, landedRed, staked, amountNow, steps, canContinue, newBalance);
    }

    /// <summary>Stop gambling and keep the winnings (already in the balance). Clears the table.</summary>
    public void CollectGamble(EFClient client) => _pendingGamble.TryRemove(client.ClientId, out _);
}

/// <summary>The settled outcome of a spin, with everything both front-ends need to render it.</summary>
public sealed record SlotSpinReceipt(SlotSpin Spin, long Bet, long Winnings, long Profit, long NewBalance);

/// <summary>The settled outcome of one gamble (double-or-nothing) flip.</summary>
public sealed record SlotGambleReceipt(
    bool Won, bool LandedRed, long Staked, long AmountNow, int Steps, bool CanContinue, long NewBalance);
