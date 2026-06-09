using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Games.Keno;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using GameConstants = Credify.Chat.Active.Core.GameConstants;

namespace Credify.Components.Games.Keno;

public partial class Keno
{
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private const long MinBet = GameConstants.MinimumCredits;
    private const int RevealMs = 1300; // total time for the 20 balls to light up

    private EFClient? _client;
    private long _balance;
    private long _bet = MinBet;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private readonly HashSet<int> _picks = [];
    private IReadOnlyList<int> _drawn = [];
    private Dictionary<int, int> _drawnOrder = new();
    private HashSet<int> _hits = [];

    private GameToast? _toast;
    private int _toastSeq;

    private IJSObjectReference? _audio;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private int Spots => _picks.Count;
    private bool HasResult => _drawn.Count > 0;
    private bool CanPlay => !_busy && _authed && _client is not null && Spots >= 1 && _bet >= MinBet && _bet <= _balance;

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

    private async Task<EFClient?> ResolveClientAsync(int clientId)
    {
        var live = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId);
        return live ?? await ClientService.Get(clientId);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
        }
    }

    private void ClearResults()
    {
        _drawn = [];
        _drawnOrder = new();
        _hits = [];
    }

    private void Toggle(int number)
    {
        if (_busy)
        {
            return;
        }

        if (HasResult)
        {
            ClearResults(); // changing the ticket clears the previous draw
            _toast = null;
        }

        if (!_picks.Remove(number) && _picks.Count < KenoPayouts.MaxSpots)
        {
            _picks.Add(number);
        }
    }

    private void QuickPick()
    {
        if (_busy)
        {
            return;
        }

        ClearResults();
        _toast = null;
        _picks.Clear();
        var pool = Enumerable.Range(1, KenoPayouts.PoolSize).ToList();
        for (var i = 0; i < KenoPayouts.MaxSpots; i++)
        {
            var j = Random.Shared.Next(pool.Count);
            _picks.Add(pool[j]);
            pool.RemoveAt(j);
        }
    }

    private void Clear()
    {
        if (_busy)
        {
            return;
        }

        _picks.Clear();
        ClearResults();
        _toast = null;
    }

    private string CellClass(int number)
    {
        var cls = "kn-cell";
        if (_picks.Contains(number))
        {
            cls += " kn-pick";
        }

        if (HasResult)
        {
            if (_hits.Contains(number))
            {
                cls += " kn-hit";
            }
            else if (_drawnOrder.ContainsKey(number))
            {
                cls += " kn-drawn";
            }
        }

        return cls;
    }

    private string CellStyle(int number) =>
        _drawnOrder.TryGetValue(number, out var order) ? $"animation-delay:{order * 55}ms" : "";

    private static string Mult(double multiplier) => multiplier.ToString("0.##", CultureInfo.InvariantCulture) + "×";

    private async Task Play()
    {
        if (!CanPlay || _client is null)
        {
            return;
        }

        _busy = true;
        _toast = null;
        ClearResults();
        StateHasChanged();

        // settle on the server first — the draw is final before any ball lights up
        _balance = await Persistence.RemoveCreditsAsync(_client, _bet);
        var result = KenoMachine.Draw(_picks);
        var payout = (long)(_bet * result.Multiplier);
        if (payout > 0)
        {
            _balance = await Persistence.AddCreditsAsync(_client, payout);
        }

        // reveal the 20 drawn numbers (CSS staggers them by draw order)
        _drawn = result.Drawn;
        _drawnOrder = result.Drawn.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);
        _hits = result.Hits.ToHashSet();
        StateHasChanged();

        if (_audio is not null) { try { await _audio.InvokeVoidAsync("play", "bet"); } catch { } }
        await Task.Delay(RevealMs);

        var profit = payout - _bet;
        _toast = BuildToast(result, profit);
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Keno", "ph-grid-four", $"{result.Hits.Count}/{result.Spots}", profit, DateTimeOffset.UtcNow));

        _busy = false;
        StateHasChanged();

        if (_audio is not null)
        {
            try
            {
                if (profit > 0) await _audio.InvokeVoidAsync("win", profit, _bet);
                else await _audio.InvokeVoidAsync("play", "lose");
            }
            catch { /* best-effort */ }
        }
    }

    private GameToast BuildToast(KenoResult result, long profit)
    {
        var variant = result.Multiplier >= 50 && profit > 0 ? GameToastVariant.Jackpot
            : profit > 0 ? GameToastVariant.Win
            : GameToastVariant.Lose;

        var text = profit > 0
            ? $"{result.Hits.Count}/{result.Spots} hits — {Mult(result.Multiplier)}"
            : $"{result.Hits.Count}/{result.Spots} hits";

        var amount = profit > 0 ? $"+{profit:N0}" : profit.ToString("N0");
        var icon = result.Multiplier >= 50 ? "ph-crown" : profit > 0 ? "ph-coins" : "ph-x-circle";
        return new GameToast(++_toastSeq, variant, text, amount, icon);
    }

    public async ValueTask DisposeAsync()
    {
        if (_audio is not null)
        {
            try { await _audio.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
