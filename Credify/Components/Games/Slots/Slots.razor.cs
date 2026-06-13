using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Components.Shared;
using Credify.Configuration;
using Credify.Games.Slots;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace Credify.Components.Games.Slots;

public partial class Slots
{
    [Inject] public required SlotsService SlotsService { get; set; }
    [Inject] public required GameHistoryService GameHistory { get; set; }
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    // emoji for the well-known symbols; anything custom falls back to its configured Display text
    private static readonly Dictionary<string, string> GlyphMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["BELL"] = "🔔",
        ["CHERRY"] = "🍒",
        ["LEMON"] = "🍋",
        ["ORANGE"] = "🍊",
        ["7"] = "7",
        ["BAR"] = "BAR",
    };

    private enum Phase { Idle, Won, Picking, Revealing }

    private EFClient? _client;
    private long _balance;
    private long _bet;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    // gamble (double-or-nothing) episode state
    private Phase _phase = Phase.Idle;
    private long _gambleAmount;   // amount currently on the table (collectable / gamblable)
    private int _gambleSteps;     // gambles taken this episode
    private long _episodeBet;     // the spin's stake, for the net history entry at episode end
    private bool _lastGambleWon;
    private bool _lastLandedRed;

    private bool GambleOn => SlotsService.GambleEnabled;
    private int MaxSteps => SlotsService.GambleMaxSteps;

    private const int BulbCount = 9; // bulbs per marquee rail (idle shimmer / win chase)

    private GameToast? _toast;
    private int _toastSeq;

    private ElementReference _machine; // the .sl-cabinet element; slots.js owns the reels inside it
    private IJSObjectReference? _audio;
    private IJSObjectReference? _slotsJs;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    private long MinBet => SlotsService.MinBet;
    // 0 in config means "no cap"; BetControl treats Max <= 0 as uncapped
    private long? MaxBet => SlotsService.MaxBet;

    private string Glyph(SlotSymbol symbol) =>
        GlyphMap.TryGetValue(symbol.Name, out var glyph) ? glyph : symbol.Display;

    private IEnumerable<string> AllGlyphs => Config.Slots.Symbols.Select(Glyph);

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
            _slotsJs = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/slots/slots.js");

            // build the resting reels so the cabinet shows symbols before the first pull
            if (_authed)
            {
                try { await _slotsJs.InvokeVoidAsync("mount", _machine, AllGlyphs.ToArray()); }
                catch { /* circuit/JS gone — the cabinet just stays blank until first spin */ }
            }
        }
    }

    private bool CanSpin => !_busy && _phase == Phase.Idle && _authed && _client is not null && _bet >= MinBet && _bet <= _balance;

    private async Task Spin()
    {
        if (!CanSpin || _client is null)
        {
            return;
        }

        _busy = true;
        _toast = null;
        StateHasChanged();

        // settle on the server first — the spin result is final before the reels even move
        var receipt = await SlotsService.SpinAsync(_client, _bet);
        var finals = receipt.Spin.Reels.Select(Glyph).ToArray();

        // reels land on the server-decided symbols; pass the win state so JS can glow the payline
        // and run the bulb chase / jackpot flash once every reel has stopped
        if (_slotsJs is not null)
        {
            // the final reel teases (slow crawl) when the first two already match — the audio's
            // heartbeat + stop-thunk timings mirror this, so fire it alongside the visual spin
            var tease = finals.Length >= 2 && finals[0] == finals[1];
            if (_audio is not null)
            {
                try { await _audio.InvokeVoidAsync("reels", tease); } catch { /* best-effort audio */ }
            }

            var opts = new
            {
                win = receipt.Winnings > 0,
                jackpot = receipt.Spin.Outcome == SlotOutcome.Jackpot
            };
            try { await _slotsJs.InvokeVoidAsync("spin", _machine, finals, AllGlyphs.ToArray(), opts); }
            catch { /* circuit/JS gone — fall through and just show the result */ }
        }

        _balance = receipt.NewBalance;
        _busy = false;

        if (receipt.Winnings > 0 && GambleOn)
        {
            // a win you can gamble: hold the result on the table and offer collect / double-or-nothing.
            // The win is already banked; history + the result banner wait until the episode is settled.
            _episodeBet = receipt.Bet;
            _gambleAmount = receipt.Winnings;
            _gambleSteps = 0;
            _phase = Phase.Won;
            StateHasChanged();
            // a win you can act on: a short chime flags it, but the money count-up is held back until
            // Collect — so it celebrates the amount you actually walk away with (incl. any doubling)
            if (_audio is not null) { try { await _audio.InvokeVoidAsync("gem", 5); } catch { /* best-effort */ } }
        }
        else
        {
            // immediate settle: a loss, or a win with gambling disabled
            _toast = BuildToast(receipt);
            var label = receipt.Winnings > 0 ? $"{receipt.Spin.Multiplier:0.##}×" : "No win";
            GameHistory.Record(_client.ClientId, new GameHistoryEntry(
                "Slots", "ph-cherries", label, receipt.Profit, DateTimeOffset.UtcNow));
            _phase = Phase.Idle;
            StateHasChanged();
            await PlayResultSoundAsync(receipt);
        }
    }

    // ── gamble (double-or-nothing) ───────────────────────────────────────────
    private async Task Collect()
    {
        if (_phase is not Phase.Won || _client is null) return;

        var profit = _gambleAmount - _episodeBet; // net for the whole episode
        SlotsService.CollectGamble(_client);
        _toast = new GameToast(++_toastSeq, GameToastVariant.Win, "Collected", $"+{profit:N0}", "ph-hand-coins");
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Slots", "ph-cherries", $"collect {_gambleAmount:N0}", profit, DateTimeOffset.UtcNow));
        _phase = Phase.Idle;

        // the satisfying money count-up fires here, on collect — scaled to the full amount banked
        if (_audio is not null && profit > 0) { try { await _audio.InvokeVoidAsync("win", profit, _episodeBet); } catch { /* best-effort */ } }
        StateHasChanged();
    }

    private void BeginGamble()
    {
        if (_phase is Phase.Won) _phase = Phase.Picking;
    }

    private async Task PickColour(bool red)
    {
        if (_phase is not Phase.Picking || _client is null) return;

        _busy = true;
        StateHasChanged();
        if (_audio is not null) { try { await _audio.InvokeVoidAsync("play", "bet"); } catch { /* best-effort */ } }

        var result = await SlotsService.GambleAsync(_client, red);
        _busy = false;

        if (result is null) { _phase = Phase.Idle; StateHasChanged(); return; }

        _balance = result.NewBalance;
        _lastGambleWon = result.Won;
        _lastLandedRed = result.LandedRed;
        _phase = Phase.Revealing;
        StateHasChanged();

        if (_audio is not null) { try { await _audio.InvokeVoidAsync("play", result.Won ? "cash" : "lose"); } catch { /* best-effort */ } }
        await Task.Delay(1100); // let the card land before resolving

        if (result.Won)
        {
            _gambleAmount = result.AmountNow;
            _gambleSteps = result.Steps;
            if (result.CanContinue)
            {
                _phase = Phase.Won; // ride it again
                StateHasChanged();
            }
            else
            {
                await Collect(); // hit the cap — bank it
            }
        }
        else
        {
            _toast = new GameToast(++_toastSeq, GameToastVariant.Lose, "Gambled away", (-_episodeBet).ToString("N0"), "ph-x-circle");
            GameHistory.Record(_client.ClientId, new GameHistoryEntry(
                "Slots", "ph-cherries", "gamble bust", -_episodeBet, DateTimeOffset.UtcNow));
            _phase = Phase.Idle;
            StateHasChanged();
        }
    }

    private async Task PlayResultSoundAsync(SlotSpinReceipt receipt)
    {
        if (_audio is null)
        {
            return;
        }

        try
        {
            if (receipt.Winnings > 0)
            {
                await _audio.InvokeVoidAsync("win", receipt.Profit, receipt.Bet);
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

    private GameToast BuildToast(SlotSpinReceipt receipt)
    {
        var variant = receipt.Spin.Outcome switch
        {
            SlotOutcome.Jackpot => GameToastVariant.Jackpot,
            _ when receipt.Winnings > 0 => GameToastVariant.Win,
            _ => GameToastVariant.Lose
        };
        var amount = receipt.Winnings > 0 ? $"+{receipt.Profit:N0}" : (-receipt.Bet).ToString("N0");
        return new GameToast(++_toastSeq, variant, BannerText(receipt), amount, BannerIcon(receipt.Spin.Outcome));
    }

    private static string BannerIcon(SlotOutcome outcome) => outcome switch
    {
        SlotOutcome.Jackpot => "ph-crown",
        SlotOutcome.ThreeOfAKind => "ph-coins",
        SlotOutcome.TwoOfAKind => "ph-hand-coins",
        _ => "ph-x-circle"
    };

    private static string BannerText(SlotSpinReceipt receipt) => receipt.Spin.Outcome switch
    {
        SlotOutcome.Jackpot => "JACKPOT!",
        SlotOutcome.ThreeOfAKind => "Three of a kind!",
        SlotOutcome.TwoOfAKind when receipt.Winnings > 0 => "Two of a kind",
        _ => "No win"
    };

    public async ValueTask DisposeAsync()
    {
        foreach (var module in new[] { _slotsJs, _audio })
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
