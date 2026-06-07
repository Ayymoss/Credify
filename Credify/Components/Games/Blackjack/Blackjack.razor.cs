using System;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Credify.Chat.Active.Games.Blackjack;
using Credify.Games.Cards;
using Credify.Games.Live;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.Blackjack;

public partial class Blackjack
{
    [Inject] public required BlackjackGame Game { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required CredifyWebPlayers WebPlayers { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required Credify.Configuration.CredifyConfiguration Config { get; set; }

    private readonly Card? _noCard = null; // typed null for the dealer's face-down hole card

    private BlackjackSnapshot _snapshot = new();
    private EFClient? _client;
    private long _balance;
    private long _stake = 50;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private IJSObjectReference? _audio;

    private string _lastPhase = "";
    private string _lastMyOutcome = "";

    private CancellationTokenSource? _loopCts;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private BlackjackSeatView? MySeat =>
        _client is null ? null : _snapshot.Seats.FirstOrDefault(s => s.ClientId == _client.ClientId);

    private bool IsMe(BlackjackSeatView s) => _client is not null && s.ClientId == _client.ClientId;

    private string StatusClass => "bj-banner " + _snapshot.Phase switch
    {
        "Betting" => "bj-banner-win",
        "Insurance" or "Decisions" => "bj-banner-jackpot",
        "Payout" => "bj-banner-push",
        _ => "bj-banner-idle"
    };

    private string StatusIcon => _snapshot.Phase switch
    {
        "Betting" => "ph-fill ph-coins",
        "Insurance" => "ph-fill ph-shield",
        "Decisions" => "ph-fill ph-hand-pointing",
        "DealerPlaying" => "ph-fill ph-user-circle",
        "Payout" => "ph-fill ph-flag-checkered",
        "Dealing" => "ph ph-spinner animate-spin",
        _ => "ph-fill ph-users"
    };

    private string StatusText => _snapshot.Phase switch
    {
        "Betting" => "Place your bets",
        "Insurance" => "Insurance?",
        "Decisions" => "Players deciding",
        "DealerPlaying" => "Dealer plays",
        "Payout" => "Round over",
        "Dealing" => "Dealing…",
        _ => "Waiting for players"
    };

    private bool StatusShowsTimer => _snapshot.Phase is "Betting" or "Insurance" or "Decisions";

    private double ActionWindow => Config.Blackjack.TimeoutForPlayerAction.TotalSeconds;

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
                }
            }
        }

        _snapshot = Game.GetSnapshot();
        _lastPhase = _snapshot.Phase;
        Game.StateChanged += OnStateChanged;

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

    // marshal onto the renderer's context so this never races the timer-loop RefreshAsync (a concurrent
    // run could pass the outcome-changed check twice and double-record the round to the history rail)
    private void OnStateChanged() => _ = InvokeAsync(RefreshAsync);

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
            _snapshot = Game.GetSnapshot();

            // cards-dealt sound on entering the decision phase
            if (_snapshot.Phase != _lastPhase)
            {
                if (_snapshot.Phase is "Decisions" or "Insurance" && _lastPhase is "Betting" or "Dealing")
                {
                    await Sfx("deal");
                }
                _lastPhase = _snapshot.Phase;
            }

            // my result sound + balance refresh when my outcome settles
            var myOutcome = MySeat?.Outcome ?? "";
            if (myOutcome != _lastMyOutcome)
            {
                _lastMyOutcome = myOutcome;

                // log the settled round to the session history rail (skip pushes — a net-zero entry is noise)
                if (myOutcome is "Win" or "Blackjack" or "Lose" && _client is not null && MySeat is { } seat)
                {
                    GameHistory.Record(_client.ClientId, new GameHistoryEntry(
                        "Blackjack", "ph-cards-three", OutcomeLabel(myOutcome), seat.Net, DateTimeOffset.UtcNow));
                }

                // refresh the balance whenever a round settles for me
                if (_client is not null && myOutcome is "Win" or "Blackjack" or "Lose" or "Push")
                {
                    _balance = await Persistence.GetClientCreditsAsync(_client);
                }

                // the coin/money count plays ONLY on a genuine positive net. A split can settle with a
                // "Win" primary hand while the round nets a loss — that must never trigger the win sound.
                var net = MySeat?.Net ?? 0;
                if (net > 0)
                {
                    var stake = MySeat?.Stake ?? 0;
                    if (_audio is not null) { try { await _audio.InvokeVoidAsync("win", net, stake); } catch { } }
                }
                else if (myOutcome == "Lose")
                {
                    await Sfx("lose");
                }
                else if (myOutcome == "Push")
                {
                    await Sfx("push");
                }
            }

            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // circuit tearing down
        }
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
            try { await _audio.InvokeVoidAsync("play", name); } catch { }
        }
    }

    private async Task Join()
    {
        if (_busy || _client is null)
        {
            return;
        }

        _busy = true;
        await Sfx("click");
        await Game.JoinGameAsync(_client);
        await RefreshAsync();
        _busy = false;
    }

    private async Task PlaceBet()
    {
        if (_busy || _client is null || _stake < 10 || _stake > _balance)
        {
            return;
        }

        _busy = true;
        await Game.HandleChatAsync(_client, _stake.ToString());
        if (_client is not null) _balance = await Persistence.GetClientCreditsAsync(_client);
        await Sfx("bet");
        await RefreshAsync();
        _busy = false;
    }

    // feed the canonical action keyword through the same path chat uses
    private async Task Act(string action)
    {
        if (_busy || _client is null)
        {
            return;
        }

        _busy = true;
        await Sfx(action switch { "hit" => "hit", "double" => "bet", "split" => "bet", "stand" => "stand", _ => "click" });
        await Game.HandleChatAsync(_client, action);
        if (_client is not null) _balance = await Persistence.GetClientCreditsAsync(_client);
        await RefreshAsync();
        _busy = false;
    }

    private static string SeatStateLabel(BlackjackSeatView s) => s.State switch
    {
        "Waiting" => "next round",
        "SittingOut" => "sitting out",
        "Betting" => "betting…",
        "Playing" => "deciding",
        "PlayingSplit" => "split",
        "Stand" => s.IsBlackjack ? "blackjack" : "stand",
        "Busted" => "bust",
        _ => ""
    };

    private static string SeatStateClass(BlackjackSeatView s) => "text-xs font-semibold " + s.State switch
    {
        "Busted" => "text-rose-300",
        "Stand" => s.IsBlackjack ? "text-amber-300" : "text-zinc-400",
        "Playing" or "PlayingSplit" => "text-emerald-300",
        _ => "text-zinc-500"
    };

    private static string OutcomeLabel(string o) => o switch
    {
        "Blackjack" => "Blackjack!",
        "Win" => "Win",
        "Push" => "Push",
        _ => "Lose"
    };

    private static string OutcomeClass(string o) => "text-sm font-bold " + o switch
    {
        "Blackjack" => "text-amber-300",
        "Win" => "text-emerald-300",
        "Push" => "text-zinc-300",
        _ => "text-rose-300"
    };

    private string WaitingText(BlackjackSeatView seat) => _snapshot.Phase switch
    {
        "Betting" when seat.Stake is not null => "Bet placed — waiting for other players.",
        "Decisions" when seat.State == "Stand" => "You stand — waiting for the dealer.",
        "Decisions" when seat.State == "Busted" => "Busted — waiting for the round to finish.",
        "Insurance" => "Insurance offered to eligible players…",
        "DealerPlaying" => "Dealer is playing…",
        "Payout" => "Round over — next round starting.",
        "Waiting" => "Waiting for the next round to begin.",
        _ => "Waiting…"
    };

    public async ValueTask DisposeAsync()
    {
        Game.StateChanged -= OnStateChanged;
        if (_loopCts is not null)
        {
            await _loopCts.CancelAsync();
            _loopCts.Dispose();
        }

        foreach (var module in new[] { _audio })
        {
            if (module is null)
            {
                continue;
            }

            try
            {
                await module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // circuit already gone
            }
        }
    }
}
