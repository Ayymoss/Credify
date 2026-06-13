using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Configuration;
using Credify.Games.Cards;
using Credify.Games.VideoPoker;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.VideoPoker;

public partial class VideoPoker
{
    [Inject] public required VideoPokerService Vp { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    // paytable rows, strongest first
    private static readonly VideoPokerRank[] PayRanks =
    [
        VideoPokerRank.RoyalFlush, VideoPokerRank.StraightFlush, VideoPokerRank.FourOfAKind,
        VideoPokerRank.FullHouse, VideoPokerRank.Flush, VideoPokerRank.Straight,
        VideoPokerRank.ThreeOfAKind, VideoPokerRank.TwoPair, VideoPokerRank.JacksOrBetter
    ];

    private enum Phase { Bet, Decision }

    private EFClient? _client;
    private long _balance;
    private long _bet;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private Phase _phase = Phase.Bet;
    private Card?[] _cards = new Card?[5];
    private bool[] _hold = new bool[5];
    private bool[] _faceDown = new bool[5];
    private VideoPokerReceipt? _result; // last settled hand — drives the banner + paytable highlight

    private GameToast? _toast;
    private int _toastSeq;
    private IJSObjectReference? _audio;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private long MinBet => Vp.MinBet;
    private long? MaxBet => Vp.MaxBet;
    private bool CanDeal => !_busy && _phase == Phase.Bet && _authed && _client is not null && _bet >= MinBet && _bet <= _balance;
    private bool CanDraw => !_busy && _phase == Phase.Decision;

    protected override async Task OnInitializedAsync()
    {
        _bet = MinBet;
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
                    if (_balance >= MinBet && _bet < MinBet) _bet = MinBet;
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

    private long PayoutFor(VideoPokerRank rank) => (long)(_bet * Config.VideoPoker.Multiplier(rank));

    private async Task Sfx(string name)
    {
        if (_audio is not null) { try { await _audio.InvokeVoidAsync("play", name); } catch { /* best-effort */ } }
    }

    private async Task Deal()
    {
        if (!CanDeal || _client is null) return;

        _busy = true;
        _toast = null;
        _result = null;
        StateHasChanged();

        var cards = await Vp.DealAsync(_client, _bet);
        _balance -= _bet;                 // DrawAsync returns the authoritative balance to reconcile
        _cards = cards.Cast<Card?>().ToArray();
        _hold = new bool[5];
        _faceDown = [true, true, true, true, true]; // dealt face-down…
        _phase = Phase.Decision;
        StateHasChanged();

        // …then flipped up left-to-right (PlayingCard plays the 3D flip on each face-down → up)
        await Sfx("deal");
        for (var i = 0; i < 5; i++)
        {
            _faceDown[i] = false;
            StateHasChanged();
            await Task.Delay(75);
        }

        _busy = false;
        StateHasChanged();
    }

    private void ToggleHold(int i)
    {
        if (_phase != Phase.Decision || _busy) return;
        _hold[i] = !_hold[i];
    }

    private async Task Draw()
    {
        if (!CanDraw || _client is null) return;

        _busy = true;
        StateHasChanged();

        // 1) discard — the cards you didn't hold flip to their backs
        for (var i = 0; i < 5; i++) if (!_hold[i]) _faceDown[i] = true;
        StateHasChanged();
        await Task.Delay(260);

        // 2) settle on the server, swap the new cards in under the backs
        var result = await Vp.DrawAsync(_client, _hold);
        if (result is null) { _busy = false; _phase = Phase.Bet; StateHasChanged(); return; }
        _cards = result.Cards.Cast<Card?>().ToArray();
        StateHasChanged();
        await Task.Delay(60);

        // 3) reveal — flip the replacements face-up (PlayingCard auto-plays the 3D flip)
        for (var i = 0; i < 5; i++) if (!_hold[i]) _faceDown[i] = false;
        await Sfx("deal");
        StateHasChanged();
        await Task.Delay(480);

        // 4) settle the result
        _result = result;
        _balance = result.NewBalance;
        _toast = BuildToast(result);
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Video Poker", "ph-spade", VideoPokerHand.Name(result.Rank), result.Profit, DateTimeOffset.UtcNow));
        _phase = Phase.Bet;
        _busy = false;
        StateHasChanged();

        if (_audio is not null)
        {
            try
            {
                if (result.Profit > 0) await _audio.InvokeVoidAsync("win", result.Profit, result.Bet);
                else if (result.Winnings > 0) await _audio.InvokeVoidAsync("play", "cash"); // push: bet returned
                else await _audio.InvokeVoidAsync("play", "lose");
            }
            catch { /* best-effort */ }
        }
    }

    private GameToast BuildToast(VideoPokerReceipt r)
    {
        var variant = r.Rank is VideoPokerRank.RoyalFlush or VideoPokerRank.StraightFlush or VideoPokerRank.FourOfAKind
            ? GameToastVariant.Jackpot
            : r.Profit > 0 ? GameToastVariant.Win
            : r.Winnings > 0 ? GameToastVariant.Win   // push
            : GameToastVariant.Lose;

        var amount = r.Profit > 0 ? $"+{r.Profit:N0}"
            : r.Winnings > 0 ? "±0"
            : $"-{r.Bet:N0}";

        var icon = variant switch
        {
            GameToastVariant.Jackpot => "ph-crown",
            GameToastVariant.Win => "ph-coins",
            _ => "ph-x-circle"
        };

        var text = r.Winnings > 0 && r.Profit == 0 ? "Push — bet returned" : VideoPokerHand.Name(r.Rank);
        return new GameToast(++_toastSeq, variant, text, amount, icon);
    }
}
