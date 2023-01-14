using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;


namespace CreditsPlugin.Commands;

public class GambleCommand : Command
{
    public GambleCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "gamble";
        Alias = "gmb";
        Description = "Gamble Credits - Payout = 7x";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "1 to 10",
                Required = true
            },
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        };
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Type != GameEvent.EventType.Command) return Task.CompletedTask;

        var argStr = gameEvent.Data.Split(" ");

        if (!int.TryParse(argStr[0], out var argUserChoice))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to parse first argument");
            return Task.CompletedTask;
        }

        if (argStr[1] == "all")
        {
            argStr[1] = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey).ToString();
        }

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to parse second argument");
            return Task.CompletedTask;
        }

        if (argUserChoice is > 10 or < 1)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Accepted range is 1 to 10");
            return Task.CompletedTask;
        }

        if (argAmount <= 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Minimum amount is 1");
            return Task.CompletedTask;
        }

        if (!Plugin.PrimaryLogic.AvailableFunds(gameEvent.Origin, argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Insufficient credits");
            return Task.CompletedTask;
        }

        var rand = new Random();
        var randNum = rand.Next(1, 11);
        var currentCredits = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey);

        if (randNum == argUserChoice)
        {
            currentCredits += argAmount * 8;
            gameEvent.Origin.Tell($"You won (Color::Cyan){argAmount * 8:N0} (Color::White)tokens!");
            Plugin.PrimaryLogic.StatisticsState.CreditsSpent += argAmount;
            Plugin.PrimaryLogic.StatisticsState.CreditsPaid += argAmount + argAmount * 8;
        }
        else
        {
            currentCredits -= argAmount;
            gameEvent.Origin.TellAsync(new []
                {
                    $"You lost (Color::Cyan){argAmount:N0} (Color::White)credits", 
                    $"You chose (Color::Cyan){argUserChoice}(Color::White), the number was (Color::Cyan){randNum}(Color::White)"
                });
            Plugin.PrimaryLogic.StatisticsState.CreditsSpent += argAmount;
        }

        gameEvent.Origin.SetAdditionalProperty(Plugin.CreditsKey, currentCredits);
        Plugin.PrimaryLogic.OrderTop(gameEvent.Origin, currentCredits);
        return Task.CompletedTask;
    }
}
