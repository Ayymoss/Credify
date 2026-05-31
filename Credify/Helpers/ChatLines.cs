using System.Text;
using System.Text.RegularExpressions;

namespace Credify.Helpers;

/// <summary>
/// Helpers for packing many short items into a small number of chat lines without
/// wrapping. CoD chat allows ~5 lines per burst and wraps long lines awkwardly, so we
/// pack tokens up to a visible-width budget and split onto new lines rather than wrap.
/// </summary>
public static class ChatLines
{
    // IW4MAdmin colour codes like "(Color::Accent)" are not visible width.
    private static readonly Regex ColourCode = new(@"\(Color::[^)]*\)", RegexOptions.Compiled);

    /// <summary>Length of a string as actually rendered in chat (colour codes stripped).</summary>
    public static int VisibleLength(string text) => ColourCode.Replace(text, "").Length;

    /// <summary>
    /// Packs <paramref name="tokens"/> into lines no wider than <paramref name="maxVisibleWidth"/>,
    /// joined by <paramref name="separator"/>. The first line is prefixed with
    /// <paramref name="firstLinePrefix"/>; continuation lines carry tokens only.
    /// </summary>
    public static List<string> Pack(string firstLinePrefix, IReadOnlyList<string> tokens,
        string separator, int maxVisibleWidth)
    {
        var lines = new List<string>();
        StringBuilder? current = null;
        var currentWidth = 0;
        var separatorWidth = VisibleLength(separator);

        foreach (var token in tokens)
        {
            var tokenWidth = VisibleLength(token);

            if (current is null)
            {
                var prefix = lines.Count == 0 ? firstLinePrefix : string.Empty;
                current = new StringBuilder(prefix.Length > 0 ? $"{prefix} {token}" : token);
                currentWidth = VisibleLength(prefix.Length > 0 ? $"{prefix} " : string.Empty) + tokenWidth;
                continue;
            }

            if (currentWidth + separatorWidth + tokenWidth > maxVisibleWidth)
            {
                lines.Add(current.ToString());
                current = new StringBuilder(token); // continuation line, no prefix
                currentWidth = tokenWidth;
            }
            else
            {
                current.Append(separator).Append(token);
                currentWidth += separatorWidth + tokenWidth;
            }
        }

        if (current is not null) lines.Add(current.ToString());
        return lines;
    }
}
