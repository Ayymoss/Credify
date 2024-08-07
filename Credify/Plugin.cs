using Credify.Chat.Active.Blackjack;
using Credify.Chat.Active.Raffle;
using Credify.Chat.Active.Roulette;
using Credify.Chat.Active.Roulette.Utilities;
using Credify.Chat.Passive;
using Credify.Configuration;
using Credify.Services;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

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
*/

public class Plugin : IPluginV2
{
    private readonly PersistenceService _persistenceService;
    private readonly PassiveManager _passiveManager;
    private readonly ChatUtils _chatUtils;
    private readonly BlackjackManager _blackjack;
    private readonly RouletteManager _roulette;
    private readonly ScheduleService _scheduleService;
    private readonly RaffleManager _raffleManager;

    public const string CreditsAmount = "Credits_Amount";
    public const string TopKey = "Credits_TopList";
    public const string StatisticsKey = "Credits_Statistics";
    public const string LotteryKey = "Credits_Lottery";
    public const string NextRaffleKey = "Credits_NextLottery";
    public const string ShopKey = "Credits_Shop";
    public const string BankCreditsKey = "Credits_Bank";
    public const string LastLottoWinner = "Credits_LastLottoWinner";
    public const string RecentBoughtItems = "Credits_RecentBoughtItems";
    public const string RaffleKey = "Credits_Raffle";
    public const string LastRaffleWinner = "Credits_LastRaffleWinner";

    public const string PluginName = "Credify";
    public string Name => PluginName;
    public string Version => "2024-08-04";
    public string Author => "Amos";

    public Plugin(PersistenceService persistenceService, CredifyConfiguration config,
        PassiveManager passiveManager, ChatUtils chatUtils, BlackjackManager blackjack, RouletteManager roulette,
        ScheduleService scheduleService, RaffleManager raffleManager)
    {
        _passiveManager = passiveManager;
        _chatUtils = chatUtils;
        _blackjack = blackjack;
        _roulette = roulette;
        _scheduleService = scheduleService;
        _raffleManager = raffleManager;
        _persistenceService = persistenceService;
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
        serviceCollection.AddSingleton<PersistenceService>();
        serviceCollection.AddSingleton<PassiveManager>();
        serviceCollection.AddSingleton<ChatUtils>();
        serviceCollection.AddSingleton<BlackjackManager>();
        serviceCollection.AddSingleton<TranslationsRoot>();
        serviceCollection.AddSingleton<ScheduleService>();
        serviceCollection.AddSingleton<CredifyCache>();

        // Roulette
        serviceCollection.AddSingleton<HandleInput>();
        serviceCollection.AddSingleton<HandleOutput>();
        serviceCollection.AddSingleton<RouletteManager>();

        // Raffle
        serviceCollection.AddSingleton<RaffleManager>();
    }

    #region Events

    private async Task OnClientMessaged(ClientMessageEvent messageEvent, CancellationToken token)
    {
        await _passiveManager.HandleChatEventAsync(messageEvent.Client, messageEvent.Message);
        await _blackjack.HandleChatEventAsync(messageEvent.Client, messageEvent.Message);
    }

    private async Task OnClientStateAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        await _persistenceService.OnJoinAsync(clientEvent.Client);
    }

    private Task OnClientKilled(ClientKillEvent clientEvent, CancellationToken token)
    {
        _persistenceService.OnKill(clientEvent.Client);
        return Task.CompletedTask;
    }

    private async Task OnClientStateDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        await _persistenceService.WriteClientCreditsAsync(clientEvent.Client);
        await _persistenceService.WriteStatisticsAsync();
        await _persistenceService.WriteTopScoreAsync();
        await _persistenceService.WriteBankCreditsAsync();
        await _blackjack.LeaveGameAsync(clientEvent.Client);
        _roulette.RemovePlayer(clientEvent.Client);
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        _chatUtils.SetManager(manager);
        await _persistenceService.ReadStatisticsAsync();
        await _persistenceService.ReadTopScoreAsync();
        await _persistenceService.ReadBankCreditsAsync();
        await _raffleManager.LoadRaffleAsync(manager);
        await _raffleManager.ReadAndCalculateNextDrawAsync();

        new Thread(StartRoulette) { Name = "Roulette" }.Start();

        _scheduleService.TriggerSchedules(manager, token);

        Console.WriteLine($"[{Name}] loaded. Version: {Version}");
        return;

        async void StartRoulette() => await _roulette.StartGameAsync(token);
    }

    #endregion
}
