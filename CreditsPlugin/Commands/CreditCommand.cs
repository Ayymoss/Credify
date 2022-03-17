using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class CreditCommand : Command
{
    public CreditCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "credits";
        Description = "Check your credits.";
        Alias = "cr";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Player",
                Required = false
            }
        };
    }

    public async override Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return;

        // Get argument from command.
        var argPlayer = e.Data;
        e.Target = e.Owner.GetClientByName(argPlayer).FirstOrDefault();

        // Check for valid target.
        if (e.Data.Length != 0 && e.Target == null)
        {
            e.Origin.Tell("(Color::Yellow)Error trying to find user.");
            return;
        }

        // Return player's credits
        if (e.Target != null)
        {
            e.Origin.Tell(
                $"{e.Target.Name} (Color::White)has (Color::Cyan){e.Target.GetAdditionalProperty<int>("Credits")} (Color::White)credits.");
            return;
        }

        // If no target specified
        e.Origin.Tell($"You have (Color::Cyan){e.Origin.GetAdditionalProperty<int>("Credits")} (Color::White)credits.");
    }
}