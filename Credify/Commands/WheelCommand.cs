using Credify.Chat.Active.Core;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Commands;

[CommandCategory("Games")]
public class WheelCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly IMetaServiceV2 _metaService;
    private readonly GamePlayerCommunication _gamePlayerCommunication;
    private readonly WheelService _wheelService;

    public WheelCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig, IMetaServiceV2 metaService,
        WheelService wheelService) 
        : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        _metaService = metaService;
        _gamePlayerCommunication = new GamePlayerCommunication();
        _wheelService = wheelService;
        Name = "credifywheel";
        Alias = "crwof";
        Description = credifyConfig.Translations.Core.CommandWheelDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
        Arguments = [];
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!_credifyConfig.Wheel.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.WheelDisabled);
            return;
        }

        // Check cooldown (24-hour, resets at 00:00 local)
        var lastUsedDate = await GetLastUsedDateAsync(gameEvent.Origin);
        if (lastUsedDate.HasValue && !CanSpinWheel(lastUsedDate.Value))
        {
            var timeUntilReset = GetTimeUntilReset();
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.WheelCooldown.FormatExt(timeUntilReset));
            return;
        }

        var userBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);

        // Always bet full balance - no amount argument accepted
        if (!PersistenceService.AvailableFunds(gameEvent.Origin, userBalance))
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Core.InsufficientCredits);
            return;
        }

        // Store original balance for percentage loss calculation
        var originalBalance = userBalance;

        // Deduct bet upfront
        await _persistenceService.RemoveCreditsAsync(gameEvent.Origin, userBalance);
        var balanceAfterBet = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);

        // Get segments with dynamic probability adjustment (based on original balance)
        var segments = _wheelService.GetAdjustedSegments(originalBalance);

        // Spinning animation
        await ShowSpinningAnimationAsync(gameEvent.Origin);

        // Spin the wheel
        var segment = _wheelService.SpinWheel(segments);
        
        // Calculate winnings based on new payout system
        // Use original balance for all calculations since balance after bet is 0 (full balance bet)
        var payout = _wheelService.CalculatePayout(segment, originalBalance);
        var newBalance = balanceAfterBet;

        // Calculate probability for broadcast
        var totalWeight = segments.Sum(s => s.Weight);
        var probability = (segment.Weight / totalWeight * 100).ToString("F1");

        if (payout > 0)
        {
            await _persistenceService.AddCreditsAsync(gameEvent.Origin, payout);
            newBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
            
            if (payout > userBalance)
            {
                ICredifyEventService.RaiseEvent(ObjectiveType.Baller, gameEvent.Origin, payout);
            }
        }
        else if (payout < 0)
        {
            // Percentage loss - apply additional loss based on original balance
            var additionalLoss = Math.Abs(payout);
            await _persistenceService.RemoveCreditsAsync(gameEvent.Origin, additionalLoss);
            newBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        }
        else
        {
            // Break even - restore the bet amount
            await _persistenceService.AddCreditsAsync(gameEvent.Origin, userBalance);
            newBalance = await _persistenceService.GetClientCreditsAsync(gameEvent.Origin);
        }

        // Store last used date
        await SetLastUsedDateAsync(gameEvent.Origin);

        // Broadcast result to all servers
        var profit = payout - userBalance;
        if (profit > 0)
        {
            var broadcastMsg = _credifyConfig.Translations.Core.WheelBroadcastWin.FormatExt(
                gameEvent.Origin.CleanedName, profit.ToString("N0"), segment.Name, probability);
            await _gamePlayerCommunication.BroadcastToAllServersAsync(gameEvent.Origin, [broadcastMsg]);
        }
        else if (profit < 0)
        {
            var broadcastMsg = _credifyConfig.Translations.Core.WheelBroadcastLoss.FormatExt(
                gameEvent.Origin.CleanedName, Math.Abs(profit).ToString("N0"), segment.Name, probability);
            await _gamePlayerCommunication.BroadcastToAllServersAsync(gameEvent.Origin, [broadcastMsg]);
        }

        // Send personal message
        string message;
        if (segment.IsOneHundredKOrDouble)
        {
            message = _credifyConfig.Translations.Core.WheelTwoXCash.FormatExt(
                segment.Name, profit.ToString("N0"), newBalance.ToString("N0"));
        }
        else if (profit > 0)
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

    private async Task ShowSpinningAnimationAsync(EFClient client)
    {
        await client.TellAsync([_credifyConfig.Translations.Core.WheelSpinning]);
        await Task.Delay(1500);
        await client.TellAsync([_credifyConfig.Translations.Core.WheelSlowing]);
        await Task.Delay(2000);
        await client.TellAsync([_credifyConfig.Translations.Core.WheelStopping]);
        await Task.Delay(1000);
    }


    private bool CanSpinWheel(DateTime lastUsedDate)
    {
        var today = DateTime.Now.Date; // Local time
        var lastUsed = lastUsedDate.Date; // Local time
        
        // If last used date is before today, cooldown has reset
        return lastUsed < today;
    }

    private async Task<DateTime?> GetLastUsedDateAsync(EFClient client)
    {
        var lastUsedMeta = await _metaService.GetPersistentMeta(PluginConstants.WheelLastUsed, client.ClientId);
        if (lastUsedMeta?.Value == null) return null;
        
        if (DateTime.TryParse(lastUsedMeta.Value, out var lastUsed))
        {
            return lastUsed.Date;
        }
        
        return null;
    }

    private async Task SetLastUsedDateAsync(EFClient client)
    {
        var today = DateTime.Now.Date.ToString("yyyy-MM-dd"); // ISO date format
        await _metaService.SetPersistentMeta(PluginConstants.WheelLastUsed, today, client.ClientId);
    }

    /// <summary>
    /// Gets the time until the next reset (midnight local time) as a humanized string.
    /// </summary>
    private string GetTimeUntilReset()
    {
        var now = DateTime.Now;
        var nextReset = now.Date.AddDays(1); // Next midnight
        var timeUntilReset = nextReset - now;
        return timeUntilReset.Humanize();
    }
}
