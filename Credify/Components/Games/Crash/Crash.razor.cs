using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Configuration;
using Credify.Games.Crash;
using Credify.Games.Live;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.Crash;

public partial class Crash
{
    [Inject] public required CrashLiveRegistry Registry { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required CredifyWebPlayers WebPlayers { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private CrashWebGame _game = null!;
    private EFClient? _client;
    private GameToast? _toast;
    private int _toastSeq;
    private long _balance;
    private long _stake = 50;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;
    private string? _error;

    // auto cash-out: deterministic — the game banks EXACTLY the target if the crash point is beyond it
    private bool _autoCashEnabled;
    private double _autoCashTarget = 2.0;

    // auto-launch: rounds remaining (-1 = until stopped) + the loop's running flag
    private int _autoRemaining;
    private bool _autoRunning;
    private static readonly int[] AutoPresets = [10, 25];

    // after a cash-out the ghost curve flies on to the real crash point; once it gets there this flips and
    // the subtitle reveals what was left behind
    private bool _crashRevealed;

    // one settle per round, whichever of (manual click | auto target | crash poll) gets there first
    private int _settleGuard;

    // invalidates pending auto-resets when a new round starts under them
    private int _roundSeq;

    private IReadOnlyList<CrashLiveEntry> _live = [];

    private ElementReference _canvasRef;
    private ElementReference _readoutRef;
    private ElementReference _profitRef;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _audio;
    private bool _mounted;

    private CancellationTokenSource? _loopCts;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private bool IsMe(CrashLiveEntry e) => _client is not null && e.ClientId == _client.ClientId;

    private bool CanLaunch => !_busy && _authed && _client is not null && _game.Phase == CrashPhase.Idle
                              && _stake >= Config.Crash.MinBet && _stake <= _balance;

    protected override async Task OnInitializedAsync()
    {
        _game = new CrashWebGame(Config.Crash);

        if (AuthState is not null)
        {
            var user = (await AuthState).User;
            if (user.Identity?.IsAuthenticated == true &&
                int.TryParse(user.FindFirst(ClaimTypes.Sid)?.Value, out var clientId))
            {
                _client = await ResolveClientAsync(clientId);
                if (_client is not null)
                {
                    _authed = true;
                    _balance = await Persistence.GetClientCreditsAsync(_client);
                }
            }
        }

        _live = Registry.Snapshot();
        Registry.Changed += OnRegistryChanged;

        _loopCts = new CancellationTokenSource();
        _ = LoopAsync(_loopCts.Token);

        _loading = false;
    }

    private async Task<EFClient?> ResolveClientAsync(int clientId)
    {
        var live = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId);
        if (live is not null)
        {
            return live;
        }

        var resolved = await ClientService.Get(clientId);
        return resolved is null ? null : WebPlayers.GetOrAdd(clientId, () => resolved);
    }

    private void OnRegistryChanged() => _ = RefreshAsync();

    // ~100ms loop: auto cash-out + detect our own crash (server-authoritative) and keep the lobby fresh
    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(token))
            {
                if (_game.Phase == CrashPhase.Flying)
                {
                    if (_autoCashEnabled && _game.TryAutoCashOut(_autoCashTarget) && TryBeginSettle())
                    {
                        await SettleCashedOutAsync();
                    }
                    else if (_game.PollCrash() && TryBeginSettle())
                    {
                        await OnCrashed();
                    }
                }

                await RefreshAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // page closed
        }
    }

    private bool TryBeginSettle() => Interlocked.Exchange(ref _settleGuard, 1) == 0;

    private async Task RefreshAsync()
    {
        try
        {
            _live = Registry.Snapshot();
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // circuit tearing down
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
        _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/crash/crash.js");
        await _jsModule.InvokeVoidAsync("mount", _canvasRef, _readoutRef, _profitRef);
        _mounted = true;
    }

    // ── stake quick-actions ─────────────────────────────────────────────────
    private void HalveStake() => _stake = Math.Max(Config.Crash.MinBet, _stake / 2);
    private void DoubleStake() => _stake = Math.Clamp(_stake * 2, Config.Crash.MinBet, Math.Max(Config.Crash.MinBet, _balance));

    private void SetAutoCashEnabled(ChangeEventArgs e) => _autoCashEnabled = e.Value is true or "true";

    private void SetAutoCashTarget(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out var v))
        {
            _autoCashTarget = Math.Clamp(Math.Round(v, 2), 1.01, Config.Crash.MaxMultiplier);
        }
    }

    // ── round flow ──────────────────────────────────────────────────────────
    private async Task Launch() => await LaunchCoreAsync();

    private async Task<bool> LaunchCoreAsync()
    {
        if (!CanLaunch || _client is null)
        {
            return false;
        }

        _busy = true;
        _error = null;
        _crashRevealed = false;
        _settleGuard = 0;
        _roundSeq++;
        _balance = await Persistence.RemoveCreditsAsync(_client, _stake);
        _game.Launch(_stake);
        Registry.Launch(_client.ClientId, _client.CleanedName, _stake, _game.StartedAt);

        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("play", "bet"); } catch { }
        }

        if (_mounted && _jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("fly",
                _game.StartedAt.ToUnixTimeMilliseconds(),
                Config.Crash.TickInterval.TotalSeconds,
                Config.Crash.GrowthPerTick,
                _game.Stake);
        }

        _busy = false;
        return true;
    }

    private async Task CashOut()
    {
        if (_busy || _game.Phase != CrashPhase.Flying || !TryBeginSettle())
        {
            return;
        }

        _busy = true;
        _game.CashOut();

        if (_game.Outcome == CrashOutcome.CashedOut)
        {
            await SettleCashedOutAsync();
        }
        else
        {
            // raced into the crash
            await OnCrashed();
        }

        _busy = false;
    }

    // shared by the manual button and the auto cash-out: pay, record, then ghost-fly to the reveal
    private async Task SettleCashedOutAsync()
    {
        var payout = _game.Payout;
        if (payout > 0 && _client is not null)
        {
            _balance = await Persistence.AddCreditsAsync(_client, payout);
        }

        Registry.Settle(_client!.ClientId, cashed: true, _game.CashedMultiplier);

        _toast = new GameToast(++_toastSeq,
            _game.CashedMultiplier >= 5d ? GameToastVariant.Jackpot : GameToastVariant.Win,
            $"Cashed {_game.CashedMultiplier:0.00}×", $"+{_game.NetResult:N0}", "ph-hand-coins");
        GameHistory.Record(_client!.ClientId, new GameHistoryEntry(
            "Crash", "ph-rocket-launch", $"{_game.CashedMultiplier:0.00}×", _game.NetResult, DateTimeOffset.UtcNow));

        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("win", _game.NetResult, _game.Stake); } catch { }
        }

        // ghost flight: the curve carries on (dimmed) to the now-revealed crash point, then we show it
        var ghostSeconds = Math.Max(0, CrashMath.TimeToReach(Config.Crash, _game.CrashPoint) - _game.ElapsedSeconds);
        _ = GhostAndRevealAsync(_roundSeq);
        ScheduleReset(ghostSeconds + 1.8);
    }

    private async Task GhostAndRevealAsync(int seq)
    {
        try
        {
            if (_mounted && _jsModule is not null)
            {
                // resolves when the ghost curve reaches the crash point
                await _jsModule.InvokeAsync<object?>("ghost",
                    _game.CashedMultiplier, _game.CrashPoint, $"+{_game.NetResult:N0}");
            }
        }
        catch
        {
            // circuit/JS gone
        }

        if (seq == _roundSeq)
        {
            _crashRevealed = true;
            try { await InvokeAsync(StateHasChanged); } catch { }
        }
    }

    // settle as a crash (from the poll loop or a too-late cash-out)
    private async Task OnCrashed()
    {
        if (_client is not null)
        {
            Registry.Settle(_client.ClientId, cashed: false, _game.CrashPoint);
            GameHistory.Record(_client.ClientId, new GameHistoryEntry(
                "Crash", "ph-rocket-launch", "Bust", -_game.Stake, DateTimeOffset.UtcNow));
        }

        _toast = new GameToast(++_toastSeq, GameToastVariant.Lose,
            $"Crashed {_game.CrashPoint:0.00}×", (-_game.Stake).ToString("N0"), "ph-rocket");

        if (_jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("crash", _game.CrashPoint);
        }
        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("bomb"); } catch { }
        }

        ScheduleReset(2.0);
    }

    // the round resets itself — no "new round" click in the loop
    private void ScheduleReset(double holdSeconds)
    {
        var seq = _roundSeq;
        _ = ResetAfterAsync(seq, TimeSpan.FromSeconds(holdSeconds));
    }

    private async Task ResetAfterAsync(int seq, TimeSpan delay)
    {
        await Task.Delay(delay);
        if (seq != _roundSeq || _game.Phase != CrashPhase.Settled)
        {
            return;
        }

        try
        {
            await InvokeAsync(async () =>
            {
                await NewRoundCoreAsync();
                StateHasChanged();
            });
        }
        catch
        {
            // circuit tearing down
        }
    }

    private async Task NewRoundCoreAsync()
    {
        _game.Reset();
        _crashRevealed = false;
        _settleGuard = 0;
        _stake = Math.Clamp(_stake, Config.Crash.MinBet, Math.Max(Config.Crash.MinBet, _balance));
        if (_client is not null)
        {
            Registry.Remove(_client.ClientId);
        }
        if (_jsModule is not null)
        {
            try { await _jsModule.InvokeVoidAsync("reset"); } catch { }
        }
    }

    // ── auto-launch ─────────────────────────────────────────────────────────
    private void StartAuto(int rounds)
    {
        if (_autoRunning || !_autoCashEnabled || !CanLaunch)
        {
            return;
        }

        _autoRemaining = rounds;
        _autoRunning = true;
        _ = AutoLoopAsync();
    }

    private void StopAuto() => _autoRemaining = 0;

    private async Task AutoLoopAsync()
    {
        try
        {
            while (_autoRemaining != 0)
            {
                var launched = false;
                await InvokeAsync(async () => launched = await LaunchCoreAsync());
                if (!launched)
                {
                    break; // can't afford the stake (or page state changed under us)
                }

                if (_autoRemaining > 0)
                {
                    _autoRemaining--;
                }

                // ride the round out: flight, then the post-settle hold (ghost flight + auto-reset)
                while (_game.Phase != CrashPhase.Idle && _autoRemaining != 0)
                {
                    await Task.Delay(150);
                }

                await Task.Delay(250);
            }
        }
        finally
        {
            _autoRemaining = 0;
            _autoRunning = false;
            try { await InvokeAsync(StateHasChanged); } catch { }
        }
    }

    // ── lobby ───────────────────────────────────────────────────────────────
    // flying rows first; settled rows linger ~10s then drop off
    private IEnumerable<CrashLiveEntry> LobbyRows => _live
        .Where(e => e.Status == "Flying" || e.SettledAt is null ||
                    DateTimeOffset.UtcNow - e.SettledAt < TimeSpan.FromSeconds(10))
        .OrderBy(e => e.Status == "Flying" ? 0 : 1)
        .ThenByDescending(e => e.SettledAt ?? DateTimeOffset.MaxValue)
        .ToList();

    private double LiveMult(CrashLiveEntry e) => e.Status == "Flying"
        ? CrashMath.MultiplierAt(Config.Crash, (DateTimeOffset.UtcNow - e.StartedAt).TotalSeconds)
        : e.FinalMultiplier;

    private string LiveMultiplier(CrashLiveEntry e) => LiveMult(e).ToString("0.00") + "×";

    private string LiveProfit(CrashLiveEntry e) => e.Status switch
    {
        "Flying" => "+" + ((long)(e.Stake * (LiveMult(e) - 1))).ToString("N0"),
        "Cashed" => "+" + ((long)(e.Stake * e.FinalMultiplier) - e.Stake).ToString("N0"),
        _ => (-e.Stake).ToString("N0")
    };

    private static string StatusClass(string status) => status switch
    {
        "Flying" => "cr-live-flying",
        "Cashed" => "cr-live-cashed",
        _ => "cr-live-crashed"
    };

    public async ValueTask DisposeAsync()
    {
        StopAuto();
        Registry.Changed -= OnRegistryChanged;
        if (_client is not null)
        {
            Registry.Remove(_client.ClientId);
        }

        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
            _loopCts.Dispose();
        }

        foreach (var module in new[] { _jsModule, _audio })
        {
            if (module is null)
            {
                continue;
            }

            try
            {
                if (ReferenceEquals(module, _jsModule))
                {
                    await module.InvokeVoidAsync("dispose");
                }
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // circuit already gone
            }
        }
    }
}
