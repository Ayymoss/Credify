using Credify.Services;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// Service for validating credit-related operations.
/// </summary>
public class CreditsValidator(PersistenceService persistenceService)
{
    /// <summary>
    /// Validates that a client has at least the minimum required credits.
    /// </summary>
    /// <returns>True if the client has sufficient credits, false otherwise</returns>
    public async Task<bool> ValidateMinimumCreditsAsync(EFClient client, long minimum)
    {
        var credits = await persistenceService.GetClientCreditsAsync(client);
        return credits >= minimum;
    }

    /// <summary>
    /// Validates that a client has available funds for a transaction.
    /// </summary>
    /// <returns>True if the client has sufficient funds, false otherwise</returns>
    public static bool ValidateAvailableFunds(EFClient client, long amount)
    {
        return CreditsService.AvailableFunds(client, amount);
    }

    /// <summary>
    /// Validates and returns a result indicating if the client has sufficient credits.
    /// </summary>
    public async Task<ValidationResult> ValidateCreditsAsync(EFClient client, long requiredAmount)
    {
        var credits = await persistenceService.GetClientCreditsAsync(client);
        if (credits < requiredAmount)
        {
            return ValidationResult.Insufficient(credits, requiredAmount);
        }

        return ValidationResult.Success;
    }

    /// <summary>
    /// Result of credit validation.
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; init; }
        public long CurrentCredits { get; init; }
        public long RequiredCredits { get; init; }

        public static ValidationResult Success => new() { IsValid = true };
        public static ValidationResult Insufficient(long current, long required) => new()
        {
            IsValid = false,
            CurrentCredits = current,
            RequiredCredits = required
        };
    }
}
