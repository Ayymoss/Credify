using Credify.Chat.Feature.Raffle;
using Credify.Configuration;
using Credify.Services;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class ShowRaffleCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly CredifyCache _cache;
    private readonly RaffleManager _raffleManager;

    public ShowRaffleCommand(CommandConfiguration config, ITranslationLookup translationLookup, CredifyConfiguration credifyConfig,
        CredifyCache cache, RaffleManager raffleManager) : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _cache = cache;
        _raffleManager = raffleManager;
        Name = "credifyshowraffle";
        Description = credifyConfig.Translations.Core.CommandShowRaffleDescription;
        Alias = "crsr";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var ticketHolders = await _raffleManager.GetPlayersAsync();
        var nextOccurrence = _raffleManager.NextOccurrence;
        if (ticketHolders.Count is 0)
        {
            await gameEvent.Origin.TellAsync(new[]
            {
                _credifyConfig.Translations.Raffle.NoTicketHolders,
                _credifyConfig.Translations.Raffle.NoTicketHoldersContinued
                    .FormatExt(_cache.BankCredits.ToString("N0"), nextOccurrence.Humanize())
            });
            return;
        }

        var lastWinner = await _raffleManager.GetLastWinnerAsync();

        List<string> lastWinnerPlaceholder = lastWinner is null
            ? [_credifyConfig.Translations.Raffle.NoLastWinner]
            :
            [
                _credifyConfig.Translations.Raffle.PreviousRaffleCount.FormatExt(lastWinner.PreviousPlayers),
                _credifyConfig.Translations.Raffle.LastWinner.FormatExt(lastWinner.ClientName, lastWinner.ClientId,
                    lastWinner.Amount.ToString("N0"))
            ];

        var headerMessages = new[]
        {
            _credifyConfig.Translations.Raffle.ShowRaffleHeader,
            _credifyConfig.Translations.Raffle.RaffleNextDraw.FormatExt(nextOccurrence.Humanize())
        };

        var ticketHolderNames = ticketHolders
            .OrderByDescending(entry => entry.Ticket)
            .Select(creditEntry => _credifyConfig.Translations.Raffle.TicketHolder
                .FormatExt(creditEntry.Ticket.ToString("N0"), creditEntry.Client.CleanedName))
            .ToArray();

        headerMessages = headerMessages.Concat(ticketHolderNames).ToArray();
        headerMessages = headerMessages.Concat(lastWinnerPlaceholder).ToArray();
        await gameEvent.Origin.TellAsync(headerMessages);
    }
}
