using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Credify.Components.Shared;

public partial class CredifyAudioControls
{
    [Inject] public required IJSRuntime JS { get; set; }

    private IJSObjectReference? _audio;
    private double _volume = 0.2;
    private double _preMute = 0.2;

    private int Pct => (int)Math.Round(_volume * 100);
    private string MuteIcon => _volume <= 0 ? "ph ph-speaker-simple-x" : "ph-fill ph-speaker-simple-high";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        _audio = await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
        _volume = await _audio.InvokeAsync<double>("getVolume");
        StateHasChanged();
    }

    private async Task OnInput(ChangeEventArgs e)
    {
        if (!double.TryParse(e.Value?.ToString(), out var pct))
        {
            return;
        }

        _volume = Math.Clamp(pct / 100.0, 0, 1);
        if (_audio is not null)
        {
            await _audio.InvokeVoidAsync("setVolume", _volume);
        }
    }

    private async Task ToggleMute()
    {
        if (_volume > 0)
        {
            _preMute = _volume;
            _volume = 0;
        }
        else
        {
            _volume = _preMute > 0 ? _preMute : 0.2;
        }

        if (_audio is not null)
        {
            await _audio.InvokeVoidAsync("setVolume", _volume);
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
