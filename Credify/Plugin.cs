using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace Credify;

// TODO:
// Debug lottery
// Confirm buy shop


public class Plugin : IPluginV2
{
    private readonly PersistenceManager _persistenceManager;
    private readonly BetManager _betManager;
    private readonly CredifyConfiguration _config;
    private readonly LotteryManager _lotteryManager;
    public const string CreditsKey = "Credits_Amount";
    public const string CreditsTopKey = "Credits_TopList";
    public const string CreditsStatisticsKey = "Credits_Statistics";
    public const string CreditsLotteryKey = "Credits_Lottery";
    public const string CreditsNextLotteryKey = "Credits_NextLottery";
    public const string CreditsShopKey = "Credits_Shop";
    public const string PluginName = "Credify";
    public string Name => PluginName;
    public string Version => "2023-05-05";
    public string Author => "Amos";

    public Plugin(PersistenceManager persistenceManager, BetManager betManager, CredifyConfiguration config,
        LotteryManager lotteryManager)
    {
        _config = config;
        _lotteryManager = lotteryManager;
        _persistenceManager = persistenceManager;
        _betManager = betManager;
        if (!config.IsEnabled) return;

        IGameEventSubscriptions.ClientKilled += OnClientKilled;
        IGameEventSubscriptions.MatchEnded += OnMatchEnded;
        IGameEventSubscriptions.ClientJoinedTeam += OnClientJoinedTeam;
        IGameServerEventSubscriptions.ClientDataUpdated += OnClientDataUpdated;
        IManagementEventSubscriptions.ClientStateAuthorized += OnClientStateAuthorized;
        IManagementEventSubscriptions.ClientStateDisposed += OnClientStateDisposed;
        IManagementEventSubscriptions.Load += OnLoad;
    }

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<PersistenceManager>();
        serviceCollection.AddSingleton<BetManager>();
        serviceCollection.AddSingleton<LotteryManager>();
        serviceCollection.AddConfiguration("CredifyConfiguration", new CredifyConfiguration());
    }

    private async Task OnMatchEnded(MatchEndEvent matchEnd, CancellationToken token) =>
        await _betManager.OnMapEndAsync(matchEnd.Owner);

    private async Task OnClientJoinedTeam(ClientJoinTeamEvent clientEvent, CancellationToken token) =>
        await _betManager.OnJoinTeamAsync(clientEvent.Client);

    private async Task OnClientStateAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token) =>
        await _persistenceManager.OnJoinAsync(clientEvent.Client);

    private async Task OnClientDataUpdated(ClientDataUpdateEvent clientEvent, CancellationToken token)
    {
        foreach (var client in clientEvent.Clients) await _betManager.OnUpdateAsync(client);
    }

    private async Task OnClientKilled(ClientKillEvent clientEvent, CancellationToken token)
    {
        _persistenceManager.OnKill(clientEvent.Client);
        await _betManager.OnKillAsync(clientEvent.Client);
    }

    private async Task OnClientStateDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        await _persistenceManager.WriteClientCredits(clientEvent.Client);
        await _persistenceManager.WriteStatisticsAsync();
        await _persistenceManager.WriteTopScoreAsync();
        await _betManager.OnDisconnectAsync(clientEvent.Client);
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        _lotteryManager.SetManager(manager);
        await _persistenceManager.ReadStatisticsAsync();
        await _persistenceManager.ReadTopScoreAsync();
        await _persistenceManager.ReadBankCreditsAsync();
        await _lotteryManager.ReadLotteryAsync();
        await _lotteryManager.CalculateNextOccurrence();

        Utilities.ExecuteAfterDelay(_config.Core.CredifyAdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelay(manager, cancellationToken), token);

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), LotteryDelayCheck, token);

        Console.WriteLine($"[{Name}] loaded. Version: {Version}");
    }

    private async Task LotteryDelayCheck(CancellationToken token)
    {
        if (_lotteryManager.HasLotteryHappened)
        {
            await _lotteryManager.PickWinner();
            await _lotteryManager.CalculateNextOccurrence();
        }
    }

    private async Task AdvertisementDelay(IManager manager, CancellationToken token)
    {
        foreach (var server in manager.GetServers())
        {
            var messages = new[]
            {
                _config.Translations.AdvertisementMessage.FormatExt(PluginName),
                _config.Translations.AdvertisementLotto.FormatExt(PluginName),
                _config.Translations.AdvertisementShop.FormatExt(PluginName)
            };
            await server.BroadcastAsync(messages, token: token);
        }

        Utilities.ExecuteAfterDelay(_config.Core.CredifyAdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelay(manager, cancellationToken), token);
    }
}
