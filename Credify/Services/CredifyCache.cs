using Credify.Models;

namespace Credify.Services;

public class CredifyCache
{
    public List<TopCreditEntry> TopCredits { get; set; } = [];
    public StatisticsState StatisticsState { get; set; } = new();

    private long _bankCredits;
    public long BankCredits
    {
        get => Interlocked.Read(ref _bankCredits);
        set => Interlocked.Exchange(ref _bankCredits, value);
    }

    public void AddBankCredits(long amount) => Interlocked.Add(ref _bankCredits, amount);
}
