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
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public BetTeamCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetManager betManager,
        PersistenceManager persistenceManager,
        CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _betManager = betManager;
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "credifybetteam";
        Alias = "crbt";
        Description = credifyConfig.Translations.CommandBetOnTeamWinDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Axis | Allies",
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
        var teamList = new Dictionary<string, EFClient.TeamType>
        {
            {"axis", EFClient.TeamType.Axis},
            {"allies", EFClient.TeamType.Allies},
            {"t", EFClient.TeamType.Axis},
            {"ct", EFClient.TeamType.Allies},
            {"red", EFClient.TeamType.Axis},
            {"blue", EFClient.TeamType.Allies}
        };

        if (!teamList.TryGetValue(argStr[0], out var selectedTeam))
        {
            await gameEvent.Origin.TellAsync(new[]
            {
                _credifyConfig.Translations.UnknownTeam,
                _credifyConfig.Translations.YourTeam.FormatExt(_betManager.TeamEnumToString(gameEvent.Origin.Team)),
                _credifyConfig.Translations.OtherTeams.FormatExt(string.Join(", ", teamList.Keys))
            });

            return;
        }

        if (argStr[1] == "all")
        {
            var allCredits = await _persistenceManager.GetClientCreditsAsync(gameEvent.Origin);
            argStr[1] = allCredits.ToString();
        }

        if (!long.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingAmount);
            return;
        }

        if (argAmount <= 0)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumAmountIsOne);
            return;
        }

        if (!_persistenceManager.AvailableFunds(gameEvent.Origin, argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        if (!_betManager.MaximumTimePassed(gameEvent.Origin))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.BetWindowRestriction
                .FormatExt(_credifyConfig.Core.TeamPlayerBetWindow.Humanize()));
            return;
        }

        if (!_betManager.MinimumPlayers(gameEvent.Origin))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.BetWindowRestriction
                .FormatExt(_credifyConfig.Core.MinimumPlayersRequiredForPlayerAndTeamBets));
            return;
        }

        var teamName = _betManager.TeamEnumToString(selectedTeam);
        _betManager.CreateTeamBet(gameEvent, teamName, argAmount);
    }
}
