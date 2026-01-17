using System.Text.RegularExpressions;
using Credify.Chat.Active.Core;
using Credify.Chat.Active.Core.Interfaces;
using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Chat.Active.Games.Poker.Services;
using Credify.Configuration.Translations;
using SharedLibraryCore;

namespace Credify.Chat.Active.Games.Poker.Utilities;

/// <summary>
/// Poker action result including optional raise amount.
/// </summary>
public record PokerActionResult(PlayerAction Action, long? RaiseAmount = null);

/// <summary>
/// Service responsible for parsing and validating poker actions from chat messages.
/// </summary>
public class PokerHandleInput(PokerActionValidator validator, PokerTranslations translations) 
    : IGameInputParser<PokerActionResult>
{
    // 'c' maps to BOTH Check and Call - validator determines which is valid
    private static readonly ActionShortcutMap<PlayerAction> ActionShortcuts = new ActionShortcutMap<PlayerAction>()
        .Add(PlayerAction.Fold, "f", "fold")
        .Add(PlayerAction.Check, "c", "check")
        .Add(PlayerAction.Call, "c", "k", "call")  // 'c' now works for both Check and Call
        .Add(PlayerAction.Raise, "r", "raise")
        .Add(PlayerAction.AllIn, "a", "all", "allin", "all-in")
        .Add(PlayerAction.TopUp, "topup", "rebuy", "top-up", "top");  // New top-up command

    /// <summary>
    /// Parses a chat message into a poker action.
    /// </summary>
    public ParseResult<PokerActionResult> Parse(string message)
    {
        // ActionShortcutMap handles case-insensitive matching internally
        if (ActionShortcuts.TryGetAction(message, out var action))
        {
            // Raise requires an amount
            if (action == PlayerAction.Raise)
            {
                return ParseResult<PokerActionResult>.Error(
                    $"{translations.InvalidAction} - Raise requires amount (r X or raise X)");
            }

            return ParseResult<PokerActionResult>.Success(new PokerActionResult(action));
        }

        // Check for raise with amount (r X or raise X) - case insensitive
        var raiseMatch = Regex.Match(message.Trim(), @"^(r|raise)\s+(\d+)$", RegexOptions.IgnoreCase);
        if (raiseMatch.Success && long.TryParse(raiseMatch.Groups[2].Value, out var raiseAmount))
        {
            return ParseResult<PokerActionResult>.Success(
                new PokerActionResult(PlayerAction.Raise, raiseAmount));
        }

        // Try parsing as just a number (assume raise)
        if (long.TryParse(message.Trim(), out var amount))
        {
            return ParseResult<PokerActionResult>.Success(
                new PokerActionResult(PlayerAction.Raise, amount));
        }

        return ParseResult<PokerActionResult>.Error(translations.InvalidAction);
    }

    /// <summary>
    /// Formats available actions for display to a player.
    /// </summary>
    /// <remarks>Convenience method for prompts; not part of IGameInputParser contract.</remarks>
    public string FormatAvailableActions(PokerPlayer player, BettingRound round)
    {
        var actions = validator.GetAvailableActions(player, round);
        var actionStrings = new List<string>();
        var amountToCall = round.CurrentBet - player.CurrentBet;

        foreach (var action in actions)
        {
            var actionString = action switch
            {
                PlayerAction.Fold => translations.Fold,
                PlayerAction.Check => translations.Check,
                PlayerAction.Call => translations.Call.FormatExt(amountToCall.ToString("N0")),
                PlayerAction.Raise => GetRaiseString(),
                PlayerAction.AllIn => translations.AllIn.FormatExt(player.Chips.ToString("N0")),
                _ => ""
            };
            
            if (!string.IsNullOrEmpty(actionString))
            {
                actionStrings.Add(actionString);
            }
        }

        string GetRaiseString()
        {
            var (min, max) = validator.GetRaiseRange(player, round);
            return translations.Raise.FormatExt(min.ToString("N0"), max.ToString("N0"));
        }

        return string.Join(", ", actionStrings.Where(s => !string.IsNullOrEmpty(s)));
    }
}

