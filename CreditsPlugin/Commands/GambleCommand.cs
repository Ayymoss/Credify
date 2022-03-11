using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class GambleCommand : Command
{
    public GambleCommand(CommandConfiguration config, ITranslationLookup translationLookup, IMetaService metaService) :
        base(config,
            translationLookup)
    {
        Name = "gamble";
        Alias = "gmb";
        Description = "Gamble Credits";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "0-10",
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
        if (!int.TryParse(argStr[0], out var argOne))
        {
            e.Origin.Tell("Error trying to parse first argument.");
            return;
        }

        if (!int.TryParse(argStr[1], out var argTwo))
        {
            e.Origin.Tell("Error trying to parse second argument.");
            return;
        }
        
        var client = new EFClient {ClientId = e.Origin.ClientId};
        var rand = new Random();
        var randNum = rand.Next(0, 11);

        if (randNum == argOne)
        {
            e.Origin.Tell($"Congratulations, you won {argTwo} tokens!");
            client.SetAdditionalProperty("Credits", client.GetAdditionalProperty<int>("Credits") + argTwo);
            
        }
        else
        {
            e.Origin.Tell($"Unlucky, you lost {argTwo} credits. You chose {argOne}, the number was {randNum}.");
            client.SetAdditionalProperty("Credits", client.GetAdditionalProperty<int>("Credits") - argTwo);
        }
    }
}