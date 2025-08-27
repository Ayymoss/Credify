using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class CoinFlipCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;

    public CoinFlipCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceService persistenceService,
        CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        Name = "creditcf";
        Alias = "crcf";
        Description = credifyConfig.Translations.Core.CommandCoinFlipDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "H | T",
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
            { "heads", 0 },
            { "tails", 1 },
            { "h", 0 },
            { "t", 1 },
        };

        if (!rpsLookup.TryGetValue(userRpsArg.ToLower(), out var playerChoice))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BadCfArgument);
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

        var computerChoice = Random.Shared.Next(2);
        string message;

        if (computerChoice == playerChoice)
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, stake * 2);
            userBalance = await _persistenceService.AddCreditsAsync(gameEvent.Origin, stake); // Since money is never taken, this is x2
            message = _credifyConfig.Translations.Core.GambleWon
                .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
        }
        else
        {
            userBalance = await _persistenceService.RemoveCreditsAsync(gameEvent.Origin, stake);
            message = _credifyConfig.Translations.Core.GambleLost
                .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
        }

        gameEvent.Origin.Tell(message);
    }
}
