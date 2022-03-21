using System.Linq.Expressions;
using Data.Abstractions;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using Stats.Config;

namespace CreditsPlugin;

public class BetManager
{
    public BetManager(IDatabaseContextFactory contextFactory, StatsConfiguration statsConfig, IMetaService metaService)
    {
        _config = statsConfig;
        _contextFactory = contextFactory;
    }

    private readonly StatsConfiguration _config;
    private readonly IDatabaseContextFactory _contextFactory;

    public async Task<int> GetPlayerRankedPosition(int clientId, long serverId)
    {
        await using var context = _contextFactory.CreateContext(false);

        var clientRanking = await context.Set<EFClientRankingHistory>()
            .Where(r => r.ClientId == clientId)
            .Where(r => r.ServerId == serverId)
            .Where(r => r.Newest)
            .FirstOrDefaultAsync();

        return clientRanking?.Ranking + 1 ?? 0;
    }

    public Expression<Func<EFClientRankingHistory, bool>> GetNewRankingFunc(long? serverId = null)
    {
        return ranking => ranking.ServerId == serverId
                          && ranking.Client.Level != Data.Models.Client.EFClient.Permission.Banned
                          && ranking.CreatedDateTime >= Extensions.FifteenDaysAgo()
                          && ranking.ZScore != null
                          && ranking.PerformanceMetric != null
                          && ranking.Newest
                          && ranking.Client.TotalConnectionTime >=
                          _config.TopPlayersMinPlayTime;
    }

    public async Task<int> GetTotalRankedPlayers(long serverId)
    {
        await using var context = _contextFactory.CreateContext(false);

        return await context.Set<EFClientRankingHistory>()
            .Where(GetNewRankingFunc(serverId))
            .CountAsync();
    }

    private Dictionary<long, DateTime> MapTime = new();
    private Dictionary<long, int> MaxScore = new();
    private Dictionary<int, OpenBetData> OpenBets = new();

    public async Task<bool> CanBet(EFClient client)
    {
        var clientServerId = await client.CurrentServer.GetIdForServer();

        if (!MapTime.ContainsKey(clientServerId)) return false;
        return MapTime[clientServerId].AddMinutes(2) >= DateTime.UtcNow;
    }

    public async void OnBetCreated(GameEvent gameEvent, int amount)
    {
        var totalKeys = OpenBets.Count;
        
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        var serverPlayerRank = await GetPlayerRankedPosition(gameEvent.Origin.ClientId, clientServerId);
        var serverTotalRanked = await GetTotalRankedPlayers(clientServerId);

        if (serverPlayerRank == 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)Client needs to be ranked to bet for.");
            return;
        }

        OpenBets.Add(totalKeys + 1, new OpenBetData
        {
            Origin = gameEvent.Origin,
            Target = gameEvent.Target,
            TargetRank = serverPlayerRank,
            TotalRanked = serverTotalRanked,
            Amount = amount
        });
    }

    public async void OnClientUpdated(GameEvent gameEvent)
    {
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        lock (MaxScore)
        {
            if (!MaxScore.ContainsKey(clientServerId)) MaxScore.Add(clientServerId, gameEvent.Origin.Score);
            if (MaxScore[clientServerId] < gameEvent.Origin.Score) MaxScore[clientServerId] = gameEvent.Origin.Score;
        }
    }

    public async void OnMatchEnd(Server server)
    {
        var serverId = await server.GetIdForServer();

        if (OpenBets.Count > 0)
        {
            for (var i = 1; i <= OpenBets.Count; i++)
            {
                if (!OpenBets[i].Origin.IsIngame || !OpenBets[i].Target.IsIngame)
                {
                    Console.WriteLine($"Removed bet {i} due to Origin or Target disconnected.");
                    OpenBets.Remove(i);
                    return;
                }

                if (!Plugin.PrimaryLogic.AvailableFunds(OpenBets[i].Origin, OpenBets[i].Amount))
                {
                    Console.WriteLine($"Removed bet {i} due to insufficient funds on Origin");
                    OpenBets.Remove(i);
                    return;
                }

                var previousCredits = OpenBets[i].Origin.GetAdditionalProperty<int>(Plugin.CreditsKey);
                var payOut = OpenBets[i].Amount;

                if (MaxScore[serverId] >= OpenBets[i].Target.Score)
                {
                    payOut *= OpenBets[i].TargetRank / OpenBets[i].TotalRanked;

                    Console.WriteLine(
                        $"DBG: {OpenBets[i].Origin.Name} betted on {OpenBets[i].Target.Name} and won {payOut + OpenBets[i].Amount:N0} credits.");
                    OpenBets[i].Origin.Tell($"Your placed bet won! Payout: {payOut + OpenBets[i].Amount:N0} credits.");
                    OpenBets[i].Origin.SetAdditionalProperty(Plugin.CreditsKey, previousCredits + payOut);
                }
                else
                {
                    Console.WriteLine(
                        $"DBG: {OpenBets[i].Origin.Name} betted on {OpenBets[i].Target.Name} and lost {OpenBets[i].Amount:N0} credits.");
                    OpenBets[i].Origin.Tell($"Your placed bet lost! You lost {OpenBets[i].Amount:N0} credits.");
                    OpenBets[i].Origin.SetAdditionalProperty(Plugin.CreditsKey, previousCredits - payOut);
                }

                OpenBets.Remove(i);
            }
        }

        if (MapTime.ContainsKey(serverId))
        {
            MapTime[serverId] = DateTime.UtcNow;
            return;
        }

        MapTime.Add(serverId, DateTime.UtcNow);
    }
}

public class OpenBetData
{
    public EFClient? Origin { get; set; }
    public EFClient? Target { get; set; }
    public int TargetRank { get; set; }
    public int TotalRanked { get; set; }
    public int Amount { get; set; }
}
