using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace CreditsPlugin.Commands;

public class BetPlayerCommand : Command
{
    public BetPlayerCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "betplayer";
        Alias = "betp";
        Description = "Bet on a Player\'s Win - Can only do within first minute of game.";
        Permission = EFClient.Permission.User;
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

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Type != GameEvent.EventType.Command) return;

        var argStr = gameEvent.Data.Split(" ");

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to parse second argument.");
            return;
        }

        gameEvent.Target = gameEvent.Owner.GetClientByName(argStr[0]).FirstOrDefault();

        if (gameEvent.Target == null)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to find user.");
            return;
        }

        if (argAmount <= 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Minimum amount is 1.");
            return;
        }

        if (!Plugin.PrimaryLogic!.AvailableFunds(gameEvent.Origin, argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Insufficient credits.");
            return;
        }

        if (!await Plugin.BetManager!.CanBet(gameEvent.Origin))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Player bets are only accepted for the first 2 minutes of the map.");
            return;
        }

        if (gameEvent.Target != null)
        {
            Plugin.BetManager.OnBetCreated(gameEvent, argAmount);
        }
    }
}
