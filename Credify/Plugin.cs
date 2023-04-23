using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Events.Server;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace Credify;

// Notify in chat on new match to team/player bet
// Self-advertise the plugin a bit better
// Create help command

public class Plugin : IPluginV2
{
    private readonly BetLogic _betLogic;
    private readonly BetManager _betManager;
    private readonly CredifyConfiguration _config;
    public const string CreditsKey = "Credits_Amount";
    public const string CreditsTopKey = "Credits_TopList";
    public const string CreditsStatisticsKey = "Credits_Statistics";
    public const string PluginName = "Credify";
    public string Name => PluginName;
    public string Version => "2023-04-23";
    public string Author => "Amos";

    public Plugin(BetLogic betLogic, BetManager betManager, CredifyConfiguration config)
    {
        _config = config;
        if (!config.IsEnabled) return;

        _betLogic = betLogic;
        _betManager = betManager;

        IGameEventSubscriptions.ClientKilled += OnClientKilled;
        IGameEventSubscriptions.MatchEnded += OnMatchEnded;
        IGameEventSubscriptions.ClientJoinedTeam += OnClientJoinedTeam;
        IGameServerEventSubscriptions.ClientDataUpdated += OnClientDataUpdated;
        IManagementEventSubscriptions.ClientStateAuthorized += OnClientStateAuthorized;
        IManagementEventSubscriptions.ClientStateDisposed += OnClientStateDisposed;
        IManagementEventSubscriptions.Load += OnLoad;
        IManagementEventSubscriptions.Unload += OnUnload;
    }

    public static void RegisterDependencies(IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<BetLogic>();
        serviceCollection.AddSingleton<BetManager>();
        serviceCollection.AddConfiguration("CredifyConfiguration", new CredifyConfiguration());
    }

    private async Task OnClientDataUpdated(ClientDataUpdateEvent clientEvent, CancellationToken token)
    {
        foreach (var client in clientEvent.Clients)
        {
            // Get top score
            await _betManager.OnUpdateAsync(client);
        }
    }

    private async Task OnClientKilled(ClientKillEvent clientEvent, CancellationToken token)
    {
        // Kill event +1 Credit on Kill - Check if in Top and Sort
        _betLogic.OnKill(clientEvent.Client);
        // If bet entries, start tracking score
        await _betManager.OnKillAsync(clientEvent.Client);
    }

    private async Task OnMatchEnded(MatchEndEvent matchEnd, CancellationToken token)
    {
        // Add each server to dictionary with time last rotation.
        await _betManager.OnMapEndAsync(matchEnd.Owner);
    }

    private async Task OnClientJoinedTeam(ClientJoinTeamEvent clientEvent, CancellationToken token)
    {
        // Add each server to dict and update teams
        await _betManager.OnJoinTeamAsync(clientEvent.Client);
    }

    private async Task OnClientStateAuthorized(ClientStateAuthorizeEvent clientEvent, CancellationToken token)
    {
        // Check if the user has any credits. New usr=0
        await _betLogic.OnJoinAsync(clientEvent.Client);
    }

    private async Task OnClientStateDisposed(ClientStateDisposeEvent clientEvent, CancellationToken token)
    {
        // Disconnect event to write back credits to database
        await _betLogic.OnDisconnectAsync(clientEvent.Client);
        // Remove client from score list
        await _betManager.OnDisconnectAsync(clientEvent.Client);
    }

    private async Task OnLoad(IManager manager, CancellationToken token)
    {
        await _betLogic.ReadStatisticsAsync();
        await _betLogic.ReadTopScoreAsync();

        Utilities.ExecuteAfterDelay(_config.CredifyAdvertisementIntervalMinutes,
            cancellationToken => OnExecuteAfterDelay(manager, cancellationToken), token);

        Console.WriteLine($"[{Name}] loaded. Version: {Version}");
    }

    private async Task OnUnload(IManager manager, CancellationToken token)
    {
        foreach (var client in manager.GetActiveClients())
        {
            await _betLogic.OnDisconnectAsync(client);
        }

        await _betLogic.WriteStatisticsAsync();
        await _betLogic.WriteTopScoreAsync();

        Console.WriteLine($"[{Name}] unloaded. Version: {Version}");
    }

    private Task OnExecuteAfterDelay(IManager manager, CancellationToken token)
    {
        foreach (var server in manager.GetServers())
        {
            server.Broadcast(_config.Translations.AdvertisementMessage.FormatExt(PluginName), Utilities.IW4MAdminClient());
        }

        Utilities.ExecuteAfterDelay(_config.CredifyAdvertisementIntervalMinutes,
            cancellationToken => OnExecuteAfterDelay(manager, cancellationToken), token);
        return Task.CompletedTask;
    }
}
