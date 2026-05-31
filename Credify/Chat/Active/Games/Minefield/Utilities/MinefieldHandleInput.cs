using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Minefield.Enums;
using Credify.Configuration.Translations;

namespace Credify.Chat.Active.Games.Minefield.Utilities;

/// <summary>
/// Minefield action result from parsing player input during the Digging phase.
/// </summary>
public record MinefieldActionResult(MinefieldAction Action);

/// <summary>
/// Parses chat input into Minefield digging actions using Core abstractions.
/// </summary>
public class MinefieldHandleInput(MinefieldTranslations translations)
    : IGameInputParser<MinefieldActionResult>
{
    private static readonly ActionShortcutMap<MinefieldAction> ActionShortcuts = new ActionShortcutMap<MinefieldAction>()
        .Add(MinefieldAction.Dig, "d", "dig", "mine", "m")
        .Add(MinefieldAction.Cash, "c", "cash", "collect", "stop")
        .Add(MinefieldAction.Status, "s", "status", "field", "grid");

    public ParseResult<MinefieldActionResult> Parse(string message)
    {
        if (ActionShortcuts.TryGetAction(message.Trim(), out var action))
        {
            return ParseResult<MinefieldActionResult>.Success(new MinefieldActionResult(action!));
        }

        return ParseResult<MinefieldActionResult>.Error(translations.DigPrompt);
    }
}
