using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Games.Wheel;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using GameConstants = Credify.Chat.Active.Core.GameConstants;

namespace Credify.Components.Games.Wheel;

public partial class Wheel
{
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private const long MinBet = GameConstants.MinimumCredits;
    private static readonly IReadOnlyList<WheelSegment> Segments = WheelMachine.DefaultWheel;
    private static readonly int TotalWeight = Segments.Sum(slice => slice.Weight);

    // precomputed slice geometry (degrees, clockwise from 12 o'clock) + the conic-gradient wheel face
    private static readonly double[] SliceMid = BuildMidAngles();
    private static readonly string ConicCss = BuildConic();

    private EFClient? _client;
    private long _balance;
    private long _bet = MinBet;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private GameToast? _toast;
    private int _toastSeq;
    private string _resultLabel = "—";

    private IJSObjectReference? _audio;
    private IJSObjectReference? _wheelJs;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private bool CanSpin => !_busy && _authed && _client is not null && _bet >= MinBet && _bet <= _balance;

    private static double[] BuildMidAngles()
    {
        var mids = new double[Segments.Count];
        double cumulative = 0;
        for (var i = 0; i < Segments.Count; i++)
        {
            var start = cumulative / TotalWeight * 360.0;
            cumulative += Segments[i].Weight;
            var end = cumulative / TotalWeight * 360.0;
            mids[i] = (start + end) / 2.0;
        }

        return mids;
    }

    private static string BuildConic()
    {
        var stops = new List<string>(Segments.Count);
        double cumulative = 0;
        foreach (var slice in Segments)
        {
            var start = cumulative / TotalWeight * 360.0;
            cumulative += slice.Weight;
            var end = cumulative / TotalWeight * 360.0;
            stops.Add($"{slice.Color} {Deg(start)}deg {Deg(end)}deg");
        }

        return $"conic-gradient(from 0deg, {string.Join(", ", stops)})";
    }

    private static string Deg(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    private static string Mult(WheelSegment slice) => slice.Multiplier.ToString("0.##", CultureInfo.InvariantCulture) + "×";
    private static string Chance(WheelSegment slice) => (slice.Weight / (double)TotalWeight * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

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
            _wheelJs = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/wheel/wheel.js");
        }
    }

    private async Task Spin()
    {
        if (!CanSpin || _client is null)
        {
            return;
        }

        _busy = true;
        _toast = null;
        StateHasChanged();

        // settle on the server first — the result is final before the wheel turns
        _balance = await Persistence.RemoveCreditsAsync(_client, _bet);
        var spin = WheelMachine.Spin(Segments);
        var payout = (long)(_bet * spin.Segment.Multiplier);
        var profit = payout - _bet;

        if (_audio is not null) { try { await _audio.InvokeVoidAsync("play", "bet"); } catch { } }

        // turn the wheel so the pointer lands on the result slice (JS owns the rotation; resolves on stop)
        if (_wheelJs is not null)
        {
            try { await _wheelJs.InvokeVoidAsync("spin", SliceMid[spin.Index]); } catch { }
        }

        if (payout > 0)
        {
            _balance = await Persistence.AddCreditsAsync(_client, payout);
        }

        _resultLabel = Mult(spin.Segment);
        _toast = BuildToast(spin, profit);
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Wheel", "ph-circle-half", _resultLabel, profit, DateTimeOffset.UtcNow));

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

    private GameToast BuildToast(WheelSpin spin, long profit)
    {
        var variant = spin.Segment.Multiplier >= 10 && profit > 0 ? GameToastVariant.Jackpot
            : profit > 0 ? GameToastVariant.Win
            : GameToastVariant.Lose;

        var text = profit > 0
            ? $"{Mult(spin.Segment)} — nice hit!"
            : spin.Segment.Multiplier >= 1 ? "Broke even" : "Back to the wheel";

        var amount = profit > 0 ? $"+{profit:N0}" : profit.ToString("N0");
        var icon = spin.Segment.Multiplier >= 10 ? "ph-crown" : profit > 0 ? "ph-coins" : "ph-x-circle";
        return new GameToast(++_toastSeq, variant, text, amount, icon);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var module in new[] { _wheelJs, _audio })
        {
            if (module is null)
            {
                continue;
            }

            try { await module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
