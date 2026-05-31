using Credify.Commands.Attributes;
using Credify.Configuration;
using Credify.Constants;
using Credify.Services;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using EFClient = Data.Models.Client.EFClient;

namespace Credify.Commands;

[CommandCategory("Credits")]
public class DailyCommand : Command
{
    private readonly PersistenceService _persistenceService;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly IMetaServiceV2 _metaService;

    public DailyCommand(CommandConfiguration config, ITranslationLookup translationLookup,
        PersistenceService persistenceService, CredifyConfiguration credifyConfig, IMetaServiceV2 metaService)
        : base(config, translationLookup)
    {
        _persistenceService = persistenceService;
        _credifyConfig = credifyConfig;
        _metaService = metaService;
        Name = "credifydaily";
        Alias = "crdaily";
        Description = credifyConfig.Translations.Economy.DailyDescription;
        Permission = EFClient.Permission.User;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var daily = _credifyConfig.Daily;
        if (!daily.IsEnabled)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.Economy.DailyDisabled);
            return;
        }

        var client = gameEvent.Origin;
        var today = DateTime.Now.Date;
        var lastClaim = await GetLastClaimAsync(client);

        if (lastClaim == today)
        {
            var timeUntilReset = (today.AddDays(1) - DateTime.Now).Humanize();
            gameEvent.Origin.Tell(_credifyConfig.Translations.Economy.DailyAlreadyClaimed.FormatExt(timeUntilReset));
            return;
        }

        // Continue the streak only if the last claim was yesterday; otherwise it resets.
        var streak = await GetStreakAsync(client);
        streak = lastClaim == today.AddDays(-1) ? streak + 1 : 1;

        var effectiveStreak = Math.Min(streak, daily.MaxStreakDays);
        var reward = daily.BaseReward + (effectiveStreak - 1) * daily.StreakBonus;

        await _persistenceService.AddCreditsAsync(client, reward);
        await _metaService.SetPersistentMeta(PluginConstants.DailyLastClaim, today.ToString("yyyy-MM-dd"), client.ClientId);
        await _metaService.SetPersistentMeta(PluginConstants.DailyStreak, streak.ToString(), client.ClientId);

        gameEvent.Origin.Tell(_credifyConfig.Translations.Economy.DailyClaimed
            .FormatExt(reward.ToString("N0"), streak.ToString()));
    }

    private async Task<DateTime?> GetLastClaimAsync(EFClient client)
    {
        var meta = await _metaService.GetPersistentMeta(PluginConstants.DailyLastClaim, client.ClientId);
        if (meta?.Value is null) return null;
        return DateTime.TryParse(meta.Value, out var date) ? date.Date : null;
    }

    private async Task<int> GetStreakAsync(EFClient client)
    {
        var meta = await _metaService.GetPersistentMeta(PluginConstants.DailyStreak, client.ClientId);
        return int.TryParse(meta?.Value, out var streak) ? streak : 0;
    }
}
