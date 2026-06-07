using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class SlotsCommand : Command
{
    private readonly CredifyConfiguration _credifyConfig;
    private readonly SlotsService _slots;
    private readonly GambleCommandHelper _gamble;

    public SlotsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig, SlotsService slots)
        : base(config, translationLookup)
    {
        _credifyConfig = credifyConfig;
        _slots = slots;
        _gamble = new GambleCommandHelper(persistenceService, credifyConfig);
        Name = "credifyslots";
        Alias = "crslots";
        Description = _credifyConfig.Translations.Slots.Description;
        Permission = Data.Models.Client.EFClient.Permission.User;
        RequiresTarget = false;
        Arguments =
        [
            new CommandArgument
            {
                Name = "Amount",
                Required = true
            }
        ];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_slots.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Slots.Disabled);
            return;
        }

        if (await _gamble.TryResolveStakeAsync(gameEvent, gameEvent.Data, _slots.MinBet,
                _slots.MaxBet, _credifyConfig.Translations.Core.ErrorParsingArgument) is not { } bet) return;

        // debit + spin + credit + objective + jackpot broadcast all live in the shared service
        var receipt = await _slots.SpinAsync(gameEvent.Origin, bet);
        var reelDisplay = string.Join(" | ", receipt.Spin.Reels.Select(reel => reel.Display));

        if (receipt.Winnings > 0)
        {
            var winMsg = _credifyConfig.Translations.Slots.Win.FormatExt(
                reelDisplay, receipt.Profit.ToString("N0"), receipt.NewBalance.ToString("N0"));
            gameEvent.Origin.Tell(winMsg);
        }
        else
        {
            var loseMsg = _credifyConfig.Translations.Slots.Lose.FormatExt(
                reelDisplay, bet.ToString("N0"), receipt.NewBalance.ToString("N0"));
            gameEvent.Origin.Tell(loseMsg);
        }
    }
}
