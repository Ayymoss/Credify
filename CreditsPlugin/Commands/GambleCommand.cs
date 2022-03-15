using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class GambleCommand : Command
{
    public GambleCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
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
            e.Origin.Tell("(Color::Red)Error trying to parse first argument.");
            return;
        }

        if (!int.TryParse(argStr[1], out var argTwo))
        {
            e.Origin.Tell("(Color::Red)Error trying to parse second argument.");
            return;
        }

        if (CreditCheck.LessThanZero(argTwo))
        {
            e.Origin.Tell("(Color::Red)Minimum amount is 1.");
            return;
        }

        if (CreditCheck.AvailableFunds(e.Origin, argTwo))
        {
            e.Origin.Tell("(Color::Red)Insufficient funds...");
            return;
        }

        var rand = new Random();
        var randNum = rand.Next(0, 11);
        var currentCredits = e.Origin.GetAdditionalProperty<int>("Credits");

        if (randNum == argOne)
        {
            currentCredits += argTwo;
            e.Origin.Tell($"Congratulations, you won {argTwo} tokens!");
            e.Origin.SetAdditionalProperty("Credits", currentCredits);
            TopCreditsLogic.OriginOrderTop(e, currentCredits);
        }
        else
        {
            currentCredits -= argTwo;
            e.Origin.Tell($"Unlucky, you lost {argTwo} credits. You chose {argOne}, the number was {randNum}.");
            e.Origin.SetAdditionalProperty("Credits", currentCredits);
            TopCreditsLogic.OriginOrderTop(e, currentCredits);
        }
    }
}