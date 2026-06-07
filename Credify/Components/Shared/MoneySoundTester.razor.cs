using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Credify.Components.Shared;

public partial class MoneySoundTester
{
    [Inject] public required IJSRuntime JS { get; set; }

    private IJSObjectReference? _audio;

    private long _amount = 500;
    private bool _autoPlays = true;
    private int _plays = 8;
    private int _perPlay = 50;
    private int _minPlays = 5;
    private int _maxPlays = 60;
    private int _interval = 35;
    private int _startDelay;
    private double _startRate = 1.2;
    private double _endRate = 0.6;
    private double _curve = 1.0;
    private int _curveDuration = 2100;
    private int _achievementAt = 10000; // win >= this plays the achievement jingle (0 = off)
    private string _achievementMode = "start"; // start | threshold | after
    private bool _counter = true;

    // mirrors audio.js playsForAmount() so the displayed length matches what will play
    private int EffectivePlays => _autoPlays
        ? Math.Max(_minPlays, Math.Min(_maxPlays, (int)Math.Round(_amount / (double)Math.Max(1, _perPlay))))
        : _plays;

    private int LengthMs => _startDelay + EffectivePlays * _interval;

    // effective credits represented by each tap (differs from "credits/tap" once min/max clamp kicks in)
    private long MoneyPerTap => EffectivePlays > 0 ? _amount / EffectivePlays : 0;

    // how far into the absolute pitch curve this run reaches (matches the audio.js mapping)
    private int CurveReachPct =>
        _curveDuration <= 0 ? 100 : (int)Math.Round(Math.Min(1.0, (EffectivePlays - 1) * _interval / (double)_curveDuration) * 100);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
        }
    }

    private async Task Play()
    {
        if (_audio is null)
        {
            return;
        }

        try
        {
            await _audio.InvokeVoidAsync("previewMoney", new
            {
                amount = _amount,
                plays = _autoPlays ? (int?)null : _plays,
                perPlay = _perPlay,
                minPlays = _minPlays,
                maxPlays = _maxPlays,
                interval = _interval,
                startDelay = _startDelay,
                startRate = _startRate,
                endRate = _endRate,
                curve = _curve,
                curveDuration = _curveDuration,
                achievementAt = _achievementAt,
                achievementMode = _achievementMode,
                counter = _counter
            });
        }
        catch
        {
            // best-effort
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_audio is not null)
        {
            try
            {
                await _audio.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // circuit already gone
            }
        }
    }
}
