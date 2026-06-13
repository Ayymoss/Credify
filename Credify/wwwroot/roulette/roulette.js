// Credify Roulette — bundled ES module, loaded by the page via JS interop
// (import("/_content/credify/roulette/roulette.js")). Builds an American double-zero wheel in the DOM and
// drives a continuous spin that eases onto the server-decided number.

// Same module instance the page's audio interop uses (same URL), so volume settings are shared.
import { pocketTick } from '../audio.js';

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
let landResolve = null; // resolves the promise landOn() returned, when the wheel stops
let lastPocket = -1;    // pocket index currently under the pointer, for per-pocket sound ticks

function pocketAt(a) { return Math.floor((((a % 360) + 360) % 360) / SEG); }

// a click each time a pocket boundary crosses the pointer — the tick rate is the wheel's true speed
function emitPocketTick(intensity) {
    const p = pocketAt(angle);
    if (p !== lastPocket) {
        lastPocket = p;
        try { pocketTick(intensity); } catch { /* audio locked/unavailable */ }
    }
}

// diagnostics for the wheel animation — paste the console output back when reporting glitches
const TAG = '[credify-roulette]';
const t0 = performance.now();
const log = (...args) => console.log(TAG, `+${((performance.now() - t0) / 1000).toFixed(2)}s`, ...args);
let warnedDetached = false;
let lastProgressLog = 0;
log('module loaded — anim v3 (aliasing-safe spin speed)');

// Host-side events (phase changes, sound emits, outcome settles) are funnelled in here by the Blazor
// component so everything shares one timeline in the console.
export function note(evt, data) {
    log(`host:${evt}`, data ?? '');
}

// Spin speed is capped by TEMPORAL ALIASING, not taste: 38 pockets = 9.47° pitch, and at 60Hz the eye
// strobes the pattern (wagon-wheel effect) whenever the per-frame step nears the pitch. The old
// 520 deg/s moved 8.67°/frame — almost exactly one pocket — so the wheel looked frozen/backwards at any
// real speed. Keep the step under half a pocket (< ~284 deg/s at 60Hz) so motion always reads true.
const SPIN_VEL = 220; // deg/s ≈ 3.7°/frame at 60Hz

// Quadratic ease-out: initial slope is exactly 2×(distance/duration), which landOn() exploits to hand
// off from the free spin with no visible speed jump (constant deceleration from spin speed to rest).
function easeOutQuad(t) { return t * (2 - t); }

export function mount(container) {
    container.innerHTML = '';
    container.classList.add('rl-wheel');

    const ring = document.createElement('div');
    ring.className = 'rl-wheel-inner';
    // The host theme puts `transition: all` on elements. A transition on transform fights the rAF loop
    // (re-smooths every frame, hiding the deceleration) and animates the final mod-360 angle
    // normalisation as a fast backwards spin. The ring must follow the JS animation verbatim.
    ring.style.transition = 'none';

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
    warnedDetached = false;
    lastPocket = pocketAt(angle);
    apply();
    ensureLoop();

    // if the host or another stylesheet puts a transition/animation on the ring, it fights the rAF loop
    const cs = getComputedStyle(ring);
    log('mount', { transition: cs.transition, animation: cs.animationName, willChange: cs.willChange });
}

function apply() {
    if (!inner) return;
    if (!inner.isConnected && !warnedDetached) {
        warnedDetached = true;
        console.warn(TAG, 'ring element is DETACHED from the DOM — Blazor re-rendered the wheel container; the animation is driving a dead node');
    }
    inner.style.transform = `rotate(${angle}deg)`;
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
        emitPocketTick(1);
        if (now - lastProgressLog > 1000) {
            lastProgressLog = now;
            log('spin progress', { angle: angle.toFixed(1), degPerSec: vel });
        }
    } else if (mode === 'land') {
        const t = Math.min(1, (now - landT0) / landDur);
        angle = landFrom + (landTarget - landFrom) * easeOutQuad(t);
        apply();
        const speed = (1 - t) * 2 * (landTarget - landFrom) / (landDur / 1000);
        emitPocketTick(speed / SPIN_VEL);
        if (now - lastProgressLog > 600) {
            lastProgressLog = now;
            log('land progress', { t: t.toFixed(3), angle: angle.toFixed(1), degPerSec: ((1 - t) * 2 * (landTarget - landFrom) / (landDur / 1000)).toFixed(0) });
        }
        if (t >= 1) {
            angle = ((landTarget % 360) + 360) % 360;
            apply();
            mode = 'idle';
            vel = 0;
            log('land complete', { finalAngle: angle.toFixed(1) });
            if (landResolve) { landResolve(); landResolve = null; }
        }
    }

    raf = requestAnimationFrame(tick);
}

// Begin a free continuous spin (called when the server enters the spinning phase).
export function startSpin() {
    if (!inner) return;
    log('startSpin', { prevMode: mode, angle: angle.toFixed(1) });
    if (mode === 'land') console.warn(TAG, 'startSpin interrupted an in-flight landing tween');
    mode = 'spin';
    vel = SPIN_VEL;
    lastPocket = pocketAt(angle);
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

    // Decelerate FROM the current spin speed: with the quadratic ease the tween's initial velocity is
    // 2*distance/duration, so duration = 2*distance/speed starts at exactly the free-spin rate and slows
    // linearly to rest on the pocket. Distance is the smallest angle >= speed*desired/2 congruent to the
    // pocket (mod 360), keeping the landing near the desired length without ever jumping in speed.
    const speed = mode === 'spin' && vel > 0 ? vel : SPIN_VEL;
    const desiredDur = 3.2;                  // seconds; actual duration runs up to ~2x this
    let target = base;
    while (target < current + speed * desiredDur / 2) target += 360;

    landFrom = current;
    landTarget = target;
    landDur = 2 * (target - current) / speed * 1000;
    landT0 = performance.now();
    if (mode === 'land') console.warn(TAG, 'landOn called while already landing — tween restarted');
    log('landOn', {
        number, idx, prevMode: mode, from: current.toFixed(1), target: target.toFixed(1),
        distanceDeg: (target - current).toFixed(1), speed: speed.toFixed(0), durMs: landDur.toFixed(0)
    });
    mode = 'land';
    lastProgressLog = 0;
    ensureLoop();

    // resolves when the wheel stops — the host holds result sounds/UI back until then
    if (landResolve) landResolve(); // a superseded landing counts as finished
    return new Promise(resolve => { landResolve = resolve; });
}

// Snap the wheel straight onto a number with no animation — initial page-load sync to a result that
// was already spun before this client arrived.
export function setTo(number) {
    if (!inner) return;
    const key = number === '00' || number === 0 ? number : Number(number);
    let idx = WHEEL.indexOf(key);
    if (idx < 0) idx = WHEEL.indexOf(String(number));
    if (idx < 0) idx = 0;
    angle = (((-idx * SEG) % 360) + 360) % 360;
    mode = 'idle';
    vel = 0;
    lastPocket = pocketAt(angle);
    apply();
    log('setTo', { number, angle: angle.toFixed(1) });
}

export function dispose() {
    log('dispose', { mode, angle: angle.toFixed(1) });
    if (landResolve) { landResolve(); landResolve = null; } // never leave the host awaiting a dead wheel
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    inner = null;
    mode = 'idle';
}
