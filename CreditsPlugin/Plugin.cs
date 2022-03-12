using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

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

            e.Origin.Tell($"You have {e.Origin.GetAdditionalProperty<int>("Credits")} credits.");
        }

        if (e.Type == GameEvent.EventType.Kill)
        {
            e.Origin.SetAdditionalProperty("Credits", e.Origin.GetAdditionalProperty<int>("Credits") + 1);
        }

        if (e.Type == GameEvent.EventType.Disconnect)
        {
            await _metaService.SetPersistentMeta("Credits", e.Origin.GetAdditionalProperty<int>("Credits").ToString(),
                e.Origin.ClientId);
        }
    }

    public async Task OnLoadAsync(IManager manager)
    {
        if (_metaService.GetPersistentMeta("TopCredits") == null)
        {
            await _metaService.AddPersistentMeta("TopCredits", "");
        }
        TopCredits.LoadTopCredits(_metaService.GetPersistentMeta("TopCredits").ToString()!);
        
        // TODO: Top stats logic 
        // Create constructor in top stats cs file
        // Pull from database on load - write data to constructor
        // compare constructor data when players credits change
        // If more, compare and reorder - write change 
    }

    public Task OnTickAsync(Server s) => Task.CompletedTask;

    public Task OnUnloadAsync() => Task.CompletedTask;
}