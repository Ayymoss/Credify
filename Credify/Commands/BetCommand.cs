using Credify.Models;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class BetCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public BetCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceManager persistenceManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "credifybet";
        Alias = "crbet";
        Description = credifyConfig.Translations.CommandGambleCreditsDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        };
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var initialStakeArg = gameEvent.Data;

        if (initialStakeArg == "all")
        {
            var allCredits = await _persistenceManager.GetClientCredits(gameEvent.Origin);
            initialStakeArg = allCredits.ToString();
        }

        if (!long.TryParse(initialStakeArg, out var initialStake))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return;
        }

        if (initialStake < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumAmount);
            return;
        }

        if (!_persistenceManager.AvailableFunds(gameEvent.Origin, initialStake))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        var grossProfit = 0L;
        var result = GambleResult.Loss;
        var randomNumber = Random.Shared.Next(0, 101);
        switch (randomNumber)
        {
            case <= 50:
                result = GambleResult.Loss;
                break;
            case <= 55:
                result = GambleResult.Draw;
                grossProfit = initialStake;
                break;
            case <= 90:
                grossProfit = Convert.ToInt64(Math.Round(initialStake * 1.25));
                result = GambleResult.Won;
                break;
            case <= 95:
                grossProfit = Convert.ToInt64(Math.Round(initialStake * 2.5));
                result = GambleResult.Won;
                break;
            case <= 98:
                grossProfit = initialStake * 5;
                result = GambleResult.Won;
                break;
            case <= 100:
                grossProfit = initialStake * 10;
                result = GambleResult.Jackpot;
                break;
        }

        var taxBook = new TaxBook(grossProfit, initialStake, _credifyConfig.Core.BankTax);
        var netProfit = -initialStake;
        if (result is GambleResult.Won or GambleResult.Jackpot)
        {
            netProfit = taxBook.NetChange;
            _persistenceManager.StatisticsState.CreditsWon += netProfit;
        }

        _persistenceManager.AddBankCredits(taxBook.Tax);
        _persistenceManager.StatisticsState.CreditsSpent += initialStake;
        var newClientBalance = await _persistenceManager
            .AlterClientCredits(netProfit, client: gameEvent.Origin);
        await AnnounceResult(result, gameEvent.Origin, netProfit, taxBook.Tax, newClientBalance);
    }

    private Task AnnounceResult(GambleResult gambleResult, EFClient client, long netProfit, long tax,
        long newCreditBalance)
    {
        var announcement = string.Empty;
        var message = string.Empty;

        switch (gambleResult)
        {
            case GambleResult.Loss:
                message = _credifyConfig.Translations.GambleLost.FormatExt($"{Math.Abs(netProfit):N0}", $"{tax:N0}",
                    $"{newCreditBalance:N0}");
                break;
            case GambleResult.Draw:
                message = _credifyConfig.Translations.GambleDraw.FormatExt($"{tax:N0}", $"{newCreditBalance:N0}");
                break;
            case GambleResult.Won:
                message = _credifyConfig.Translations.GambleWon.FormatExt($"{netProfit:N0}", $"{tax:N0}",
                    $"{newCreditBalance:N0}");
                announcement = _credifyConfig.Translations.GambleWonAnnouncement.FormatExt(Plugin.PluginName,
                    client.CleanedName, $"{netProfit:N0}", "!crbet");
                break;
            case GambleResult.Jackpot:
                message = _credifyConfig.Translations.GambleWon.FormatExt($"{netProfit:N0}", $"{tax:N0}",
                    $"{newCreditBalance:N0}");
                announcement = _credifyConfig.Translations.GambleWonJackpotAnnouncement.FormatExt(Plugin.PluginName,
                    client.CleanedName, $"{netProfit:N0}", "!crbet");
                break;
        }

        client.Tell(message);
        if (gambleResult is GambleResult.Jackpot or GambleResult.Won)
        {
            client.CurrentServer.Broadcast(announcement, Utilities.IW4MAdminClient());
        }

        return Task.CompletedTask;
    }

    private enum GambleResult
    {
        Loss,
        Draw,
        Won,
        Jackpot
    }
}
