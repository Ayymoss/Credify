using Credify.Chat.Passive.Quests.Enums;
using Credify.Chat.Passive.Quests.Models;
using Credify.Chat.Feature.Raffle.Models;
using Credify.Models;
using SharedLibraryCore.Database.Models;

namespace Credify.Services;

/// <summary>
/// Facade service that delegates to specialized persistence services.
/// Maintains backward compatibility while providing separation of concerns.
/// </summary>
public class PersistenceService(
    CreditsService creditsService,
    StatisticsService statisticsService,
    BankService bankService,
    ShopPersistenceService shopPersistenceService,
    QuestPersistenceService questPersistenceService,
    RafflePersistenceService rafflePersistenceService)
{
    // Credits operations
    public static bool AvailableFunds(EFClient client, long amount) => CreditsService.AvailableFunds(client, amount);
    public async Task WriteClientCreditsAsync(EFClient client, long? amount = null) => await creditsService.WriteClientCreditsAsync(client, amount);
    public async Task<long> GetClientCreditsAsync(EFClient client) => await creditsService.GetClientCreditsAsync(client);
    public async Task<long> AddCreditsAsync(EFClient client, long credits) => await creditsService.AddCreditsAsync(client, credits);
    public async Task<long> RemoveCreditsAsync(EFClient client, long credits) => await creditsService.RemoveCreditsAsync(client, credits);

    // Statistics operations
    public async Task WriteTopScoreAsync() => await statisticsService.WriteTopScoreAsync();
    public async Task ReadTopScoreAsync() => await statisticsService.ReadTopScoreAsync();
    public async Task ReadStatisticsAsync() => await statisticsService.ReadStatisticsAsync();
    public async Task WriteStatisticsAsync() => await statisticsService.WriteStatisticsAsync();
    public void ResetTop() => statisticsService.ResetTop();
    public void ResetStatistics() => statisticsService.ResetStatistics();
    public void OrderTop(EFClient client, long amount) => statisticsService.OrderTop(client, amount);

    // Bank operations
    public void ResetBank() => bankService.ResetBank();
    public void AddBankCredits(long credits) => bankService.AddBankCredits(credits);
    public async Task WriteBankCreditsAsync() => await bankService.WriteBankCreditsAsync();
    public async Task ReadBankCreditsAsync() => await bankService.ReadBankCreditsAsync();

    // Shop operations
    public async Task<List<ClientShopItem>> GetClientShopItemsAsync(EFClient client) => await shopPersistenceService.GetClientShopItemsAsync(client);
    public async Task WriteClientShopAsync(EFClient client, List<ClientShopItem> shopItems) => await shopPersistenceService.WriteClientShopAsync(client, shopItems);
    public async Task WriteRecentBoughtItemsAsync(ClientShopContext item) => await shopPersistenceService.WriteRecentBoughtItemsAsync(item);
    public async Task<IEnumerable<ClientShopContext>> ReadRecentBoughtItemsAsync() => await shopPersistenceService.ReadRecentBoughtItemsAsync();

    // Quest operations
    public async Task<List<QuestMeta>> ReadClientQuestsAsync(EFClient client) => await questPersistenceService.ReadClientQuestsAsync(client);
    public async Task WriteClientQuestsAsync(EFClient client) => await questPersistenceService.WriteClientQuestsAsync(client);

    // Raffle operations
    public async Task WriteLastRaffleWinnerAsync(LastWinner lastWinner) => await rafflePersistenceService.WriteLastRaffleWinnerAsync(lastWinner);
    public async Task<LastWinner?> ReadLastRaffleWinnerAsync() => await rafflePersistenceService.ReadLastRaffleWinnerAsync();
    public async Task<List<Player>> ReadRaffleAsync() => await rafflePersistenceService.ReadRaffleAsync();
    public async Task WriteRaffle(List<Player> rafflePlayers) => await rafflePersistenceService.WriteRaffleAsync(rafflePlayers);
    public async Task WriteNextRaffleAsync(DateTimeOffset dateTime) => await rafflePersistenceService.WriteNextRaffleAsync(dateTime);
    public async Task<DateTimeOffset?> ReadNextRaffleAsync() => await rafflePersistenceService.ReadNextRaffleAsync();

    // Composite operations
    public async Task OnJoinAsync(EFClient client)
    {
        await questPersistenceService.LoadQuestsOnJoinAsync(client);
        await shopPersistenceService.LoadShopItemsOnJoinAsync(client);
        await creditsService.LoadCreditsOnJoinAsync(client);
    }

    public async Task OnKill(EFClient client)
    {
        await creditsService.AddCreditsAsync(client, 1);
        bankService.AddBankCredits(1);
        statisticsService.IncrementCreditsEarned();
    }
}
