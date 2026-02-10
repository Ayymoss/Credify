using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Blackjack.Enums;
using Credify.Chat.Active.Games.Blackjack.Models;
using Credify.Configuration.Translations;

namespace Credify.Chat.Active.Games.Blackjack.Utilities;

/// <summary>
/// Blackjack action result from parsing player input.
/// </summary>
public record BlackjackActionResult(PlayerAction Action);

/// <summary>
/// Parses and validates blackjack player input using Core abstractions.
/// </summary>
public class BlackjackHandleInput(BlackjackTranslations translations)
    : IGameInputParser<BlackjackActionResult>
{
    private static readonly ActionShortcutMap<PlayerAction> ActionShortcuts = new ActionShortcutMap<PlayerAction>()
        .Add(PlayerAction.Hit, "h", "hit")
        .Add(PlayerAction.Stand, "s", "stand")
        .Add(PlayerAction.Cards, "c", "cards")
        .Add(PlayerAction.Double, "d", "double")
        .Add(PlayerAction.Split, "sp", "split")
        .Add(PlayerAction.Insurance, "i", "ins", "insurance");

    /// <summary>
    /// Parses a chat message into a blackjack action.
    /// </summary>
    public ParseResult<BlackjackActionResult> Parse(string message)
    {
        if (ActionShortcuts.TryGetAction(message.Trim(), out var action))
        {
            return ParseResult<BlackjackActionResult>.Success(new BlackjackActionResult(action!));
        }

        return ParseResult<BlackjackActionResult>.Error(translations.PlayerDecision);
    }

    /// <summary>
    /// Formats available actions for display to a player.
    /// </summary>
    public string FormatAvailableActions() => translations.PlayerDecisionBasic;

    /// <summary>
    /// Formats available actions based on player's current state and options.
    /// </summary>
    /// <remarks>Convenience method for prompts; not part of IGameInputParser contract.</remarks>
    public string FormatAvailableActions(BlackjackPlayer player, bool dealerShowsAce)
    {
        var options = new List<string>
        {
            "[H]it",
            "[S]tand",
            "[C]ards"
        };

        // Only add special actions when they're actually available
        if (player.CanDouble())
        {
            options.Add("[D]ouble");
        }

        if (player.CanSplit())
        {
            options.Add("[SP]lit");
        }

        // Insurance only valid during the offering phase with dealer showing Ace
        // and player hasn't already taken it
        if (dealerShowsAce && !player.HasInsurance && player.Cards.Count == 2 && !player.HasSplit)
        {
            options.Add("[I]nsurance");
        }

        return "(Color::Accent)" + string.Join(" ", options);
    }
}
