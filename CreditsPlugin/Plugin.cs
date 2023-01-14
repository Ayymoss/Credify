using Data.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Stats.Config;

namespace CreditsPlugin;

// Notify in chat on new match to team/player bet
// Self-advertise the plugin a bit better

public class Plugin : IPlugin
{
    private IManager _manager = null!;
    public static BetManager BetManager = null!;
    public static PrimaryLogic PrimaryLogic = null!;
    public const string CreditsKey = "Credits_Amount";
    public const string CreditsTopKey = "Credits_TopList";
    public const string CreditsStatisticsKey = "Credits_Statistics";
    private const string PluginName = "Credits";
    public const int CreditsMinimumPlayers = 10;
    public const int CreditsBetWindow = 120; // Seconds

    public string Name => PluginName;
    public float Version => 20220403f;
    public string Author => "Amos";

    public Plugin(IDatabaseContextFactory contextFactory, IMetaServiceV2 metaService, StatsConfiguration statsConfig)
    {
        BetManager = new BetManager(contextFactory, statsConfig);
        PrimaryLogic = new PrimaryLogic(metaService, contextFactory);
    }

    public async Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join: // Client Event
                // Check if the user has any credits. New usr=0
                PrimaryLogic.OnJoin(gameEvent.Origin);
                break;

            case GameEvent.EventType.Kill: // Client Event
                // Kill event +1 Credit on Kill - Check if in Top and Sort
                PrimaryLogic.OnKill(gameEvent.Origin);
                // If bet entries, start tracking score
                await BetManager.OnKill(gameEvent.Origin);
                break;

            case GameEvent.EventType.Disconnect: // Client Event
                // Disconnect event to write back credits to database
                PrimaryLogic.OnDisconnect(gameEvent.Origin);
                // Remove client from score list
                await BetManager.OnDisconnect(gameEvent.Origin);
                break;

            case GameEvent.EventType.Update: // Client Event (Runs against ALL clients)
                await BetManager.OnUpdate(gameEvent.Origin); // Get top score
                break;

            case GameEvent.EventType.MapEnd: // Server Event
                await BetManager.OnMapEnd(server); // Add each server to dictionary with time last rotation.
                break;

            case GameEvent.EventType.JoinTeam: // Client Event
                await BetManager.OnJoinTeam(gameEvent.Origin); // Add each server to dict and update teams
                break;
        }
    }

    public Task OnLoadAsync(IManager manager)
    {
        _manager = manager;
        PrimaryLogic.ReadStatistics();
        PrimaryLogic.ReadTopScore();

        Console.WriteLine($"[{PluginName}] loaded ({Version})");
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync()
    {
        foreach (var client in _manager.GetActiveClients())
        {
            PrimaryLogic.OnDisconnect(client);
        }

        PrimaryLogic.WriteStatistics();
        PrimaryLogic.WriteTopScore();

        Console.WriteLine($"{PluginName} unloaded");
        return Task.CompletedTask;
    }

    public Task OnTickAsync(Server server) => Task.CompletedTask;
}
