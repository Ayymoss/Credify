using Credify.Chat.Passive.Quests.Enums;
using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Models;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class RockPaperScissorsCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;

    public RockPaperScissorsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
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

        if (!rpsLookup.TryGetValue(userRpsArg.ToLower(), out var playerChoice))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BadRpsArgument);
            return;
        }

        var userBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
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

        if (!PersistenceService.AvailableFunds(gameEvent.Origin, stake))
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
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, stake * 2);
                userBalance = await _persistenceService.AddCreditsAsync(gameEvent.Origin, stake); // Since money is never taken, this is x2
                message = _credifyConfig.Translations.Core.GambleWon
                    .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
                break;
            default: // User loses
                userBalance = await _persistenceService.RemoveCreditsAsync(gameEvent.Origin, stake);
                message = _credifyConfig.Translations.Core.GambleLost
                    .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
                break;
        }

        gameEvent.Origin.Tell(message);
    }
}
