// Credify Roulette — bundled ES module, loaded by the page via JS interop
// (import("/_content/credify/roulette/roulette.js")). Builds an American double-zero wheel in the DOM and
// drives a continuous spin that eases onto the server-decided number. Self-contained, no dependencies.

// American wheel pocket order (0 and 00 opposite each other).
const WHEEL = [0, 28, 9, 26, 30, 11, 7, 20, 32, 17, 5, 22, 34, 15, 3, 24, 36, 13, 1,
    '00', 27, 10, 25, 29, 12, 8, 19, 31, 18, 6, 21, 33, 16, 4, 23, 35, 14, 2];
const RED = new Set([1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36]);
const N = WHEEL.length;          // 38
const SEG = 360 / N;             // degrees per pocket

function colorOf(n) {
    if (n === 0 || n === '00') return '#0a7d3c';
    return RED.has(n) ? '#b32430' : '#15151a';
}

let inner = null;       // the rotating ring element
let angle = 0;          // current rotation (deg)
let vel = 0;            // deg/sec (free-spin mode)
let mode = 'idle';      // 'idle' | 'spin' | 'land'
let raf = 0;
let last = 0;
// land tween
let landFrom = 0, landTarget = 0, landT0 = 0, landDur = 0;

function easeOutCubic(t) { return 1 - Math.pow(1 - t, 3); }

export function mount(container) {
    container.innerHTML = '';
    container.classList.add('rl-wheel');

    const ring = document.createElement('div');
    ring.className = 'rl-wheel-inner';

    for (let i = 0; i < N; i++) {
        const n = WHEEL[i];
        // each pocket is a full-wheel-size box rotated around the centre; its number chip sits at the top
        // edge, so rotating the box distributes the chips evenly around the rim.
        const cell = document.createElement('div');
        cell.className = 'rl-pocket';
        cell.style.transform = `rotate(${i * SEG}deg)`;
        const label = document.createElement('span');
        label.className = 'rl-pocket-num';
        label.textContent = n;
        label.style.background = colorOf(n);
        cell.appendChild(label);
        ring.appendChild(cell);
    }

    container.appendChild(ring);

    const hub = document.createElement('div');
    hub.className = 'rl-wheel-hub';
    container.appendChild(hub);

    const pointer = document.createElement('div');
    pointer.className = 'rl-wheel-pointer';
    container.appendChild(pointer);

    inner = ring;
    angle = 0;
    mode = 'idle';
    apply();
    ensureLoop();
}

function apply() {
    if (inner) inner.style.transform = `rotate(${angle}deg)`;
}

function ensureLoop() {
    if (raf) return;
    last = performance.now();
    raf = requestAnimationFrame(tick);
}

function tick(now) {
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;

    if (mode === 'spin') {
        angle = (angle + vel * dt) % 360;
        apply();
    } else if (mode === 'land') {
        const t = Math.min(1, (now - landT0) / landDur);
        angle = landFrom + (landTarget - landFrom) * easeOutCubic(t);
        apply();
        if (t >= 1) {
            angle = ((landTarget % 360) + 360) % 360;
            apply();
            mode = 'idle';
        }
    }

    raf = requestAnimationFrame(tick);
}

// Begin a free continuous spin (called when the server enters the spinning phase).
export function startSpin() {
    if (!inner) return;
    mode = 'spin';
    vel = 520; // ~1.4 rev/s
    ensureLoop();
}

// Ease onto the given number (0..36 or "00") and stop, after a few extra turns.
export function landOn(number) {
    if (!inner) return;
    const key = number === '00' || number === 0 ? number : Number(number);
    let idx = WHEEL.indexOf(key);
    if (idx < 0) idx = WHEEL.indexOf(String(number));
    if (idx < 0) idx = 0;

    const base = -idx * SEG;                 // angle that puts pocket idx under the top pointer
    const current = angle;
    const minTurns = 4;
    // smallest target >= current + minTurns*360 that is congruent to base (mod 360)
    let target = base;
    while (target < current + minTurns * 360) target += 360;

    landFrom = current;
    landTarget = target;
    landDur = 3600;
    landT0 = performance.now();
    mode = 'land';
    ensureLoop();
}

export function dispose() {
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    inner = null;
    mode = 'idle';
}
