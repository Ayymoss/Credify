using Credify.Games.Cards;

namespace Credify.Chat.Active.Games.Blackjack;

/// <summary>
/// In-game-chat rendering for a <see cref="Card"/>. The COD console charset can't render suit glyphs, so
/// chat uses single-letter suits (e.g. "K-H", "A-S", "10-D"). This is the chat frontend's I/O concern —
/// the webfront renders the same shared <see cref="Card"/> with its own glyph-based component.
/// </summary>
public static class BlackjackChatFormat
{
    public static string ToChatString(this Card card) => $"{card.RankLabel}-{card.SuitLetter}";
}
