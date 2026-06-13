using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Configuration;
using Credify.Games.Baccarat;
using Credify.Games.Cards;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.Baccarat;

public partial class Baccarat
{
    [Inject] public required BaccaratService Bac { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private enum Phase { Bet, Dealing, Result }

    private EFClient? _client;
    private long _balance;
    private long _bet;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private Phase _phase = Phase.Bet;
    private BaccaratBet _choice = BaccaratBet.Player;

    private readonly Card?[] _player = new Card?[3];
    private readonly Card?[] _banker = new Card?[3];
    private readonly bool[] _pFace = [true, true, true];
    private readonly bool[] _bFace = [true, true, true];
    private int _pCount;
    private int _bCount;

    private BaccaratReceipt? _result;
    private GameToast? _toast;
    private int _toastSeq;
    private IJSObjectReference? _audio;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private long MinBet => Bac.MinBet;
    private long? MaxBet => Bac.MaxBet;
    // can deal from Bet or after a Result (a new coup just resets) — only blocked mid-deal
    private bool CanDeal => !_busy && _phase != Phase.Dealing && _authed && _client is not null && _bet >= MinBet && _bet <= _balance;

    private BaccaratResult? ResultOutcome => _phase == Phase.Result ? _result?.Hand.Result : null;

    private int PlayerShown => RevealedTotal(_player, _pFace, _pCount);
    private int BankerShown => RevealedTotal(_banker, _bFace, _bCount);

    private static int RevealedTotal(Card?[] cards, bool[] face, int count)
    {
        var sum = 0;
        for (var i = 0; i < count; i++)
            if (!face[i] && cards[i] is { } c) sum += BaccaratRules.Value(c);
        return sum % 10;
    }

    // profit odds for a bet spot, e.g. "1.03 : 1"
    private string Odds(BaccaratBet bet)
    {
        var gross = bet switch
        {
            BaccaratBet.Player => Config.Baccarat.PlayerPayout,
            BaccaratBet.Banker => Config.Baccarat.BankerPayout,
            _ => Config.Baccarat.TiePayout
        };
        return $"{(gross - 1):0.##} : 1";
    }

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

    private void Choose(BaccaratBet bet)
    {
        if (_phase != Phase.Dealing && !_busy) _choice = bet;
    }

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
        Array.Clear(_player);
        Array.Clear(_banker);
        for (var i = 0; i < 3; i++) { _pFace[i] = true; _bFace[i] = true; }
        _pCount = _bCount = 0;
        _phase = Phase.Dealing;
        StateHasChanged();

        var receipt = await Bac.PlayAsync(_client, _bet, _choice);
        _balance -= _bet; // reconciled to the authoritative balance at settle
        var hand = receipt.Hand;

        _pCount = hand.Player.Count;
        _bCount = hand.Banker.Count;
        for (var i = 0; i < _pCount; i++) _player[i] = hand.Player[i];
        for (var i = 0; i < _bCount; i++) _banker[i] = hand.Banker[i];
        StateHasChanged();
        await Task.Delay(150);

        // reveal in dealing order: P, B, P, B, then any third cards (player first, then banker)
        var order = new List<(bool Player, int Index)> { (true, 0), (false, 0), (true, 1), (false, 1) };
        if (_pCount > 2) order.Add((true, 2));
        if (_bCount > 2) order.Add((false, 2));

        foreach (var (isPlayer, idx) in order)
        {
            if (isPlayer) _pFace[idx] = false; else _bFace[idx] = false;
            await Sfx("deal");
            StateHasChanged();
            await Task.Delay(430);
        }

        _result = receipt;
        _balance = receipt.NewBalance;
        _toast = BuildToast(receipt);
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Baccarat", "ph-cards", ResultLabel(hand.Result), receipt.Profit, DateTimeOffset.UtcNow));
        _phase = Phase.Result;
        _busy = false;
        StateHasChanged();

        if (_audio is not null)
        {
            try
            {
                if (receipt.Profit > 0) await _audio.InvokeVoidAsync("win", receipt.Profit, receipt.Bet);
                else if (receipt.Winnings > 0) await _audio.InvokeVoidAsync("play", "cash"); // tie push
                else await _audio.InvokeVoidAsync("play", "lose");
            }
            catch { /* best-effort */ }
        }
    }

    private static string ResultLabel(BaccaratResult result) => result switch
    {
        BaccaratResult.PlayerWin => "Player wins",
        BaccaratResult.BankerWin => "Banker wins",
        _ => "Tie"
    };

    private GameToast BuildToast(BaccaratReceipt r)
    {
        var tieWin = r.Choice == BaccaratBet.Tie && r.Hand.Result == BaccaratResult.Tie;
        var variant = tieWin ? GameToastVariant.Jackpot
            : r.Profit > 0 ? GameToastVariant.Win
            : r.Winnings > 0 ? GameToastVariant.Win // push
            : GameToastVariant.Lose;

        var amount = r.Profit > 0 ? $"+{r.Profit:N0}" : r.Winnings > 0 ? "±0" : $"-{r.Bet:N0}";
        var text = r.Winnings > 0 && r.Profit == 0 ? "Push — bet returned" : ResultLabel(r.Hand.Result);
        var icon = tieWin ? "ph-crown" : r.Profit > 0 ? "ph-coins" : "ph-x-circle";
        return new GameToast(++_toastSeq, variant, text, amount, icon);
    }
}
