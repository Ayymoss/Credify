using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Credify.Chat.Active.Games.Poker;
using Credify.Configuration;
using Credify.Games.Cards;
using Credify.Games.Live;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.Poker;

public partial class Poker
{
    [Inject] public required PokerManager PokerGame { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required CredifyWebPlayers WebPlayers { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private readonly Card? _noCard = null;

    private PokerSnapshot _snapshot = new();
    private EFClient? _client;
    private int _viewerId = -1;
    private long _balance;
    private long _buyIn;
    private long _raise;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private IJSObjectReference? _audio;

    private int? _lastActive;
    private int _lastCommunity;
    private long _lastChips = -1;

    private CancellationTokenSource? _loopCts;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private PokerSeatView? MySeat =>
        _client is null ? null : _snapshot.Seats.FirstOrDefault(s => s.ClientId == _client.ClientId);

    private bool IsMe(PokerSeatView s) => _client is not null && s.ClientId == _client.ClientId;

    private string PhaseLabel => _snapshot.Phase switch
    {
        "WaitingForPlayers" => "Waiting for players",
        "BetweenHands" => "Shuffling up…",
        "PreFlop" => "Pre-flop",
        "Showdown" => "Showdown",
        _ => _snapshot.Phase
    };

    private string ActiveName =>
        _snapshot.Seats.FirstOrDefault(s => s.ClientId == _snapshot.ActiveClientId)?.Name ?? "player";

    protected override async Task OnInitializedAsync()
    {
        _buyIn = Config.Poker.MinimumBuyIn;

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
                    _viewerId = clientId;
                    _balance = await Persistence.GetClientCreditsAsync(_client);
                    _buyIn = Math.Clamp(Config.Poker.MinimumBuyIn, Config.Poker.MinimumBuyIn, Math.Max(Config.Poker.MinimumBuyIn, _balance));
                }
            }
        }

        _snapshot = PokerGame.GetSnapshot(_viewerId);
        _lastActive = _snapshot.ActiveClientId;
        _lastCommunity = _snapshot.Community.Count;
        PokerGame.StateChanged += OnStateChanged;

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

    private void OnStateChanged() => _ = RefreshAsync();

    private async Task LoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
            while (await timer.WaitForNextTickAsync(token))
            {
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
            _snapshot = PokerGame.GetSnapshot(_viewerId);

            // keep the raise slider within the live range
            if (_snapshot.MyActions is { } a)
            {
                if (_raise < a.MinRaise || _raise > a.MaxRaise)
                {
                    _raise = a.MinRaise;
                }
            }

            await DriveAudioAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // circuit tearing down
        }
    }

    private async Task DriveAudioAsync()
    {
        if (_audio is null)
        {
            return;
        }

        // community card(s) dealt
        if (_snapshot.Community.Count != _lastCommunity)
        {
            _lastCommunity = _snapshot.Community.Count;
            if (_snapshot.Community.Count > 0) { try { await _audio.InvokeVoidAsync("play", "deal"); } catch { } }
        }

        // it just became my turn — alert ping
        if (_snapshot.ActiveClientId != _lastActive)
        {
            _lastActive = _snapshot.ActiveClientId;
            if (_snapshot.ActiveClientId == _viewerId) { try { await _audio.InvokeVoidAsync("play", "click"); } catch { } }
        }

        // my chips went up (won a pot) — money effect
        var myChips = MySeat?.Chips;
        if (myChips is { } chips)
        {
            if (_lastChips >= 0 && chips > _lastChips)
            {
                try { await _audio.InvokeVoidAsync("win", chips - _lastChips, chips - _lastChips >= _snapshot.BigBlind * 20); } catch { }
            }
            _lastChips = chips;
        }
        else
        {
            _lastChips = -1;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
        }
    }

    private async Task Join()
    {
        if (_busy || _client is null || _buyIn < Config.Poker.MinimumBuyIn || _buyIn > _balance)
        {
            return;
        }

        _busy = true;
        if (_audio is not null) { try { await _audio.InvokeVoidAsync("play", "bet"); } catch { } }
        await PokerGame.JoinGameAsync(_client, _buyIn);
        _balance = await Persistence.GetClientCreditsAsync(_client);
        _lastChips = MySeat?.Chips ?? _buyIn; // baseline so the buy-in doesn't read as a win
        await RefreshAsync();
        _busy = false;
    }

    private async Task Leave()
    {
        if (_busy || _client is null)
        {
            return;
        }

        _busy = true;
        await PokerGame.LeaveGameAsync(_client);
        _balance = await Persistence.GetClientCreditsAsync(_client);
        _lastChips = -1;
        await RefreshAsync();
        _busy = false;
    }

    private async Task Act(string command)
    {
        if (_busy || _client is null)
        {
            return;
        }

        _busy = true;
        var sfx = command[0] switch { 'f' => "fold", 'r' => "bet", 'a' => "bet", _ => "stand" };
        if (_audio is not null) { try { await _audio.InvokeVoidAsync("play", sfx); } catch { } }
        await PokerGame.HandleChatAsync(_client, command);
        await RefreshAsync();
        _busy = false;
    }

    public async ValueTask DisposeAsync()
    {
        PokerGame.StateChanged -= OnStateChanged;
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
            _loopCts.Dispose();
        }

        if (_audio is not null)
        {
            try { await _audio.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }
}
