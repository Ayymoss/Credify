using System;
using System.Threading.Tasks;
using Credify.Games.Cards;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Credify.Components.Shared;

public partial class PlayingCard : IAsyncDisposable
{
    [Inject] public required IJSRuntime JS { get; set; }

    [Parameter, EditorRequired] public Card? Card { get; set; }
    [Parameter] public bool FaceDown { get; set; }
    [Parameter] public int Index { get; set; }

    /// <summary>Glows the card (a winning hand). Purely cosmetic.</summary>
    [Parameter] public bool Highlight { get; set; }

    /// <summary>Renders the card smaller — for layouts that show many at once (e.g. a 5-card video-poker row).</summary>
    [Parameter] public bool Compact { get; set; }

    /// <summary>Play a short deal/flip swish on deal and on reveal. On by default; respects the global
    /// volume/mute and is best-effort (silently no-ops if audio is unavailable). Set false to silence.</summary>
    [Parameter] public bool Sound { get; set; } = true;

    private bool? _prevFaceDown;
    private bool _flipped;          // this card has flipped from face-down → face-up
    private bool _pendingFlipSound;
    private IJSObjectReference? _audio;

    // A fresh face-up card slides in; one that just flipped from face-down plays the 3D reveal.
    private string StateClass =>
        FaceDown || Card is null ? "" : _flipped ? "credify-card-reveal" : "credify-card-deal";

    protected override void OnParametersSet()
    {
        // Detect the face-down → face-up transition so the card flips (and chimes) on reveal.
        if (_prevFaceDown is true && !FaceDown)
        {
            _flipped = true;
            _pendingFlipSound = Sound;
        }

        _prevFaceDown = FaceDown;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!Sound) return;

        try
        {
            if (firstRender && !FaceDown && Card is not null)
            {
                // dealt straight to face-up — swish in time with the staggered slide-in
                await Swish(Index * 90);
            }
            else if (_pendingFlipSound)
            {
                _pendingFlipSound = false;
                await Swish(0);
            }
        }
        catch
        {
            // audio is a nicety, never a failure path
        }
    }

    private async Task Swish(int delayMs)
    {
        _audio ??= await JS.InvokeAsync<IJSObjectReference>("import", "/_content/credify/audio.js");
        if (delayMs > 0) await Task.Delay(delayMs);
        await _audio.InvokeVoidAsync("play", "deal");
    }

    public async ValueTask DisposeAsync()
    {
        if (_audio is not null)
        {
            try { await _audio.DisposeAsync(); } catch { /* circuit may already be gone */ }
        }
    }
}
