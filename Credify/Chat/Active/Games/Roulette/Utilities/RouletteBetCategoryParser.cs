using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Configuration.Translations;

namespace Credify.Chat.Active.Games.Roulette.Utilities;

/// <summary>
/// Parser for roulette bet categories (inside/outside).
/// </summary>
public class RouletteBetCategoryParser(RouletteTranslations translations) 
    : IGameInputParser<BetCategory?>
{
    private static readonly ActionShortcutMap<BetCategory> BetCategoryShortcuts = new ActionShortcutMap<BetCategory>()
        .Add(BetCategory.Outside, "o", "out", "outside")
        .Add(BetCategory.Inside, "i", "in", "inside");

    public ParseResult<BetCategory?> Parse(string message)
    {
        if (BetCategoryShortcuts.TryGetAction(message, out var category))
        {
            return ParseResult<BetCategory?>.Success(category);
        }

        return ParseResult<BetCategory?>.Error(
            translations.Prefix(translations.InvalidBetCategory));
    }
}
