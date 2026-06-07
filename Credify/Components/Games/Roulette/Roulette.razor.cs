using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Credify.Chat.Active.Games.Roulette;
using Credify.Configuration;
using Credify.Games.Live;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.Roulette;

public partial class Roulette
{
    [Inject] public required Table RouletteTable { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required CredifyWebPlayers WebPlayers { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }

    private static readonly HashSet<int> _redNumbers =
        [1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36];

    private RouletteSnapshot _snapshot = new();
    private EFClient? _client;
    private long _balance;
    private long _stake = 50;

    // bets the player is building this round, committed together via PlaceBets()
    private readonly List<PendingBet> _pendingBets = [];

    private sealed record PendingBet(string Input, string Label, long Stake);
    private bool _authed;
    private bool _loading = true;
    private bool _busy;
    private string? _error;

    private ElementReference _wheelRef;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _audio;
    private bool _wheelMounted;
    private bool _spinStarted;
    private string? _lastLandedDisplay;
    private string? _lastOutcome;

    private CancellationTokenSource? _loopCts;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private RoulettePlayerView? MyView =>
        _client is null ? null : _snapshot.Players.FirstOrDefault(p => p.ClientId == _client.ClientId);

    private bool Seated => MyView is not null;
    private bool IsMe(RoulettePlayerView p) => _client is not null && p.ClientId == _client.ClientId;

    private string StatusClass => "rl-status " + _snapshot.Phase switch
    {
        "Betting" => "rl-status-betting",
        "Spinning" => "rl-status-spinning",
        _ => "rl-status-idle"
    };

    // betting window is TimeoutForPlayerAction*3; GameCountdown scales the bar against this
    private double BettingWindow => Config.Roulette.TimeoutForPlayerAction.TotalSeconds * 3;

    private static bool IsRed(int n) => _redNumbers.Contains(n);

    private static string ColourClass(string colour) => colour switch
    {
        "Red" => "rl-red",
        "Green" => "rl-green",
        _ => "rl-black"
    };

    protected override async Task OnInitializedAsync()
    {
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

        _snapshot = RouletteTable.GetSnapshot();
        RouletteTable.StateChanged += OnStateChanged;

        // refresh loop drives the countdown + picks up state changes smoothly
        _loopCts = new CancellationTokenSource();
        _ = RefreshLoopAsync(_loopCts.Token);

        _loading = false;
    }

    private async Task<EFClient?> ResolveClientAsync(int clientId)
    {
        // prefer the live in-game client (shared identity with the chat table); otherwise a stable,
        // cached web client so this player keeps one seat across page reloads.
        var live = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId);
        if (live is not null)
        {
            return live;
        }

        var resolved = await ClientService.Get(clientId);
        return resolved is null ? null : WebPlayers.GetOrAdd(clientId, () => resolved);
    }

    // marshal onto the renderer's context so this never races the timer-loop RefreshAsync
    private void OnStateChanged() => _ = InvokeAsync(RefreshAsync);

    private async Task RefreshLoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));
            while (await timer.WaitForNextTickAsync(token))
            {
                await RefreshAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // page closed
        }
    }

    private async Task RefreshAsync()
    {
        try
        {
            _snapshot = RouletteTable.GetSnapshot();
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // circuit may be tearing down; ignore
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
            _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/roulette/roulette.js");
            await _jsModule.InvokeVoidAsync("mount", _wheelRef);
            _wheelMounted = true;
        }

        if (_wheelMounted && _jsModule is not null)
        {
            await DriveWheelAsync();
        }
    }

    // map the server phase/result onto the wheel animation
    private async Task DriveWheelAsync()
    {
        if (_jsModule is null)
        {
            return;
        }

        if (_snapshot.Phase == "Spinning")
        {
            if (!_spinStarted)
            {
                _spinStarted = true;
                await _jsModule.InvokeVoidAsync("startSpin");
                if (_audio is not null)
                {
                    try { await _audio.InvokeVoidAsync("spin"); } catch { }
                }
            }
            return;
        }

        // once the result is known (Resolving / next betting), land on it exactly once
        if (_snapshot.LastSpin is { } spin && spin.Display != _lastLandedDisplay)
        {
            _lastLandedDisplay = spin.Display;
            _spinStarted = false;
            await _jsModule.InvokeVoidAsync("landOn", spin.Display);
            if (_audio is not null)
            {
                try { await _audio.InvokeVoidAsync("land"); } catch { }
            }
        }

        // my own result sound (once per settle)
        if (MyView is { } me && me.Outcome != _lastOutcome)
        {
            _lastOutcome = me.Outcome;

            // log the settled spin to the session history rail (labelled with the number it landed on)
            if (me.Outcome is "Won" or "Lost" && _client is not null)
            {
                var label = _lastLandedDisplay is { } d ? $"#{d}" : (me.Outcome == "Won" ? "Won" : "Lost");
                GameHistory.Record(_client.ClientId, new GameHistoryEntry(
                    "Roulette", "ph-circle-half", label, me.Net, DateTimeOffset.UtcNow));
                _balance = await Persistence.GetClientCreditsAsync(_client);
            }

            if (_audio is not null)
            {
                try
                {
                    if (me.Outcome == "Won") await _audio.InvokeVoidAsync("win", me.Net, me.TotalStake);
                    else if (me.Outcome == "Lost") await _audio.InvokeVoidAsync("play", "lose");
                }
                catch { }
            }
        }
    }

    private async Task TakeSeat()
    {
        if (_busy || _client is null)
        {
            return;
        }

        _busy = true;
        _error = null;
        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("play", "click"); } catch { }
        }
        await RouletteTable.JoinGameAsync(_client);
        await RefreshAsync();
        _busy = false;
    }

    private async Task LeaveSeat()
    {
        if (_client is null)
        {
            return;
        }

        await RouletteTable.LeaveGameAsync(_client);
        await RefreshAsync();
    }

    // ── building & committing a batch of bets ──────────────────────────────────
    private long PendingTotal => _pendingBets.Sum(bet => bet.Stake);
    private bool CanBet => _snapshot.Phase == "Betting" && Seated && MyView is { HasBet: false } && !_busy;
    private bool CanAdd => CanBet && _stake >= 10 && _stake <= _balance - PendingTotal;

    private void AddPending(string input, string label)
    {
        if (!CanAdd)
        {
            return;
        }

        _pendingBets.Add(new PendingBet(input, label, _stake));
        _error = null;
    }

    private void RemovePending(int index)
    {
        if (index >= 0 && index < _pendingBets.Count)
        {
            _pendingBets.RemoveAt(index);
        }
    }

    private void ClearPending() => _pendingBets.Clear();

    private async Task PlaceBets()
    {
        if (_busy || _client is null || _pendingBets.Count == 0)
        {
            return;
        }

        _busy = true;
        _error = null;
        var batch = _pendingBets.Select(bet => ((int)bet.Stake, bet.Input)).ToList();
        var err = await RouletteTable.PlaceWebBetsAsync(_client, batch);
        if (err is not null)
        {
            _error = err;
        }
        else
        {
            _pendingBets.Clear();
            if (_audio is not null)
            {
                try { await _audio.InvokeVoidAsync("play", "bet"); } catch { }
            }
        }

        _balance = await Persistence.GetClientCreditsAsync(_client);
        _busy = false;
        await RefreshAsync();
    }

    public async ValueTask DisposeAsync()
    {
        RouletteTable.StateChanged -= OnStateChanged;
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
            _loopCts.Dispose();
        }

        if (_jsModule is not null)
        {
            try
            {
                await _jsModule.InvokeVoidAsync("dispose");
                await _jsModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // circuit already gone
            }
        }

        if (_audio is not null)
        {
            try { await _audio.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
