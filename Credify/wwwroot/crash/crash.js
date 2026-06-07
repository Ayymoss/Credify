// Credify Crash — bundled ES module (import("/_content/credify/crash/crash.js")). Animates the rising
// multiplier curve on a canvas and the big multiplier readout, smoothly via requestAnimationFrame. The
// client never learns the crash point: it just renders multiplier = growth^(elapsed/tick) until the server
// (authoritative) tells it the round crashed or was cashed out. Self-contained, no dependencies.

let canvas = null, ctx = null, readout = null;
let raf = 0;
let startMs = 0, tickSec = 1.2, growth = 1.12;
let mode = 'idle'; // 'idle' | 'fly' | 'done'
let frozenMult = 1, frozenColor = '#fcd34d';

export function mount(canvasEl, readoutEl) {
    canvas = canvasEl;
    readout = readoutEl;
    ctx = canvas.getContext('2d');
    resize();
    draw(1, 0, '#52525b');
}

function resize() {
    if (!canvas) return;
    const dpr = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    canvas.width = Math.max(1, rect.width * dpr);
    canvas.height = Math.max(1, rect.height * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
}

function multAt(elapsed) { return Math.pow(growth, elapsed / tickSec); }

function colorFor(m) {
    if (m >= 10) return '#f472b6';
    if (m >= 5) return '#fb923c';
    if (m >= 2) return '#fcd34d';
    return '#6ee7b7';
}

export function fly(startEpochMs, tickIntervalSec, growthPerTick) {
    startMs = startEpochMs;
    tickSec = tickIntervalSec;
    growth = growthPerTick;
    mode = 'fly';
    if (!raf) raf = requestAnimationFrame(loop);
}

function loop() {
    if (mode === 'fly') {
        const elapsed = (Date.now() - startMs) / 1000;
        const m = multAt(elapsed);
        render(m, elapsed, colorFor(m));
        raf = requestAnimationFrame(loop);
    } else {
        raf = 0;
    }
}

function render(m, elapsed, color) {
    if (readout) {
        readout.textContent = m.toFixed(2) + '×';
        readout.style.color = color;
    }
    draw(m, elapsed, color);
}

// draw the multiplier curve, normalised so the current point sits near the top-right
function draw(m, elapsed, color) {
    if (!ctx || !canvas) return;
    const w = canvas.clientWidth, h = canvas.clientHeight;
    ctx.clearRect(0, 0, w, h);

    // grid baseline
    ctx.strokeStyle = 'rgba(255,255,255,0.06)';
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(0, h - 0.5);
    ctx.lineTo(w, h - 0.5);
    ctx.stroke();

    const span = Math.max(elapsed, 0.0001);
    const top = Math.max(m, 1.05);
    const pad = 10;

    const xOf = t => pad + (t / span) * (w - pad * 2);
    const yOf = v => (h - pad) - ((v - 1) / (top - 1)) * (h - pad * 2);

    // area + line
    ctx.beginPath();
    ctx.moveTo(xOf(0), yOf(1));
    const steps = 64;
    for (let i = 1; i <= steps; i++) {
        const t = span * (i / steps);
        ctx.lineTo(xOf(t), yOf(multAt(t)));
    }
    const lastX = xOf(span), lastY = yOf(m);

    // fill under curve
    ctx.lineTo(lastX, h - pad);
    ctx.lineTo(xOf(0), h - pad);
    ctx.closePath();
    const grad = ctx.createLinearGradient(0, 0, 0, h);
    grad.addColorStop(0, hexA(color, 0.35));
    grad.addColorStop(1, hexA(color, 0.02));
    ctx.fillStyle = grad;
    ctx.fill();

    // stroke line
    ctx.beginPath();
    ctx.moveTo(xOf(0), yOf(1));
    for (let i = 1; i <= steps; i++) {
        const t = span * (i / steps);
        ctx.lineTo(xOf(t), yOf(multAt(t)));
    }
    ctx.strokeStyle = color;
    ctx.lineWidth = 2.5;
    ctx.stroke();

    // rocket dot
    ctx.beginPath();
    ctx.arc(lastX, lastY, 5, 0, Math.PI * 2);
    ctx.fillStyle = color;
    ctx.fill();
    ctx.beginPath();
    ctx.arc(lastX, lastY, 9, 0, Math.PI * 2);
    ctx.strokeStyle = hexA(color, 0.4);
    ctx.lineWidth = 2;
    ctx.stroke();
}

function hexA(hex, a) {
    const n = parseInt(hex.slice(1), 16);
    return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${a})`;
}

export function crash(atMult) {
    mode = 'done';
    frozenMult = atMult;
    frozenColor = '#f87171';
    if (readout) { readout.textContent = atMult.toFixed(2) + '× BUST'; readout.style.color = frozenColor; }
    const elapsed = (Date.now() - startMs) / 1000;
    draw(atMult, elapsed, frozenColor);
    flash('rgba(248,113,113,0.25)');
}

export function cashOut(atMult) {
    mode = 'done';
    frozenMult = atMult;
    frozenColor = '#6ee7b7';
    if (readout) { readout.textContent = atMult.toFixed(2) + '×'; readout.style.color = frozenColor; }
    const elapsed = (Date.now() - startMs) / 1000;
    draw(atMult, elapsed, frozenColor);
    flash('rgba(110,231,183,0.2)');
}

function flash(color) {
    if (!canvas) return;
    const o = document.createElement('div');
    o.style.cssText =
        `position:absolute;inset:0;pointer-events:none;border-radius:0.9rem;background:${color};opacity:1;transition:opacity .6s ease;`;
    canvas.parentElement?.appendChild(o);
    requestAnimationFrame(() => { o.style.opacity = '0'; });
    setTimeout(() => o.remove(), 650);
}

export function reset() {
    mode = 'idle';
    if (readout) { readout.textContent = '1.00×'; readout.style.color = '#a1a1aa'; }
    draw(1, 0, '#52525b');
}

export function dispose() {
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    mode = 'idle';
    canvas = ctx = readout = null;
}
