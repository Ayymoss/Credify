using Credify.Chat.Active.Raffle;
using Credify.Chat.Passive;
using Credify.Configuration;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace Credify.Services;

public class ScheduleService(CredifyConfiguration config, RaffleManager raffleManager, PassiveManager passiveManager)
{
    public void TriggerSchedules(IManager manager, CancellationToken token)
    {
        if (config.ChatGame.IsEnabled) Utilities.ExecuteAfterDelay(config.ChatGame.Frequency, InitChatGameAsync, token);

        Utilities.ExecuteAfterDelay(config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelayAsync(manager, cancellationToken), token);

        Utilities.ExecuteAfterDelay(TimeSpan.FromMinutes(1), LotteryDelayCheck, token);
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
                config.Translations.Core.AdvertisementLotto.FormatExt(Plugin.PluginName),
                config.Translations.Core.AdvertisementShop.FormatExt(Plugin.PluginName)
            ];
            await server.BroadcastAsync(messages, token: token);
        }

        Utilities.ExecuteAfterDelay(config.Core.AdvertisementIntervalMinutes,
            cancellationToken => AdvertisementDelayAsync(manager, cancellationToken), token);
    }
}
