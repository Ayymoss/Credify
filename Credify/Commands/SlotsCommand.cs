using Credify.Chat.Passive.Quests.Enums;
using Credify.Commands.Attributes;
using Credify.Commands.Base;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class SlotsCommand : Command
{
    private readonly PersistenceService _persistence;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly GambleCommandHelper _gamble;

    public SlotsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistence = persistenceService;
        _credifyConfig = credifyConfig;
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
        if (!_credifyConfig.Slots.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Slots.Disabled);
            return;
        }

        if (await _gamble.TryResolveStakeAsync(gameEvent, gameEvent.Data, _credifyConfig.Slots.MinBet,
                _credifyConfig.Slots.MaxBet, _credifyConfig.Translations.Core.ErrorParsingArgument) is not { } bet) return;

        // Deduct bet upfront
        await _persistence.RemoveCreditsAsync(gameEvent.Origin, bet);

        // Spin the reels
        var symbols = _credifyConfig.Slots.Symbols;
        var totalWeight = symbols.Sum(s => s.Weight);
        
        var reel1 = SpinReel(symbols, totalWeight);
        var reel2 = SpinReel(symbols, totalWeight);
        var reel3 = SpinReel(symbols, totalWeight);

        // Calculate winnings
        long winnings = 0;
        string resultType;

        if (reel1.Name == reel2.Name && reel2.Name == reel3.Name)
        {
            // Three of a kind
            if (reel1.IsJackpot)
            {
                winnings = (long)(bet * _credifyConfig.Slots.JackpotMultiplier);
                resultType = "JACKPOT";
            }
            else
            {
                winnings = (long)(bet * _credifyConfig.Slots.ThreeMatchMultiplier);
                resultType = "THREE";
            }
        }
        else if (reel1.Name == reel2.Name || reel2.Name == reel3.Name || reel1.Name == reel3.Name)
        {
            // Two of a kind
            winnings = (long)(bet * _credifyConfig.Slots.TwoMatchMultiplier);
            resultType = "TWO";
        }
        else
        {
            resultType = "LOSS";
        }

        // Format result display
        var reelDisplay = $"{reel1.Display} | {reel2.Display} | {reel3.Display}";

        if (winnings > 0)
        {
            await _persistence.AddCreditsAsync(gameEvent.Origin, winnings);
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, winnings);
            
            var newBalance = await _persistence.GetClientCreditsAsync(gameEvent.Origin);
            var profit = winnings - bet;
            
            if (resultType == "JACKPOT")
            {
                // Announce jackpot to server
                var jackpotMsg = _credifyConfig.Translations.Slots.Jackpot.FormatExt(
                    PluginConstants.PluginName, gameEvent.Origin.CleanedName, winnings.ToString("N0"));
                gameEvent.Owner?.Broadcast(jackpotMsg);
            }
            
            var winMsg = _credifyConfig.Translations.Slots.Win.FormatExt(
                reelDisplay, profit.ToString("N0"), newBalance.ToString("N0"));
            gameEvent.Origin.Tell(winMsg);
        }
        else
        {
            var newBalance = await _persistence.GetClientCreditsAsync(gameEvent.Origin);
            var loseMsg = _credifyConfig.Translations.Slots.Lose.FormatExt(
                reelDisplay, bet.ToString("N0"), newBalance.ToString("N0"));
            gameEvent.Origin.Tell(loseMsg);
        }
    }

    private static SlotSymbol SpinReel(List<SlotSymbol> symbols, int totalWeight)
    {
        var roll = Random.Shared.Next(totalWeight);
        var cumulative = 0;
        
        foreach (var symbol in symbols)
        {
            cumulative += symbol.Weight;
            if (roll < cumulative)
            {
                return symbol;
            }
        }
        
        return symbols.Last();
    }
}
