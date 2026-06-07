using System;
using System.Collections.Generic;
using System.Linq;

namespace Credify.Games.Wheel;

/// <summary>
/// Pure money-wheel rules: a weighted draw over the wheel's slices. No I/O — the web page spins through
/// here so the odds and the visual wheel stay in lock-step. Slices are sized by weight, so the big
/// multipliers are the thin slivers.
/// </summary>
public static class WheelMachine
{
    /// <summary>
    /// Default ~0.97 RTP wheel: every spin pays a multiplier, mostly small (you usually get part of your
    /// stake back), with a rare 50× sliver. Tune the weights to change the house edge.
    /// </summary>
    public static readonly IReadOnlyList<WheelSegment> DefaultWheel =
    [
        new(0.2, 150, "#52525b"),
        new(0.5, 120, "#6366f1"),
        new(1.0, 70, "#0ea5e9"),
        new(1.5, 40, "#22c55e"),
        new(2.0, 25, "#eab308"),
        new(3.0, 12, "#f97316"),
        new(5.0, 6, "#ef4444"),
        new(10.0, 3, "#ec4899"),
        new(50.0, 1, "#fcd34d"),
    ];

    /// <param name="next">Returns an int in [0, maxExclusive). Defaults to <see cref="Random.Shared"/>.</param>
    public static WheelSpin Spin(IReadOnlyList<WheelSegment> wheel, Func<int, int>? next = null)
    {
        if (wheel is null || wheel.Count == 0)
        {
            throw new ArgumentException("The wheel needs at least one slice.", nameof(wheel));
        }

        next ??= Random.Shared.Next;
        var totalWeight = wheel.Sum(slice => slice.Weight);
        var roll = next(totalWeight);

        var cumulative = 0;
        for (var i = 0; i < wheel.Count; i++)
        {
            cumulative += wheel[i].Weight;
            if (roll < cumulative)
            {
                return new WheelSpin(i, wheel[i]);
            }
        }

        return new WheelSpin(wheel.Count - 1, wheel[^1]);
    }
}
