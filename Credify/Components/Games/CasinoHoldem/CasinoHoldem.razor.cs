using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Chat.Active.Games.Poker.Enums;
using Credify.Chat.Active.Games.Poker.Models;
using Credify.Chat.Active.Games.Poker.Services;
using Credify.Components.Shared;
using Credify.Games.CasinoHoldem;
using Credify.Games.Cards;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using GameConstants = Credify.Chat.Active.Core.GameConstants;

namespace Credify.Components.Games.CasinoHoldem;

public partial class CasinoHoldem
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
    private readonly PokerHandEvaluator _evaluator = new();
    private readonly Card? _noCard = null;

    private EFClient? _client;
    private long _balance;
    private long _ante = MinBet;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private Phase _phase = Phase.Betting;
    private readonly List<Card> _playerHole = [];
    private readonly List<Card> _dealerHole = [];
    private readonly List<Card> _community = [];
    private PokerHand? _playerHint;  // best available from 2 hole + flop, shown at the decision
    private PokerHand? _playerHand;   // final 7-card hand
    private PokerHand? _dealerHand;
    private CasinoHoldemOutcome? _outcome;

    private GameToast? _toast;
    private int _toastSeq;
    private IJSObjectReference? _audio;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private long CallCost => _ante * 2;
    private bool CanDeal => !_busy && _authed && _client is not null && _ante >= MinBet && _ante <= _balance;
    private bool CanCall => !_busy && _balance >= CallCost;

    private static PokerCard ToPoker(Card card) => new((PokerCard.Suit)(int)card.Suit, (PokerCard.Rank)(int)card.Rank);

    private static string RankName(HandRank rank) => rank switch
    {
        HandRank.RoyalFlush => "Royal flush",
        HandRank.StraightFlush => "Straight flush",
        HandRank.FourOfAKind => "Four of a kind",
        HandRank.FullHouse => "Full house",
        HandRank.Flush => "Flush",
        HandRank.Straight => "Straight",
        HandRank.ThreeOfAKind => "Three of a kind",
        HandRank.TwoPair => "Two pair",
        HandRank.Pair => "Pair",
        _ => "High card"
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
        _playerHand = null;
        _dealerHand = null;
        _playerHole.Clear();
        _dealerHole.Clear();
        _community.Clear();

        _balance = await Persistence.RemoveCreditsAsync(_client, _ante);

        for (var i = 0; i < 2; i++)
        {
            _playerHole.Add(_deck.Draw());
            _dealerHole.Add(_deck.Draw());
        }
        for (var i = 0; i < 5; i++)
        {
            _community.Add(_deck.Draw());
        }

        _playerHint = _evaluator.EvaluateBestAvailable(
            _playerHole.Select(ToPoker).ToList(),
            _community.Take(3).Select(ToPoker).ToList());
        _phase = Phase.Decision;

        await Sfx("deal");
        _busy = false;
    }

    private Task Call() => Resolve(called: true);
    private Task Fold() => Resolve(called: false);

    private async Task Resolve(bool called)
    {
        if (_busy || _client is null || _phase != Phase.Decision)
        {
            return;
        }

        _busy = true;

        if (called)
        {
            if (_balance < CallCost)
            {
                _busy = false;
                return;
            }

            _balance = await Persistence.RemoveCreditsAsync(_client, CallCost);
            await Sfx("bet");

            _playerHand = _evaluator.EvaluateBestHand(_playerHole.Select(ToPoker).ToList(), _community.Select(ToPoker).ToList());
            _dealerHand = _evaluator.EvaluateBestHand(_dealerHole.Select(ToPoker).ToList(), _community.Select(ToPoker).ToList());
        }
        else
        {
            await Sfx("fold");
        }

        var outcome = called
            ? CasinoHoldemRules.Settle(_playerHand!, _dealerHand!, _ante, called: true)
            : CasinoHoldemRules.Settle(_playerHint!, _playerHint!, _ante, called: false); // folded — hands unused

        if (outcome.TotalReturn > 0)
        {
            _balance = await Persistence.AddCreditsAsync(_client, outcome.TotalReturn);
        }

        _outcome = outcome;
        _phase = Phase.Settle;
        _toast = BuildToast(outcome);
        var label = called && _playerHand is not null ? RankName(_playerHand.Rank) : "Folded";
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Casino Hold'em", "ph-spade", label, outcome.Net, DateTimeOffset.UtcNow));

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
        _playerHole.Clear();
        _dealerHole.Clear();
        _community.Clear();
        _playerHint = null;
        _playerHand = null;
        _dealerHand = null;
        _outcome = null;
        _toast = null;
        _ante = Math.Clamp(_ante, MinBet, Math.Max(MinBet, _balance));
    }

    private GameToast BuildToast(CasinoHoldemOutcome outcome)
    {
        var big = _playerHand is not null && _playerHand.Rank >= HandRank.Flush;
        var variant = outcome.Net > 0 ? (big ? GameToastVariant.Jackpot : GameToastVariant.Win) : GameToastVariant.Lose;

        var text = outcome.Result switch
        {
            "Fold" => "Folded",
            "Push" => "Push",
            "Win" when !outcome.DealerQualified => "Dealer didn't qualify",
            "Win" => _playerHand is not null ? $"You win — {RankName(_playerHand.Rank)}" : "You win",
            _ => "Dealer wins"
        };

        var amount = outcome.Net >= 0 ? $"+{outcome.Net:N0}" : outcome.Net.ToString("N0");
        var icon = outcome.Net > 0 ? (big ? "ph-crown" : "ph-coins") : "ph-x-circle";
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
