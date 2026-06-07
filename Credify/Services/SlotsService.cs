using System;
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
        }
        else
        {
            newBalance = await persistence.GetClientCreditsAsync(client);
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
}

/// <summary>The settled outcome of a spin, with everything both front-ends need to render it.</summary>
public sealed record SlotSpinReceipt(SlotSpin Spin, long Bet, long Winnings, long Profit, long NewBalance);
