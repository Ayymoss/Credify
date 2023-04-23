using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class BetTeamCommand : Command
{
    private readonly BetManager _betManager;
    private readonly BetLogic _betLogic;
    private readonly CredifyConfiguration _credifyConfig;

    public BetTeamCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetManager betManager, BetLogic betLogic,
        CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _betManager = betManager;
        _betLogic = betLogic;
        _credifyConfig = credifyConfig;
        Name = "betteam";
        Alias = "bett";
        Description = credifyConfig.Translations.CommandBetOnTeamWinDescription;
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
        var argStr = gameEvent.Data.Split(" ");
        var teamList = new List<string> {"axis", "allies", "t", "ct", "red", "blue"};

        if (!teamList.Contains(argStr[0]))
        {
            await gameEvent.Origin.TellAsync(new[]
            {
                _credifyConfig.Translations.UnknownTeam,
                _credifyConfig.Translations.YourTeam.FormatExt(_betManager.TeamEnumToString(gameEvent.Origin.Team)),
                _credifyConfig.Translations.OtherTeams.FormatExt(string.Join(", ", teamList))
            });

            return;
        }

        if (argStr[1] == "all")
        {
            argStr[1] = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey).ToString();
        }

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingAmount);
            return;
        }

        if (argAmount <= 0)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumAmountIsOne);
            return;
        }

        if (!_betLogic.AvailableFunds(gameEvent.Origin, argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        if (!_betManager.MaximumTimePassed(gameEvent.Origin))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCreditsForBet
                .FormatExt(_betManager.CreditsBetWindow.Humanize()));
            return;
        }

        if (!_betManager.MinimumPlayers(gameEvent.Origin))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.BetWindowRestriction
                .FormatExt(_credifyConfig.MinimumPlayersRequiredForPlayerAndTeamBets));
            return;
        }

        var teamName = string.Empty;
        if (argStr[0] == teamList[0] || argStr[0] == teamList[2] || argStr[0] == teamList[4])
        {
            teamName = _betManager.TeamEnumToString(EFClient.TeamType.Axis);
        }

        if (argStr[0] == teamList[1] || argStr[0] == teamList[3] || argStr[0] == teamList[5])
        {
            teamName = _betManager.TeamEnumToString(EFClient.TeamType.Allies);
        }

        _betManager.CreateTeamBet(gameEvent, teamName, argAmount);
    }
}
