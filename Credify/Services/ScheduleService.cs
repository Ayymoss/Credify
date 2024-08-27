using Credify.Chat.Active.Raffle;
using Credify.Chat.Passive;
using Credify.Chat.Passive.ChatGames;
using Credify.Chat.Passive.Quests;
using Credify.Configuration;
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
        if (config.ChatGame.IsEnabled) Utilities.ExecuteAfterDelay(config.ChatGame.Frequency, InitChatGameAsync, token);

        Utilities.ExecuteAfterDelay(config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelayAsync(manager, cancellationToken), token);

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), LotteryDelayCheck, token);

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), GenerateDailyQuestsAsync, token);
    }

    private async Task InitChatGameAsync(CancellationToken token)
    {
        await passiveManager.InitGameAsync();
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

    private async Task AdvertisementDelayAsync(IManager manager, CancellationToken token)
    {
        foreach (var server in manager.GetServers())
        {
            if (server.ConnectedClients.Count is 0) continue;
            List<string> messages =
            [
                config.Translations.Core.AdvertisementMessage.FormatExt(Plugin.PluginName),
                config.Translations.Core.AdvertisementRaffle.FormatExt(Plugin.PluginName),
                config.Translations.Core.AdvertisementShop.FormatExt(Plugin.PluginName)
            ];
            await server.BroadcastAsync(messages, token: token);
        }

        Utilities.ExecuteAfterDelay(config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelayAsync(manager, cancellationToken), token);
    }

    private Task GenerateDailyQuestsAsync(CancellationToken token)
    {
        questManager.GenerateDailyQuests();

        var now = TimeProvider.System.GetLocalNow();
        var nextMidnight = new DateTimeOffset(now.Year, now.Month, now.Day, 0, 0, 0, now.Offset).AddDays(1);
        var timeUntilMidnight = nextMidnight - now;

        Utilities.ExecuteAfterDelay(timeUntilMidnight, GenerateDailyQuestsAsync, token);
        return Task.CompletedTask;
    }
}
