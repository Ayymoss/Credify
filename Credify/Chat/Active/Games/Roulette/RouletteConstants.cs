using Credify.Chat.Active.Games.Roulette.Enums;

namespace Credify.Chat.Active.Games.Roulette;

/// <summary>
/// Constants for American Roulette.
/// </summary>
public static class RouletteConstants
{
    /// <summary>
    /// Internal representation of "00" as integer 37.
    /// </summary>
    public const int DoubleZero = 37;
    
    /// <summary>
    /// Maximum valid number (0-36 + 00).
    /// </summary>
    public const int MaxNumber = 37;
    
    /// <summary>
    /// Total number of outcomes on American wheel (0, 00, 1-36).
    /// </summary>
    public const int TotalOutcomes = 38;
    
    /// <summary>
    /// Red numbers on American Roulette wheel.
    /// </summary>
    private static readonly HashSet<int> RedNumbers = 
        [1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36];
    
    /// <summary>
    /// Converts a number to its display string ("00" for 37).
    /// </summary>
    public static string ToDisplayString(int number) =>
        number == DoubleZero ? "00" : number.ToString();
    
    /// <summary>
    /// Checks if a number is a zero (0 or 00).
    /// </summary>
    public static bool IsZero(int number) =>
        number == 0 || number == DoubleZero;
    
    /// <summary>
    /// Gets the color for a given number according to American Roulette rules.
    /// 0 and 00 are green, red numbers are defined by lookup, all others are black.
    /// </summary>
    public static Colour GetColor(int number)
    {
        if (IsZero(number)) return Colour.Green;
        return RedNumbers.Contains(number) ? Colour.Red : Colour.Black;
    }
    
    /// <summary>
    /// Checks if a number is red.
    /// </summary>
    public static bool IsRed(int number) => RedNumbers.Contains(number);
    
    /// <summary>
    /// Checks if a number is black (not zero and not red).
    /// </summary>
    public static bool IsBlack(int number) => !IsZero(number) && !RedNumbers.Contains(number);
}
