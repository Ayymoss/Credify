using Data.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Stats.Config;

namespace CreditsPlugin;

public class Plugin : IPlugin
{
    public Plugin(IDatabaseContextFactory contextFactory, IMetaService metaService, StatsConfiguration statsConfig)
    {
        _config = statsConfig;
        _contextFactory = contextFactory;
        _metaService = metaService;
        BetManager = new BetManager(_contextFactory, _config, _metaService);
        PrimaryLogic = new PrimaryLogic(_metaService);
    }

    public static BetManager BetManager;
    public static PrimaryLogic PrimaryLogic;
    private readonly StatsConfiguration _config;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IMetaService _metaService;
    public const string CreditsKey = "Credits_Amount";
    public string Name => "Credits";
    public float Version => 0.5f;
    public string Author => "Amos";

    public async Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join: // Client Event
                PrimaryLogic.InitialisePlayer(gameEvent); // Join event to check if the user has any credits. New usr = 0.
                break;

            case GameEvent.EventType.Kill: // Client Event
                PrimaryLogic.IncrementCredits(gameEvent); // Kill event +1 Credit on Kill - Check if in Top and Sort.

                break;

            case GameEvent.EventType.Disconnect: // Client Event
                PrimaryLogic.WriteCredits(gameEvent); // Disconnect event to write back credits to database.
                break;

            case GameEvent.EventType.Update: // Client Event (Runs against ALL clients)
                BetManager.OnClientUpdated(gameEvent);
                break;
            
            case GameEvent.EventType.MapEnd: // Server Event
                BetManager.OnMatchEnd(server); // Add each server to dictionary with time last rotation.
                break;
        }
    }
    
    public async Task OnLoadAsync(IManager manager)
    {
        // Pull top credit data on IW4MAdmin load and deserialise. 
        new PrimaryLogic(_metaService).ReadTopScore();
        Console.WriteLine($"[Credits] Plugin Loaded. Version: {Version}");
    }

    public async Task OnUnloadAsync()
    {
        // Remove old top credit entry and write updated one.
        //new PrimaryLogic(_metaService).WriteTopScore();
        Console.WriteLine("[Credits] Plugin Unloaded.");
    }

    public Task OnTickAsync(Server server) => Task.CompletedTask;
}
