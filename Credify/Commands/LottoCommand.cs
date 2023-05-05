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
        Description = credifyConfig.Translations.CommandLottoDescription;
        Alias = "crlotto";
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Tickets",
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

        if (credits < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumAmount);
            return;
        }

        var ticketsCost = credits * 10;
        var currentCredits = await _persistenceManager.GetClientCredits(gameEvent.Origin);

        if (currentCredits < ticketsCost)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        await _persistenceManager.AlterClientCredits(-ticketsCost, client: gameEvent.Origin);
        var totalTickets = await _lotteryManager.AddToLottery(gameEvent.Origin, credits);

        gameEvent.Origin.Tell(_credifyConfig.Translations.BoughtLottoTickets
            .FormatExt($"{credits:N0}", $"{ticketsCost:N0}", $"{totalTickets:N0}"));
    }
}
