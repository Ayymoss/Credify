using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class SetCreditsCommand : Command
{
    public SetCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "setcredits";
        Description = "Set Credits";
        Alias = "scr";
        Permission = EFClient.Permission.Owner;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Player",
                Required = true
            },
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        };
    }

    public async override Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return;

        var argStr = e.Data.Split(" ");

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            e.Origin.Tell("(Color::Yellow)Error trying to parse second argument.");
            return;
        }

        e.Target = e.Owner.GetClientByName(argStr[0]).FirstOrDefault();

        if (e.Target == null)
        {
            e.Origin.Tell("(Color::Yellow)Error trying to find user.");
            return;
        }

        // Check if target isn't null - Set credits, sort, and tell the origin and target.
        if (e.Target != null)
        {
            e.Target.SetAdditionalProperty("Credits", Math.Abs(argAmount));
            e.Origin.Tell(
                $"Set credits for {e.Target.Name} (Color::White)to (Color::Cyan){Math.Abs(argAmount)}(Color::White).");
            if (e.Origin.ClientId != e.Target.ClientId)
                e.Target.Tell(
                    $"{e.Origin.Name} (Color::White)set your credits to (Color::Cyan){Math.Abs(argAmount)}(Color::White).");
            PrimaryLogic.OrderTop(e, Math.Abs(argAmount), 1);
        }
    }
}