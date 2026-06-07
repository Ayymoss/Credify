using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using GameConstants = Credify.Chat.Active.Core.GameConstants;

namespace Credify.Components.Shared;

/// <summary>
/// The standard Credify stake selector: cumulative chip denominations + Max/Clear + a free-typed amount,
/// shared by every single-bet game so the betting UI is identical everywhere. Two-way bind the amount
/// with <c>@bind-Value</c>. Caps are opt-in: leave <see cref="Max"/> null/0 for the common "no limit"
/// case (the player's balance is the only ceiling), or set it where the game enforces one (e.g. Slots'
/// MaxBet, Poker's MaximumBuyIn).
/// </summary>
public partial class BetControl
{
    [Parameter] public long Value { get; set; }
    [Parameter] public EventCallback<long> ValueChanged { get; set; }

    /// <summary>The player's available credits — the hard ceiling regardless of <see cref="Max"/>.</summary>
    [Parameter] public long Balance { get; set; }

    /// <summary>Minimum stake. Defaults to the in-game minimum.</summary>
    [Parameter] public long Min { get; set; } = GameConstants.MinimumCredits;

    /// <summary>Optional cap. Null or 0 means uncapped (balance is the only limit).</summary>
    [Parameter] public long? Max { get; set; }

    [Parameter] public bool Disabled { get; set; }

    /// <summary>Quick-add chip denominations.</summary>
    [Parameter] public long[] Denominations { get; set; } = [10, 50, 100, 500];

    /// <summary>Step for the typed-amount spinner.</summary>
    [Parameter] public long Step { get; set; } = 10;

    [Parameter] public string Label { get; set; } = "Bet";

    private long Cap => Max is > 0 ? Math.Min(Max.Value, Balance) : Balance;
    private bool CanRaise => !Disabled && Cap > Value;
    private bool CanClear => !Disabled && Value > Min;

    private Task AddAsync(long amount) => CommitAsync(Value + amount);
    private Task MaxAsync() => CommitAsync(Cap);
    private Task ClearAsync() => CommitAsync(Min);

    private Task EntryAsync(ChangeEventArgs args) =>
        long.TryParse(args.Value?.ToString(), out var parsed) ? CommitAsync(parsed) : CommitAsync(Value);

    private async Task CommitAsync(long desired)
    {
        var clamped = Clamp(desired);
        if (clamped != Value)
        {
            Value = clamped;
            await ValueChanged.InvokeAsync(clamped);
        }
        // if unchanged, the post-event re-render snaps a rejected typed value back to the clamped one
    }

    private long Clamp(long value)
    {
        var cap = Cap;
        // when the player can't afford even the minimum, pin to the (unaffordable) min — the game's own
        // action button stays disabled on its affordability check, so this never lets an invalid bet through.
        return cap < Min ? Min : Math.Clamp(value, Min, cap);
    }
}
