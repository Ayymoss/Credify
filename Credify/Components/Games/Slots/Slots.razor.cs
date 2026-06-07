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

    private EFClient? _client;
    private long _balance;
    private long _bet;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private string[] _reels = ["7", "BAR", "🔔"];
    private GameToast? _toast;
    private int _toastSeq;

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
        }
    }

    private bool CanSpin => !_busy && _authed && _client is not null && _bet >= MinBet && _bet <= _balance;

    private async Task Spin()
    {
        if (!CanSpin || _client is null)
        {
            return;
        }

        _busy = true;
        StateHasChanged();

        // mechanical reel sound (lever + whir + staggered stops), fired on the click for instant feedback;
        // its stop thunks are timed to land with the reels' visual stops in slots.js
        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("reels"); } catch { /* best-effort audio */ }
        }

        // settle on the server first — the spin result is final before the reels even move
        var receipt = await SlotsService.SpinAsync(_client, _bet);
        var finals = receipt.Spin.Reels.Select(Glyph).ToArray();

        // let the JS reels flicker and settle on the final symbols (it owns the DOM during the roll)
        if (_slotsJs is not null)
        {
            try { await _slotsJs.InvokeVoidAsync("spin", finals, AllGlyphs.ToArray()); }
            catch { /* circuit/JS gone — fall through and just show the result */ }
        }

        _reels = finals;
        _balance = receipt.NewBalance;
        _toast = BuildToast(receipt);
        var label = receipt.Winnings > 0 ? $"{receipt.Spin.Multiplier:0.##}×" : "No win";
        GameHistory.Record(_client.ClientId, new GameHistoryEntry(
            "Slots", "ph-cherries", label, receipt.Profit, DateTimeOffset.UtcNow));
        _busy = false;
        StateHasChanged();

        await PlayResultSoundAsync(receipt);
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
