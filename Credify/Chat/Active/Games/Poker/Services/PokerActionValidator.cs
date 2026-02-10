using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;

namespace Credify.Chat.Active.Games.Poker.Services;

/// <summary>
/// Service responsible for validating player actions based on game state.
/// </summary>
public class PokerActionValidator
{
    private readonly PokerBettingService _bettingService;

    public PokerActionValidator(PokerBettingService bettingService)
    {
        _bettingService = bettingService;
    }

    /// <summary>
    /// Validates if a player can perform a specific action.
    /// </summary>
    public (bool IsValid, string? ErrorMessage) ValidateAction(
        PokerPlayer player,
        PlayerAction action,
        long? raiseAmount,
        BettingRound round)
    {
        if (player.IsFolded)
        {
            return (false, "You have already folded");
        }

        if (player.IsAllIn)
        {
            return (false, "You are already all-in");
        }

        if (player.Chips <= 0)
        {
            return (false, "You have no chips left");
        }

        var amountToCall = round.CurrentBet - player.CurrentBet;

        return action switch
        {
            PlayerAction.Fold => (true, null),
            PlayerAction.Check => ValidateCheck(player, round, amountToCall),
            PlayerAction.Call => ValidateCall(player, round, amountToCall),
            PlayerAction.Raise => ValidateRaise(player, raiseAmount, round, amountToCall),
            PlayerAction.AllIn => (true, null),
            _ => (false, "Unknown action")
        };
    }

    /// <summary>
    /// Gets available actions for a player.
    /// </summary>
    public List<PlayerAction> GetAvailableActions(PokerPlayer player, BettingRound round)
    {
        var actions = new List<PlayerAction>();
        var amountToCall = round.CurrentBet - player.CurrentBet;

        // Fold is always available (unless all-in or folded)
        if (!player.IsFolded && !player.IsAllIn)
        {
            actions.Add(PlayerAction.Fold);
        }

        // Check is available if no bet to call
        if (amountToCall == 0 && player.Chips > 0)
        {
            actions.Add(PlayerAction.Check);
        }

        // Call is available if there's a bet to match
        if (amountToCall > 0 && player.Chips >= amountToCall)
        {
            actions.Add(PlayerAction.Call);
        }

        // Raise is available if player has chips beyond call amount
        var raiseIncrement = round.CurrentBet > 0 ? round.CurrentBet : 10;
        var minRaise = _bettingService.CalculateMinimumRaise(round.CurrentBet, raiseIncrement);
        var additionalNeeded = minRaise - player.CurrentBet;
        if (player.Chips > amountToCall && (player.Chips >= additionalNeeded || amountToCall == 0))
        {
            actions.Add(PlayerAction.Raise);
        }

        // All-in is always available if player has chips
        if (player.Chips > 0)
        {
            actions.Add(PlayerAction.AllIn);
        }

        return actions;
    }

    /// <summary>
    /// Gets the minimum and maximum raise amounts (total bet amount, not additional).
    /// </summary>
    public (long Min, long Max) GetRaiseRange(PokerPlayer player, BettingRound round)
    {
        var amountToCall = round.CurrentBet - player.CurrentBet;
        var raiseIncrement = round.CurrentBet > 0 ? round.CurrentBet : 10; // Default increment
        var minRaiseTotal = _bettingService.CalculateMinimumRaise(round.CurrentBet, raiseIncrement);
        var maxRaiseTotal = round.CurrentBet + player.Chips;

        return (minRaiseTotal, maxRaiseTotal);
    }

    private (bool IsValid, string? ErrorMessage) ValidateCheck(PokerPlayer player, BettingRound round, long amountToCall)
    {
        if (amountToCall > 0)
        {
            return (false, "Cannot check - must call or fold");
        }

        return (true, null);
    }

    private (bool IsValid, string? ErrorMessage) ValidateCall(PokerPlayer player, BettingRound round, long amountToCall)
    {
        if (amountToCall == 0)
        {
            return (false, "Nothing to call - you can check");
        }

        if (player.Chips < amountToCall)
        {
            return (false, "Insufficient chips to call");
        }

        return (true, null);
    }

    private (bool IsValid, string? ErrorMessage) ValidateRaise(PokerPlayer player, long? raiseAmount, BettingRound round, long amountToCall)
    {
        if (!raiseAmount.HasValue)
        {
            return (false, "Raise amount required");
        }

        var totalNeeded = raiseAmount.Value - player.CurrentBet;
        if (totalNeeded > player.Chips)
        {
            return (false, "Insufficient chips for this raise");
        }

        var raiseIncrement = round.CurrentBet > 0 ? round.CurrentBet : 10;
        var minRaise = _bettingService.CalculateMinimumRaise(round.CurrentBet, raiseIncrement);
        if (raiseAmount.Value < minRaise)
        {
            return (false, $"Raise must be at least ${minRaise:N0}");
        }

        if (raiseAmount.Value > round.CurrentBet + player.Chips)
        {
            return (false, "Raise exceeds available chips");
        }

        return (true, null);
    }
}
