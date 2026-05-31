using Credify.Chat.Feature.Raffle;
using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Helpers;
using Credify.Services;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Raffle")]
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
        Description = credifyConfig.Translations.Raffle.ShowDescription;
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

        var raffle = _credifyConfig.Translations.Raffle;
        var next = nextOccurrence.Humanize();
        const string separator = " (Color::White)| ";
        const int maxWidth = 56;
        const int maxEntriesShown = 8;

        List<string> lines =
        [
            raffle.ShowSummary.FormatExt(_cache.BankCredits.ToString("N0"), ticketHolders.Count),
        ];

        // Highlight the caller's own ticket, or nudge them to buy one.
        var ownEntry = ticketHolders.FirstOrDefault(e => e.Client.ClientId == gameEvent.Origin.ClientId);
        lines.Add(ownEntry is not null
            ? raffle.YourTicket.FormatExt(ownEntry.Ticket.ToString("N0"), next)
            : raffle.NoTicketYet.FormatExt(next));

        // Top entries by ticket, packed onto as few lines as possible, with an overflow note.
        var shown = ticketHolders.OrderByDescending(entry => entry.Ticket).Take(maxEntriesShown).ToList();
        var tokens = shown
            .Select(entry => raffle.TicketHolder.FormatExt(entry.Ticket.ToString("N0"), entry.Client.CleanedName))
            .ToList();
        lines.AddRange(ChatLines.Pack(raffle.TicketsLabel, tokens, separator, maxWidth));
        if (ticketHolders.Count > shown.Count)
            lines.Add(raffle.MoreTickets.FormatExt(ticketHolders.Count - shown.Count));

        // Last winner, if there's room within the line budget.
        var lastWinner = await _raffleManager.GetLastWinnerAsync();
        if (lastWinner is not null)
            lines.Add(raffle.LastWinner.FormatExt(lastWinner.ClientName, lastWinner.ClientId,
                lastWinner.Amount.ToString("N0")));

        if (lines.Count > 5) lines = lines.Take(5).ToList();
        await gameEvent.Origin.TellAsync(lines);
    }
}
