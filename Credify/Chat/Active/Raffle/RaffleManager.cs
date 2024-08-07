using Credify.Chat.Active.Raffle.Enums;
using Credify.Chat.Active.Raffle.Models;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
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
    private IManager? _manager;

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
        if (_raffle is null || _manager is null) return;
        await _raffle.DrawWinnerAsync(_manager);
        _raffle = CreateNewRaffle();
        
        await _raffle.ReadAndCalculateNextDrawAsync();
        await persistenceService.WriteRaffle([]);
    }

    public async Task LoadRaffleAsync(IManager manager)
    {
        _manager = manager;
        var raffle = await persistenceService.ReadRaffleAsync();
        _raffle = CreateNewRaffle(raffle);
        await _raffle.ReadAndCalculateNextDrawAsync();
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
