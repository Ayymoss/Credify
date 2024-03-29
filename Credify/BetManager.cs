﻿using System.Collections.Concurrent;
using System.Linq.Expressions;
using Credify.Models;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using Stats.Config;

namespace Credify;

public class BetManager
{
    #region Fields

    private readonly PersistenceManager _persistenceManager;
    private readonly StatsConfiguration _configStats;
    private readonly CredifyConfiguration _credifyConfig;
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly ConcurrentDictionary<long, DateTime> _mapTime = new();
    private readonly ConcurrentDictionary<long, int> _maxPlayerScore = new();
    private readonly ConcurrentDictionary<long, Dictionary<int, ClientState>> _maxServerScore = new();
    private readonly List<BetData> _betList = new();
    private readonly SemaphoreSlim _onMapEnd = new(1, 1);
    private readonly SemaphoreSlim _onJoinTeam = new(1, 1);
    private readonly SemaphoreSlim _onKill = new(1, 1);
    private readonly SemaphoreSlim _onUpdate = new(1, 1);
    private readonly SemaphoreSlim _onDisconnect = new(1, 1);

    #endregion

    public BetManager(PersistenceManager persistenceManager, IDatabaseContextFactory contextFactory,
        StatsConfiguration statsConfigStats,
        CredifyConfiguration credifyConfig)
    {
        _persistenceManager = persistenceManager;
        _configStats = statsConfigStats;
        _credifyConfig = credifyConfig;
        _contextFactory = contextFactory;
    }

    #region Expressions

    public IReadOnlyList<BetData> GetBetsList() => _betList;

    private static bool IsSpectatorOrUnknown(EFClient.TeamType team) =>
        team is EFClient.TeamType.Spectator or EFClient.TeamType.Unknown;

    public bool MinimumPlayers(EFClient client) =>
        client.CurrentServer.GetClientsAsList().Count >= _credifyConfig.Core.MinimumPlayersRequiredForPlayerAndTeamBets;

    #endregion

    #region Bet Management

    public async Task CreatePlayerBet(GameEvent gameEvent, long amount)
    {
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        var serverPlayerRank = await GetPlayerRankedPosition(gameEvent.Origin.ClientId, clientServerId);
        var serverTotalRanked = await GetTotalRankedPlayers(clientServerId);

        if (serverPlayerRank == 0)
        {
            gameEvent.Origin.Tell(
                _credifyConfig.Translations.TargetPlayerNeedsToBeRanked.FormatExt(gameEvent.Target.Name));
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

        gameEvent.Origin.Tell(_credifyConfig.Translations.BetCreatedOnTarget
            .FormatExt(gameEvent.Target.Name, $"{amount:N0}"));
    }

    public async void CreateTeamBet(GameEvent gameEvent, string teamName, long amount)
    {
        var clientServerId = await gameEvent.Origin.CurrentServer.GetIdForServer();
        var serverTotalRanked = await GetTotalRankedPlayers(clientServerId);
        var teamRankAverage = 0;
        var index = 0;

        foreach (var client in gameEvent.Owner.GetClientsAsList()
                     .Where(client => client.TeamName == teamName.ToLower()))
        {
            var serverPlayerRank = await GetPlayerRankedPosition(client.ClientId, clientServerId);
            if (serverPlayerRank == 0) continue;

            teamRankAverage += serverPlayerRank;
            index++;
        }

        if (index is 0)
        {
            gameEvent.Origin.Tell(_credifyConfig.Translations.NoRankedPlayersOnTeam);
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

        gameEvent.Origin.Tell(_credifyConfig.Translations.BetCreatedOnTarget.FormatExt(teamName, $"{amount:N0}"));
    }

    private async Task CompleteBet(BetData openBet, long previousCredits, bool targetWon)
    {
        openBet.BetCompleted = true;
        openBet.TargetWon = targetWon;

        if (openBet.Origin.State is EFClient.ClientState.Connected)
        {
            openBet.Origin.SetAdditionalProperty(Plugin.CreditsAmount,
                targetWon ? previousCredits + openBet.PayOut : previousCredits - openBet.PayOut);
        }
        else
        {
            await _persistenceManager.WriteClientCreditsAsync(openBet.Origin);
        }

        _persistenceManager.StatisticsState.CreditsSpent += (ulong)openBet.InitAmount;

        if (targetWon)
        {
            _persistenceManager.StatisticsState.CreditsWon += (ulong)(openBet.PayOut + openBet.InitAmount);
            await openBet.Origin.CurrentServer.BroadcastAsync(new[]
            {
                _credifyConfig.Translations.GambleWonAnnouncement.FormatExt(Plugin.PluginName,
                    openBet.Origin.CleanedName,
                    $"{openBet.PayOut + openBet.InitAmount:N0}", "!crbet"),
            }, Utilities.IW4MAdminClient());
        }
    }

    public void RemoveCompletedBets(EFClient client)
    {
        var expiredBet = _betList.Where(completedBet => completedBet.Origin.ClientId == client.ClientId)
            .Where(completedBet => completedBet.BetCompleted).ToList();

        foreach (var bet in expiredBet) _betList.Remove(bet);
    }

    #endregion

    #region Event Handlers

    public async Task OnUpdateAsync(EFClient client)
    {
        try
        {
            await _onUpdate.WaitAsync();
            if (!_betList.Any()) return;
            var clientServerId = client.CurrentServer.EndPoint;
            if (!_maxPlayerScore.ContainsKey(clientServerId)) _maxPlayerScore.TryAdd(clientServerId, client.Score);
            if (_maxPlayerScore[clientServerId] < client.Score) _maxPlayerScore[clientServerId] = client.Score;
        }
        finally
        {
            if (_onUpdate.CurrentCount == 0) _onUpdate.Release();
        }
    }

    public async Task OnJoinTeamAsync(EFClient client)
    {
        try
        {
            await _onJoinTeam.WaitAsync();
            if (IsSpectatorOrUnknown(client.Team)) return;
            var clientServerId = client.CurrentServer.EndPoint;
            NotifyClaimableBets(client);
            UpdateClientScoreAndTeam(client, clientServerId);
        }
        finally
        {
            if (_onJoinTeam.CurrentCount == 0) _onJoinTeam.Release();
        }
    }

    public async Task OnKillAsync(EFClient client)
    {
        try
        {
            await _onKill.WaitAsync();
            var clientServerId = client.CurrentServer.EndPoint;
            if (!_betList.Any()) return;
            if (_maxServerScore.ContainsKey(clientServerId) ||
                _maxServerScore[clientServerId].ContainsKey(client.ClientId))
                _maxServerScore[clientServerId][client.ClientId].Score = client.Score;
        }
        finally
        {
            if (_onKill.CurrentCount == 0) _onKill.Release();
        }
    }

    public async Task OnMapEndAsync(Server server)
    {
        try
        {
            await _onMapEnd.WaitAsync();
            var serverId = server.EndPoint;
            UpdateMapTime(serverId);
            if (!_betList.Any()) return;
            await ProcessBets(serverId);
        }
        finally
        {
            if (_onMapEnd.CurrentCount == 0) _onMapEnd.Release();
        }
    }

    public async Task OnDisconnectAsync(EFClient client)
    {
        try
        {
            await _onDisconnect.WaitAsync();
            var clientServerId = client.CurrentServer.EndPoint;
            if (!_maxServerScore.ContainsKey(clientServerId)) return;
            if (_maxServerScore[clientServerId].ContainsKey(client.ClientId))
                _maxServerScore[clientServerId].Remove(client.ClientId);
        }
        finally
        {
            if (_onDisconnect.CurrentCount == 0) _onDisconnect.Release();
        }
    }

    #endregion

    #region Helper Methods

    private void NotifyClaimableBets(EFClient client)
    {
        var completedMessages = CompletedBetsMessages(client);
        if (completedMessages is not null && completedMessages.Any())
            client.Tell(_credifyConfig.Translations.ClaimableBetsAvailable);
    }

    private void UpdateClientScoreAndTeam(EFClient client, long clientServerId)
    {
        if (!_maxServerScore.ContainsKey(clientServerId))
        {
            AddServerWithClient(client, clientServerId);
            return;
        }

        if (!_maxServerScore[clientServerId].ContainsKey(client.ClientId))
        {
            AddClientToServer(client, clientServerId);
            return;
        }

        UpdateExistingClient(client, clientServerId);
    }

    private void AddServerWithClient(EFClient client, long clientServerId)
    {
        _maxServerScore.TryAdd(clientServerId, new Dictionary<int, ClientState>
        {
            {
                client.ClientId, new ClientState
                {
                    Score = client.Score,
                    TeamName = client.TeamName
                }
            }
        });
    }

    private void AddClientToServer(EFClient client, long clientServerId)
    {
        _maxServerScore[clientServerId].Add(client.ClientId, new ClientState
        {
            Score = client.Score,
            TeamName = client.TeamName
        });
    }

    private void UpdateExistingClient(EFClient client, long clientServerId)
    {
        _maxServerScore[clientServerId][client.ClientId].Score = client.Score;
        _maxServerScore[clientServerId][client.ClientId].TeamName = client.TeamName;
    }

    public List<string>? CompletedBetsMessages(EFClient client)
    {
        var clientMessages = new List<string>();

        if (!_betList.Any()) return null;

        foreach (var completedBet in _betList.Where(completedBet =>
                     completedBet.Origin.ClientId == client.ClientId))
        {
            lock (completedBet)
            {
                if (!completedBet.BetCompleted) continue;
                if (completedBet.Message is not null)
                {
                    clientMessages.Add(completedBet.Message);
                    continue;
                }

                // Win condition
                if (completedBet.TargetWon)
                {
                    if (completedBet.TargetPlayer != null)
                        clientMessages.Add(_credifyConfig.Translations.BetWonOnTarget.FormatExt(
                            $"{completedBet.InitAmount:N0}",
                            completedBet.TargetPlayer.Name));
                    if (completedBet.TargetTeam != null)
                        clientMessages.Add(_credifyConfig.Translations.BetWonOnTarget.FormatExt(
                            $"{completedBet.InitAmount:N0}",
                            completedBet.TargetTeam));
                    continue;
                }

                // Loss condition
                if (completedBet.TargetPlayer != null)
                    clientMessages.Add(_credifyConfig.Translations.BetLostOnTarget.FormatExt(
                        $"{completedBet.InitAmount:N0}",
                        completedBet.TargetPlayer.Name));
                if (completedBet.TargetTeam != null)
                    clientMessages.Add(
                        _credifyConfig.Translations.BetLostOnTarget.FormatExt($"{completedBet.InitAmount:N0}",
                            completedBet.TargetTeam));
            }
        }

        return clientMessages;
    }

    private void UpdateMapTime(long serverId)
    {
        if (_mapTime.ContainsKey(serverId)) _mapTime[serverId] = DateTime.UtcNow;
        else _mapTime.TryAdd(serverId, DateTime.UtcNow);
    }

    private async Task ProcessBets(long serverId)
    {
        foreach (var openBet in _betList)
        {
            if (!_persistenceManager.AvailableFunds(openBet.Origin, openBet.InitAmount))
            {
                RemoveBetDueToInsufficientFunds(openBet);
                continue;
            }

            var previousCredits = openBet.Origin.GetAdditionalProperty<long>(Plugin.CreditsAmount);
            openBet.PayOut = openBet.InitAmount;

            if (openBet.TargetPlayer != null) await ProcessPlayerBet(serverId, openBet, previousCredits);
            if (openBet.TargetTeam != null) await ProcessTeamBet(serverId, openBet, previousCredits);
        }

        ResetServerScores(serverId);
    }

    private void RemoveBetDueToInsufficientFunds(BetData openBet)
    {
        openBet.Message = _credifyConfig.Translations.BetRemovedDueToInsufficientCredits;
        openBet.BetCompleted = true;
    }

    private async Task ProcessPlayerBet(long serverId, BetData openBet, long previousCredits)
    {
        if (openBet.TargetPlayer.State != EFClient.ClientState.Connected)
        {
            RemoveBetDueToPlayerLeaving(openBet);
            return;
        }

        _maxPlayerScore.TryGetValue(serverId, out var result);
        if (result <= openBet.TargetPlayer.Score)
        {
            var taxBook = new TaxBook(openBet.InitAmount * (openBet.TargetPlayerRank / openBet.TotalRanked),
                openBet.InitAmount, _credifyConfig.Core.BankTax);
            await _persistenceManager.AddBankCreditsAsync(taxBook.Tax);
            openBet.PayOut = taxBook.NetChange;

            await CompleteBet(openBet, previousCredits, true);
        }
        else await CompleteBet(openBet, previousCredits, false);
    }

    private void RemoveBetDueToPlayerLeaving(BetData openBet)
    {
        openBet.Message = _credifyConfig.Translations.BetRemovedDueToTargetLeaving
            .FormatExt(openBet.TargetPlayer.CleanedName);
        openBet.BetCompleted = true;
    }

    private async Task ProcessTeamBet(long serverId, BetData openBet, long previousCredits)
    {
        var scores = _maxServerScore[serverId].Values
            .GroupBy(state => state.TeamName, StringComparer.InvariantCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Sum(state => state.Score));

        var alliesScore = scores.TryGetValue("allies", out var aScore) ? aScore : 0;
        var axisScore = scores.TryGetValue("axis", out var axScore) ? axScore : 0;

        var winningTeam = alliesScore > axisScore ? "Allies" : "Axis";
        await ProcessTeamWin(openBet, previousCredits, winningTeam);
    }

    private async Task ProcessTeamWin(BetData openBet, long previousCredits, string winningTeam)
    {
        var isWin = openBet.TargetTeam!.Equals(winningTeam, StringComparison.InvariantCultureIgnoreCase);
        if (isWin)
        {
            var taxBook = new TaxBook(openBet.InitAmount * (openBet.TeamRankAverage / openBet.TotalRanked),
                openBet.InitAmount, _credifyConfig.Core.BankTax);
            await _persistenceManager.AddBankCreditsAsync(taxBook.Tax);
            openBet.PayOut = taxBook.NetChange;
        }

        await CompleteBet(openBet, previousCredits, isWin);
    }

    private void ResetServerScores(long serverId)
    {
        foreach (var state in _maxServerScore[serverId].Values) state.Score = 0;
        _maxPlayerScore.TryRemove(serverId, out _);
    }

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

    private async Task<int> GetTotalRankedPlayers(long serverId)
    {
        await using var context = _contextFactory.CreateContext(false);

        return await context.Set<EFClientRankingHistory>()
            .Where(GetNewRankingFunc(serverId))
            .CountAsync();
    }

    private Expression<Func<EFClientRankingHistory, bool>> GetNewRankingFunc(long? serverId = null)
    {
        return ranking => ranking.ServerId == serverId
                          && ranking.Client.Level != Data.Models.Client.EFClient.Permission.Banned
                          && ranking.CreatedDateTime >= DateTime.UtcNow.AddDays(-15)
                          && ranking.ZScore != null
                          && ranking.PerformanceMetric != null
                          && ranking.Newest
                          && ranking.Client.TotalConnectionTime >=
                          _configStats.TopPlayersMinPlayTime;
    }

    public bool MaximumTimePassed(EFClient client)
    {
        var clientServerId = client.CurrentServer.EndPoint;

        if (!_mapTime.ContainsKey(clientServerId)) return false;
        return _mapTime[clientServerId].Add(_credifyConfig.Core.TeamPlayerBetWindow) >= DateTime.UtcNow;
    }

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

    #endregion
}
