using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class BetPlayerCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly BetManager _betManager;
    private readonly CredifyConfiguration _credifyConfig;

    public BetPlayerCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceManager persistenceManager, BetManager betManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _betManager = betManager;
        _credifyConfig = credifyConfig;
        Name = "credifybetplayer";
        Alias = "crbp";
        Description = credifyConfig.Translations.CommandBetPlayerDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = true;
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
        var amount = gameEvent.Data;

        if (amount == "all")
        {
            var allCredits = await _persistenceManager.GetClientCredits(gameEvent.Origin);
            amount = allCredits.ToString();
        }

        if (!uint.TryParse(amount, out var argAmount))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
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
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumPlayersNeeded
                .FormatExt(_credifyConfig.Core.MinimumPlayersRequiredForPlayerAndTeamBets));
            return;
        }

        if (gameEvent.Target != null) await _betManager.CreatePlayerBet(gameEvent, argAmount);
    }
}
