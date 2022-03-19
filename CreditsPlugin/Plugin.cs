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
    }

    private readonly StatsConfiguration _config;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IMetaService _metaService;
    public string Name => "Credits";
    public float Version => 0.5f;
    public string Author => "Amos";

    public async Task OnEventAsync(GameEvent player, Server server)
    {
        switch (player.Type)
        {
            case GameEvent.EventType.Join: // Client Event
                PrimaryLogic
                    .InitialisePlayer(
                        player); // Join event to check if the user has any credits, if new user, set to 0.
                break;

            case GameEvent.EventType.Kill: // Client Event
                PrimaryLogic.IncrementCredits(player); // Kill event +1 Credit on Kill - Check if in Top and Sort.

                // WORKING - Move to a better location
                var serverPlayerRank =
                    await new BetManager(_contextFactory, _config).GetPlayerRankedPosition(player.Origin.ClientId,
                        await player.Origin.CurrentServer.GetIdForServer());
                var serverTotalRanked =
                    await new BetManager(_contextFactory, _config).GetTotalRankedPlayers(
                        await player.Origin.CurrentServer.GetIdForServer());
                Console.WriteLine($"Ranked: {serverPlayerRank}/{serverTotalRanked}");
                // WORKING
                break;

            case GameEvent.EventType.Disconnect: // Client Event
                PrimaryLogic.WriteCredits(player); // Disconnect event to write back credits to database.
                break;

            case GameEvent.EventType.Update: //Server Event
                var result = BetManager.MapEndHighestFragger(server);
                Console.WriteLine($"FRAGS - ID: {result[0]} SCORE: {result[1]}");
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
