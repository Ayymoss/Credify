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
    public BetManager(IDatabaseContextFactory contextFactory, StatsConfiguration statsConfig)
    {
        _config = statsConfig;
        _contextFactory = contextFactory;
    }

    private readonly StatsConfiguration _config;
    private readonly IDatabaseContextFactory _contextFactory;

    private readonly Dictionary<long, DateTime> _mapTime = new();
    private readonly Dictionary<long, int> _maxScore = new();
    private readonly List<BetData> _betList = new();

    // TODO: Check
    // Clean up code - move app printouts to call origins, shouldn't really be handled here

    /// <summary>
    /// Prints a list of initialise bets in-game
    /// </summary>
    /// <returns><see cref="BetData"/></returns>
    public IReadOnlyList<BetData> GetOpenBets() => _betList;

    /// <summary>
    /// Gets player's ranked position for given server
    /// </summary>
    /// <param name="clientId">Client's ID</param>
    /// <param name="serverId">Server's ID</param>
    /// <returns>Their ranked position on the server</returns>
    private async Task<int> GetPlayerRankedPosition(int clientId, long serverId)
    {
        await using var context = _contextFactory.CreateContext(false);

        var clientRanking = await context.Set<EFClientRankingHistory>()
            .Where(r => r.ClientId == clientId)
            .Where(r => r.ServerId == serverId)
            .Where(r => r.Newest)
            .FirstOrDefaultAsync();

        return clientRanking?.Ranking + 1 ?? 0;
    }

    /// <summary>
    /// Queries database for provided Server ID
    /// </summary>
    /// <param name="serverId">Server's ID</param>
    /// <returns></returns>
    private Expression<Func<EFClientRankingHistory, bool>> GetNewRankingFunc(long? serverId = null)
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

    /// <summary>
    /// Gets the total amount of ranked players on given server
    /// </summary>
    /// <param name="serverId">Server's ID</param>
    /// <returns>The amount of ranked players</returns>
    private async Task<int> GetTotalRankedPlayers(long serverId)
    {
        await using var context = _contextFactory.CreateContext(false);

        return await context.Set<EFClientRankingHistory>()
            .Where(GetNewRankingFunc(serverId))
            .CountAsync();
    }

    /// <summary>
    /// Whether it's been 2 minutes since map change
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    /// <returns>Boolean. True if it has been more than 2 minutes</returns>
    public async Task<bool> CanBet(EFClient client)
    {
        var clientServerId = client.CurrentServer.EndPoint;

        if (!_mapTime.ContainsKey(clientServerId)) return false;
        return _mapTime[clientServerId].AddMinutes(2) >= DateTime.UtcNow;
    }

    /// <summary>
    /// Main logic to instantiate a new bet
    /// </summary>
    /// <param name="gameEvent"><see cref="GameEvent"/></param>
    /// <param name="amount">Amount of credits</param>
    public async void OnBetCreated(GameEvent gameEvent, int amount)
    {
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        var serverPlayerRank = await GetPlayerRankedPosition(gameEvent.Origin.ClientId, clientServerId);
        var serverTotalRanked = await GetTotalRankedPlayers(clientServerId);

        if (serverPlayerRank == 0)
        {
            gameEvent.Origin.Tell(
                $"(Color::Yellow){gameEvent.Target.Name} (Color::Yellow)needs to be ranked to set a bet");
            return;
        }

        _betList.Add(new BetData
        {
            Origin = gameEvent.Origin,
            Target = gameEvent.Target,
            TargetRank = serverPlayerRank,
            TotalRanked = serverTotalRanked,
            InitAmount = amount,
            PayOut = 0,
            Message = null,
            TargetWon = false,
            BetCompleted = false
        });

        gameEvent.Origin.Tell(
            $"Bet on {gameEvent.Target.Name} (Color::White)for (Color::Cyan){amount:N0} (Color::White)created");
        gameEvent.Origin.Tell(
            "Payout is made after map rotation. Disconnecting will void bet");
    }

    /// <summary>
    /// Checks if provided GameEvent's EFClient has higher score on their server
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    public void OnClientUpdated(EFClient client)
    {
        if (_betList.Count == 0) return;

        var clientServerId = client.CurrentServer.EndPoint;
        lock (_maxScore)
        {
            if (!_maxScore.ContainsKey(clientServerId)) _maxScore.Add(clientServerId, client.Score);
            if (_maxScore[clientServerId] < client.Score) _maxScore[clientServerId] = client.Score;
        }
    }

    /// <summary>
    /// Prints queued messages to people whose bets have completed
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    public void OnKillMessageSystem(EFClient client)
    {
        var expiredBet = new List<BetData>();

        if (!_betList.Any()) return;
        lock (expiredBet)
        {
            foreach (var completedBet in
                     _betList.Where(completedBet => completedBet.Origin.ClientId == client.ClientId))
            {
                lock (completedBet)
                {
                    if (completedBet.BetCompleted == false) continue;

                    if (completedBet.Message is not null)
                    {
                        client.Tell(completedBet.Message);
                        continue;
                    }

                    if (completedBet.TargetWon)
                    {
                        client.Tell("Your bet (Color::Green)won (Color::Cyan)" +
                                    $"{completedBet.PayOut + completedBet.InitAmount:N0} " +
                                    $"(Color::White)credits on {completedBet.Target.Name}");
                        expiredBet.Add(completedBet);
                        continue;
                    }

                    client.Tell($"Your bet (Color::Red)lost (Color::Cyan){completedBet.InitAmount:N0} " +
                                $"(Color::White)credits on {completedBet.Target.Name}");
                    expiredBet.Add(completedBet);
                }
            }

            foreach (var completedBet in expiredBet)
            {
                _betList.Remove(completedBet);
            }
        }
    }

    /// <summary>
    /// Main logic for map rotation - paying out and removing old bets
    /// </summary>
    /// <param name="server"><see cref="Server"/></param>
    public void OnMatchEnd(Server server)
    {
        var serverId = server.EndPoint;

        if (_mapTime.ContainsKey(serverId)) _mapTime[serverId] = DateTime.UtcNow;
        else _mapTime.Add(serverId, DateTime.UtcNow);

        if (!_betList.Any()) return;
        foreach (var openBet in _betList)
        {
            lock (openBet)
            {
                if (openBet.Target.State != EFClient.ClientState.Connected)
                {
                    openBet.Message = $"(Color::Red)Your bet was removed due to {openBet.Target.CleanedName} leaving.";
                    openBet.BetCompleted = true;
                    continue;
                }

                if (!Plugin.PrimaryLogic!.AvailableFunds(openBet.Origin, openBet.InitAmount))
                {
                    openBet.Message = "(Color::Red)Bet was removed due to you no longer having available credits.";
                    openBet.BetCompleted = true;
                    continue;
                }

                var previousCredits = openBet.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey);
                var payOut = openBet.InitAmount;

                lock (_maxScore)
                {
                    if (_maxScore[serverId] <= openBet.Target.Score)
                    {
                        payOut = openBet.InitAmount * openBet.TargetRank / openBet.TotalRanked;
                        
                        openBet.PayOut = payOut;
                        openBet.BetCompleted = true;
                        openBet.TargetWon = true;

                        if (openBet.Origin.State == EFClient.ClientState.Connected) 
                            openBet.Origin.SetAdditionalProperty(Plugin.CreditsKey, previousCredits + payOut);
                        else Plugin.PrimaryLogic.WriteCredits(openBet.Origin);
                    }
                    else
                    {
                        openBet.PayOut = payOut;
                        openBet.BetCompleted = true;

                        if (openBet.Origin.State == EFClient.ClientState.Connected)
                            openBet.Origin?.SetAdditionalProperty(Plugin.CreditsKey, previousCredits - payOut);
                        else Plugin.PrimaryLogic.WriteCredits(openBet.Origin);
                    }
                }
            }
        }

        lock (_maxScore)
        {
            _maxScore.Remove(serverId);
        }
    }
}

public class BetData
{
    public EFClient Origin { get; init; }
    public EFClient Target { get; init; }
    public int TargetRank { get; init; }
    public int TotalRanked { get; init; }
    public int InitAmount { get; init; }
    public int PayOut { get; set; }
    public string Message { get; set; }
    public bool TargetWon { get; set; }
    public bool BetCompleted { get; set; }
}
