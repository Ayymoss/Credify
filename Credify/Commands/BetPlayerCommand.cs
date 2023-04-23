using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class BetPlayerCommand : Command
{
    private readonly BetLogic _betLogic;
    private readonly BetManager _betManager;
    private readonly CredifyConfiguration _credifyConfig;

    public BetPlayerCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetLogic betLogic, BetManager betManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _betLogic = betLogic;
        _betManager = betManager;
        _credifyConfig = credifyConfig;
        Name = "betplayer";
        Alias = "betp";
        Description = credifyConfig.Translations.CommandBetPlayerDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
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

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var argStr = gameEvent.Data.Split(" ");

        if (argStr[1] == "all")
        {
            argStr[1] = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey).ToString();
        }

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return;
        }

        gameEvent.Target = gameEvent.Owner.GetClientByName(argStr[0]).FirstOrDefault();

        if (gameEvent.Target == null)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorFindingTargetUser);
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
            gameEvent.Origin.Tell(_credifyConfig.Translations.BetWindowRestriction
                .FormatExt(_betManager.CreditsBetWindow.Humanize()));
            return;
        }

        if (!_betManager.MinimumPlayers(gameEvent.Origin))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumPlayersNeeded
                .FormatExt(_credifyConfig.MinimumPlayersRequiredForPlayerAndTeamBets));
            return;
        }

        if (gameEvent.Target != null)
        {
            await _betManager.CreatePlayerBet(gameEvent, argAmount);
        }
    }
}
