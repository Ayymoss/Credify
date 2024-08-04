using Credify.Configuration;
using Credify.Models;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class RockPaperScissorsCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public RockPaperScissorsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceManager persistenceManager, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "creditsrps";
        Alias = "crrps";
        Description = credifyConfig.Translations.Core.CommandRockPaperScissorsDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Rock | Paper | Scissors",
                Required = true
            },
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var args = gameEvent.Data.Split(" ");
        var userRpsArg = args[0];
        var userStakeArg = args[1];

        var rpsLookup = new Dictionary<string, int>
        {
            { "r", 0 },
            { "p", 1 },
            { "s", 2 },
            { "rock", 0 },
            { "paper", 1 },
            { "scissors", 2 }
        };

        if (!rpsLookup.TryGetValue(userRpsArg, out var playerChoice))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BadRpsArgument);
            return;
        }

        var userBalance = await _persistenceManager.GetClientCreditsAsync(gameEvent.Origin);
        if (userStakeArg.Equals("all"))
        {
            userStakeArg = userBalance.ToString();
        }

        if (!long.TryParse(userStakeArg, out var stake))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingSecondArgument);
            return;
        }

        if (stake < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.MinimumAmount);
            return;
        }

        if (!PersistenceManager.AvailableFunds(gameEvent.Origin, stake))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        var outcomeMatrix = new[,]
        {
            //R, P, S
            { 0, 2, 1 }, // Rock
            { 1, 0, 2 }, // Paper
            { 2, 1, 0 } // Scissors
        };

        var computerChoice = Random.Shared.Next(3);
        var outcome = outcomeMatrix[playerChoice, computerChoice];

        string message;

        switch (outcome)
        {
            case 0: // Tie
                message = _credifyConfig.Translations.Core.GambleDraw.FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
                break;
            case 1: // User wins
                await _persistenceManager.AddCreditsAsync(gameEvent.Origin, stake);
                message = _credifyConfig.Translations.Core.GambleWon
                    .FormatExt(stake.ToString("N0"), (userBalance + stake).ToString("N0"));
                break;
            default: // User loses
                var newBalance = await _persistenceManager.RemoveCreditsAsync(gameEvent.Origin, stake);
                message = _credifyConfig.Translations.Core.GambleLost
                    .FormatExt(stake.ToString("N0"), newBalance.ToString("N0"));
                break;
        }

        gameEvent.Origin.Tell(message);
    }
}
