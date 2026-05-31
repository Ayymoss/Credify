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
public class SlotsCommand : GambleCommandBase
{
    public SlotsCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) : base(config, translationLookup, persistenceService, credifyConfig)
    {
        Name = "credifyslots";
        Alias = "crslots";
        Description = CredifyConfig.Translations.Core.CommandSlotsDescription;
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
        if (!CredifyConfig.Slots.IsEnabled)
        {
            gameEvent.Origin.Tell(CredifyConfig.Translations.Core.SlotsDisabled);
            return;
        }

        if (await TryResolveStakeAsync(gameEvent, gameEvent.Data, CredifyConfig.Slots.MinBet,
                CredifyConfig.Slots.MaxBet, CredifyConfig.Translations.Core.ErrorParsingArgument) is not { } bet) return;

        // Deduct bet upfront
        await Persistence.RemoveCreditsAsync(gameEvent.Origin, bet);

        // Spin the reels
        var symbols = CredifyConfig.Slots.Symbols;
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
                winnings = (long)(bet * CredifyConfig.Slots.JackpotMultiplier);
                resultType = "JACKPOT";
            }
            else
            {
                winnings = (long)(bet * CredifyConfig.Slots.ThreeMatchMultiplier);
                resultType = "THREE";
            }
        }
        else if (reel1.Name == reel2.Name || reel2.Name == reel3.Name || reel1.Name == reel3.Name)
        {
            // Two of a kind
            winnings = (long)(bet * CredifyConfig.Slots.TwoMatchMultiplier);
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
            await Persistence.AddCreditsAsync(gameEvent.Origin, winnings);
            ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, winnings);
            
            var newBalance = await Persistence.GetClientCreditsAsync(gameEvent.Origin);
            var profit = winnings - bet;
            
            if (resultType == "JACKPOT")
            {
                // Announce jackpot to server
                var jackpotMsg = CredifyConfig.Translations.Core.SlotsJackpot.FormatExt(
                    PluginConstants.PluginName, gameEvent.Origin.CleanedName, winnings.ToString("N0"));
                gameEvent.Owner?.Broadcast(jackpotMsg);
            }
            
            var winMsg = CredifyConfig.Translations.Core.SlotsWin.FormatExt(
                reelDisplay, profit.ToString("N0"), newBalance.ToString("N0"));
            gameEvent.Origin.Tell(winMsg);
        }
        else
        {
            var newBalance = await Persistence.GetClientCreditsAsync(gameEvent.Origin);
            var loseMsg = CredifyConfig.Translations.Core.SlotsLose.FormatExt(
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
