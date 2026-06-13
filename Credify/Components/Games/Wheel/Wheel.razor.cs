using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text;
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

    // precomputed slice geometry (degrees, clockwise from 12 o'clock) + the SVG wheel face
    private static readonly double[] SliceMid = BuildMidAngles();
    private static readonly MarkupString FaceSvg = BuildFace();

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

    // SVG pie sectors rather than a CSS conic-gradient: Chromium paints conic gradients in four 90° arcs
    // and leaves hairline seams/clips at the compass points, plus ragged antialiasing where the gradient
    // meets the border-radius clip. Each slice starts a sliver early so antialiasing can't open a gap
    // against the slice painted before it (the first slice tucks under the last one across 0°).
    private static MarkupString BuildFace()
    {
        var svg = new StringBuilder("<svg viewBox=\"-51 -51 102 102\" aria-hidden=\"true\">");
        double cumulative = 0;
        for (var i = 0; i < Segments.Count; i++)
        {
            var start = cumulative / TotalWeight * 360.0;
            cumulative += Segments[i].Weight;
            var end = cumulative / TotalWeight * 360.0;
            svg.Append(Sector(start - 0.35, end, Segments[i].Color));
        }

        svg.Append("</svg>");
        return new MarkupString(svg.ToString());
    }

    private static string Sector(double startDeg, double endDeg, string color)
    {
        const double radius = 50; // viewBox half-size is 51 — the spare unit keeps edge antialiasing unclipped
        var (x0, y0) = Point(startDeg, radius);
        var (x1, y1) = Point(endDeg, radius);
        var largeArc = endDeg - startDeg > 180 ? 1 : 0;
        return $"<path d=\"M0 0 L{F(x0)} {F(y0)} A{radius} {radius} 0 {largeArc} 1 {F(x1)} {F(y1)} Z\" fill=\"{color}\"/>";
    }

    private static (double X, double Y) Point(double deg, double radius)
    {
        var rad = deg * Math.PI / 180.0;
        return (radius * Math.Sin(rad), -radius * Math.Cos(rad));
    }

    private static string F(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);
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
