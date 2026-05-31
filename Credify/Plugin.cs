using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Blackjack;
using Credify.Chat.Active.Games.Blackjack.Utilities;
using Credify.Chat.Active.Games.Crash;
using Credify.Chat.Active.Games.Crash.Utilities;
using Credify.Chat.Active.Games.Minefield;
using Credify.Chat.Active.Games.Minefield.Utilities;
using Credify.Chat.Active.Games.Poker;
using Credify.Chat.Active.Games.Roulette;
using Credify.Chat.Active.Games.Roulette.Utilities;
using Credify.Chat.Feature.Achievements;
using Credify.Chat.Feature.Bounty;
using Credify.Chat.Feature.Duel;
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

// Active Games are looping. We can't play them.

public class Plugin : IPluginV2
{
    private readonly PersistenceService _persistenceService;
    private readonly ChatUtils _chatUtils;
    private readonly Table _rouletteTable;
    private readonly ScheduleService _scheduleService;
    private readonly RaffleManager _raffleManager;
    private readonly PokerManager _pokerManager;
    private readonly BlackjackGame _blackjackGame;
    private readonly MinefieldGame _minefieldGame;
    private readonly CrashGame _crashGame;
    private readonly ClientKilledEventHandler _clientKilledEventHandler;
    private readonly ClientMessagedEventHandler _clientMessagedEventHandler;
    private readonly ClientStateAuthorizedEventHandler _clientStateAuthorizedEventHandler;
    private readonly ClientStateDisposedEventHandler _clientStateDisposedEventHandler;
    private readonly CredifyEventHandler _credifyEventHandler;
    private readonly ActiveGameTracker _activeGameTracker;

    public string Name => PluginConstants.PluginName;
    public string Version => "2026-05-31";
    public string Author => "Amos";

    public Plugin(
        PersistenceService persistenceService,
        ChatUtils chatUtils,
        Table rouletteTable,
        ScheduleService scheduleService,
        RaffleManager raffleManager,
        PokerManager pokerManager,
        BlackjackGame blackjackGame,
        MinefieldGame minefieldGame,
        CrashGame crashGame,
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
        _rouletteTable = rouletteTable;
        _scheduleService = scheduleService;
        _raffleManager = raffleManager;
        _pokerManager = pokerManager;
        _blackjackGame = blackjackGame;
        _minefieldGame = minefieldGame;
        _crashGame = crashGame;
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
        serviceCollection.AddConfiguration("CredifyConfigurationV4", new CredifyConfiguration());
        serviceCollection.AddSingleton<CredifyCache>();

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
        serviceCollection.AddSingleton<CommandDiscoveryService>();

        // Active Games Core
        serviceCollection.AddSingleton<GamePlayerCommunication>();
        serviceCollection.AddSingleton<ActiveGameTracker>();
        
        // Active games are registered directly as their game class (each implements
        // IActiveGame). The I/O handlers they need are constructed here in the factory,
        // which removes the per-game shim "manager" that used to do only this.

        // Blackjack
        serviceCollection.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<CredifyConfiguration>();
            var communication = sp.GetRequiredService<GamePlayerCommunication>();
            var persistence = sp.GetRequiredService<PersistenceService>();
            var input = new BlackjackHandleInput(config.Translations.Blackjack);
            var output = new BlackjackHandleOutput(config.Translations.Blackjack, communication);
            return new BlackjackGame(persistence, config, communication, input, output);
        });

        // Minefield
        serviceCollection.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<CredifyConfiguration>();
            var communication = sp.GetRequiredService<GamePlayerCommunication>();
            var persistence = sp.GetRequiredService<PersistenceService>();
            var input = new MinefieldHandleInput(config.Translations.Minefield);
            var output = new MinefieldHandleOutput(config.Translations.Minefield, communication);
            return new MinefieldGame(persistence, config, communication, input, output);
        });

        // Roulette
        serviceCollection.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<CredifyConfiguration>();
            var translations = sp.GetRequiredService<TranslationsRoot>();
            var communication = sp.GetRequiredService<GamePlayerCommunication>();
            var persistence = sp.GetRequiredService<PersistenceService>();
            var output = new RouletteHandleOutput(translations, communication);
            return new Table(config, translations, persistence, communication, output);
        });

        // Crash
        serviceCollection.AddSingleton(sp =>
        {
            var config = sp.GetRequiredService<CredifyConfiguration>();
            var communication = sp.GetRequiredService<GamePlayerCommunication>();
            var persistence = sp.GetRequiredService<PersistenceService>();
            var output = new CrashHandleOutput(config.Translations.Crash, communication);
            return new CrashGame(config, persistence, communication, output);
        });

        // Poker (keeps its manager: buy-in overload + cards/river routing)
        serviceCollection.AddSingleton<PokerManager>();

        // Raffle
        serviceCollection.AddSingleton<RaffleManager>();

        // Quests
        serviceCollection.AddSingleton<QuestManager>();
        
        // Wheel
        serviceCollection.AddSingleton<WheelService>();
        
        // Streaks & Bounties
        serviceCollection.AddSingleton<StreakTracker>();
        
        // Bounty Contracts
        serviceCollection.AddSingleton<BountyContractManager>();

        // Achievements
        serviceCollection.AddSingleton<AchievementManager>();

        // Duels
        serviceCollection.AddSingleton<DuelManager>();
        
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
        _activeGameTracker.RegisterGame(_blackjackGame);
        _activeGameTracker.RegisterGame(_rouletteTable);
        _activeGameTracker.RegisterGame(_pokerManager);
        _activeGameTracker.RegisterGame(_minefieldGame);
        _activeGameTracker.RegisterGame(_crashGame);

        // Continuous games run their loop in the background
        _ = Task.Run(async () => await _rouletteTable.GameLoopAsync(token), token);
        _ = Task.Run(async () => await _pokerManager.StartGameAsync(token), token);
        _ = Task.Run(async () => await _crashGame.GameLoopAsync(token), token);

        _scheduleService.TriggerSchedules(manager, token);

        Console.WriteLine($"[{Name}] loaded. Version: {Version}");
    }

    #endregion
}
