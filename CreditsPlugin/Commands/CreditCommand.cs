using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class CreditCommand : Command
{
    public CreditCommand(CommandConfiguration config, ITranslationLookup translationLookup, IMetaService metaService) :
        base(config,
            translationLookup)
    {
        Name = "credits";
        Description = "Check your credits.";
        Alias = "cr";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }
    

    public override Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return Task.CompletedTask;
        
        var client = new EFClient {ClientId = e.Origin.ClientId};
        e.Origin.Tell($"You have {client.GetAdditionalProperty<int>("Credits")} credits.");
        
        return Task.CompletedTask;
    }
}