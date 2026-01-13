using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Chat.Active.Games.Poker.Services;
using Credify.Configuration.Translations;
using SharedLibraryCore;

namespace Credify.Chat.Active.Games.Poker.Utilities;

/// <summary>
/// Service responsible for parsing and validating poker actions from chat messages.
/// </summary>
public class PokerHandleInput(PokerActionValidator validator, PokerTranslations translations)
{
    /// <summary>
    /// Parses a chat message into a poker action.
    /// </summary>
    public (bool IsValid, PlayerAction? Action, long? RaiseAmount, string? ErrorMessage) ParseAction(
        string message)
    {
        var input = message.Trim().ToLower();
        
        // Action shortcuts mapping
        var actionMap = new Dictionary<string, PlayerAction>
        {
            { "f", PlayerAction.Fold },
            { "fold", PlayerAction.Fold },
            { "c", PlayerAction.Check },
            { "check", PlayerAction.Check },
            { "k", PlayerAction.Call },
            { "call", PlayerAction.Call },
            { "r", PlayerAction.Raise },
            { "raise", PlayerAction.Raise },
            { "a", PlayerAction.AllIn },
            { "all", PlayerAction.AllIn },
            { "allin", PlayerAction.AllIn },
            { "all-in", PlayerAction.AllIn }
        };

        // Check for exact matches first
        if (actionMap.TryGetValue(input, out var action))
        {
            // For raise, check if there's an amount
            if (action == PlayerAction.Raise)
            {
                return (false, null, null, translations.InvalidAction + " - Raise requires amount (r X or raise X)");
            }

            return (true, action, null, null);
        }

        // Check for raise with amount (r X or raise X)
        var raiseMatch = System.Text.RegularExpressions.Regex.Match(input, @"^(r|raise)\s+(\d+)$");
        if (raiseMatch.Success)
        {
            if (long.TryParse(raiseMatch.Groups[2].Value, out var raiseAmount))
            {
                return (true, PlayerAction.Raise, raiseAmount, null);
            }
        }

        // Try parsing as just a number (assume raise)
        if (long.TryParse(input, out var amount))
        {
            return (true, PlayerAction.Raise, amount, null);
        }

        return (false, null, null, translations.InvalidAction);
    }

    /// <summary>
    /// Formats available actions for display to a player.
    /// </summary>
    public string FormatAvailableActions(PokerPlayer player, BettingRound round, PokerBettingService bettingService)
    {
        var actions = validator.GetAvailableActions(player, round);
        var actionStrings = new List<string>();
        var amountToCall = round.CurrentBet - player.CurrentBet;

        foreach (var action in actions)
        {
            string actionString = action switch
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
