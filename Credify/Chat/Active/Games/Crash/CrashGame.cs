using Credify.Chat.Active.Core;
using Credify.Chat.Active.Games.Crash.Enums;
using Credify.Chat.Active.Games.Crash.Models;
using Credify.Chat.Active.Games.Crash.Utilities;
using Credify.Chat.Passive.Quests.Enums;
using Credify.Configuration;
using Credify.Configuration.Translations;
using Credify.Services;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;

namespace Credify.Chat.Active.Games.Crash;

/// <summary>
/// "Crash" — players bet, a shared rocket's multiplier climbs live, and each player races to
/// type CASH to bank their stake * current multiplier before it crashes at a random point.
/// Continuous shared-round game (like Roulette), but all output goes to participants only
/// (like Minefield) so the live tick stream never spams non-players.
/// </summary>
public class CrashGame(
    CredifyConfiguration config,
    PersistenceService persistenceService,
    GamePlayerCommunication communication,
    CrashHandleOutput output)
    : BaseContinuousGame<CrashPlayer>(persistenceService, config, communication)
{
    private CrashGameState _gameState = CrashGameState.WaitingForPlayers;
    private List<CrashPlayer> _roundPlayers = [];
    private CancellationTokenSource? _bettingToken;
    private double _multiplier = 1.0;
    private double _crashPoint = 1.0;

    // Flight timeline for latency-fair cash-outs: _tickValues[k] is the multiplier broadcast
    // at tick k (index 0 = launch/1.00), and _flightStart is when the flight began (UTC, the
    // same clock as ClientMessageEvent.Time).
    private DateTime _flightStart;
    private readonly List<double> _tickValues = [];
    private readonly object _tickValuesLock = new();

    private CrashConfiguration Settings => Config.Crash;
    private CrashTranslations Translations => Config.Translations.Crash;

    protected override int GetMinimumPlayers() => 1;
    protected override TimeSpan GetDelayBetweenRounds() => TimeSpan.FromSeconds(4);

    #region Game Loop

    protected override async Task ExecuteGameRoundAsync(CancellationToken token)
    {
        _roundPlayers = Players.Values.ToList();
        foreach (var player in _roundPlayers) player.ResetForNewRound();

        await CollectBetsAsync(token);

        // Keep only players who actually placed a bet.
        lock (_roundPlayers) _roundPlayers = _roundPlayers.Where(p => p.HasBet).ToList();
        if (_roundPlayers.Count == 0)
        {
            _gameState = CrashGameState.WaitingForPlayers;
            return;
        }

        await FlyAsync(token);
        await ResolveAsync();

        _gameState = CrashGameState.WaitingForPlayers;
    }

    private async Task CollectBetsAsync(CancellationToken token)
    {
        _gameState = CrashGameState.Betting;

        foreach (var player in _roundPlayers)
        {
            var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
            await output.TellPlayerAsync(player,
            [
                Translations.EnterStake.FormatExt(credits.ToString("N0")),
                Translations.BetSyntax
            ]);
        }

        _bettingToken?.Dispose();
        _bettingToken = new CancellationTokenSource();

        var warnAfter = Settings.BettingDuration - TimeSpan.FromSeconds(5);
        if (warnAfter > TimeSpan.Zero)
        {
            SharedLibraryCore.Utilities.ExecuteAfterDelay(warnAfter, async warnToken =>
            {
                if (warnToken.IsCancellationRequested) return;
                List<CrashPlayer> pending;
                lock (_roundPlayers) pending = _roundPlayers.Where(p => !p.HasBet).ToList();
                if (pending.Count > 0) await output.TellPlayersAsync(pending, [Translations.TimeWarning]);
            }, _bettingToken.Token);
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _bettingToken.Token);
            await Task.Delay(Settings.BettingDuration, linked.Token);
        }
        catch (OperationCanceledException)
        {
            // All players bet (token cancelled) or the game is shutting down.
        }
    }

    private async Task FlyAsync(CancellationToken token)
    {
        _gameState = CrashGameState.Flying;
        _crashPoint = ComputeCrashPoint();
        _multiplier = 1.0;
        _flightStart = DateTime.UtcNow;
        lock (_tickValuesLock)
        {
            _tickValues.Clear();
            _tickValues.Add(1.0); // index 0 = launch
        }

        await output.TellPlayersAsync(_roundPlayers, [Translations.Launched]);

        while (!token.IsCancellationRequested)
        {
            await Task.Delay(Settings.TickInterval, token);

            var next = Math.Round(_multiplier * Settings.GrowthPerTick, 2);
            if (next >= _crashPoint) break; // rocket crashes this tick
            _multiplier = next;
            lock (_tickValuesLock) _tickValues.Add(next);

            List<CrashPlayer> stillIn;
            lock (_roundPlayers) stillIn = _roundPlayers.Where(p => p.CashedMultiplier is null).ToList();
            if (stillIn.Count == 0) break; // everyone banked

            await output.TellPlayersAsync(stillIn, [Translations.Tick.FormatExt(_multiplier.ToString("0.00"))]);
        }
    }

    private async Task ResolveAsync()
    {
        _gameState = CrashGameState.Resolving;
        var crashDisplay = _crashPoint.ToString("0.00");

        List<CrashPlayer> losers;
        lock (_roundPlayers) losers = _roundPlayers.Where(p => p.CashedMultiplier is null).ToList();

        foreach (var player in losers)
        {
            await output.TellPlayerAsync(player, [Translations.Lost.FormatExt(crashDisplay, player.Stake.ToString("N0"))]);
        }
    }

    private double ComputeCrashPoint()
    {
        // crash = (1 - edge) / (1 - u); P(crash >= x) = (1-edge)/x, so cashing at x returns
        // x with that probability => RTP = 1 - edge. Clamped to [1, MaxMultiplier].
        var u = Random.Shared.NextDouble();
        var raw = (1.0 - Settings.HouseEdge) / (1.0 - u);
        return Math.Clamp(Math.Round(raw, 2), 1.0, Settings.MaxMultiplier);
    }

    #endregion

    #region IActiveGame

    public override async Task JoinGameAsync(EFClient client)
    {
        Players.GetOrAdd(client, c => new CrashPlayer(c));
        await output.TellClientAsync(client, [Translations.Join], true);
        OnPlayerJoined();
    }

    public override async Task LeaveGameAsync(EFClient client)
    {
        await ExecuteUnderChatLockAsync(async () =>
        {
            if (!Players.TryRemove(client, out _)) return;

            CrashPlayer? roundPlayer;
            lock (_roundPlayers)
            {
                roundPlayer = _roundPlayers.FirstOrDefault(p => p.Client.ClientId == client.ClientId);
                _roundPlayers.RemoveAll(p => p.Client.ClientId == client.ClientId);
            }

            // Refund a placed-but-not-flown bet; leaving mid-flight forfeits (you didn't cash).
            if (roundPlayer is { HasBet: true, CashedMultiplier: null } && _gameState == CrashGameState.Betting)
            {
                await PersistenceService.AddCreditsAsync(client, roundPlayer.Stake);
            }

            OnPlayerLeft();
        });
    }

    // IActiveGame entry point. The fan-out uses the time-aware overload below; this fallback
    // (no message timestamp) just treats the cash as happening now.
    public override Task HandleChatAsync(EFClient client, string message) =>
        HandleChatAsync(client, message, DateTime.UtcNow);

    /// <summary>
    /// Time-aware chat handler. <paramref name="eventTime"/> (UTC) is the message timestamp,
    /// used to latency-compensate cash-outs back to the multiplier the player actually saw.
    /// </summary>
    public async Task HandleChatAsync(EFClient client, string message, DateTime eventTime)
    {
        if (!Players.ContainsKey(client)) return;

        await ExecuteUnderChatLockAsync(async () =>
        {
            CrashPlayer? player;
            lock (_roundPlayers) player = _roundPlayers.FirstOrDefault(p => p.Client.ClientId == client.ClientId);
            if (player is null) return; // not part of the current round (e.g. joined mid-round)

            switch (_gameState)
            {
                case CrashGameState.Betting:
                    await HandleBetAsync(player, message);
                    break;
                case CrashGameState.Flying:
                    await HandleCashAsync(player, message, eventTime);
                    break;
            }
        });
    }

    #endregion

    #region Input

    private async Task HandleBetAsync(CrashPlayer player, string message)
    {
        if (player.HasBet)
        {
            await output.TellPlayerAsync(player, [Translations.AlreadyBet]);
            return;
        }

        var trimmed = message.Trim();
        var credits = await PersistenceService.GetClientCreditsAsync(player.Client);
        if (trimmed.Equals("all", StringComparison.OrdinalIgnoreCase)) trimmed = credits.ToString();

        // Ignore non-numeric chatter during betting rather than nagging on every message.
        if (!long.TryParse(trimmed, out var stake)) return;

        if (stake < Settings.MinBet)
        {
            await output.TellPlayerAsync(player, [Config.Translations.Gambling.MinimumAmount]);
            return;
        }
        if (Settings.MaxBet > 0 && stake > Settings.MaxBet)
        {
            await output.TellPlayerAsync(player, [Config.Translations.Gambling.MaximumAmount.FormatExt(Settings.MaxBet.ToString("N0"))]);
            return;
        }
        if (!PersistenceService.AvailableFunds(player.Client, stake))
        {
            await output.TellPlayerAsync(player, [Config.Translations.Core.InsufficientCredits]);
            return;
        }

        await PersistenceService.RemoveCreditsAsync(player.Client, stake);
        player.Stake = stake;
        player.HasBet = true;
        await output.TellPlayerAsync(player, [Translations.BetAccepted.FormatExt(stake.ToString("N0"))]);

        // Launch early once everyone has bet.
        bool allBet;
        lock (_roundPlayers) allBet = _roundPlayers.All(p => p.HasBet);
        if (allBet) _bettingToken?.Cancel();
    }

    private async Task HandleCashAsync(CrashPlayer player, string message, DateTime eventTime)
    {
        if (!player.HasBet || player.CashedMultiplier is not null) return;

        var trimmed = message.Trim();
        if (!trimmed.Equals("cash", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("c", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Equals("out", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var multiplier = MultiplierSeenAt(player.Client, eventTime);
        player.CashedMultiplier = multiplier;

        var payout = (long)(player.Stake * multiplier);
        var profit = payout - player.Stake;
        await PersistenceService.AddCreditsAsync(player.Client, payout);
        ICredifyEventService.RaiseEvent(ObjectiveType.Baller, player.Client, payout);

        await output.TellPlayerAsync(player,
            [Translations.CashedOut.FormatExt(multiplier.ToString("0.00"), payout.ToString("N0"), profit.ToString("N0"))]);
    }

    /// <summary>
    /// The multiplier the player actually saw when they typed CASH, back-dated by their
    /// server's log/RCON latency. The cash reached us at <paramref name="eventTime"/> after the
    /// log pipeline delay, and the value they saw had also travelled to them (half RCON RTT) -
    /// so we read the tick that was on their screen at that adjusted moment. Never exceeds the
    /// latest broadcast tick (no cashing the future).
    /// </summary>
    private double MultiplierSeenAt(EFClient client, DateTime eventTime)
    {
        var offset = LatencyOffsetSeconds(client);
        var elapsedSeen = (eventTime - _flightStart).TotalSeconds - offset;
        var seenTick = (int)Math.Floor(elapsedSeen / Settings.TickInterval.TotalSeconds);

        lock (_tickValuesLock)
        {
            seenTick = Math.Clamp(seenTick, 0, _tickValues.Count - 1);
            return _tickValues[seenTick];
        }
    }

    /// <summary>
    /// Per-server view-staleness in seconds: log pipeline (answer reaching us) plus the
    /// broadcast send leg (half RCON RTT). Crash requires GameLogPipelineMs (enforced at join),
    /// so this is normally present; returns 0 as a safe fallback.
    /// </summary>
    private static double LatencyOffsetSeconds(EFClient client)
    {
        var metrics = client.CurrentServer.LatencyMetrics;
        if (metrics?.GameLogPipelineMs is not { } logLatency) return 0;

        var offset = logLatency / 1000.0;
        if (metrics.RconRoundTripMs is { } rtt) offset += rtt / 2000.0;
        return offset;
    }

    #endregion
}
