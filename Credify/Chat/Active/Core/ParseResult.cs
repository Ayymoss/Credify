namespace Credify.Chat.Active.Core;

/// <summary>
/// Standard result type for input parsing operations.
/// Provides consistent validation feedback across all games.
/// </summary>
/// <typeparam name="T">The parsed result type</typeparam>
public record ParseResult<T>
{
    public bool IsValid { get; init; }
    public T? Result { get; init; }
    public string? ErrorMessage { get; init; }

    public static ParseResult<T> Success(T result) => new()
    {
        IsValid = true,
        Result = result,
        ErrorMessage = null
    };

    public static ParseResult<T> Error(string message) => new()
    {
        IsValid = false,
        Result = default,
        ErrorMessage = message
    };
}
