namespace Credify.Models;

public record StatisticsState
{
    private ulong _creditsSpent;
    private ulong _creditsEarned;
    private ulong _creditsWon;

    public ulong CreditsSpent
    {
        get => Interlocked.Read(ref _creditsSpent);
        private set => Interlocked.Exchange(ref _creditsSpent, value);
    }

    public ulong CreditsEarned
    {
        get => Interlocked.Read(ref _creditsEarned);
        private set => Interlocked.Exchange(ref _creditsEarned, value);
    }

    public ulong CreditsWon
    {
        get => Interlocked.Read(ref _creditsWon);
        private set => Interlocked.Exchange(ref _creditsWon, value);
    }

    public void AddCreditsSpent(ulong amount) => Interlocked.Add(ref _creditsSpent, amount);
    public void AddCreditsWon(ulong amount) => Interlocked.Add(ref _creditsWon, amount);
    public void IncrementCreditsEarned() => Interlocked.Increment(ref _creditsEarned);

    public void SetReadCredits(StatisticsStateStore store)
    {
        CreditsSpent = store.Spent;
        CreditsEarned = store.Earned;
        CreditsWon = store.Won;
    }

    public StatisticsStateStore GetWriteCredits() => new(CreditsSpent, CreditsEarned, CreditsWon);
}

public record StatisticsStateStore(ulong Spent, ulong Earned, ulong Won);
