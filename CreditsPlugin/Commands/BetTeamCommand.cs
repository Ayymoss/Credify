using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;

namespace CreditsPlugin.Commands;

public class BetTeamCommand : Command
{
    public BetTeamCommand(CommandConfiguration config, ITranslationLookup translationLookup) :
        base(config, translationLookup)
    {
        Name = "betteam";
        Alias = "bett";
        Description = "Bet on a Team\'s Win - Can only do within first minute of game.";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Team",
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
        var teamType = EFClient.TeamType.Unknown;
        var teamList = new List<string> {"axis", "allies", "t", "ct"};

        if (!teamList.Contains(argStr[0]))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to parse team");
            return;
        }
        
        if (argStr[1] == "all")
        {
            argStr[1] = gameEvent.Origin.GetAdditionalProperty<string>(Plugin.CreditsKey);
        }
        
        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Error trying to parse amount");
            return;
        }
        
        if (argAmount <= 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Minimum amount is 1");
            return;
        }

        if (!Plugin.PrimaryLogic.AvailableFunds(gameEvent.Origin, argAmount))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Insufficient credits");
            return;
        }

        if (!await Plugin.BetManager.CanBet(gameEvent.Origin))
        {
            gameEvent.Origin.Tell("(Color::Yellow)Player bets are only accepted for the first 2 minutes of the map");
            return;
        }
        
        if (argStr[0] == teamList[0] || argStr[0] == teamList[2]) teamType = EFClient.TeamType.Axis;
        if (argStr[0] == teamList[1] || argStr[0] == teamList[3]) teamType = EFClient.TeamType.Allies;

        Plugin.BetManager.CreateTeamBet(gameEvent, teamType, argAmount);
    }
}
