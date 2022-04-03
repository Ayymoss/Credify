using Data.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Stats.Config;

namespace CreditsPlugin;

public class Plugin : IPlugin
{
    public Plugin(IDatabaseContextFactory contextFactory, IMetaServiceV2 metaService, StatsConfiguration statsConfig)
    {
        BetManager = new BetManager(contextFactory, statsConfig);
        PrimaryLogic = new PrimaryLogic(metaService, contextFactory);
    }

    private IManager Manager { get; set; }
    public static BetManager BetManager;
    public static PrimaryLogic PrimaryLogic;
    public const string CreditsKey = "Credits_Amount";
    public const string CreditsTopKey = "Credits_TopList";
    public const string CreditsStatistics = "Credits_Statistics";
    private const string CreditsPrefix = "[Credits]";
    public const int CreditsMinimumPlayers = 10;
    public const int CreditsMaximumBetTime = 120;

    // TODO: Uncomment Cancel and BetT time check logic
    // TODO: Add Total Payout/Spent credits lifetime commands (statistics)

    // TEST LIST
    // Player Bets
    // Completed Bets
    // Bet Cancel
    // Open Bets

    private readonly DateTime _date = DateTime.Now;
    public string Name => "Credits";
    public float Version => float.Parse(_date.ToString("yyyyMMdd"));
    public string Author => "Amos";

    public Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join: // Client Event
                // Check if the user has any credits. New usr=0
                PrimaryLogic.OnJoin(gameEvent.Origin);
                break;

            case GameEvent.EventType.Kill: // Client Event
                // Kill event +1 Credit on Kill - Check if in Top and Sort.
                PrimaryLogic.OnKill(gameEvent.Origin);
                // If bets have been made, return the expired bet to the completed player.
                BetManager.OnKill(gameEvent.Origin);
                break;

            case GameEvent.EventType.Disconnect: // Client Event
                // Disconnect event to write back credits to database.
                PrimaryLogic.OnDisconnect(gameEvent.Origin);
                BetManager.OnDisconnect(gameEvent.Origin);
                break;

            case GameEvent.EventType.Update: // Client Event (Runs against ALL clients)
                BetManager.OnUpdate(gameEvent.Origin); // Get top score
                break;

            case GameEvent.EventType.MapEnd: // Server Event
                BetManager.OnMapEnd(server); // Add each server to dictionary with time last rotation.
                break;
            case GameEvent.EventType.JoinTeam:
                BetManager.OnJoinTeam(gameEvent.Origin); // Add each server to dict and update teams
                break;
        }

        return Task.CompletedTask;
    }

    public Task OnLoadAsync(IManager manager)
    {
        // Assign manager to class level property
        Manager = manager;
        PrimaryLogic.ReadStatistics();
        PrimaryLogic.ReadTopScore();

        Console.WriteLine($"{CreditsPrefix} loaded ({Version})");
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync()
    {
        foreach (var client in Manager.GetActiveClients()) PrimaryLogic.OnDisconnect(client);
        PrimaryLogic.WriteStatistics();
        PrimaryLogic.WriteTopScore();

        Console.WriteLine($"{CreditsPrefix} unloaded");
        return Task.CompletedTask;
    }

    public Task OnTickAsync(Server server) => Task.CompletedTask;
}
