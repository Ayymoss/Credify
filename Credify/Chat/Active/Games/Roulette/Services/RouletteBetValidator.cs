using Credify.Chat.Active.Games.Roulette.Enums;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes.Inside;
using Credify.Chat.Active.Games.Roulette.Models.BetTypes.Outside;

namespace Credify.Chat.Active.Games.Roulette.Services;

/// <summary>
/// Service responsible for validating roulette bets.
/// </summary>
public class RouletteBetValidator
{
    /// <summary>
    /// Validates and creates an inside bet from input string.
    /// </summary>
    public static (bool IsValid, InsideBaseBet? Bet, string? ErrorMessage) ValidateInsideBet(string input, int betValue)
    {
        var argsSplit = input.Split(' ');
        if (argsSplit.Length is < 1 or > 6) 
            return (false, null, "Invalid number of arguments for inside bet");

        if (!TryParseNumbers(argsSplit, out var numbers, out var parseError))
            return (false, null, parseError);

        if (numbers.Any(n => n is < 0 or > 36) || numbers.Distinct().Count() != numbers.Count)
            return (false, null, "Invalid range or duplicate numbers");

        InsideBaseBet? bet = numbers.Count switch
        {
            1 => new StraightUpBet(betValue, numbers[0]),
            2 => IsValidSplit(numbers) ? new SplitBet(betValue, numbers[0], numbers[1]) : null,
            3 => IsValidStreet(numbers) ? new StreetBet(betValue, numbers[0], numbers[1], numbers[2]) : null,
            4 => IsValidCorner(numbers) ? new CornerBet(betValue, numbers[0], numbers[1], numbers[2], numbers[3]) : null,
            6 => IsValidSixLine(numbers)
                ? new SixLineBet(betValue, numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5])
                : null,
            _ => null
        };

        return bet is not null 
            ? (true, bet, null) 
            : (false, null, "Invalid bet type for given numbers");
    }

    /// <summary>
    /// Validates and creates an outside bet from input string.
    /// </summary>
    public static (bool IsValid, OutsideBaseBet? Bet, string? ErrorMessage) ValidateOutsideBet(string input, int betValue)
    {
        var argsSplit = input.Split(' ');
        if (argsSplit.Length is not 1) 
            return (false, null, "Invalid number of arguments for outside bet");

        var rawBetType = argsSplit.First().ToLower();

        OutsideBaseBet? bet = rawBetType switch
        {
            "r" or "red" => new RedBlackBet(betValue, Colour.Red),
            "b" or "black" => new RedBlackBet(betValue, Colour.Black),
            "o" or "odd" => new OddEvenBet(betValue, false),
            "e" or "even" => new OddEvenBet(betValue, true),
            "l" or "low" => new LowHighBet(betValue, LowHigh.Low),
            "h" or "high" => new LowHighBet(betValue, LowHigh.High),
            "d1" or "dozen1" => new DozenBet(betValue, Dozen.First),
            "d2" or "dozen2" => new DozenBet(betValue, Dozen.Second),
            "d3" or "dozen3" => new DozenBet(betValue, Dozen.Third),
            "c1" or "column1" => new ColumnBet(betValue, Column.First),
            "c2" or "column2" => new ColumnBet(betValue, Column.Second),
            "c3" or "column3" => new ColumnBet(betValue, Column.Third),
            _ => null
        };

        return bet is not null 
            ? (true, bet, null) 
            : (false, null, "Invalid outside bet type");
    }

    private static bool TryParseNumbers(string[] args, out List<int> numbers, out string? errorMessage)
    {
        numbers = new List<int>();
        errorMessage = null;

        foreach (var arg in args)
        {
            if (!int.TryParse(arg, out var number))
            {
                errorMessage = "Invalid number format";
                return false;
            }
            numbers.Add(number);
        }

        return true;
    }

    private static bool IsValidSplit(List<int> numbers)
    {
        if (numbers.Count is not 2) return false;

        numbers.Sort();

        var diff = numbers[1] - numbers[0];

        switch (diff)
        {
            // Horizontal adjacency (same row)
            case 1 when numbers[0] % 3 != 0:
            // Vertical adjacency (same column, except for 0/00)
            case 3 when numbers[0] != 0 && numbers[1] != 36:
                return true;
            default:
                return false;
        }
    }

    private static bool IsValidStreet(List<int> numbers)
    {
        if (numbers.Count is not 3) return false;

        numbers.Sort();

        // Check for consecutive numbers on the same row (difference of 1 between each)
        return numbers[1] - numbers[0] == 1 &&
               numbers[2] - numbers[1] == 1 &&
               numbers[0] % 3 == 1; // Starts at the beginning of a row (1, 4, 7, ...)
    }

    private static bool IsValidCorner(List<int> numbers)
    {
        if (numbers.Count is not 4) return false;

        numbers.Sort();

        // Check if numbers form a 2x2 square on the roulette layout
        return numbers[1] - numbers[0] == 1 && // First row horizontal adjacency
               numbers[3] - numbers[2] == 1 && // Second row horizontal adjacency
               numbers[2] - numbers[0] == 3 && // First column vertical adjacency
               numbers[3] - numbers[1] == 3; // Second column vertical adjacency
    }

    private static bool IsValidSixLine(List<int> numbers)
    {
        if (numbers.Count is not 6) return false;

        numbers.Sort();

        // Check if numbers form two consecutive Streets on the same two rows
        return numbers[1] - numbers[0] == 1 &&
               numbers[2] - numbers[1] == 1 &&
               numbers[3] - numbers[2] == 1 &&
               numbers[4] - numbers[3] == 1 &&
               numbers[5] - numbers[4] == 1 &&
               numbers[0] % 3 == 1 && // Starts at the beginning of a row (1, 4, 7, ...)
               numbers[3] - numbers[0] == 3; // Vertical adjacency between rows
    }
}
