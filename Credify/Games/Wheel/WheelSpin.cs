namespace Credify.Games.Wheel;

/// <summary>The pure result of a wheel spin: the landed slice and its index on the wheel.</summary>
public sealed record WheelSpin(int Index, WheelSegment Segment);
