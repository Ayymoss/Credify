namespace Credify.Games.Wheel;

/// <summary>A slice of the money wheel: its payout multiplier, relative draw weight, and display colour.</summary>
public sealed record WheelSegment(double Multiplier, int Weight, string Color);
