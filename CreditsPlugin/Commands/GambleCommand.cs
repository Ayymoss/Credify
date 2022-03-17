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
        Description = "Gamble Credits - Payout = Amount * 10";
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
        if (!int.TryParse(argStr[0], out var argRange))
        {
            e.Origin.Tell("(Color::Yellow)Error trying to parse first argument.");
            return;
        }

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            e.Origin.Tell("(Color::Yellow)Error trying to parse second argument.");
            return;
        }

        if (argRange is > 10 or < 0)
        {
            e.Origin.Tell("(Color::Yellow)Accepted number range is 0 to 10.");
            return;
        }

        if (argAmount <= 0)
        {
            e.Origin.Tell("(Color::Yellow)Minimum amount is 1.");
            return;
        }

        if (CreditLogic.AvailableFunds(e, argAmount))
        {
            e.Origin.Tell("(Color::Yellow)Insufficient credits.");
            return;
        }

        var rand = new Random();
        var randNum = rand.Next(0, 11);
        var currentCredits = e.Origin.GetAdditionalProperty<int>("Credits");

        if (randNum == argRange)
        {
            currentCredits += argAmount * 10;
            e.Origin.Tell($"Congratulations, you won (Color::Cyan){argAmount * 10} (Color::White)tokens!");
        }
        else
        {
            currentCredits -= argAmount;
            e.Origin.Tell(
                $"Unlucky, you lost (Color::Cyan){argAmount} (Color::White)credits. You chose (Color::Cyan){argRange}(Color::White), the number was (Color::Cyan){randNum}(Color::White).");
        }

        e.Origin.SetAdditionalProperty("Credits", currentCredits);
        CreditLogic.OrderTop(e, currentCredits, 0);
    }
}