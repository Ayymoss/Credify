using Data.Abstractions;
using Humanizer;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class ShowLottoCommand : Command
{
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly LotteryManager _lotteryManager;
    private readonly PersistenceManager _persistenceManager;

    public ShowLottoCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        IDatabaseContextFactory contextFactory, CredifyConfiguration credifyConfig,
        LotteryManager lotteryManager, PersistenceManager persistenceManager) : base(config, translationLookup)
    {
        _contextFactory = contextFactory;
        _credifyConfig = credifyConfig;
        _lotteryManager = lotteryManager;
        _persistenceManager = persistenceManager;
        Name = "credifyshowlotto";
        Description = credifyConfig.Translations.CommandShowLottoDescription;
        Alias = "crsl";
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var ticketHolders = _lotteryManager.Lottery.Any();
        if (!ticketHolders)
        {
            await gameEvent.Origin.TellAsync(new[]
            {
                _credifyConfig.Translations.NoTicketHolders,
                _credifyConfig.Translations.NoTicketHoldersContinued
                    .FormatExt($"{_persistenceManager.BankCredits:N0}", _lotteryManager.NextOccurrence.Humanize())
            });
            return;
        }

        await using var context = _contextFactory.CreateContext(false);
        var names = await context.Clients
            .Where(client => _lotteryManager.Lottery.Select(credit => credit.ClientId).Contains(client.ClientId))
            .Select(client => new {client.ClientId, client.CurrentAlias.Name})
            .ToDictionaryAsync(selector => selector.ClientId, selector => selector.Name);

        var lastWinner = await _persistenceManager.ReadLastLotteryWinnerAsync();
        
        var lastWinnerPlaceholder = lastWinner is null 
            ? _credifyConfig.Translations.NoLastWinner
            : _credifyConfig.Translations.LastWinner
                .FormatExt(lastWinner.Value.ClientName, lastWinner.Value.ClientId, $"{lastWinner.Value.PayOut:N0}");
        
        var headerMessages = new[]
        {
            _credifyConfig.Translations.ShowLottoHeader,
            _credifyConfig.Translations.LottoNextDraw.FormatExt(_lotteryManager.NextOccurrence.Humanize())
        };

        var ticketHolderNames = _lotteryManager.Lottery.OrderByDescending(entry => entry.Tickets).Select(
            (creditEntry, index) => _credifyConfig.Translations.TicketHolder
                .FormatExt(index + 1, $"{creditEntry.Tickets:N0}", names[creditEntry.ClientId])).ToArray();

        var footerMessages = new[]
        {
            lastWinnerPlaceholder
        };

        headerMessages = headerMessages.Concat(ticketHolderNames).ToArray();
        headerMessages = headerMessages.Concat(footerMessages).ToArray();
        await gameEvent.Origin.TellAsync(headerMessages);
    }
}
