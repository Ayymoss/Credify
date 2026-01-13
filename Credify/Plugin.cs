using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack;
using Credify.Chat.Active.Games.Poker;
using Credify.Chat.Active.Games.Roulette;
using Credify.Chat.Active.Games.Roulette.Utilities;
using Credify.Chat.Feature.Bounty;
using Credify.Chat.Feature.Raffle;
using Credify.Chat.Passive.ChatGames;
using Credify.Chat.Passive.Quests;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.EventHandlers;
using Credify.Services;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace Credify;

// TODO: Check Roulette/Blackjack's implementation for feature/saliency. 
// TODO: The 'help' command needs to be refactored hopefully to be scalable so I don't need to remember.
public class Plugin : IPluginV2
{
    private readonly PersistenceService _persistenceService;
    private readonly ChatUtils _chatUtils;
    private readonly RouletteManager _rouletteManager;
    private readonly ScheduleService _scheduleService;
    private readonly RaffleManager _raffleManager;
    private readonly PokerManager _pokerManager;
    private readonly BlackjackManager _blackjackManager;
    private readonly ClientKilledEventHandler _clientKilledEventHandler;
    private readonly ClientMessagedEventHandler _clientMessagedEventHandler;
    private readonly ClientStateAuthorizedEventHandler _clientStateAuthorizedEventHandler;
    private readonly ClientStateDisposedEventHandler _clientStateDisposedEventHandler;
    private readonly CredifyEventHandler _credifyEventHandler;
    private readonly ActiveGameTracker _activeGameTracker;

    public string Name => PluginConstants.PluginName;
    public string Version => "2026-01-13";
    public string Author => "Amos";

    public Plugin(
        PersistenceService persistenceService,
        ChatUtils chatUtils,
        RouletteManager rouletteManager,
        ScheduleService scheduleService,
        RaffleManager raffleManager,
        PokerManager pokerManager,
        BlackjackManager blackjackManager,
        ClientKilledEventHandler clientKilledEventHandler,
        ClientMessagedEventHandler clientMessagedEventHandler,
        ClientStateAuthorizedEventHandler clientStateAuthorizedEventHandler,
        ClientStateDisposedEventHandler clientStateDisposedEventHandler,
        CredifyEventHandler credifyEventHandler,
        ActiveGameTracker activeGameTracker,
        CredifyConfiguration config)
    {
        _persistenceService = persistenceService;
        _chatUtils = chatUtils;
        _rouletteManager = rouletteManager;
        _scheduleService = scheduleService;
        _raffleManager = raffleManager;
        _pokerManager = pokerManager;
        _blackjackManager = blackjackManager;
        _clientKilledEventHandler = clientKilledEventHandler;
        _clientMessagedEventHandler = clientMessagedEventHandler;
        _clientStateAuthorizedEventHandler = clientStateAuthorizedEventHandler;
        _clientStateDisposedEventHandler = clientStateDisposedEventHandler;
        _credifyEventHandler = credifyEventHandler;
        _activeGameTracker = activeGameTracker;
        
        if (!config.IsEnabled) return;

        ICredifyEventService.OnCredifyEvent += OnCredifyEvent;

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
        serviceCollection.AddSingleton<CredifyCache>();
        serviceCollection.AddSingleton<ServerTimeTracker>();

        // Persistence Services (order matters due to dependencies)
        serviceCollection.AddSingleton<StatisticsService>();
        serviceCollection.AddSingleton<BankService>();
        serviceCollection.AddSingleton<CreditsService>();
        serviceCollection.AddSingleton<ShopPersistenceService>();
        serviceCollection.AddSingleton<QuestPersistenceService>();
        serviceCollection.AddSingleton<RafflePersistenceService>();
        serviceCollection.AddSingleton<PersistenceService>(); // Facade that depends on above services

        serviceCollection.AddSingleton<PassiveManager>();
        serviceCollection.AddSingleton<ChatUtils>();
        serviceCollection.AddSingleton<TranslationsRoot>();
        serviceCollection.AddSingleton<ScheduleService>();

        // Active Games Core
        serviceCollection.AddSingleton<GamePlayerCommunication>();
        serviceCollection.AddSingleton<ActiveGameTracker>();
        
        // Blackjack
        serviceCollection.AddSingleton<BlackjackManager>();

        // Roulette
        serviceCollection.AddSingleton<HandleInput>();
        serviceCollection.AddSingleton<HandleOutput>();
        serviceCollection.AddSingleton<RouletteManager>();

        // Poker
        serviceCollection.AddSingleton<PokerManager>();

        // Raffle
        serviceCollection.AddSingleton<RaffleManager>();

        // Quests
        serviceCollection.AddSingleton<QuestManager>();
        
        // Streaks & Bounties
        serviceCollection.AddSingleton<StreakTracker>();
        
        // Bounty Contracts
        serviceCollection.AddSingleton<BountyContractManager>();
        
        // Event Handlers
        serviceCollection.AddSingleton<CredifyEventHandler>();
        serviceCollection.AddSingleton<ClientKilledEventHandler>();
        serviceCollection.AddSingleton<ClientMessagedEventHandler>();
        serviceCollection.AddSingleton<ClientStateAuthorizedEventHandler>();
        serviceCollection.AddSingleton<ClientStateDisposedEventHandler>();
    }

    #region Events

    private async void OnCredifyEvent(ObjectiveType objective, EFClient client, object? data)
    {
        await _credifyEventHandler.HandleAsync(objective, client, data);
    }

    private async Task OnClientMessaged(ClientMessageEvent messageEvent, CancellationToken token)
    {
        await _clientMessagedEventHandler.HandleAsync(messageEvent, token);
    }

    private async Task OnClientStateAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        await _clientStateAuthorizedEventHandler.HandleAsync(clientEvent, token);
    }

    private async Task OnClientKilled(ClientKillEvent clientEvent, CancellationToken token)
    {
        await _clientKilledEventHandler.HandleAsync(clientEvent, token);
    }

    private async Task OnClientStateDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        await _clientStateDisposedEventHandler.HandleAsync(clientEvent, token);
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        _chatUtils.SetManager(manager);
        await _persistenceService.ReadStatisticsAsync();
        await _persistenceService.ReadTopScoreAsync();
        await _persistenceService.ReadBankCreditsAsync();
        await _raffleManager.LoadRaffleAsync(manager);
        await _raffleManager.ReadAndCalculateNextDrawAsync();

        // Register all active games with the tracker
        _activeGameTracker.RegisterGame(_blackjackManager);
        _activeGameTracker.RegisterGame(_rouletteManager);
        _activeGameTracker.RegisterGame(_pokerManager);

        // Use Task.Run instead of Thread for async operations
        _ = Task.Run(async () => await _rouletteManager.StartGameAsync(token), token);
        _ = Task.Run(async () => await _pokerManager.StartGameAsync(token), token);

        _scheduleService.TriggerSchedules(manager, token);

        Console.WriteLine($"[{Name}] loaded. Version: {Version}");
    }

    #endregion
}
