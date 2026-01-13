using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

public class WheelCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;

    public WheelCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig) : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        Name = "credifywheel";
        Alias = "crwheel";
        Description = credifyConfig.Translations.Core.CommandWheelDescription;
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
        if (!_credifyConfig.Wheel.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.WheelDisabled);
            return;
        }

        var betArg = gameEvent.Data;
        var userBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);

        // Handle "all" bet
        if (betArg.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            betArg = userBalance.ToString();
        }

        if (!long.TryParse(betArg, out var bet))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.ErrorParsingArgument);
            return;
        }

        // Validate bet amount
        if (bet < _credifyConfig.Wheel.MinBet)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.MinimumAmount);
            return;
        }

        if (_credifyConfig.Wheel.MaxBet > 0 && bet > _credifyConfig.Wheel.MaxBet)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.MaximumAmount.FormatExt(_credifyConfig.Wheel.MaxBet.ToString("N0")));
            return;
        }

        if (!PersistenceService.AvailableFunds(gameEvent.Origin, bet))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        // Deduct bet upfront
        await _persistenceService.RemoveCreditsAsync(gameEvent.Origin, bet);

        // Spin the wheel
        var segment = SpinWheel(_credifyConfig.Wheel.Segments);
        
        // Calculate winnings
        var winnings = (long)(bet * segment.Multiplier);
        var newBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);

        if (winnings > 0)
        {
            await _persistenceService.AddCreditsAsync(gameEvent.Origin, winnings);
            newBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
            
            if (winnings > bet)
            {
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, winnings);
            }
            
            if (segment.IsJackpot)
            {
                // Announce jackpot to server
                var jackpotMsg = _credifyConfig.Translations.Core.WheelJackpot.FormatExt(
                    PluginConstants.PluginName, gameEvent.Origin.CleanedName, winnings.ToString("N0"));
                gameEvent.Owner?.Broadcast(jackpotMsg);
            }
            
            var profit = winnings - bet;
            string message;
            
            if (profit > 0)
            {
                message = _credifyConfig.Translations.Core.WheelWin.FormatExt(
                    segment.Name, profit.ToString("N0"), newBalance.ToString("N0"));
            }
            else if (profit == 0)
            {
                message = _credifyConfig.Translations.Core.WheelBreakEven.FormatExt(
                    segment.Name, newBalance.ToString("N0"));
            }
            else
            {
                message = _credifyConfig.Translations.Core.WheelPartialLoss.FormatExt(
                    segment.Name, Math.Abs(profit).ToString("N0"), newBalance.ToString("N0"));
            }
            
            gameEvent.Origin.Tell(message);
        }
        else
        {
            // Bankrupt
            var message = _credifyConfig.Translations.Core.WheelBankrupt.FormatExt(
                bet.ToString("N0"), newBalance.ToString("N0"));
            gameEvent.Origin.Tell(message);
        }
    }

    private static WheelSegment SpinWheel(List<WheelSegment> segments)
    {
        var totalWeight = segments.Sum(s => s.Weight);
        var roll = Random.Shared.NextDouble() * totalWeight;
        var cumulative = 0.0;
        
        foreach (var segment in segments)
        {
            cumulative += segment.Weight;
            if (roll < cumulative)
            {
                return segment;
            }
        }
        
        return segments.Last();
    }
}
