using System.Linq.Expressions;
using Data.Abstractions;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
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
    private readonly Dictionary<int, OpenBetData> _openBets = new();

    /// <summary>
    /// Prints a list of initalised bets in-game
    /// </summary>
    /// <param name="gameEvent">GameEvent</param>
    public void GetOpenBets(GameEvent gameEvent)
    {
        if (_openBets.Count == 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)There are no open bets.");
            return;
        }

        gameEvent.Origin.Tell("(Color::Cyan)--Open Bets--");
        for (var i = 0; i < _openBets.Count; i++)
        {
            foreach (var (_, value) in _openBets)
            {
                gameEvent.Origin.Tell(
                    $"#(Color::Cyan){i + 1} (Color::White)- (Color::Green){value.Origin!.CleanedName} (Color::White)- (Color::Red){value.Target!.CleanedName} (Color::White)- (Color::Cyan){value.Amount}");
            }
        }
    }

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
    /// <param name="client">EFClient</param>
    /// <returns>Boolean. True if it has been more than 2 minutes</returns>
    public async Task<bool> CanBet(EFClient client)
    {
        var clientServerId = await client.CurrentServer.GetIdForServer();

        if (!_mapTime.ContainsKey(clientServerId)) return false;
        return _mapTime[clientServerId].AddMinutes(2) >= DateTime.UtcNow;
    }

    /// <summary>
    /// Main logic to instantiate a new bet
    /// </summary>
    /// <param name="gameEvent">GameEvent</param>
    /// <param name="amount">Amount of credits</param>
    public async void OnBetCreated(GameEvent gameEvent, int amount)
    {
        var totalKeys = _openBets.Count;

        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        var serverPlayerRank = await GetPlayerRankedPosition(gameEvent.Origin.ClientId, clientServerId);
        var serverTotalRanked = await GetTotalRankedPlayers(clientServerId);

        if (serverPlayerRank == 0)
        {
            gameEvent.Origin.Tell(
                $"(Color::Yellow){gameEvent.Target.Name} (Color::Yellow)needs to be ranked to set a bet.");
            return;
        }

        _openBets.Add(totalKeys + 1, new OpenBetData
        {
            Origin = gameEvent.Origin,
            Target = gameEvent.Target,
            TargetRank = serverPlayerRank,
            TotalRanked = serverTotalRanked,
            Amount = amount
        });

        gameEvent.Origin.Tell(
            $"Bet on {gameEvent.Target.Name} (Color::White)for (Color::Cyan){amount:N0} (Color::White)created.");
        gameEvent.Origin.Tell(
            "Payout is made after map rotation. Disconnecting will void bet.");
    }

    /// <summary>
    /// Checks if provided GameEvent's EFClient has higher score on their server
    /// </summary>
    /// <param name="gameEvent">GameEvent</param>
    public async void OnClientUpdated(GameEvent gameEvent)
    {
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        lock (_maxScore)
        {
            if (!_maxScore.ContainsKey(clientServerId)) _maxScore.Add(clientServerId, gameEvent.Origin.Score);
            if (_maxScore[clientServerId] < gameEvent.Origin.Score) _maxScore[clientServerId] = gameEvent.Origin.Score;
        }
    }

    /// <summary>
    /// Main logic for map rotation - paying out and removing old bets
    /// </summary>
    /// <param name="server">Server</param>
    public async void OnMatchEnd(Server server)
    {
        var serverId = await server.GetIdForServer();

        if (_openBets.Count > 0)
        {
            for (var i = 1; i <= _openBets.Count; i++)
            {
                if (!_openBets[i].Origin!.IsIngame || !_openBets[i].Target!.IsIngame)
                {
                    _openBets[i].Origin?.Tell("(Color::Red)Bet removed due to Target disconnecting.");
                    _openBets.Remove(i);
                    return;
                }

                if (!Plugin.PrimaryLogic!.AvailableFunds(_openBets[i].Origin, _openBets[i].Amount))
                {
                    _openBets[i].Origin?.Tell("(Color::Red)Bet removed due to you no longer having available funds.");
                    _openBets.Remove(i);
                    return;
                }

                var previousCredits = _openBets[i].Origin?.GetAdditionalProperty<int>(Plugin.CreditsKey);
                var payOut = _openBets[i].Amount;

                lock (_maxScore)
                {
                    if (_maxScore[serverId] >= _openBets[i].Target?.Score)
                    {
                        payOut *= _openBets[i].TargetRank / _openBets[i].TotalRanked;

                        Console.WriteLine(
                            $"DBG: {_openBets[i].Origin!.Name} bet on {_openBets[i].Target?.Name} and won {payOut + _openBets[i].Amount:N0} credits.");
                        _openBets[i].Origin
                            ?.Tell($"Your placed bet won! Payout: {payOut + _openBets[i].Amount:N0} credits.");
                        _openBets[i].Origin?.SetAdditionalProperty(Plugin.CreditsKey, previousCredits + payOut);
                    }
                    else
                    {
                        Console.WriteLine(
                            $"DBG: {_openBets[i].Origin?.Name} bet on {_openBets[i].Target?.Name} and lost {_openBets[i].Amount:N0} credits.");
                        _openBets[i].Origin?.Tell($"Your placed bet lost! You lost {_openBets[i].Amount:N0} credits.");
                        _openBets[i].Origin?.SetAdditionalProperty(Plugin.CreditsKey, previousCredits - payOut);
                    }
                }

                _openBets.Remove(i);
            }
        }

        if (_mapTime.ContainsKey(serverId))
        {
            _mapTime[serverId] = DateTime.UtcNow;
            return;
        }

        _mapTime.Add(serverId, DateTime.UtcNow);
    }
}

public class OpenBetData
{
    public EFClient? Origin { get; init; }
    public EFClient? Target { get; init; }
    public int TargetRank { get; init; }
    public int TotalRanked { get; init; }
    public int Amount { get; init; }
}
