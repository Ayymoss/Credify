using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Games.Cards;
using Credify.Games.ThreeCardPoker;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using GameConstants = Credify.Chat.Active.Core.GameConstants;

namespace Credify.Components.Games.ThreeCardPoker;

public partial class ThreeCardPoker
{
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private const long MinBet = GameConstants.MinimumCredits;

    private enum Phase { Betting, Decision, Settle }

    private readonly CardDeck _deck = new();
    private readonly Card? _noCard = null;

    private EFClient? _client;
    private long _balance;
    private long _ante = MinBet;
    private bool _pairPlusOn;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private Phase _phase = Phase.Betting;
    private readonly List<Card> _playerCards = [];
    private readonly List<Card> _dealerCards = [];
    private ThreeCardHand? _playerHand;
    private ThreeCardHand? _dealerHand;
    private ThreeCardOutcome? _outcome;

    private GameToast? _toast;
    private int _toastSeq;
    private IJSObjectReference? _audio;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    // winning-side card glow at showdown (ThreeCardOutcome.Result: "Win" | "Lose" | "Push" | "Fold")
    private bool PlayerWon => _phase == Phase.Settle && _outcome is { Result: "Win" };
    private bool DealerWon => _phase == Phase.Settle && _outcome is { Result: "Lose" };

    private long PairPlus => _pairPlusOn ? _ante : 0;
    private long DealCost => _ante + PairPlus;
    private bool CanDeal => !_busy && _authed && _client is not null && _ante >= MinBet && DealCost <= _balance;
    private bool CanPlay => !_busy && _balance >= _ante; // the Play bet equals the ante

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
                    _ante = Math.Clamp(Math.Min(MinBet, _balance), 0, Math.Max(0, _balance));
                    if (_balance >= MinBet && _ante < MinBet)
                    {
                        _ante = MinBet;
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

    private async Task Sfx(string name)
    {
        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("play", name); } catch { /* best-effort */ }
        }
    }

    private async Task Deal()
    {
        if (!CanDeal || _client is null)
        {
            return;
        }

        _busy = true;
        _toast = null;
        _outcome = null;
        _playerCards.Clear();
        _dealerCards.Clear();

        _balance = await Persistence.RemoveCreditsAsync(_client, DealCost);

        for (var i = 0; i < 3; i++)
        {
            _playerCards.Add(_deck.Draw());
            _dealerCards.Add(_deck.Draw());
        }

        _playerHand = ThreeCardHand.Evaluate(_playerCards);
        _dealerHand = ThreeCardHand.Evaluate(_dealerCards);
        _phase = Phase.Decision;

        await Sfx("deal");
        _busy = false;
    }

    private Task Play() => Resolve(played: true);
    private Task Fold() => Resolve(played: false);

    private async Task Resolve(bool played)
    {
        if (_busy || _client is null || _phase != Phase.Decision || _playerHand is null || _dealerHand is null)
        {
            return;
        }

        _busy = true;

        if (played)
        {
            if (_balance < _ante)
            {
                _busy = false;
                return;
            }

            _balance = await Persistence.RemoveCreditsAsync(_client, _ante); // the Play bet
            await Sfx("bet");
        }
        else
        {
            await Sfx("fold");
        }

        var outcome = ThreeCardRules.Settle(_playerHand, _dealerHand, _ante, PairPlus, played);
        if (outcome.TotalReturn > 0)
        {
            _balance = await Persistence.AddCreditsAsync(_client, outcome.TotalReturn);
        }

        _outcome = outcome;
        _phase = Phase.Settle;
        _toast = BuildToast(outcome);
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Three-Card Poker", "ph-cards", _playerHand.Name, outcome.Net, DateTimeOffset.UtcNow));

        _busy = false;
        StateHasChanged();

        if (_audio is not null)
        {
            try
            {
                if (outcome.Net > 0) await _audio.InvokeVoidAsync("win", outcome.Net, outcome.TotalStaked);
                else await _audio.InvokeVoidAsync("play", "lose");
            }
            catch { /* best-effort */ }
        }
    }

    private void NewHand()
    {
        if (_busy)
        {
            return;
        }

        _phase = Phase.Betting;
        _playerCards.Clear();
        _dealerCards.Clear();
        _playerHand = null;
        _dealerHand = null;
        _outcome = null;
        _toast = null;
        _ante = Math.Clamp(_ante, MinBet, Math.Max(MinBet, _balance));
    }

    private GameToast BuildToast(ThreeCardOutcome outcome)
    {
        var variant = outcome.Net > 0
            ? (_playerHand!.Category >= ThreeCardCategory.Straight ? GameToastVariant.Jackpot : GameToastVariant.Win)
            : GameToastVariant.Lose;

        var text = outcome.Result switch
        {
            "Fold" => outcome.PairPlusReturn > 0 ? "Folded — Pair Plus paid" : "Folded",
            "Push" => "Push",
            "Win" when !outcome.DealerQualified => "Dealer didn't qualify",
            "Win" => $"You win — {_playerHand!.Name}",
            _ => $"Dealer wins"
        };

        var amount = outcome.Net >= 0 ? $"+{outcome.Net:N0}" : outcome.Net.ToString("N0");
        var icon = outcome.Net > 0 ? (_playerHand!.Category >= ThreeCardCategory.Straight ? "ph-crown" : "ph-coins") : "ph-x-circle";
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
