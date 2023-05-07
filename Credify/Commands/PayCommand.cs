using Credify.Models;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class PayCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public PayCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceManager persistenceManager,
        CredifyConfiguration credifyConfig) :
        base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "credifypay";
        Description = credifyConfig.Translations.CommandPayCreditsDescription;
        Alias = "crpay";
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
        var argCredits = gameEvent.Data;

        if (!long.TryParse(argCredits, out var credits))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return;
        }

        if (gameEvent.Origin.ClientId == gameEvent.Target.ClientId)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.CannotTargetSelf);
            return;
        }

        if (gameEvent.Target.ClientId == 1)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.CannotTargetConsole);
            return;
        }

        if (credits < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumAmount);
            return;
        }

        var taxBook = new TaxBook(credits, 0, _credifyConfig.Core.BankTax);
        await _persistenceManager.AddBankCredits(taxBook.Tax);
        await _persistenceManager.AlterClientCredits(-taxBook.GrossCredits, client: gameEvent.Origin);
        await _persistenceManager.AlterClientCredits(taxBook.NetChange, client: gameEvent.Target);
        gameEvent.Origin.Tell(_credifyConfig.Translations.PaySent
            .FormatExt($"{taxBook.NetChange:N0}", $"{taxBook.Tax:N0}", gameEvent.Target.CleanedName));
        gameEvent.Target.Tell(_credifyConfig.Translations.PayReceived
            .FormatExt($"{taxBook.NetChange:N0}", gameEvent.Origin.CleanedName));
    }
}
