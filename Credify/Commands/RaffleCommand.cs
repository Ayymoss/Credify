using Credify.Chat.Feature.Raffle;
using Credify.Chat.Feature.Raffle.Enums;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

public class RaffleCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly RaffleManager _raffleManager;

    public RaffleCommand(CommandConfiguration config, ITranslationLookup translationLookup, PersistenceService persistenceService,
        CredifyConfiguration credifyConfig, RaffleManager raffleManager) :
        base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        _raffleManager = raffleManager;
        Name = "credifyraffle";
        Description = credifyConfig.Translations.Core.CommandRaffleDescription;
        Alias = "crraf";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Ticket Number",
                Required = false
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var currentCredits = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        if (currentCredits < _credifyConfig.Core.RaffleCost)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        var ticketNumberRaw = gameEvent.Data;
        int? ticketNumber = null;

        switch (string.IsNullOrWhiteSpace(ticketNumberRaw))
        {
            case false when int.TryParse(ticketNumberRaw, out var userTicket):
                ticketNumber = userTicket;
                break;
            case false:
                gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingArgument);
                return;
        }

        var result = await _raffleManager.PurchaseTicketAsync(gameEvent.Origin, ticketNumber);

        switch (result.Status)
        {
            case StatusTypes.Success:
                gameEvent.Origin.Tell(_credifyConfig.Translations.Raffle.Success.FormatExt(result.Ticket?.ToString("N0")));
                ICredifyEventService.RaiseEvent(ObjectiveType.Raffle, gameEvent.Origin);
                break;
            case StatusTypes.ClientAlreadyPurchased:
                gameEvent.Origin.Tell(_credifyConfig.Translations.Raffle.ClientAlreadyPurchased);
                break;
            case StatusTypes.TicketAlreadyPurchased:
                gameEvent.Origin.Tell(_credifyConfig.Translations.Raffle.TicketAlreadyPurchased);
                break;
            case StatusTypes.InvalidTicketRange:
                gameEvent.Origin.Tell(_credifyConfig.Translations.Raffle.InvalidTicketRange);
                break;
            case StatusTypes.RaffleNotStarted:
                gameEvent.Origin.Tell(_credifyConfig.Translations.Raffle.RaffleNotStarted);
                break;
        }
    }
}
