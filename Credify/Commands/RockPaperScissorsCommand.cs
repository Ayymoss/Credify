using Credify.Models;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class RockPaperScissorsCommand : Command
{
    private readonly PersistenceManager _persistenceManager;
    private readonly CredifyConfiguration _credifyConfig;

    public RockPaperScissorsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceManager persistenceManager, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistenceManager = persistenceManager;
        _credifyConfig = credifyConfig;
        Name = "creditsrps";
        Alias = "crrps";
        Description = credifyConfig.Translations.CommandRockPaperScissorsDescription;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Rock | Paper | Scissors",
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
        var args = gameEvent.Data.Split(" ");
        var userRpsArg = args[0];
        var userStakeArg = args[1];

        var rpsLookup = new Dictionary<string, int>
        {
            {"r", 0},
            {"p", 1},
            {"s", 2},
            {"rock", 0},
            {"paper", 1},
            {"scissors", 2}
        };

        if (!rpsLookup.ContainsKey(userRpsArg))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.BadRpsArgument);
            return;
        }

        if (userStakeArg == "all")
        {
            var allCredits = await _persistenceManager.GetClientCredits(gameEvent.Origin);
            userStakeArg = allCredits.ToString();
        }

        if (!long.TryParse(userStakeArg, out var stake))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.ErrorParsingSecondArgument);
            return;
        }

        if (stake < 10)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.MinimumAmount);
            return;
        }

        if (!_persistenceManager.AvailableFunds(gameEvent.Origin, stake))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.InsufficientCredits);
            return;
        }

        var outcomeMatrix = new[,]
        {
            //R, P, S
            {0, 2, 1}, // Rock
            {1, 0, 2}, // Paper
            {2, 1, 0} // Scissors
        };

        var userRps = rpsLookup[userRpsArg];
        var computerChoice = Random.Shared.Next(3);
        var outcome = outcomeMatrix[userRps, computerChoice];

        var taxBook = new TaxBook(stake, 0, _credifyConfig.Core.BankTax);
        string message, announcement = string.Empty;
        long newBalance;

        switch (outcome)
        {
            case 0: // Tie
                newBalance = await _persistenceManager.AlterClientCredits(-taxBook.Tax, client: gameEvent.Origin);
                message = _credifyConfig.Translations.GambleDraw
                    .FormatExt($"{taxBook.Tax:N0}", $"{newBalance:N0}");
                break;
            case 1: // User wins
                taxBook = new TaxBook(stake * 2, stake, _credifyConfig.Core.BankTax);
                newBalance = await _persistenceManager.AlterClientCredits(taxBook.NetChange, client: gameEvent.Origin);
                message = _credifyConfig.Translations.GambleWon
                    .FormatExt($"{taxBook.NetChange:N0}", $"{taxBook.Tax:N0}", $"{newBalance:N0}");
                announcement = _credifyConfig.Translations.RpsWonAnnouncement
                    .FormatExt(Plugin.PluginName, gameEvent.Origin.CleanedName, $"{taxBook.NetChange:N0}");
                break;
            default: // User loses
                newBalance = await _persistenceManager.AlterClientCredits(-stake, client: gameEvent.Origin);
                message = _credifyConfig.Translations.GambleLost
                    .FormatExt($"{stake:N0}", $"{taxBook.Tax:N0}", $"{newBalance:N0}");
                break;
        }

        await _persistenceManager.AddBankCredits(taxBook.Tax);
        gameEvent.Origin.Tell(message);
        if (!string.IsNullOrEmpty(announcement)) gameEvent.Owner.Broadcast(announcement);
    }
}
