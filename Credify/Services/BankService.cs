using Credify.Constants;
using Credify.Services;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

/// <summary>
/// Service responsible for managing bank credits.
/// </summary>
public class BankService(
    IMetaServiceV2 metaService,
    CredifyCache cache)
{
    /// <summary>
    /// Resets the bank credits to zero.
    /// </summary>
    public void ResetBank() => cache.BankCredits = 0;

    /// <summary>
    /// Adds credits to the bank.
    /// </summary>
    public void AddBankCredits(long credits) => cache.AddBankCredits(credits);

    /// <summary>
    /// Writes bank credits to persistent storage.
    /// </summary>
    public async Task WriteBankCreditsAsync()
    {
        await metaService.SetPersistentMeta(PluginConstants.BankCreditsKey, cache.BankCredits.ToString());
    }

    /// <summary>
    /// Reads bank credits from persistent storage.
    /// </summary>
    public async Task ReadBankCreditsAsync()
    {
        var bankCredits = (await metaService.GetPersistentMeta(PluginConstants.BankCreditsKey))?.Value;
        var credits = bankCredits is null
            ? 0
            : long.Parse(bankCredits);
        cache.BankCredits = credits;
    }
}
