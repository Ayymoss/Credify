using Data.Abstractions;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
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
        RequiresTarget = true;
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

        if (!BetPlayerLogic.CanBet())
        {
            e.Origin.Tell("Player bets are only accepted for the first 2 minutes of the map.");
        }

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
            e.Origin.Tell("Not implemented.");
        }
    }
}