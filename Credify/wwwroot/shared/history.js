// Credify session-history rail helper (imported by GameHistoryPanel). When the drawer is toggled the
// host content column animates its width (CSS :has push), but the canvas games size to their container
// only on a window 'resize'. So we dispatch a few resize ticks spanning the 0.25s push transition to
// make their canvases re-measure into the new width instead of stretching. No-op for non-canvas pages.
export function nudge() {
    const fire = () => window.dispatchEvent(new Event('resize'));
    fire();
    setTimeout(fire, 160);
    setTimeout(fire, 300);
}
