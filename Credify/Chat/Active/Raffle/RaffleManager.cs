using Credify.Chat.Active.Raffle.Enums;
using Credify.Chat.Active.Raffle.Models;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Services;

namespace Credify.Chat.Active.Raffle;

public class RaffleManager(
    ClientService clientService,
    CredifyConfiguration config,
    TranslationsRoot translationsRoot,
    CredifyCache cache,
    PersistenceService persistenceService)
{
    private Raffle? _raffle;

    public bool ShouldDrawRaffle => _raffle?.ShouldDrawRaffle ?? false;
    public DateTimeOffset NextOccurrence => _raffle?.NextOccurrence ?? DateTimeOffset.MinValue;

    public async Task<RaffleResult> PurchaseTicketAsync(EFClient client, int? ticket)
    {
        if (_raffle is null) return new RaffleResult(StatusTypes.RaffleNotStarted, null);
        return await _raffle.PurchaseTicketAsync(client, ticket);
    }

    public async Task<List<PlayerFull>> GetPlayersAsync()
    {
        if (_raffle is null) return [];
        return await _raffle.GetPlayersAsync();
    }

    public async Task DrawWinnerAsync()
    {
        if (_raffle is null) return;
        await _raffle.DrawWinnerAsync();
        await _raffle.ReadAndCalculateNextDrawAsync();
        _raffle = CreateNewRaffle();
    }

    public async Task LoadRaffleAsync()
    {
        var raffle = await persistenceService.ReadRaffleAsync();
        _raffle = CreateNewRaffle(raffle);
    }

    public async Task ReadAndCalculateNextDrawAsync()
    {
        if (_raffle is null) return;
        await _raffle.ReadAndCalculateNextDrawAsync();
    }

    public async Task<LastWinner?> GetLastWinnerAsync()
    {
        return await persistenceService.ReadLastRaffleWinnerAsync();
    }

    private Raffle CreateNewRaffle(List<Player>? players = null)
    {
        return new Raffle(clientService, config, translationsRoot, persistenceService, cache, players ?? []);
    }
}
