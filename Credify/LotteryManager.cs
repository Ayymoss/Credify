using System.Security.Cryptography;
using Credify.Models;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify;

public class LotteryManager
{
    public List<Lottery> Lottery { get; set; } = null!;
    public DateTimeOffset NextOccurrence { get; set; }
    private IManager? _manager;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly PersistenceManager _persistenceManager;

    public LotteryManager(CredifyConfiguration credifyConfig, PersistenceManager persistenceManager)
    {
        _credifyConfig = credifyConfig;
        _persistenceManager = persistenceManager;
    }

    public void SetManager(IManager manager) => _manager = manager;
    public bool HasLotteryHappened => DateTimeOffset.UtcNow >= NextOccurrence;
    public async Task ReadLotteryAsync() => Lottery = await _persistenceManager.ReadLotteryAsync();
    private async Task WriteLotteryAsync() => await _persistenceManager.WriteLotteryAsync(Lottery);

    public async Task CalculateNextOccurrence()
    {
        if (NextOccurrence < DateTimeOffset.UtcNow)
        {
            var next = await _persistenceManager.ReadNextLotteryAsync();
            if (next is null || next < DateTimeOffset.UtcNow)
            {
                var nextLotteryDate = DateTimeOffset.UtcNow.Date.Add(_credifyConfig.Core.LotteryFrequency);
                NextOccurrence = nextLotteryDate.Add(_credifyConfig.Core.LotteryFrequencyAtTime);
                await _persistenceManager.WriteNextLotteryAsync(NextOccurrence);
            }
            else
            {
                NextOccurrence = next.Value;
            }
        }
    }

    public async Task<long> AddToLottery(EFClient client, long tickets)
    {
        long boughtTotal;
        if (Lottery.FirstOrDefault(x => x.ClientId == client.ClientId) is { } holder)
        {
            holder.Tickets += tickets;
            boughtTotal = holder.Tickets;
        }
        else
        {
            Lottery.Add(new Lottery(client.ClientId) {CleanedName = client.CleanedName, Tickets = tickets});
            boughtTotal = tickets;
        }

        await WriteLotteryAsync();
        return boughtTotal;
    }

    private static long NextLong(long minValue, long maxValue)
    {
        if (minValue > maxValue) throw new ArgumentOutOfRangeException(nameof(minValue));

        var buffer = new byte[8];
        Random.Shared.NextBytes(buffer);
        var longValue = BitConverter.ToInt64(buffer, 0);
        return Math.Abs(longValue % (maxValue - minValue)) + minValue;
    }

    public async Task PickWinner()
    {
        if (Lottery.Count == 0) return;

        var totalTickets = Lottery.Sum(x => x.Tickets);
        var winningTicket = NextLong(1, totalTickets + 1);
        int? clientId = null;
        string? clientName = null;
        var winnerTickets = 0L;

        foreach (var holder in Lottery)
        {
            if (winningTicket <= holder.Tickets)
            {
                clientId = holder.ClientId;
                clientName = holder.CleanedName;
                winnerTickets = holder.Tickets;
                break;
            }

            winningTicket -= holder.Tickets;
        }

        if (clientId is null || clientName is null) return;

        var winPercentage = (float)winnerTickets / totalTickets * 100;
        AnnounceWinner(clientName, winPercentage);
        await _persistenceManager.AlterClientCredits(_persistenceManager.BankCredits, clientId);
        _persistenceManager.ResetBank();

        Lottery.Clear();
        await WriteLotteryAsync();
    }

    private void AnnounceWinner(string name, double winPercentage)
    {
        if (_manager is null) return;
        foreach (var server in _manager.GetServers())
        {
            server.Broadcast(_credifyConfig.Translations.AnnounceLottoWinner
                .FormatExt(name, _persistenceManager.BankCredits, $"{winPercentage:N1}"));
        }
    }
}
