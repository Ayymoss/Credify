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
        var teamName = "Unknown";
        var teamList = new List<string> {"axis", "allies", "t", "ct", "red", "blue"};

        if (!teamList.Contains(argStr[0]))
        {
            await gameEvent.Origin.TellAsync(new[]
            {
                "(Color::Yellow)Unknown Team",
                $"Your Team: {Plugin.BetManager.TeamEnumToString(gameEvent.Origin.Team)}",
                $"Other Teams: {string.Join(", ", teamList)}"
            });

            return;
        }

        if (argStr[1] == "all")
        {
            argStr[1] = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey).ToString();
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

        //if (!Plugin.BetManager.MaximumTimePassed(gameEvent.Origin))
        //{
        //    gameEvent.Origin.Tell(
        //        $"(Color::Yellow)Bets only accepted during first {Plugin.CreditsMaximumBetTime} minutes");
        //    return;
        //}

        //if (!Plugin.BetManager.MinimumPlayers(gameEvent.Origin))
        //{
        //    gameEvent.Origin.Tell($"(Color::Yellow){Plugin.CreditsMinimumPlayers} players minimum are needed to bet");
        //    return;
        //}

        if (argStr[0] == teamList[0] ||
            argStr[0] == teamList[2] ||
            argStr[0] == teamList[4])
        {
            teamName = Plugin.BetManager.TeamEnumToString(EFClient.TeamType.Axis);
        }

        if (argStr[0] == teamList[1] ||
            argStr[0] == teamList[3] ||
            argStr[0] == teamList[5])
        {
            teamName = Plugin.BetManager.TeamEnumToString(EFClient.TeamType.Allies);
        }


        Plugin.BetManager.CreateTeamBet(gameEvent, teamName, argAmount);
    }
}
