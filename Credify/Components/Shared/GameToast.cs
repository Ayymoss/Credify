namespace Credify.Components.Shared;

/// <summary>Visual style of a <see cref="GameToast"/> — drives its colour and glow.</summary>
public enum GameToastVariant
{
    Win,
    Lose,
    Jackpot
}

/// <summary>
/// An immutable win/loss notification model shared by every single-player game (Slots, Minefield, Crash,
/// Plinko). It's rendered by <c>GameResultToast</c> as a fixed-position toast so a result never reflows the
/// page. <see cref="Sequence"/> must increment on every new result: the component keys its element on it, so a
/// repeat result with the same text still re-triggers the pop-in/auto-dismiss animation.
/// </summary>
public sealed record GameToast(
    int Sequence,
    GameToastVariant Variant,
    string Title,
    string? Amount,
    string Icon);
