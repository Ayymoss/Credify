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
    private readonly Dictionary<long, int> _maxPlayerScore = new();
    private readonly Dictionary<long, Dictionary<int, ClientState>> _maxServerScore = new();
    private readonly List<BetData> _betList = new();

    // TODO: Clean up code - move app printouts to call origins, shouldn't really be handled here

    /// <summary>
    /// Cancels client's open bet(s)
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    /// <returns><see cref="int"/> - amount of bets cancelled</returns>
    public int CancelBets(EFClient client)
    {
        var cancelledBets = new List<BetData>();
        var count = 0;

        lock (_betList)
        {
            cancelledBets.AddRange(_betList.Where(bet => bet.Origin.ClientId == client.ClientId));
            foreach (var bet in cancelledBets.Where(bet => !bet.BetCompleted))
            {
                _betList.Remove(bet);
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Prints a list of initialise bets in-game
    /// </summary>
    /// <returns><see cref="BetData"/></returns>
    public IReadOnlyList<BetData> GetBetsList() => _betList;

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
    public bool MaximumTimePassed(EFClient client)
    {
        var clientServerId = client.CurrentServer.EndPoint;

        if (!_mapTime.ContainsKey(clientServerId)) return false;
        return _mapTime[clientServerId].AddMinutes(Plugin.CreditsMaximumBetTime) >= DateTime.UtcNow;

    }

    /// <summary>
    /// Return bool on configured players is more than server's players
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    /// <returns><see cref="bool"/> - True if Server has equal or more than configured, False if not</returns>
    public bool MinimumPlayers(EFClient client) =>
        client.CurrentServer.GetClientsAsList().Count >= Plugin.CreditsMinimumPlayers;

    /// <summary>
    /// Converts Team Enum to String
    /// </summary>
    /// <param name="teamType">Team enum</param>
    /// <returns><see cref="string"/> Team Name</returns>
    public string TeamEnumToString(EFClient.TeamType teamType)
    {
        return teamType switch
        {
            EFClient.TeamType.Spectator => "Spectator",
            EFClient.TeamType.Allies => "Allies",
            EFClient.TeamType.Axis => "Axis",
            _ => "Unknown"
        };
    }

    /// <summary>
    /// Converts Team String to Enum
    /// </summary>
    /// <param name="teamName">Team formatted string</param>
    /// <returns><see cref="EFClient"/>.TeamType <see cref="Enum"/></returns>
    public EFClient.TeamType TeamStringToEnum(string teamName)
    {
        return teamName switch
        {
            "Spectator" => EFClient.TeamType.Spectator,
            "Allies" => EFClient.TeamType.Allies,
            "Axis" => EFClient.TeamType.Axis,
            _ => EFClient.TeamType.Unknown
        };
    }

    /// <summary>
    /// Instantiates a new team bet 
    /// </summary>
    /// <param name="gameEvent"><see cref="GameEvent"/></param>
    /// <param name="teamName"><see cref="EFClient"/>.TeamType</param>
    /// <param name="amount"><see cref="int"/> credit amount</param>
    public async void CreateTeamBet(GameEvent gameEvent, string teamName, int amount)
    {
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        var serverTotalRanked = await GetTotalRankedPlayers(clientServerId);
        var teamRankAverage = 0;
        var index = 0;

        foreach (var client in gameEvent.Owner.GetClientsAsList()
                     .Where(client => TeamEnumToString(client.Team) == teamName))
        {
            var serverPlayerRank = await GetPlayerRankedPosition(client.ClientId, clientServerId);
            if (serverPlayerRank == 0) continue;

            teamRankAverage += serverPlayerRank;
            index++;
        }

        if (index == 0)
        {
            gameEvent.Origin.Tell("(Color::Yellow)No one on the team is ranked");
            return;
        }

        teamRankAverage /= index;

        _betList.Add(new BetData
        {
            Origin = gameEvent.Origin,
            Server = clientServerId,
            TargetTeam = teamName,
            InitAmount = amount,
            TeamRankAverage = teamRankAverage,
            TotalRanked = serverTotalRanked,
            BetCompleted = false
        });

        gameEvent.Origin.Tell(
            $"Bet on {teamName} (Color::White)for (Color::Cyan){amount:N0} (Color::White)created");
    }

    /// <summary>
    /// Instantiates a new player bet
    /// </summary>
    /// <param name="gameEvent"><see cref="GameEvent"/></param>
    /// <param name="amount">Amount of credits</param>
    public async void CreatePlayerBet(GameEvent gameEvent, int amount)
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
            TargetPlayer = gameEvent.Target,
            Server = clientServerId,
            TargetPlayerRank = serverPlayerRank,
            TotalRanked = serverTotalRanked,
            InitAmount = amount,
            BetCompleted = false
        });

        gameEvent.Origin.Tell(
            $"Bet on {gameEvent.Target.Name} (Color::White)for (Color::Cyan){amount:N0} (Color::White)created");
    }

    /// <summary>
    /// Checks if provided GameEvent's EFClient has higher score on their server
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    public void OnUpdate(EFClient client)
    {
        if (!_betList.Any()) return;

        var clientServerId = client.CurrentServer.EndPoint;
        lock (_maxPlayerScore)
        {
            if (!_maxPlayerScore.ContainsKey(clientServerId))
            {
                _maxPlayerScore.Add(clientServerId, client.Score);
            }

            if (_maxPlayerScore[clientServerId] < client.Score)
            {
                _maxPlayerScore[clientServerId] = client.Score;
            }
        }
    }

    /// <summary>
    /// Initialise server score and update client in it
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    public void OnJoinTeam(EFClient client)
    {
        var clientServerId = client.CurrentServer.EndPoint;

        if (client.Team is EFClient.TeamType.Spectator or EFClient.TeamType.Unknown) return;

        lock (_maxServerScore)
        {
            // Server doesn't exist in dictionary
            if (!_maxServerScore.ContainsKey(clientServerId))
            {
                _maxServerScore.Add(clientServerId,
                    new Dictionary<int, ClientState>
                    {
                        {
                            client.ClientId, new ClientState
                            {
                                Score = client.Score,
                                TeamName = client.TeamName
                            }
                        }
                    });
                return;
            }

            // Client doesn't exist in dictionary
            if (!_maxServerScore[clientServerId].ContainsKey(client.ClientId))
            {
                _maxServerScore[clientServerId].Add(client.ClientId, new ClientState
                {
                    Score = client.Score,
                    TeamName = client.TeamName
                });
                return;
            }

            // Client exists in dictionary
            if (_maxServerScore[clientServerId].ContainsKey(client.ClientId))
            {
                _maxServerScore[clientServerId][client.ClientId].Score = client.Score;
                _maxServerScore[clientServerId][client.ClientId].TeamName = client.TeamName;
            }
        }
    }

    /// <summary>
    /// Prints queued messages to people whose bets have completed
    /// </summary>
    /// <param name="client"><see cref="EFClient"/></param>
    public void OnKill(EFClient client)
    {
        var expiredBet = new List<BetData>();
        var clientServerId = client.CurrentServer.EndPoint;

        if (!_betList.Any()) return;

        lock (_maxServerScore)
        {
            if (_maxServerScore.ContainsKey(clientServerId) ||
                _maxServerScore[clientServerId].ContainsKey(client.ClientId))
            {
                _maxServerScore[clientServerId][client.ClientId].Score = client.Score;
            }
        }

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
                        expiredBet.Add(completedBet);
                        continue;
                    }

                    // Win condition
                    if (completedBet.TargetWon)
                    {
                        if (completedBet.TargetPlayer != null)
                        {
                            client.Tell("Your bet (Color::Green)won (Color::Cyan)" +
                                        $"{completedBet.PayOut + completedBet.InitAmount:N0} " +
                                        $"(Color::White)credits on {completedBet.TargetPlayer.Name}");
                        }

                        if (completedBet.TargetTeam != null)
                        {
                            client.Tell("Your bet (Color::Green)won (Color::Cyan)" +
                                        $"{completedBet.PayOut + completedBet.InitAmount:N0} " +
                                        $"(Color::White)credits on {completedBet.TargetTeam}");
                        }

                        expiredBet.Add(completedBet);
                        continue;
                    }

                    // Loss condition
                    if (completedBet.TargetPlayer != null)
                    {
                        client.Tell($"Your bet (Color::Red)lost (Color::Cyan){completedBet.InitAmount:N0} " +
                                    $"(Color::White)credits on {completedBet.TargetPlayer.Name}");
                    }

                    if (completedBet.TargetTeam != null)
                    {
                        client.Tell($"Your bet (Color::Red)lost (Color::Cyan){completedBet.InitAmount:N0} " +
                                    $"(Color::White)credits on {completedBet.TargetTeam}");
                    }

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
                if (!Plugin.PrimaryLogic!.AvailableFunds(openBet.Origin, openBet.InitAmount))
                {
                    openBet.Message = "(Color::Red)Bet was removed due to you no longer having available credits";
                    openBet.BetCompleted = true;
                    continue;
                }

                var previousCredits = openBet.Origin.GetAdditionalProperty<int>(Plugin.CreditsKey);
                var payOut = openBet.InitAmount;

                if (openBet.TargetPlayer != null)
                {
                    if (openBet.TargetPlayer.State != EFClient.ClientState.Connected)
                    {
                        openBet.Message =
                            $"(Color::Red)Your bet was removed due to {openBet.TargetPlayer.CleanedName} leaving";
                        openBet.BetCompleted = true;
                        continue;
                    }

                    lock (_maxPlayerScore)
                    {
                        if (_maxPlayerScore[serverId] <= openBet.TargetPlayer.Score)
                        {
                            payOut = openBet.InitAmount * openBet.TargetPlayerRank / openBet.TotalRanked;

                            openBet.PayOut = payOut;
                            openBet.BetCompleted = true;
                            openBet.TargetWon = true;

                            if (openBet.Origin.State == EFClient.ClientState.Connected)
                                openBet.Origin.SetAdditionalProperty(Plugin.CreditsKey, previousCredits + payOut);
                            else Plugin.PrimaryLogic.OnDisconnect(openBet.Origin);
                        }
                        else
                        {
                            openBet.PayOut = payOut;
                            openBet.BetCompleted = true;
                            openBet.TargetWon = false;

                            if (openBet.Origin.State == EFClient.ClientState.Connected)
                                openBet.Origin?.SetAdditionalProperty(Plugin.CreditsKey, previousCredits - payOut);
                            else Plugin.PrimaryLogic.OnDisconnect(openBet.Origin);
                        }

                        _maxPlayerScore.Remove(serverId);
                    }
                }

                if (openBet.TargetTeam != null)
                {
                    lock (_maxServerScore)
                    {
                        var alliesScore = _maxServerScore[serverId].Values.Where(state =>
                                state.TeamName.Equals("allies", StringComparison.InvariantCultureIgnoreCase))
                            .Sum(state => state.Score);

                        var axisScore = _maxServerScore[serverId].Values.Where(state =>
                                state.TeamName.Equals("axis", StringComparison.InvariantCultureIgnoreCase))
                            .Sum(state => state.Score);


                        if (axisScore < alliesScore) // Allies Won
                        {
                            if (openBet.TargetTeam == TeamEnumToString(EFClient.TeamType.Allies))
                            {
                                payOut = openBet.InitAmount * openBet.TeamRankAverage;

                                openBet.PayOut = payOut;
                                openBet.BetCompleted = true;
                                openBet.TargetWon = true;

                                if (openBet.Origin.State == EFClient.ClientState.Connected)
                                    openBet.Origin.SetAdditionalProperty(Plugin.CreditsKey,
                                        previousCredits + payOut);
                                else Plugin.PrimaryLogic.OnDisconnect(openBet.Origin);
                            }
                            else
                            {
                                openBet.PayOut = payOut;
                                openBet.BetCompleted = true;
                                openBet.TargetWon = false;

                                if (openBet.Origin.State == EFClient.ClientState.Connected)
                                    openBet.Origin?.SetAdditionalProperty(Plugin.CreditsKey,
                                        previousCredits - payOut);
                                else Plugin.PrimaryLogic.OnDisconnect(openBet.Origin);
                            }
                        }

                        if (axisScore > alliesScore) // Axis Won
                        {
                            if (openBet.TargetTeam == TeamEnumToString(EFClient.TeamType.Axis))
                            {
                                payOut = openBet.InitAmount * openBet.TeamRankAverage;

                                openBet.PayOut = payOut;
                                openBet.BetCompleted = true;
                                openBet.TargetWon = true;

                                if (openBet.Origin.State == EFClient.ClientState.Connected)
                                    openBet.Origin.SetAdditionalProperty(Plugin.CreditsKey,
                                        previousCredits + payOut);
                                else Plugin.PrimaryLogic.OnDisconnect(openBet.Origin);
                            }
                            else
                            {
                                openBet.PayOut = payOut;
                                openBet.BetCompleted = true;
                                openBet.TargetWon = false;

                                if (openBet.Origin.State == EFClient.ClientState.Connected)
                                    openBet.Origin?.SetAdditionalProperty(Plugin.CreditsKey,
                                        previousCredits - payOut);
                                else Plugin.PrimaryLogic.OnDisconnect(openBet.Origin);
                            }
                        }

                        foreach (var serverScore in _maxServerScore[serverId].Values)
                        {
                            serverScore.Score = 0;
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// OnDisconnect remove client from _MaxServerScore (if exists)
    /// </summary>
    /// <param name="client"></param>
    public void OnDisconnect(EFClient client)
    {
        var clientServerId = client.CurrentServer.EndPoint;
        lock (_maxServerScore)
        {
            if (_maxServerScore.ContainsKey(clientServerId) ||
                _maxServerScore[clientServerId].ContainsKey(client.ClientId))
            {
                _maxServerScore[clientServerId].Remove(client.ClientId);
            }
        }
    }
}

public class BetData
{
    public EFClient Origin { get; init; }
    public EFClient TargetPlayer { get; init; }
    public long Server { get; set; }
    public string TargetTeam { get; init; }
    public int TeamRankAverage { get; init; }
    public int TargetPlayerRank { get; init; }
    public int TotalRanked { get; init; }
    public int InitAmount { get; init; }
    public int PayOut { get; set; }
    public string Message { get; set; }
    public bool TargetWon { get; set; }
    public bool BetCompleted { get; set; }
}

public class ClientState
{
    public int Score { get; set; }
    public string TeamName { get; set; }
}
