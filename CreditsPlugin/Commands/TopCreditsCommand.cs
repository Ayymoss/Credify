using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class TopCreditsCommand : Command
{
    public TopCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "topcredits";
        Alias = "topcr";
        Description = "List top 5 players with most credits.";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }
    
    public override Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return Task.CompletedTask;
        
        
        //TODO: Implement command logic
        e.Origin.Tell("Command Not Implemented...");


        return Task.CompletedTask;
    }
}