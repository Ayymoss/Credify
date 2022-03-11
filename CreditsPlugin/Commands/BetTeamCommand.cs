using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class BetTeamCommand : Command
{
    public BetTeamCommand(CommandConfiguration config, ITranslationLookup translationLookup, IMetaService metaService) :
        base(config,
            translationLookup)
    {
        Name = "betteam";
        Alias = "bett";
        Description = "Bet on a Team\'s Win - Can only do within first minute of game.";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Team",
                Required = true
            },
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        };
    }
    
    public override Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return Task.CompletedTask;

        //TODO: Implement command logic
        e.Origin.Tell("Command Not Implemented...");

        return Task.CompletedTask;
    }
}