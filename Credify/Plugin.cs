using Credify.Chat.Active.Blackjack;
using Credify.Chat.Active.Roulette;
using Credify.Chat.Active.Roulette.Utilities;
using Credify.Chat.Passive;
using Credify.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using Utilities = SharedLibraryCore.Utilities;

namespace Credify;

// TODO:
/*
Achievements -> get kill with X
MOD, Description, Amount, Payout
There would need to be stored current progress for each player

Roulette should show the table's numbers if more than 1 player so people can see what's happening.

Daily Quests/Challenges - Kill 10 people for example.

Reaction Tests should have 'Per Server Timing' and use the incoming message timestamp rather than a timer.
This would prevent issues with server message delays.

Consider adding a Discord integration for the plugin.

Clarify the purpose of credits for new players to avoid confusion.
    When player joins add "do !crhelp for more" to the welcome message.
    
Rewrite lottery to use a raffle-type system like Impulse. Rollover bank if no one wins.
    Up to 100 tickets - Pick a winner from the taken tickets - Guarantee a winner.
*/

public class Plugin : IPluginV2
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _config;
    private readonly LotteryManager _lotteryManager;
    private readonly ChatGameManager _chatGameManager;
    private readonly ChatUtils _chatUtils;
    private readonly IConfigurationHandlerV2<CredifyConfiguration> _configHandler;
    private readonly BlackjackManager _blackjack;
    private readonly TableManager _roulette;
    public const string CreditsAmount = "Credits_Amount";
    public const string TopKey = "Credits_TopList";
    public const string StatisticsKey = "Credits_Statistics";
    public const string LotteryKey = "Credits_Lottery";
    public const string NextLotteryKey = "Credits_NextLottery";
    public const string ShopKey = "Credits_Shop";
    public const string BankCreditsKey = "Credits_Bank";
    public const string LastLottoWinner = "Credits_LastLottoWinner";
    public const string RecentBoughtItems = "Credits_RecentBoughtItems";

    public const string PluginName = "Credify";
    public string Name => PluginName;
    public string Version => "2024-08-04";
    public string Author => "Amos";

    public Plugin(PersistenceManager persistenceManager, CredifyConfiguration config, LotteryManager lotteryManager,
        ChatGameManager chatGameManager, ChatUtils chatUtils, IConfigurationHandlerV2<CredifyConfiguration> configHandler,
        BlackjackManager blackjack, TableManager roulette)
    {
        _config = config;
        _lotteryManager = lotteryManager;
        _chatGameManager = chatGameManager;
        _chatUtils = chatUtils;
        _configHandler = configHandler;
        _blackjack = blackjack;
        _roulette = roulette;
        _persistenceManager = persistenceManager;
        if (!config.IsEnabled) return;

        IGameEventSubscriptions.ClientKilled += OnClientKilled;
        IGameEventSubscriptions.ClientMessaged += OnClientMessaged;
        IManagementEventSubscriptions.ClientStateAuthorized += OnClientStateAuthorized;
        IManagementEventSubscriptions.ClientStateDisposed += OnClientStateDisposed;
        IManagementEventSubscriptions.Load += OnLoad;
    }

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        // Core
        serviceCollection.AddConfiguration("CredifyConfigurationV2", new CredifyConfiguration());
        serviceCollection.AddSingleton<PersistenceManager>();
        serviceCollection.AddSingleton<LotteryManager>();
        serviceCollection.AddSingleton<ChatGameManager>();
        serviceCollection.AddSingleton<ChatUtils>();
        serviceCollection.AddSingleton<BlackjackManager>();
        serviceCollection.AddSingleton<TranslationsRoot>();

        // Roulette
        serviceCollection.AddSingleton<HandleInput>();
        serviceCollection.AddSingleton<HandleOutput>();
        serviceCollection.AddSingleton<Table>();
        serviceCollection.AddSingleton<TableManager>();
    }

    #region Events

    private async Task OnClientMessaged(ClientMessageEvent messageEvent, CancellationToken token)
    {
        await _chatGameManager.HandleChatEventAsync(messageEvent.Client, messageEvent.Message);
        await _blackjack.HandleChatEventAsync(messageEvent.Client, messageEvent.Message);
    }

    private async Task OnClientStateAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        await _persistenceManager.OnJoinAsync(clientEvent.Client);
    }

    private Task OnClientKilled(ClientKillEvent clientEvent, CancellationToken token)
    {
        _persistenceManager.OnKill(clientEvent.Client);
        return Task.CompletedTask;
    }

    private async Task OnClientStateDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        await _persistenceManager.WriteClientCreditsAsync(clientEvent.Client);
        await _persistenceManager.WriteStatisticsAsync();
        await _persistenceManager.WriteTopScoreAsync();
        await _persistenceManager.WriteBankCreditsAsync();
        await _blackjack.LeaveGameAsync(clientEvent.Client);
        _roulette.RemovePlayer(clientEvent.Client);
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        var configResult = await _configHandler.Get("CredifyConfiguration");
        if (configResult is null)
        {
            Console.WriteLine($"[{Name}] Failed to load config. Creating a new configuration.");
            await _configHandler.Set(new CredifyConfiguration());
        }

        _lotteryManager.SetManager(manager);
        _chatUtils.SetManager(manager);
        await _persistenceManager.ReadStatisticsAsync();
        await _persistenceManager.ReadTopScoreAsync();
        await _persistenceManager.ReadBankCreditsAsync();
        await _lotteryManager.ReadLotteryAsync();
        await _lotteryManager.CalculateNextOccurrence();

        var thread = new Thread(Start) { Name = "Roulette" };
        thread.Start();
        //_ = Task.Run(async () => await _roulette.StartGame(token), token); // fire and forget

        if (_config.ChatGame.IsEnabled) Utilities.ExecuteAfterDelay(_config.ChatGame.Frequency, InitChatGame, token);
        Utilities.ExecuteAfterDelay(_config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelay(manager, cancellationToken), token);
        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), LotteryDelayCheck, token);

        Console.WriteLine($"[{Name}] loaded. Version: {Version}");
        return;

        async void Start() => await _roulette.StartGame(token);
    }

    #endregion

    #region Notifs

    private async Task InitChatGame(CancellationToken token)
    {
        await _chatGameManager.InitGameAsync();
        Utilities.ExecuteAfterDelay(_config.ChatGame.Frequency, InitChatGame, token);
    }

    private async Task LotteryDelayCheck(CancellationToken token)
    {
        if (_lotteryManager.HasLotteryHappened)
        {
            await _lotteryManager.PickWinner();
            await _lotteryManager.CalculateNextOccurrence();
        }

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), LotteryDelayCheck, token);
    }

    private async Task AdvertisementDelay(IManager manager, CancellationToken token)
    {
        foreach (var server in manager.GetServers())
        {
            if (server.ConnectedClients.Count is 0) continue;
            List<string> messages =
            [
                _config.Translations.Core.AdvertisementMessage.FormatExt(PluginName),
                _config.Translations.Core.AdvertisementLotto.FormatExt(PluginName),
                _config.Translations.Core.AdvertisementShop.FormatExt(PluginName)
            ];
            await server.BroadcastAsync(messages, token: token);
        }

        Utilities.ExecuteAfterDelay(_config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelay(manager, cancellationToken), token);
    }

    #endregion
}
