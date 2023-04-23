using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class GambleCommand : Command
{
    private readonly BetLogic _betLogic;
    private readonly CredifyConfiguration _credifyConfig;

    public GambleCommand(CommandConfiguration config, ITranslationLookup translationLookup, BetLogic betLogic,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _betLogic = betLogic;
        _credifyConfig = credifyConfig;
        Name = "gamble";
        Alias = "gmb";
        Description = credifyConfig.Translations.CommandGambleCreditsDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "1 to 10",
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

        if (!int.TryParse(argStr[0], out var argUserChoice))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingFirstArgument);
            return;
        }

        if (argStr[1] == "all")
        {
            argStr[1] = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey).ToString();
        }

        if (!int.TryParse(argStr[1], out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return;
        }

        if (argUserChoice is > 10 or < 1)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.AcceptedRange);
            return;
        }

        if (argAmount <= 0)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumAmount);
            return;
        }

        if (!_betLogic.AvailableFunds(gameEvent.Origin, argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        var randNum = Random.Shared.Next(1, 11);
        var currentCredits = gameEvent.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey);

        if (randNum == argUserChoice)
        {
            currentCredits += argAmount * _credifyConfig.GambleMultiplier;
            gameEvent.Origin.Tell(_credifyConfig.Translations.GambleWon.FormatExt($"{argAmount * _credifyConfig.GambleMultiplier:N0}"));
            await gameEvent.Origin.CurrentServer.BroadcastAsync(new[]
            {
                _credifyConfig.Translations.GambleWonAnnouncement.FormatExt(Plugin.PluginName, gameEvent.Origin.Name,
                    $"{argAmount * _credifyConfig.GambleMultiplier:N0}"),
                _credifyConfig.Translations.GambleWonInstructions.FormatExt(Plugin.PluginName, "!gmb")
            }, Utilities.IW4MAdminClient());
            _betLogic.StatisticsState.CreditsSpent += argAmount;
            _betLogic.StatisticsState.CreditsPaid += argAmount + argAmount * _credifyConfig.GambleMultiplier;
        }
        else
        {
            currentCredits -= argAmount;
            await gameEvent.Origin.TellAsync(new[]
            {
                _credifyConfig.Translations.GambleLost.FormatExt($"{argAmount:N0}"),
                _credifyConfig.Translations.GambleChoices.FormatExt(argUserChoice, randNum)
            });
            _betLogic.StatisticsState.CreditsSpent += argAmount;
        }

        gameEvent.Origin.SetAdditionalProperty(Plugin.CreditsKey, currentCredits);
        _betLogic.OrderTop(gameEvent.Origin, currentCredits);
    }
}
