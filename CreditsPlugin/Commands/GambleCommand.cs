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

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Type != GameEvent.EventType.Command) return;

        var argStr = gameEvent.Data.Split(" ");
        if (!int.TryParse(argStr[0], out var argRange))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to parse first argument.");
            return;
        }

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to parse second argument.");
            return;
        }

        if (argRange is > 10 or < 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Accepted number range is 0 to 10.");
            return;
        }

        if (argAmount <= 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Minimum amount is 1.");
            return;
        }

        if (!Plugin.PrimaryLogic.AvailableFunds(gameEvent.Origin, argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Insufficient credits.");
            return;
        }

        var rand = new Random();
        var randNum = rand.Next(0, 11);
        var currentCredits = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey);

        if (randNum == argRange)
        {
            currentCredits += argAmount * 10;
            gameEvent.Origin.Tell($"Congratulations, you won (Color::Cyan){argAmount * 10:N0} (Color::White)tokens!");
        }
        else
        {
            currentCredits -= argAmount;
            gameEvent.Origin.Tell(
                $"Unlucky, you lost (Color::Cyan){argAmount:N0} (Color::White)credits. You chose (Color::Cyan){argRange}(Color::White), the number was (Color::Cyan){randNum}(Color::White).");
        }

        gameEvent.Origin.SetAdditionalProperty(Plugin.CreditsKey, currentCredits);
        Plugin.PrimaryLogic.OrderTop(gameEvent.Origin, currentCredits);
    }
}
