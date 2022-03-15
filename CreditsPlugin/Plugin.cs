using System.Text.Json.Nodes;
using CreditsPlugin.Commands;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using Newtonsoft.Json;
using Serilog.Core;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CreditsPlugin;

// TODO: Make Top Credits
// TODO: Get Players Credits
// TODO: Bet on Player to Win (Dynamic Payout based on ELO?)

public class Plugin : IPlugin
{
    public Plugin(IMetaService metaService)
    {
        _metaService = metaService;
    }

    private readonly IMetaService _metaService;


    public string Name => "Credits";
    public float Version => 1.1f;
    public string Author => "Amos";


    public async Task OnEventAsync(GameEvent e, Server s)
    {
        // Join event to check if the user has any credits, if new user, set to 0.
        if (e.Type == GameEvent.EventType.Join)
        {
            var userCredits = (await _metaService.GetPersistentMeta("Credits", e.Origin))?.Value ?? "0";

            e.Origin.SetAdditionalProperty("Credits", int.Parse(userCredits));

            e.Origin.Tell($"You have (Color::Blue){userCredits} (Color::White)credits.");
        }

        // Kill event +1 Credit on Kill - Check if in Top and Sort.
        if (e.Type == GameEvent.EventType.Kill)
        {
            var currentCredits = e.Origin.GetAdditionalProperty<int>("Credits");
            currentCredits++;
            e.Origin.SetAdditionalProperty("Credits", currentCredits);
            TopCreditsLogic.OriginOrderTop(e, currentCredits);
        }

        // Disconnect event to write back credits to database.
        if (e.Type == GameEvent.EventType.Disconnect)
        {
            await _metaService.SetPersistentMeta("Credits", e.Origin.GetAdditionalProperty<int>("Credits").ToString(),
                e.Origin.ClientId);
        }
    }

    public async Task OnLoadAsync(IManager manager)
    {
        // Pull top credit data on IW4MAdmin load and deserialise. 
        var topCreditsValue = (await _metaService.GetPersistentMeta("TopCredits")).FirstOrDefault()?.Value;

        TopCreditsLogic.TopCredits = topCreditsValue is null
            ? new List<TopCreditEntry>()
            : JsonSerializer.Deserialize<List<TopCreditEntry>>(topCreditsValue)!;
    }

    public Task OnTickAsync(Server s) => Task.CompletedTask;

    public async Task OnUnloadAsync()
    {
        // Remove old top credit entry and write updated one.
        await _metaService.RemovePersistentMeta("TopCredits");
        await _metaService.AddPersistentMeta("TopCredits", JsonSerializer.Serialize(TopCreditsLogic.TopCredits));
    }
}