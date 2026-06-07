using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Credify.Configuration;
using Credify.Games.Minefield;
using Credify.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using GameConstants = Credify.Chat.Active.Core.GameConstants;

namespace Credify.Components.Games.Minefield;

public partial class Minefield
{
    [Inject] public required PersistenceService Persistence { get; set; }
    [Inject] public required CredifyCache Cache { get; set; }
    [Inject] public required CredifyConfiguration Config { get; set; }
    [Inject] public required IManager Manager { get; set; }
    [Inject] public required IEntityService<EFClient> ClientService { get; set; }
    [Inject] public required IJSRuntime JS { get; set; }

    private static readonly long[] _chips = [10, 50, 100, 500];
    private static readonly int[] _minePresets = [1, 3, 5, 10];
    private const long MinBet = GameConstants.MinimumCredits;

    private MinefieldWebGame _game = null!;
    private EFClient? _client;
    private long _balance;
    private long _bet = MinBet;
    private int _mines = 3;
    private int _cols = 5;
    private bool _authed;
    private bool _loading = true;
    private bool _busy;

    private IJSObjectReference? _jsModule;
    private IJSObjectReference? _audio;

    [CascadingParameter] private Task<AuthenticationState>? AuthState { get; set; }

    // preview values shown on the configuring screen (a fresh field, 0 dug)
    private double FirstGemMultiplier => new MinefieldPayoutCalculator(Config.Minefield)
        .CalculateMultiplier(_game.TotalTiles, Math.Clamp(_mines, 1, _game.MaxMines), 1);
    private double FirstTileRisk => (double)Math.Clamp(_mines, 1, _game.MaxMines) / _game.TotalTiles;

    protected override async Task OnInitializedAsync()
    {
        _game = new MinefieldWebGame(Config.Minefield);
        _cols = Math.Max(1, (int)Math.Round(Math.Sqrt(_game.TotalTiles)));
        _mines = Math.Clamp(_mines, 1, _game.MaxMines);

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
                    _bet = Math.Clamp(Math.Min(50, _balance), 0, _balance);
                    if (_balance >= MinBet && _bet < MinBet)
                    {
                        _bet = MinBet;
                    }
                }
            }
        }

        _loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
            if (_authed)
            {
                _jsModule = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/minefield/minefield.js");
            }
        }
    }

    private async Task Sfx(string name)
    {
        if (_audio is not null)
        {
            try { await _audio.InvokeVoidAsync("play", name); } catch { /* best-effort */ }
        }
    }

    private async Task<EFClient?> ResolveClientAsync(int clientId)
    {
        // prefer the live in-game client so the in-memory balance stays consistent with the game server;
        // fall back to a DB lookup for web-only players who aren't currently connected.
        var live = Manager.GetActiveClients().FirstOrDefault(c => c.ClientId == clientId);
        return live ?? await ClientService.Get(clientId);
    }

    private void AddToBet(long amount) => _bet = Math.Clamp(_bet + amount, MinBet, _balance);
    private void BetMax() => _bet = Math.Max(MinBet, _balance);
    private void ClearBet() => _bet = Math.Min(MinBet, _balance);

    private void AdjustMines(int delta) => _mines = Math.Clamp(_mines + delta, 1, _game.MaxMines);
    private void SetMines(int value) => _mines = Math.Clamp(value, 1, _game.MaxMines);

    private async Task StartGame()
    {
        if (_busy || _client is null || _bet < MinBet || _bet > _balance)
        {
            return;
        }

        _busy = true;
        _balance = await Persistence.RemoveCreditsAsync(_client, _bet);
        _game.Start(_bet, _mines);
        await Sfx("bet");
        _busy = false;
    }

    private async Task Dig(int index)
    {
        if (_busy || _game.Phase != MinefieldPhase.Digging)
        {
            return;
        }

        _busy = true;
        _game.Dig(index);

        // safe reveal: a ping that climbs with each gem cleared (bust/win sounds fire in Finish)
        if (_game.Result is not MinefieldResult.Busted && _audio is not null)
        {
            try { await _audio.InvokeVoidAsync("gem", _game.DugCount); } catch { }
        }

        StateHasChanged();

        if (_game.Phase == MinefieldPhase.Settled)
        {
            await Finish();
        }
        else
        {
            _busy = false;
        }
    }

    private async Task CashOut()
    {
        if (_busy || !_game.CanCashOut)
        {
            return;
        }

        _busy = true;
        _game.CashOut();
        StateHasChanged();
        await Finish();
    }

    private async Task Finish()
    {
        var payout = _game.Payout;
        if (payout > 0 && _client is not null)
        {
            // mirror the chat games: stake was already removed on Start, winnings are "invented".
            // the house bank is intentionally untouched — losses don't drain it, wins aren't funded from it.
            _balance = await Persistence.AddCreditsAsync(_client, payout);
        }

        _busy = false;
        StateHasChanged();

        if (_game.Result is MinefieldResult.Busted)
        {
            if (_audio is not null)
            {
                try { await _audio.InvokeVoidAsync("bomb"); } catch { }
            }
            if (_jsModule is not null)
            {
                await _jsModule.InvokeVoidAsync("boom");
            }
        }
        else if (_game.NetResult > 0)
        {
            // a full clear, or a big multiplier, gets the bigger celebration
            var big = _game.Result is MinefieldResult.Cleared || _game.CurrentMultiplier >= 5d;
            if (_audio is not null)
            {
                try { await _audio.InvokeVoidAsync("win", _game.NetResult, big); } catch { }
            }
            if (_jsModule is not null)
            {
                await _jsModule.InvokeVoidAsync("cashOut", big);
            }
        }
    }

    private void NewGame()
    {
        _game.Reset();
        _bet = Math.Clamp(_bet, MinBet, Math.Max(MinBet, _balance));
        _mines = Math.Clamp(_mines, 1, _game.MaxMines);
    }

    private string TileClass(int index)
    {
        var tile = _game.Tiles[index];
        return tile switch
        {
            TileState.Gem => _game.WasPlayerDug(index) ? "mf-tile-gem" : "mf-tile-gem mf-tile-ghost",
            TileState.Mine when _game.LastDugIndex == index && _game.Result == MinefieldResult.Busted => "mf-tile-mine mf-tile-boom",
            TileState.Mine => "mf-tile-mine mf-tile-ghost",
            _ => "mf-tile-hidden"
        };
    }

    private static string BannerText(MinefieldResult r) => r switch
    {
        MinefieldResult.Cleared => "Field cleared!",
        MinefieldResult.CashedOut => "Cashed out",
        _ => "Boom!"
    };

    private static string BannerClass(MinefieldResult r) => r switch
    {
        MinefieldResult.Cleared => "mf-banner-jackpot",
        MinefieldResult.CashedOut => "mf-banner-win",
        _ => "mf-banner-lose"
    };

    private static string BannerIcon(MinefieldResult r) => r switch
    {
        MinefieldResult.Cleared => "ph-crown",
        MinefieldResult.CashedOut => "ph-hand-coins",
        _ => "ph-bomb"
    };

    private static string FormatPct(double probability)
    {
        var pct = probability * 100;
        return pct switch
        {
            >= 10 => $"{pct:0}%",
            >= 1 => $"{pct:0.0}%",
            _ => $"{pct:0.00}%"
        };
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var module in new[] { _jsModule, _audio })
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
                // circuit already gone — nothing to clean up
            }
        }
    }
}
