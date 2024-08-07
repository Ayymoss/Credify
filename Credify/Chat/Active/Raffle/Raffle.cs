using Credify.Chat.Active.Raffle.Enums;
using Credify.Chat.Active.Raffle.Models;
using Credify.Chat.Active.Raffle.Utilities;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;

namespace Credify.Chat.Active.Raffle;

public class Raffle(
    ClientService clientService,
    CredifyConfiguration config,
    TranslationsRoot translationsRoot,
    PersistenceService persistenceService,
    CredifyCache cache,
    List<Player> players)
{
    public bool ShouldDrawRaffle => TimeProvider.System.GetLocalNow() >= NextOccurrence;
    public DateTimeOffset NextOccurrence { get; private set; }

    private readonly RaffleTranslations _raffleTrans = translationsRoot.Raffle;

    public async Task<RaffleResult> PurchaseTicketAsync(EFClient client, int? ticket)
    {
        var checkedTicket = CheckTicket(ticket);
        if (!ValidTicket(checkedTicket)) return new RaffleResult(StatusTypes.InvalidTicketRange, null);

        if (players.Any(p => p.ClientId == client.ClientId)) return new RaffleResult(StatusTypes.ClientAlreadyPurchased, null);
        if (players.Any(x => x.Ticket == checkedTicket)) return new RaffleResult(StatusTypes.TicketAlreadyPurchased, null);

        players.Add(new Player(client.ClientId, checkedTicket));
        persistenceService.AddBankCredits(config.Core.RaffleCost);
        await persistenceService.RemoveCreditsAsync(client, config.Core.RaffleCost);
        await persistenceService.WriteRaffle(players);
        return new RaffleResult(StatusTypes.Success, checkedTicket);
    }

    public async Task DrawWinnerAsync(IManager manager)
    {
        var winner = players.ElementAt(Random.Shared.Next(players.Count));
        var client = await clientService.Get(winner.ClientId);
        if (client is null) return;

        var bankCredits = cache.BankCredits;
        cache.BankCredits = 0;

        await persistenceService.WriteBankCreditsAsync();
        await persistenceService.AddCreditsAsync(client, bankCredits);
        await persistenceService.WriteLastRaffleWinnerAsync(new LastWinner(client.ClientId, client.CleanedName, bankCredits,
            players.Count));

        var winPercentage = (double)1 / players.Count * 100;
        var announcement = _raffleTrans.AnnounceRaffleWinner
            .FormatExt(client.CleanedName, bankCredits.ToString("N0"), winPercentage.ToString("N1"));

        await HandleOutput.TellAllServersAsync(manager, [_raffleTrans.Prefix(announcement)]);
    }

    public async Task ReadAndCalculateNextDrawAsync()
    {
        var currentDate = TimeProvider.System.GetLocalNow();
        if (NextOccurrence > currentDate) return;

        var next = await persistenceService.ReadNextRaffleAsync();
        if (next is null || next < currentDate)
        {
            var nextLotteryDate = currentDate.Date
                .AddDays(config.Core.RaffleFrequency.TotalDays)
                .Add(config.Core.RaffleDrawTime);

            NextOccurrence = nextLotteryDate;
            await persistenceService.WriteNextRaffleAsync(NextOccurrence);
            return;
        }

        NextOccurrence = next.Value;
    }

    private int CheckTicket(int? ticket)
    {
        if (ticket.HasValue) return ticket.Value;

        do
        {
            ticket = Random.Shared.Next(1_000);
        } while (players.Any(x => x.Ticket == ticket));

        return ticket.Value;
    }

    private static bool ValidTicket(int ticket) => ticket is > 0 and < 1_000;

    public async Task<List<PlayerFull>> GetPlayersAsync()
    {
        List<PlayerFull> clients = [];
        foreach (var player in players)
        {
            var client = await clientService.Get(player.ClientId);
            if (client is not null) clients.Add(new PlayerFull(client, player.Ticket));
        }

        return clients;
    }
}
