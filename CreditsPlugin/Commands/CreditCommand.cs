using SharedLibraryCore;
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
    }


    public override Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return Task.CompletedTask;

        e.Origin.Tell($"You have (Color::Blue){e.Origin.GetAdditionalProperty<int>("Credits")} (Color::White)credits.");

        return Task.CompletedTask;
    }
}