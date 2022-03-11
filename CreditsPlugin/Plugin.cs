using Data.Models.Client;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using EFClient = SharedLibraryCore.Database.Models.EFClient;

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
            var client = new EFClient {ClientId = e.Origin.ClientId};
            if (_metaService.GetPersistentMeta("Credits", client) == null)
            {
                await _metaService.SetPersistentMeta("Credits", "0", e.Origin.ClientId);
            }

            client.SetAdditionalProperty("Credits", int.Parse(_metaService.GetPersistentMeta("Credits", client).Result.Value));

            e.Origin.Tell($"You have {client.GetAdditionalProperty<int>("Credits")} credits.");
        }
        
        if (e.Type == GameEvent.EventType.Kill)
        {
            var client = new EFClient {ClientId = e.Origin.ClientId};
            client.SetAdditionalProperty("Credits", client.GetAdditionalProperty<int>("Credits") + 1);
        }

        if (e.Type == GameEvent.EventType.Disconnect)
        {
            var client = new EFClient {ClientId = e.Origin.ClientId};
            await _metaService.SetPersistentMeta("Credits", client.GetAdditionalProperty<int>("Credits").ToString(), e.Origin.ClientId);
        }
    }

    public Task OnLoadAsync(IManager manager) => Task.CompletedTask;

    public Task OnTickAsync(Server s) => Task.CompletedTask;

    public Task OnUnloadAsync() => Task.CompletedTask;
}