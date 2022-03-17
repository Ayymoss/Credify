using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CreditsPlugin;

// TODO: Bet on Player to Win (Dynamic Payout based on ELO?)

public class Plugin : IPlugin
{
    public Plugin(IMetaService metaService)
    {
        _metaService = metaService;
    }

    private readonly IMetaService _metaService;

    public string Name => "Credits";
    public float Version => 0.5f;
    public string Author => "Amos";

    public async Task OnEventAsync(GameEvent e, Server s)
    {
        // PLAYER EVENTS 

        // Join event to check if the user has any credits, if new user, set to 0.
        if (e.Type == GameEvent.EventType.Join)
        {
            var userCredits = (await _metaService.GetPersistentMeta("Credits", e.Origin))?.Value ?? "0";

            e.Origin.SetAdditionalProperty("Credits", int.Parse(userCredits));

            e.Origin.Tell($"You have (Color::Cyan){userCredits} (Color::White)credits.");
        }

        // Kill event +1 Credit on Kill - Check if in Top and Sort.
        if (e.Type == GameEvent.EventType.Kill)
        {
            var userCredits = e.Origin.GetAdditionalProperty<int>("Credits");
            userCredits++;
            e.Origin.SetAdditionalProperty("Credits", userCredits);
            CreditLogic.OrderTop(e, userCredits, 0);

            //DBG
            foreach (var test in BetPlayerLogic.ServerList)
            {
                Console.WriteLine("Server: " + test.ServerId);
            }
        }

        // Disconnect event to write back credits to database.
        if (e.Type == GameEvent.EventType.Disconnect)
        {
            await _metaService.SetPersistentMeta("Credits", e.Origin.GetAdditionalProperty<int>("Credits").ToString(),
                e.Origin.ClientId);
        }

        // SERVER EVENTS
        // TODO: Fix
        if (e.Type == GameEvent.EventType.MapChange)
        {
            BetPlayerLogic.Timer.Restart();
        }
        // TODO: Fix
        if (e.Type == GameEvent.EventType.Start)
        {
            var serverEntry = new ServerEntry {ServerId = s.GetIdForServer().Result, MapTime = 0};
            BetPlayerLogic.ServerList.Add(serverEntry);
        }
    }

    public async Task OnLoadAsync(IManager manager)
    {
        // Pull top credit data on IW4MAdmin load and deserialise. 
        var topCreditsValue = (await _metaService.GetPersistentMeta("TopCredits")).FirstOrDefault()?.Value;

        CreditLogic.TopCredits = topCreditsValue is null
            ? new List<TopCreditEntry>()
            : JsonSerializer.Deserialize<List<TopCreditEntry>>(topCreditsValue)!;
        Console.WriteLine($"[Credits] Plugin Loaded. Version: {Version}");
    }

    public Task OnTickAsync(Server s) => Task.CompletedTask;

    public async Task OnUnloadAsync()
    {
        // Remove old top credit entry and write updated one.
        await _metaService.RemovePersistentMeta("TopCredits");
        await _metaService.AddPersistentMeta("TopCredits", JsonSerializer.Serialize(CreditLogic.TopCredits));
        Console.WriteLine("[Credits] Plugin Unloaded.");
    }
}