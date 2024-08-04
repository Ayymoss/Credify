using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class LottoCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly LotteryManager _lotteryManager;

    public LottoCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceManager persistenceManager,
        CredifyConfiguration credifyConfig, LotteryManager lotteryManager) :
        base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        _lotteryManager = lotteryManager;
        Name = "credifylotto";
        Description = credifyConfig.Translations.Core.CommandLottoDescription;
        Alias = "crlotto";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Tickets",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var argCredits = gameEvent.Data;

        if (!long.TryParse(argCredits, out var credits))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingSecondArgument);
            return;
        }

        if (credits < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.MinimumAmount);
            return;
        }

        const int fixedTicketCost = 10; // TODO: Move to config.
        var ticketsCost = credits * fixedTicketCost;
        var currentCredits = await _persistenceManager.GetClientCreditsAsync(gameEvent.Origin);

        if (currentCredits < ticketsCost)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        _persistenceManager.AddBankCreditsAsync(ticketsCost);
        await _persistenceManager.RemoveCreditsAsync(gameEvent.Origin, ticketsCost);
        var totalTickets = await _lotteryManager.AddToLottery(gameEvent.Origin, credits);
        gameEvent.Origin.Tell(_credifyConfig.Translations.Core.BoughtLottoTickets
            .FormatExt(credits.ToString("N0"), ticketsCost.ToString("N0"), totalTickets.ToString("N0")));
    }
}
