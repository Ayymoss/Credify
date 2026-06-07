using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Configuration;
using Credify.Games.Plinko;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.Plinko;

public partial class Plinko
{
    [Inject] public required PlinkoService PlinkoService { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private EFClient? _client;
    private long _balance;
    private long _bet;
    private int _rows;
    private PlinkoRisk _risk = PlinkoRisk.Medium;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private IReadOnlyList<double> _multipliers = [];
    private readonly Dictionary<int, int> _bucketHits = new();
    private GameToast? _toast;
    private int _toastSeq;
    private int _balls = 1;

    private ElementReference _canvasRef;
    private IJSObjectReference? _plinkoJs;
    private IJSObjectReference? _audio;
    private bool _mounted;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private long MinBet => PlinkoService.MinBet;
    // 0 in config means "no cap"; BetControl treats Max <= 0 as uncapped
    private long? MaxBet => PlinkoService.MaxBet > 0 ? PlinkoService.MaxBet : null;
    private IReadOnlyList<int> RowPresets => PlinkoService.RowPresets;
    private static readonly PlinkoRisk[] Risks = Enum.GetValues<PlinkoRisk>();

    // selectable ball counts — dropping N balls stakes the bet N times
    private static readonly int[] BallPresets = [1, 3, 5, 10];
    private const int MaxBalls = 10;
    private const int BallStaggerMs = 260; // gap between successive ball releases

    private long TotalCost => _bet * _balls;

    private bool CanDrop => !_busy && _authed && _client is not null && _bet >= MinBet && TotalCost <= _balance;

    protected override async Task OnInitializedAsync()
    {
        _rows = ResolveDefaultRows();
        _bet = MinBet;
        RefreshMultipliers();

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
                    _bet = Math.Clamp(Math.Min(MinBet, _balance), 0, Math.Max(0, _balance));
                    if (_balance >= MinBet && _bet < MinBet)
                    {
                        _bet = MinBet;
                    }
                }
            }
        }

        _loading = false;
    }

    private int ResolveDefaultRows()
    {
        // honour the configured default if it's actually a selectable preset, else fall back to the first one
        if (RowPresets.Contains(PlinkoService.DefaultRows))
        {
            return PlinkoService.DefaultRows;
        }

        return RowPresets.Count > 0 ? RowPresets[0] : 12;
    }

    private async Task<EFClient?> ResolveClientAsync(int clientId)
    {
        // prefer the live in-game client so the in-memory balance stays consistent with the game server;
        // fall back to a DB lookup for web-only players who aren't currently connected.
        var live = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId);
        return live ?? await ClientService.Get(clientId);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !_authed)
        {
            return;
        }

        _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
        _plinkoJs = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/plinko/plinko.js");
        await _plinkoJs.InvokeVoidAsync("mount", _canvasRef);
        await _plinkoJs.InvokeVoidAsync("render", _rows);
        _mounted = true;
    }

    private void RefreshMultipliers() => _multipliers = PlinkoService.Multipliers(_rows, _risk);

    private async Task SetRows(int rows)
    {
        if (_busy || rows == _rows || !RowPresets.Contains(rows))
        {
            return;
        }

        _rows = rows;
        _bucketHits.Clear();
        RefreshMultipliers();

        if (_mounted && _plinkoJs is not null)
        {
            await _plinkoJs.InvokeVoidAsync("render", _rows);
        }
    }

    private async Task SetRisk(PlinkoRisk risk)
    {
        if (_busy || risk == _risk)
        {
            return;
        }

        _risk = risk;
        _bucketHits.Clear();
        RefreshMultipliers();

        if (_mounted && _plinkoJs is not null)
        {
            await _plinkoJs.InvokeVoidAsync("render", _rows);
        }
    }

    private void SetBalls(int count)
    {
        if (!_busy)
        {
            _balls = Math.Clamp(count, 1, MaxBalls);
        }
    }

    private async Task Drop()
    {
        if (!CanDrop || _client is null)
        {
            return;
        }

        _busy = true;
        _bucketHits.Clear();
        StateHasChanged();

        await Sfx("bet");

        // settle every ball on the server first — each bucket is final before anything moves
        var receipts = new List<PlinkoDropReceipt>(_balls);
        for (var i = 0; i < _balls; i++)
        {
            receipts.Add(await PlinkoService.DropAsync(_client, _bet, _rows, _risk));
        }

        // animate the balls down their exact paths, released one after another (no inter-ball collision)
        if (_mounted && _plinkoJs is not null)
        {
            var paths = receipts.Select(r => r.Drop.Path.ToArray()).ToArray();
            var mults = receipts.Select(r => r.Multiplier).ToArray();
            try { await _plinkoJs.InvokeVoidAsync("dropMany", paths, BallStaggerMs, mults); }
            catch { /* circuit/JS gone — fall through and just show the result */ }
        }

        foreach (var receipt in receipts)
        {
            _bucketHits[receipt.Drop.Bucket] = _bucketHits.GetValueOrDefault(receipt.Drop.Bucket) + 1;
        }

        var totalBet = receipts.Sum(r => r.Bet);
        var totalProfit = receipts.Sum(r => r.Payout) - totalBet;

        _balance = receipts[^1].NewBalance;
        _toast = BuildToast(receipts, totalProfit);
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Plinko", "ph-circles-three", HistoryLabel(receipts), totalProfit, DateTimeOffset.UtcNow));
        _busy = false;
        StateHasChanged();

        await PlayResultSoundAsync(totalProfit, totalBet);
    }

    private async Task PlayResultSoundAsync(long totalProfit, long totalBet)
    {
        if (_audio is null)
        {
            return;
        }

        try
        {
            if (totalProfit > 0)
            {
                await _audio.InvokeVoidAsync("win", totalProfit, totalProfit >= totalBet * 10);
            }
            else
            {
                await _audio.InvokeVoidAsync("play", "lose");
            }
        }
        catch
        {
            // best-effort audio
        }
    }

    private async Task Sfx(string name)
    {
        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("play", name); } catch { /* best-effort */ }
        }
    }

    private GameToast BuildToast(IReadOnlyList<PlinkoDropReceipt> receipts, long totalProfit)
    {
        // single ball: show the bucket multiplier. batch: show the combined net across all the balls.
        if (receipts.Count == 1)
        {
            var r = receipts[0];
            var v = r.Profit > 0 ? (r.Multiplier >= 10 ? GameToastVariant.Jackpot : GameToastVariant.Win) : GameToastVariant.Lose;
            // a bucket below 1× still returns part of the stake, so the toast shows the true net, not the full bet
            var amt = r.Profit > 0 ? $"+{r.Profit:N0}" : r.Profit.ToString("N0");
            var ic = r.Profit > 0 ? "ph-confetti" : "ph-arrow-fat-line-down";
            return new GameToast(++_toastSeq, v, FormatMultiplier(r.Multiplier), amt, ic);
        }

        var variant = totalProfit > 0 ? GameToastVariant.Win : GameToastVariant.Lose;
        var amount = totalProfit > 0 ? $"+{totalProfit:N0}" : totalProfit.ToString("N0");
        var icon = totalProfit > 0 ? "ph-confetti" : "ph-arrow-fat-line-down";
        return new GameToast(++_toastSeq, variant, $"{receipts.Count} balls", amount, icon);
    }

    private string HistoryLabel(IReadOnlyList<PlinkoDropReceipt> receipts) =>
        receipts.Count == 1 ? FormatMultiplier(receipts[0].Multiplier) : $"{receipts.Count} balls";

    private static string RiskLabel(PlinkoRisk risk) => risk switch
    {
        PlinkoRisk.Low => "Low",
        PlinkoRisk.High => "High",
        _ => "Medium"
    };

    // colour the bucket strip by how the multiplier compares to the stake: green = profit, amber = big, red = loss
    private static string BucketClass(double multiplier) => multiplier switch
    {
        >= 10 => "pk-bucket-huge",
        >= 2 => "pk-bucket-big",
        >= 1 => "pk-bucket-win",
        _ => "pk-bucket-low"
    };

    private static string FormatMultiplier(double multiplier) => multiplier switch
    {
        >= 100 => multiplier.ToString("0") + "×",
        >= 10 => multiplier.ToString("0.0") + "×",
        _ => multiplier.ToString("0.00") + "×"
    };

    public async ValueTask DisposeAsync()
    {
        foreach (var module in new[] { _plinkoJs, _audio })
        {
            if (module is null)
            {
                continue;
            }

            try
            {
                if (ReferenceEquals(module, _plinkoJs))
                {
                    await module.InvokeVoidAsync("dispose");
                }

                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // circuit already gone — nothing to clean up
            }
        }
    }
}
