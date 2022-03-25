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
    private const string CreditsPrefix = "[Credits]";
    
    public string Name => "Credits";
    public float Version => 0.1f;
    public string Author => "Amos";

    public Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join: // Client Event
                // Check if the user has any credits. New usr=0
                PrimaryLogic.InitialisePlayer(gameEvent.Origin);
                break;

            case GameEvent.EventType.Kill: // Client Event
                // Kill event +1 Credit on Kill - Check if in Top and Sort.
                PrimaryLogic.IncrementCredits(gameEvent.Origin);
                // If bets have been made, return the expired bet to the completed player.
                BetManager.OnKillMessageSystem(gameEvent.Origin);
                break;

            case GameEvent.EventType.Disconnect: // Client Event
                PrimaryLogic.WriteCredits(gameEvent.Origin); // Disconnect event to write back credits to database.
                break;

            case GameEvent.EventType.Update: // Client Event (Runs against ALL clients)
                BetManager.OnClientUpdated(gameEvent.Origin); // Get top score
                break;

            case GameEvent.EventType.MapEnd: // Server Event
                BetManager.OnMatchEnd(server); // Add each server to dictionary with time last rotation.
                break;
        }

        return Task.CompletedTask;
    }

    public Task OnLoadAsync(IManager manager)
    {
        // Assign manager to class level property
        Manager = manager;
        PrimaryLogic.ReadTopScore();
        
        Console.WriteLine($"{CreditsPrefix} Loaded - Version: {Version}");
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync()
    {
        foreach (var client in Manager.GetActiveClients()) PrimaryLogic.WriteCredits(client);
        PrimaryLogic.WriteTopScore();
        
        Console.WriteLine($"{CreditsPrefix} Unloaded");
        return Task.CompletedTask;
    }

    public Task OnTickAsync(Server server) => Task.CompletedTask;
}
