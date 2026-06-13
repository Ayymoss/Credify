using Credify.Chat.Feature.Raffle;
using Credify.Chat.Passive;
using Credify.Chat.Passive.ChatGames;
using Credify.Chat.Passive.Quests;
using Credify.Configuration;
using Credify.Constants;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

public class ScheduleService(
    CredifyConfiguration config,
    RaffleManager raffleManager,
    PassiveManager passiveManager,
    QuestManager questManager)
{
    public void TriggerSchedules(IManager manager, CancellationToken token)
    {
        if (config.ChatGame.IsEnabled)
        {
            passiveManager.NextGameDue = DateTimeOffset.UtcNow + config.ChatGame.Frequency;
            Utilities.ExecuteAfterDelay(config.ChatGame.Frequency, InitChatGameAsync, token);
        }

        Utilities.ExecuteAfterDelay(config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelayAsync(manager, cancellationToken), token);

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), LotteryDelayCheck, token);

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1),
            cancellationToken => GenerateDailyQuestsAsync(manager, cancellationToken), token);
    }

    private async Task InitChatGameAsync(CancellationToken token)
    {
        await passiveManager.InitGameAsync();
        passiveManager.NextGameDue = DateTimeOffset.UtcNow + config.ChatGame.Frequency;
        Utilities.ExecuteAfterDelay(config.ChatGame.Frequency, InitChatGameAsync, token);
    }

    private async Task LotteryDelayCheck(CancellationToken token)
    {
        if (raffleManager.ShouldDrawRaffle)
        {
            await raffleManager.DrawWinnerAsync();
        }

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), LotteryDelayCheck, token);
    }

    private int _advertisementIndex;

    private async Task AdvertisementDelayAsync(IManager manager, CancellationToken token)
    {
        // Rotate one themed advert per interval rather than dumping every line at once,
        // so chat stays uncluttered while still cycling through all features over time.
        string[] adverts =
        [
            config.Translations.Core.AdvertisementMessage,
            config.Translations.Core.AdvertisementQuickBets,
            config.Translations.Core.AdvertisementRaffle,
            config.Translations.Core.AdvertisementShop,
            config.Translations.Core.AdvertisementProfile
        ];

        var advert = adverts[_advertisementIndex % adverts.Length].FormatExt(PluginConstants.PluginName);
        _advertisementIndex++;

        foreach (var server in manager.GetServers())
        {
            if (server.ConnectedClients.Count is 0) continue;
            await server.BroadcastAsync([advert], token: token);
        }

        Utilities.ExecuteAfterDelay(config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelayAsync(manager, cancellationToken), token);
    }

    private async Task GenerateDailyQuestsAsync(IManager manager, CancellationToken token)
    {
        questManager.GenerateDailyQuests();

        // Let online players know fresh dailies are available (drives them to the now-compact !crq).
        foreach (var server in manager.GetServers())
        {
            if (server.ConnectedClients.Count is 0) continue;
            await server.BroadcastAsync([config.Translations.Quests.DailyReset], token: token);
        }

        var now = TimeProvider.System.GetLocalNow();
        var nextMidnight = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).AddDays(1);
        var timeUntilMidnight = nextMidnight - now;

        Utilities.ExecuteAfterDelay(timeUntilMidnight,
            cancellationToken => GenerateDailyQuestsAsync(manager, cancellationToken), token);
    }
}
