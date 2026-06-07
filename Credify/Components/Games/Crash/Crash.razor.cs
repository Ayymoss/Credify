using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
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
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required CredifyWebPlayers WebPlayers { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private static readonly long[] _chips = [10, 50, 100, 500];

    private CrashWebGame _game = null!;
    private EFClient? _client;
    private long _balance;
    private long _stake = 50;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;
    private string? _error;

    private IReadOnlyList<CrashLiveEntry> _live = [];

    private ElementReference _canvasRef;
    private ElementReference _readoutRef;
    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _audio;
    private bool _mounted;

    private CancellationTokenSource? _loopCts;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private bool IsMe(CrashLiveEntry e) => _client is not null && e.ClientId == _client.ClientId;

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

    // ~100ms loop: detect our own crash (server-authoritative) and keep the lobby fresh
    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100));
            while (await timer.WaitForNextTickAsync(token))
            {
                if (_game.Phase == CrashPhase.Flying && _game.PollCrash())
                {
                    await OnCrashed();
                }

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
        await _jsModule.InvokeVoidAsync("mount", _canvasRef, _readoutRef);
        _mounted = true;
    }

    private async Task Launch()
    {
        if (_busy || _client is null || _game.Phase != CrashPhase.Idle || _stake < Config.Crash.MinBet || _stake > _balance)
        {
            return;
        }

        _busy = true;
        _error = null;
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
                Config.Crash.GrowthPerTick);
        }

        _busy = false;
    }

    private async Task CashOut()
    {
        if (_busy || _game.Phase != CrashPhase.Flying)
        {
            return;
        }

        _busy = true;
        _game.CashOut();

        if (_game.Outcome == CrashOutcome.CashedOut)
        {
            var payout = _game.Payout;
            if (payout > 0 && _client is not null)
            {
                _balance = await Persistence.AddCreditsAsync(_client, payout);
            }

            Registry.Settle(_client!.ClientId, cashed: true, _game.CashedMultiplier);

            if (_jsModule is not null)
            {
                await _jsModule.InvokeVoidAsync("cashOut", _game.CashedMultiplier);
            }
            if (_audio is not null)
            {
                try { await _audio.InvokeVoidAsync("win", _game.NetResult, _game.CashedMultiplier >= 5d); } catch { }
            }
        }
        else
        {
            // raced into the crash
            await OnCrashed();
        }

        _busy = false;
    }

    // settle as a crash (from the poll loop or a too-late cash-out)
    private async Task OnCrashed()
    {
        if (_client is not null)
        {
            Registry.Settle(_client.ClientId, cashed: false, _game.CrashPoint);
        }

        if (_jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("crash", _game.CrashPoint);
        }
        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("bomb"); } catch { }
        }
    }

    private async Task NewRound()
    {
        _game.Reset();
        _stake = Math.Clamp(_stake, Config.Crash.MinBet, Math.Max(Config.Crash.MinBet, _balance));
        if (_client is not null)
        {
            Registry.Remove(_client.ClientId);
        }
        if (_jsModule is not null)
        {
            await _jsModule.InvokeVoidAsync("reset");
        }
    }

    private string LiveMultiplier(CrashLiveEntry e)
    {
        if (e.Status == "Flying")
        {
            var mult = CrashMath.MultiplierAt(Config.Crash, (DateTimeOffset.UtcNow - e.StartedAt).TotalSeconds);
            return mult.ToString("0.00") + "×";
        }

        return e.FinalMultiplier.ToString("0.00") + "×";
    }

    private static string StatusClass(string status) => status switch
    {
        "Flying" => "cr-live-flying",
        "Cashed" => "cr-live-cashed",
        _ => "cr-live-crashed"
    };

    public async ValueTask DisposeAsync()
    {
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
