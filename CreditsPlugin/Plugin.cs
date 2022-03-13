using System.Text.Json.Nodes;
using CreditsPlugin.Commands;
using Microsoft.EntityFrameworkCore;
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
        if (e.Type == GameEvent.EventType.Join)
        {
            if (_metaService.GetPersistentMeta("Credits", e.Origin) == null)
            {
                await _metaService.SetPersistentMeta("Credits", "0", e.Origin.ClientId);
            }

            e.Origin.SetAdditionalProperty("Credits",
                int.Parse(_metaService.GetPersistentMeta("Credits", e.Origin).Result.Value));

            e.Origin.Tell(
                $"You have (Color::Blue){e.Origin.GetAdditionalProperty<int>("Credits")} (Color::White)credits.");
        }

        // +1 Credit on Kill - Check if in Top and Sort.
        if (e.Type == GameEvent.EventType.Kill)
        {
            var currentCredits = e.Origin.GetAdditionalProperty<int>("Credits");
            currentCredits++;
            e.Origin.SetAdditionalProperty("Credits", currentCredits);
            TopCreditsLogic.OriginOrderTop(e, currentCredits);
        }

        if (e.Type == GameEvent.EventType.Disconnect)
        {
            await _metaService.SetPersistentMeta("Credits", e.Origin.GetAdditionalProperty<int>("Credits").ToString(),
                e.Origin.ClientId);
        }
    }

    public async Task OnLoadAsync(IManager manager)
    {
        var topCreditsValue = (await _metaService.GetPersistentMeta("TopCredits")).FirstOrDefault()?.Value;

        TopCreditsLogic.TopCredits = topCreditsValue is null
            ? new List<TopCreditEntry>()
            : JsonSerializer.Deserialize<List<TopCreditEntry>>(topCreditsValue)!;
    }

    public Task OnTickAsync(Server s) => Task.CompletedTask;

    public async Task OnUnloadAsync()
    {
        await _metaService.RemovePersistentMeta("TopCredits");
        await _metaService.AddPersistentMeta("TopCredits", JsonSerializer.Serialize(TopCreditsLogic.TopCredits));
    }
}