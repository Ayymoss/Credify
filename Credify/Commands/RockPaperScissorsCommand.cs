using Credify.Chat.Active.Core;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class RockPaperScissorsCommand : Command
{
    private readonly PersistenceService _persistence;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly GambleCommandHelper _gamble;

    public RockPaperScissorsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistence = persistenceService;
        _credifyConfig = credifyConfig;
        _gamble = new GambleCommandHelper(persistenceService, credifyConfig);
        Name = "creditsrps";
        Alias = "crrps";
        Description = _credifyConfig.Translations.Gambling.RockPaperScissorsDescription;
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
            gameEvent.Origin.Tell(_credifyConfig.Translations.Gambling.BadRpsArgument);
            return;
        }

        if (await _gamble.TryResolveStakeAsync(gameEvent, userStakeArg, GameConstants.MinimumCredits, 0,
                _credifyConfig.Translations.Core.ErrorParsingSecondArgument) is not { } stake) return;

        long userBalance;
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
                userBalance = await _persistence.GetClientCreditsAsync(gameEvent.Origin);
                message = _credifyConfig.Translations.Gambling.Draw.FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
                break;
            case 1: // User wins
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, stake * 2);
                userBalance = await _persistence.AddCreditsAsync(gameEvent.Origin, stake); // Since money is never taken, this is x2
                message = _credifyConfig.Translations.Gambling.Won
                    .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
                break;
            default: // User loses
                userBalance = await _persistence.RemoveCreditsAsync(gameEvent.Origin, stake);
                message = _credifyConfig.Translations.Gambling.Lost
                    .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
                break;
        }

        gameEvent.Origin.Tell(message);
    }
}
