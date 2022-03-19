using System.Linq;
using System.Linq.Expressions;
using Data.Abstractions;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using Stats.Config;

namespace CreditsPlugin;

public class BetManager
{
    public BetManager(IDatabaseContextFactory contextFactory, StatsConfiguration statsConfig)
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

    public Expression<Func<EFClientRankingHistory, bool>> GetNewRankingFunc(int? clientId = null, long? serverId = null)
    {
        return (ranking) => ranking.ServerId == serverId
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
            .Where(GetNewRankingFunc(serverId: serverId))
            .CountAsync();
    }
    
    
    // TODO: Fix this. Called on Event. Event calls foreach person in server.
    // Only need to do the work once an update.
    // Also need to fix check for map rotation.
    public static int[] MapEndHighestFragger(Server server)
    {
        var highestScore = 0;
        var highestScoreClientId = 0;
        var highestScoreOld = 0;
        var highestScoreClientIdOld = 0;

        foreach (var client in server.GetClientsAsList().Where(client => highestScore < client.Score))
        {
            highestScore = client.Score;
            highestScoreClientId = client.ClientId;
        }
        
        if (highestScoreOld != highestScore && highestScore == 0)
        {
            Console.WriteLine("Map rotated?");
        }

        if (highestScore > 0)
        {
            highestScoreOld = highestScore;
            highestScoreClientIdOld = highestScoreClientId;
        }

       
        
        return highestScore == 0 ? new[] {0, 0} : new[] {highestScoreClientId, highestScore};
    }
}

public class ClientEntry
{
    public int ClientId { get; set; }
    public long Score { get; set; }
}

public class ServerEntry
{
    public long ServerId { get; set; }

    public long DateNow { get; set; }
}

/*
TODO: See below
1. Get player rank in server
2. Store player score on update
    2.1 Need to store PER server
3. Store time when map updates 
    3.1 Need to store PER server
4. Check if user (command sender) is within 2 minute window
5. Write command logic

2.1/3.1 -> Create a new server object with required parameters 
    Update when new values are provided by IW4MAdmin

*/
