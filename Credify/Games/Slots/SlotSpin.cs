using System.Collections.Generic;

namespace Credify.Games.Slots;

/// <summary>
/// The pure result of a spin: the three reel symbols, the classified <see cref="SlotOutcome"/>,
/// and the gross payout multiplier (winnings = stake * multiplier; 0 for a loss).
/// </summary>
public sealed record SlotSpin(IReadOnlyList<SlotSymbol> Reels, SlotOutcome Outcome, double Multiplier);
