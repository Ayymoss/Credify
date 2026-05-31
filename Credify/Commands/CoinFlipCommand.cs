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
public class CoinFlipCommand : Command
{
    private readonly PersistenceService _persistence;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly GambleCommandHelper _gamble;

    public CoinFlipCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceService persistenceService,
        CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistence = persistenceService;
        _credifyConfig = credifyConfig;
        _gamble = new GambleCommandHelper(persistenceService, credifyConfig);
        Name = "creditcf";
        Alias = "crcf";
        Description = _credifyConfig.Translations.Gambling.CoinFlipDescription;
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
            gameEvent.Origin.Tell(_credifyConfig.Translations.Gambling.BadCfArgument);
            return;
        }

        if (await _gamble.TryResolveStakeAsync(gameEvent, userStakeArg, GameConstants.MinimumCredits, 0,
                _credifyConfig.Translations.Core.ErrorParsingSecondArgument) is not { } stake) return;

        var computerChoice = Random.Shared.Next(2);
        string message;
        long userBalance;

        if (computerChoice == playerChoice)
        {
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, stake * 2);
            userBalance = await _persistence.AddCreditsAsync(gameEvent.Origin, stake); // Since money is never taken, this is x2
            message = _credifyConfig.Translations.Gambling.Won
                .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
        }
        else
        {
            userBalance = await _persistence.RemoveCreditsAsync(gameEvent.Origin, stake);
            message = _credifyConfig.Translations.Gambling.Lost
                .FormatExt(stake.ToString("N0"), userBalance.ToString("N0"));
        }

        gameEvent.Origin.Tell(message);
    }
}
