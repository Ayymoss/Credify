using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class SetCreditsCommand : Command
{
    public SetCreditsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        IMetaService metaService) :
        base(config,
            translationLookup)
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

    public override async Task ExecuteAsync(GameEvent e)
    {
        if (e.Type != GameEvent.EventType.Command) return;

        var argStr = e.Data.Split(" ");

        if (!int.TryParse(argStr[1], out var argTwo))
        {
            e.Origin.Tell("Error trying to parse second argument.");
            return;
        }

        e.Target = e.Owner.GetClientByName(argStr[0]).FirstOrDefault();

        if (e.Target == null)
        {
            e.Origin.Tell("Error trying to find user.");
            return;
        }

        //TODO: Figure out why it's not storing or retrieving credit values.
        if (e.Target != null)
        {
            var client = new EFClient {ClientId = e.Target.ClientId};
            client.SetAdditionalProperty("Credits", argTwo);
            e.Origin.Tell($"You have given {e.Target.Name} {argTwo} credits.");
        }
    }
}