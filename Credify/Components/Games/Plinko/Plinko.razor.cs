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

    private IReadOnlyList<double> _multipliers = [];
    private readonly Dictionary<int, int> _bucketHits = new();
    private GameToast? _toast;
    private int _toastSeq;
    private int _balls = 1;

    private ElementReference _canvasRef;
    private IJSObjectReference? _plinkoJs;
    private IJSObjectReference? _audio;
    private bool _mounted;

    // Drops are fire-and-forget: the server settles instantly, the animation runs as a background task, and
    // the button stays live so the player can rapid-fire. _heldPayout keeps settled-but-not-yet-revealed
    // winnings out of the displayed balance so the wallet number can't spoil a ball still in the air.
    private int _dropsInFlight;
    private long _heldPayout;
    private const int MaxConcurrentDrops = 6;

    // auto-drop: remaining drops (-1 = until stopped) + the loop's running flag
    private int _autoRemaining;
    private bool _autoRunning;
    private static readonly int[] AutoPresets = [10, 25];

    // Quiet auto mode: drops launched while auto runs skip the full money-count (just a short cash blip per
    // win — overlapping counts at turbo cadence are a racket) and accumulate here; ONE full money-count with
    // the session net fires when auto stops and its last ball has landed.
    private long _autoSessionWagered;
    private long _autoSessionProfit;
    private int _autoBatchesInFlight;

    private bool _turbo;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private long MinBet => PlinkoService.MinBet;
    // 0 in config means "no cap"; BetControl treats Max <= 0 as uncapped
    private long? MaxBet => PlinkoService.MaxBet > 0 ? PlinkoService.MaxBet : null;
    private IReadOnlyList<int> RowPresets => PlinkoService.RowPresets;
    private static readonly PlinkoRisk[] Risks = Enum.GetValues<PlinkoRisk>();

    // selectable ball counts — dropping N balls stakes the bet N times
    private static readonly int[] BallPresets = [1, 3, 5, 10];
    private const int MaxBalls = 10;
    private const int BallStaggerMs = 260; // gap between successive ball releases (plinko.js halves it in turbo)

    private long TotalCost => _bet * _balls;

    // what the wallet shows: true balance minus winnings whose balls haven't landed yet
    private long DisplayBalance => Math.Max(0, _balance - _heldPayout);

    private bool CanDrop => _authed && _client is not null && _bet >= MinBet && TotalCost <= DisplayBalance
                            && _dropsInFlight < MaxConcurrentDrops;

    // board geometry can't change under balls that are mid-flight
    private bool BoardLocked => _dropsInFlight > 0 || _autoRunning;

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
        await _plinkoJs.InvokeVoidAsync("setTheme", RiskKey(_risk));
        _turbo = await _plinkoJs.InvokeAsync<bool>("getTurbo"); // persisted in localStorage by plinko.js
        _mounted = true;
        StateHasChanged();
    }

    private void RefreshMultipliers() => _multipliers = PlinkoService.Multipliers(_rows, _risk);

    private async Task SetRows(int rows)
    {
        if (BoardLocked || rows == _rows || !RowPresets.Contains(rows))
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
        if (BoardLocked || risk == _risk)
        {
            return;
        }

        _risk = risk;
        _bucketHits.Clear();
        RefreshMultipliers();

        if (_mounted && _plinkoJs is not null)
        {
            await _plinkoJs.InvokeVoidAsync("render", _rows);
            await _plinkoJs.InvokeVoidAsync("setTheme", RiskKey(_risk));
        }
    }

    private void SetBalls(int count)
    {
        if (!_autoRunning)
        {
            _balls = Math.Clamp(count, 1, MaxBalls);
        }
    }

    private async Task ToggleTurbo()
    {
        _turbo = !_turbo;
        if (_mounted && _plinkoJs is not null)
        {
            try { await _plinkoJs.InvokeVoidAsync("setTurbo", _turbo); } catch { /* best-effort */ }
        }
    }

    private async Task Drop()
    {
        if (!_autoRunning)
        {
            await DropOnceAsync();
        }
    }

    // Settles a batch on the server, kicks the animation off as a background task and returns immediately,
    // so further drops can launch while these balls are still falling.
    private async Task<bool> DropOnceAsync()
    {
        if (!CanDrop || _client is null)
        {
            return false;
        }

        if (_dropsInFlight == 0)
        {
            _bucketHits.Clear();
        }

        _dropsInFlight++;
        StateHasChanged();
        if (!_autoRunning)
        {
            await Sfx("bet"); // auto cadence would clink every few hundred ms — keep auto quiet
        }

        // settle every ball on the server first — each bucket is final before anything moves
        var receipts = new List<PlinkoDropReceipt>(_balls);
        try
        {
            for (var i = 0; i < _balls; i++)
            {
                receipts.Add(await PlinkoService.DropAsync(_client, _bet, _rows, _risk));
            }
        }
        catch
        {
            if (receipts.Count == 0)
            {
                _dropsInFlight--;
                StateHasChanged();
                return false;
            }
            // partial batch settled — animate and account for what went through
        }

        _balance = receipts[^1].NewBalance;
        var payoutTotal = receipts.Sum(r => r.Payout);
        _heldPayout += payoutTotal;

        // batches launched under auto stay quiet even if they land after auto stops
        var quiet = _autoRunning;
        if (quiet)
        {
            _autoBatchesInFlight++;
        }

        _ = FinishDropAsync(receipts, payoutTotal, quiet);
        return true;
    }

    // background: wait for this batch's balls to land, then reveal the results
    private async Task FinishDropAsync(List<PlinkoDropReceipt> receipts, long payoutTotal, bool quiet)
    {
        try
        {
            if (_mounted && _plinkoJs is not null)
            {
                var paths = receipts.Select(r => r.Drop.Path.ToArray()).ToArray();
                var mults = receipts.Select(r => r.Multiplier).ToArray();
                var labels = receipts.Select(r => new
                {
                    text = (r.Profit > 0 ? "+" : "") + r.Profit.ToString("N0"),
                    win = r.Profit > 0
                }).ToArray();
                // dropMany resolves once every ball in this batch has settled
                await _plinkoJs.InvokeVoidAsync("dropMany", paths, BallStaggerMs, mults, labels);
            }

            await InvokeAsync(async () =>
            {
                foreach (var receipt in receipts)
                {
                    _bucketHits[receipt.Drop.Bucket] = _bucketHits.GetValueOrDefault(receipt.Drop.Bucket) + 1;
                }

                var totalBet = receipts.Sum(r => r.Bet);
                var totalProfit = receipts.Sum(r => r.Payout) - totalBet;

                _heldPayout = Math.Max(0, _heldPayout - payoutTotal);
                if (_client is not null)
                {
                    // re-query rather than trusting this batch's receipt — a newer batch may have settled since
                    _balance = await Persistence.GetClientCreditsAsync(_client);
                }

                _toast = BuildToast(receipts, totalProfit);
                GameHistory.Record(_client!.ClientId, new GameHistoryEntry(
                    "Plinko", "ph-circles-three", HistoryLabel(receipts), totalProfit, DateTimeOffset.UtcNow));
                _dropsInFlight = Math.Max(0, _dropsInFlight - 1);
                StateHasChanged();

                if (quiet)
                {
                    _autoSessionWagered += totalBet;
                    _autoSessionProfit += totalProfit;
                    _autoBatchesInFlight = Math.Max(0, _autoBatchesInFlight - 1);
                    await Sfx(totalProfit > 0 ? "cash" : "lose");
                    await MaybeFinishAutoSessionAsync();
                }
                else
                {
                    await PlayResultSoundAsync(totalProfit, totalBet);
                }
            });
        }
        catch
        {
            // circuit/JS torn down mid-flight — release the accounting so the page stays consistent
            _heldPayout = Math.Max(0, _heldPayout - payoutTotal);
            _dropsInFlight = Math.Max(0, _dropsInFlight - 1);
            if (quiet)
            {
                _autoBatchesInFlight = Math.Max(0, _autoBatchesInFlight - 1);
            }
        }
    }

    // ── auto-drop ───────────────────────────────────────────────────────────
    private void StartAuto(int count)
    {
        if (_autoRunning || !CanDrop)
        {
            return;
        }

        _autoRemaining = count;
        _autoSessionWagered = 0;
        _autoSessionProfit = 0;
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
                var dropped = false;
                await InvokeAsync(async () => dropped = await DropOnceAsync());
                if (!dropped)
                {
                    if (_dropsInFlight > 0)
                    {
                        await Task.Delay(250); // all flight slots busy — wait for one to land
                        continue;
                    }

                    break; // can't afford the next drop
                }

                if (_autoRemaining > 0)
                {
                    _autoRemaining--;
                }

                await Task.Delay(_turbo ? 400 : 700);
            }
        }
        finally
        {
            _autoRemaining = 0;
            _autoRunning = false;
            await InvokeAsync(StateHasChanged);
            await MaybeFinishAutoSessionAsync(); // all balls may already be down (e.g. stopped while idle)
        }
    }

    // the auto session's single full money-count: fires once auto has stopped AND its last batch has landed
    private async Task MaybeFinishAutoSessionAsync()
    {
        if (_autoRunning || _autoBatchesInFlight > 0)
        {
            return;
        }

        var profit = _autoSessionProfit;
        var wagered = _autoSessionWagered;
        _autoSessionProfit = 0;
        _autoSessionWagered = 0;

        if (profit > 0 && _audio is not null)
        {
            try { await _audio.InvokeVoidAsync("win", profit, wagered); } catch { /* best-effort audio */ }
        }
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
                await _audio.InvokeVoidAsync("win", totalProfit, totalBet);
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

    private static string RiskKey(PlinkoRisk risk) => risk switch
    {
        PlinkoRisk.Low => "low",
        PlinkoRisk.High => "high",
        _ => "medium"
    };

    // colour the bucket strip by how the multiplier compares to the stake: green = profit, amber = big, red = loss
    private static string BucketClass(double multiplier) => multiplier switch
    {
        >= 10 => "pl-bucket-huge",
        >= 2 => "pl-bucket-big",
        >= 1 => "pl-bucket-win",
        _ => "pl-bucket-low"
    };

    private static string FormatMultiplier(double multiplier) => multiplier switch
    {
        >= 100 => multiplier.ToString("0") + "×",
        >= 10 => multiplier.ToString("0.0") + "×",
        _ => multiplier.ToString("0.00") + "×"
    };

    // compact strip label (no × glyph, no padded decimals) so the buckets stay legible on tall boards
    private static string BucketLabel(double multiplier) => multiplier switch
    {
        >= 100 => multiplier.ToString("0"),
        >= 10 => multiplier.ToString("0.#"),
        _ => multiplier.ToString("0.##")
    };

    // binomial landing chance for bucket k of a rows-row board: C(rows, k) / 2^rows
    private static double BucketChance(int rows, int bucket)
    {
        var p = Math.Pow(0.5, rows);
        for (var i = 0; i < bucket; i++)
        {
            p = p * (rows - i) / (i + 1);
        }

        return p * 100;
    }

    private string BucketTooltip(int bucket) =>
        $"{BucketChance(_rows, bucket):0.###}% chance · pays {FormatMultiplier(_multipliers[bucket])}";

    public async ValueTask DisposeAsync()
    {
        StopAuto();
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
