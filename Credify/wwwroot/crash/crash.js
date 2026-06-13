// Credify Crash — bundled ES module (import("/_content/credify/crash/crash.js")). Animates the flight on a
// canvas: a parallax starfield, fixed multiplier gridlines inside a smoothly re-scaling view (so altitude
// visibly grows instead of the curve being pinned to the top-right), a rocket sprite with an exhaust trail,
// and the big multiplier readout + live profit. The client never learns the crash point during flight: it
// renders multiplier = growth^(elapsed/tick) until the server (authoritative) settles the round. After a
// cash-out the server REVEALS the crash point and ghost() keeps flying a dimmed curve to it, showing what
// was left behind. Drives the shared rocket-thrust sound + milestone chimes.
import { rocketStart, rocketSet, rocketStop, milestone } from '../audio.js';

let canvas = null, ctx = null, readout = null, profitEl = null;
let raf = 0;
let startMs = 0, tickSec = 1.2, growth = 1.12;
let stake = 0;
let mode = 'idle'; // 'idle' | 'fly' | 'ghost' | 'done'
let frozen = { m: 1, elapsed: 0, color: '#52525b', crashed: false };
let marker = null;        // { t, m, label } — where the player cashed out
let ghostTarget = 0;      // crash point the ghost curve flies to
let ghostResolve = null;  // resolves ghost()'s promise when the ghost reaches the crash point
let milestonesHit = 0;
const MILESTONES = [2, 5, 10, 20, 50];
const GRID = [1.5, 2, 3, 5, 10, 20, 50, 100, 250, 500, 1000];

// smoothly eased view window: top = multiplier at the top edge, span = seconds across the width
let view = { top: 2, span: 5 };
let stars = [];
let exhaust = [];   // rocket trail particles
let boom = [];      // explosion particles
let lastTick = 0;
let shakeTimer = 0;

const fmt = new Intl.NumberFormat();

export function mount(canvasEl, readoutEl, profitElement) {
    canvas = canvasEl;
    readout = readoutEl;
    profitEl = profitElement ?? null;
    ctx = canvas.getContext('2d');
    resize();
    seedStars();
    staticDraw();
    window.addEventListener('resize', onResize);
}

function resize() {
    if (!canvas) return;
    const dpr = window.devicePixelRatio || 1;
    const rect = canvas.getBoundingClientRect();
    canvas.width = Math.max(1, rect.width * dpr);
    canvas.height = Math.max(1, rect.height * dpr);
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
}

function onResize() {
    resize();
    seedStars();
    if (mode === 'idle') staticDraw();
    else if (mode === 'done') draw(frozen.m, frozen.elapsed, frozen.color, 0, marker !== null);
}

function seedStars() {
    if (!canvas) return;
    const w = canvas.clientWidth, h = canvas.clientHeight;
    stars = [];
    for (let i = 0; i < 70; i++) {
        stars.push({
            x: Math.random() * w,
            y: Math.random() * h,
            depth: 0.25 + Math.random() * 0.75, // parallax layer
            r: 0.5 + Math.random() * 1.3
        });
    }
}

function multAt(elapsed) { return Math.pow(growth, elapsed / tickSec); }
function timeToReach(m) { return m <= 1 ? 0 : Math.log(m) / Math.log(growth) * tickSec; }

function colorFor(m) {
    if (m >= 10) return '#f472b6';
    if (m >= 5) return '#fb923c';
    if (m >= 2) return '#fcd34d';
    return '#6ee7b7';
}

// ── public API ──────────────────────────────────────────────────────────────
export function fly(startEpochMs, tickIntervalSec, growthPerTick, stakeAmount) {
    startMs = startEpochMs;
    tickSec = tickIntervalSec;
    growth = growthPerTick;
    stake = stakeAmount ?? 0;
    mode = 'fly';
    marker = null;
    ghostTarget = 0;
    milestonesHit = 0;
    exhaust = [];
    boom = [];
    view = { top: 2, span: 5 };
    lastTick = performance.now();
    rocketStart();
    ensureLoop();
}

// Cash-out: pin the marker, then keep flying a dimmed ghost curve to the (now-revealed) crash point.
// Resolves when the ghost gets there — the page uses that beat to reveal "it crashed at X×".
export function ghost(cashMult, crashPoint, profitLabel) {
    rocketStop();
    mode = 'ghost';
    marker = { t: timeToReach(cashMult), m: cashMult, label: profitLabel ?? null };
    ghostTarget = Math.max(crashPoint, cashMult);
    if (readout) {
        readout.textContent = cashMult.toFixed(2) + '×';
        readout.style.color = '#6ee7b7';
    }
    if (profitEl) profitEl.textContent = '';
    flash('rgba(110,231,183,0.2)');
    ensureLoop();
    return new Promise(resolve => { ghostResolve = resolve; });
}

export function crash(atMult) {
    rocketStop();
    const elapsed = (Date.now() - startMs) / 1000;
    frozen = { m: atMult, elapsed, color: '#f87171', crashed: true };
    mode = 'done';
    if (readout) { readout.textContent = atMult.toFixed(2) + '× BUST'; readout.style.color = '#f87171'; }
    if (profitEl) profitEl.textContent = '';
    spawnBoom(curveEnd(atMult, elapsed), '#f87171');
    shakeStage();
    flash('rgba(248,113,113,0.25)');
    ensureLoop();
}

export function reset() {
    rocketStop();
    settleGhost();
    mode = 'idle';
    marker = null;
    exhaust = [];
    boom = [];
    if (readout) { readout.textContent = '1.00×'; readout.style.color = '#a1a1aa'; }
    if (profitEl) profitEl.textContent = '';
    staticDraw();
}

export function dispose() {
    rocketStop();
    settleGhost();
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    mode = 'idle';
    clearTimeout(shakeTimer);
    window.removeEventListener('resize', onResize);
    canvas = ctx = readout = profitEl = null;
}

function settleGhost() {
    if (ghostResolve) { ghostResolve(); ghostResolve = null; }
}

// ── animation loop ──────────────────────────────────────────────────────────
function ensureLoop() {
    if (!raf) { lastTick = performance.now(); raf = requestAnimationFrame(loop); }
}

function loop(now) {
    const dt = Math.min(0.05, (now - lastTick) / 1000);
    lastTick = now;

    if (mode === 'fly') {
        const elapsed = (Date.now() - startMs) / 1000;
        const m = multAt(elapsed);
        easeView(m, elapsed);
        draw(m, elapsed, colorFor(m), dt, false);
        rocketSet(m);
        if (readout) { readout.textContent = m.toFixed(2) + '×'; readout.style.color = colorFor(m); }
        if (profitEl && stake > 0) profitEl.textContent = '+' + fmt.format(Math.floor(stake * (m - 1)));

        while (milestonesHit < MILESTONES.length && m >= MILESTONES[milestonesHit]) {
            try { milestone(milestonesHit); } catch { /* audio optional */ }
            milestonesHit++;
            pulseReadout();
        }
    } else if (mode === 'ghost') {
        const elapsed = (Date.now() - startMs) / 1000;
        const m = Math.min(multAt(elapsed), ghostTarget);
        easeView(m, elapsed);
        draw(m, elapsed, '#6ee7b7', dt, true);
        if (m >= ghostTarget) {
            frozen = { m: ghostTarget, elapsed, color: '#6ee7b7', crashed: true };
            mode = 'done';
            spawnBoom(curveEnd(ghostTarget, elapsed), 'rgba(248,113,113,0.9)');
            settleGhost();
        }
    } else {
        // idle/done — keep animating only while particles are alive
        draw(frozen.m, frozen.elapsed, frozen.color, dt, mode === 'done' && marker !== null);
        if (!boom.length && !exhaust.length) { raf = 0; return; }
    }

    raf = requestAnimationFrame(loop);
}

// view follows the rocket with headroom, easing outward so the climb is visible
function easeView(m, elapsed) {
    const targetTop = Math.max(2, m * 1.35);
    const targetSpan = Math.max(5, elapsed * 1.25);
    view.top += (targetTop - view.top) * 0.06;
    view.span += (targetSpan - view.span) * 0.06;
}

function staticDraw() {
    view = { top: 2, span: 5 };
    draw(1, 0, '#52525b', 0, false);
}

// ── rendering ───────────────────────────────────────────────────────────────
const PAD = 12;
const LABEL_W = 34; // right gutter for gridline labels

function xOf(t) { return PAD + (t / view.span) * (canvas.clientWidth - PAD * 2 - LABEL_W); }
function yOf(v) {
    const h = canvas.clientHeight;
    return (h - PAD) - ((v - 1) / (view.top - 1)) * (h - PAD * 2);
}

function curveEnd(m, elapsed) { return { x: xOf(Math.min(elapsed, view.span)), y: yOf(Math.min(m, view.top)) }; }

function draw(m, elapsed, color, dt, ghosted) {
    if (!ctx || !canvas) return;
    const w = canvas.clientWidth, h = canvas.clientHeight;
    ctx.clearRect(0, 0, w, h);

    drawStars(m, dt, w, h);
    drawGrid(w);

    if (mode !== 'idle') {
        drawCurve(m, elapsed, color, ghosted);
    } else {
        // idle baseline
        ctx.strokeStyle = 'rgba(255,255,255,0.06)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(0, h - 0.5);
        ctx.lineTo(w, h - 0.5);
        ctx.stroke();
    }

    drawMarker();
    updateExhaust(dt);
    updateBoom(dt);
}

function drawStars(m, dt, w, h) {
    // drift down-left, faster the higher the multiplier — sells the rocket's velocity
    const level = Math.min(1, Math.log2(Math.max(1, m)) / Math.log2(30));
    const speed = 4 + level * 90;
    ctx.fillStyle = '#fff';
    for (const s of stars) {
        if (mode === 'fly' || mode === 'ghost') {
            s.x -= speed * s.depth * dt;
            s.y += speed * 0.55 * s.depth * dt;
            if (s.x < -2) { s.x = w + 2; s.y = Math.random() * h; }
            if (s.y > h + 2) { s.y = -2; s.x = Math.random() * w; }
        }
        ctx.globalAlpha = 0.12 + s.depth * 0.3;
        ctx.beginPath();
        ctx.arc(s.x, s.y, s.r, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.globalAlpha = 1;
}

function drawGrid(w) {
    ctx.font = '600 10px ui-sans-serif, system-ui, sans-serif';
    ctx.textAlign = 'left';
    for (const g of GRID) {
        if (g >= view.top) break;
        const y = yOf(g);
        ctx.strokeStyle = 'rgba(255,255,255,0.07)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(PAD, y + 0.5);
        ctx.lineTo(w - PAD - LABEL_W + 14, y + 0.5);
        ctx.stroke();
        ctx.fillStyle = 'rgba(255,255,255,0.35)';
        ctx.fillText(g + '×', w - PAD - LABEL_W + 18, y + 3);
    }
}

// the multiplier curve; in ghost mode the stretch past the cash-out marker is drawn dimmed
function drawCurve(m, elapsed, color, ghosted) {
    const h = canvas.clientHeight;
    const span = Math.min(elapsed, view.span);
    const steps = 72;

    const sample = i => {
        const t = elapsed * (i / steps);
        return { x: xOf(Math.min(t, view.span)), y: yOf(Math.min(multAt(t), view.top)), t };
    };

    // area fill under the whole curve
    ctx.beginPath();
    ctx.moveTo(xOf(0), yOf(1));
    for (let i = 1; i <= steps; i++) { const p = sample(i); ctx.lineTo(p.x, p.y); }
    const end = sample(steps);
    ctx.lineTo(end.x, h - PAD);
    ctx.lineTo(xOf(0), h - PAD);
    ctx.closePath();
    const grad = ctx.createLinearGradient(0, 0, 0, h);
    grad.addColorStop(0, colorA(color, 0.3));
    grad.addColorStop(1, colorA(color, 0.02));
    ctx.fillStyle = grad;
    ctx.fill();

    // stroke — split at the marker when ghosting (bright up to the cash-out, dimmed beyond)
    const splitT = ghosted && marker ? marker.t : elapsed;
    strokeSegment(0, Math.min(splitT, elapsed), color, 2.5, steps);
    if (ghosted && marker && elapsed > marker.t) {
        strokeSegment(marker.t, elapsed, 'rgba(148,163,184,0.55)', 2, steps);
    }

    // rocket (or ghost dot) at the head
    const headT = elapsed;
    const head = { x: xOf(Math.min(headT, view.span)), y: yOf(Math.min(multAt(headT), view.top)) };
    const prev = { x: xOf(Math.min(headT - 0.12, view.span)), y: yOf(Math.min(multAt(Math.max(0, headT - 0.12)), view.top)) };
    const angle = Math.atan2(head.y - prev.y, head.x - prev.x);

    if (mode === 'fly') {
        emitExhaust(head, angle);
        drawRocket(head, angle, color);
    } else if (mode === 'ghost') {
        ctx.beginPath();
        ctx.arc(head.x, head.y, 4, 0, Math.PI * 2);
        ctx.fillStyle = 'rgba(148,163,184,0.8)';
        ctx.fill();
    } else if (frozen.crashed) {
        // crash point X
        ctx.strokeStyle = '#f87171';
        ctx.lineWidth = 2.5;
        ctx.beginPath();
        ctx.moveTo(head.x - 5, head.y - 5); ctx.lineTo(head.x + 5, head.y + 5);
        ctx.moveTo(head.x + 5, head.y - 5); ctx.lineTo(head.x - 5, head.y + 5);
        ctx.stroke();
    }
}

function strokeSegment(t0, t1, style, width, steps) {
    if (t1 <= t0) return;
    ctx.beginPath();
    for (let i = 0; i <= steps; i++) {
        const t = t0 + (t1 - t0) * (i / steps);
        const x = xOf(Math.min(t, view.span));
        const y = yOf(Math.min(multAt(t), view.top));
        if (i === 0) ctx.moveTo(x, y); else ctx.lineTo(x, y);
    }
    ctx.strokeStyle = style;
    ctx.lineWidth = width;
    ctx.stroke();
}

// cash-out flag: dashed vertical line + label at the banked multiplier
function drawMarker() {
    if (!marker || mode === 'fly' || mode === 'idle') return;
    const x = xOf(Math.min(marker.t, view.span));
    const y = yOf(Math.min(marker.m, view.top));
    const h = canvas.clientHeight;

    ctx.setLineDash([4, 4]);
    ctx.strokeStyle = 'rgba(110,231,183,0.55)';
    ctx.lineWidth = 1.5;
    ctx.beginPath();
    ctx.moveTo(x, y);
    ctx.lineTo(x, h - PAD);
    ctx.stroke();
    ctx.setLineDash([]);

    ctx.beginPath();
    ctx.arc(x, y, 5, 0, Math.PI * 2);
    ctx.fillStyle = '#6ee7b7';
    ctx.fill();

    const text = marker.label ? `${marker.m.toFixed(2)}× ${marker.label}` : `${marker.m.toFixed(2)}×`;
    ctx.font = '800 11px ui-sans-serif, system-ui, sans-serif';
    ctx.textAlign = 'left';
    ctx.lineWidth = 3;
    ctx.strokeStyle = 'rgba(0,0,0,0.6)';
    ctx.strokeText(text, x + 8, y - 8);
    ctx.fillStyle = '#6ee7b7';
    ctx.fillText(text, x + 8, y - 8);
}

// ── rocket + particles ──────────────────────────────────────────────────────
function drawRocket(p, angle, color) {
    ctx.save();
    ctx.translate(p.x, p.y);
    ctx.rotate(angle);

    // flame (flickers)
    const fl = 7 + Math.random() * 6;
    ctx.beginPath();
    ctx.moveTo(-7, 0);
    ctx.lineTo(-7 - fl, -2.5 + Math.random() * 1.5);
    ctx.lineTo(-7 - fl * 0.55, 0);
    ctx.lineTo(-7 - fl, 2.5 - Math.random() * 1.5);
    ctx.closePath();
    ctx.fillStyle = 'rgba(251,146,60,0.9)';
    ctx.fill();

    // body
    ctx.beginPath();
    ctx.moveTo(9, 0);        // nose
    ctx.quadraticCurveTo(4, -4, -5, -3.2);
    ctx.lineTo(-5, 3.2);
    ctx.quadraticCurveTo(4, 4, 9, 0);
    ctx.closePath();
    ctx.fillStyle = '#e2e8f0';
    ctx.fill();
    ctx.lineWidth = 1;
    ctx.strokeStyle = 'rgba(0,0,0,0.35)';
    ctx.stroke();

    // fins + window
    ctx.fillStyle = color;
    ctx.beginPath(); ctx.moveTo(-3, -3); ctx.lineTo(-8, -6.5); ctx.lineTo(-5, -1.5); ctx.closePath(); ctx.fill();
    ctx.beginPath(); ctx.moveTo(-3, 3); ctx.lineTo(-8, 6.5); ctx.lineTo(-5, 1.5); ctx.closePath(); ctx.fill();
    ctx.beginPath(); ctx.arc(2.5, 0, 1.8, 0, Math.PI * 2); ctx.fillStyle = '#1e293b'; ctx.fill();

    ctx.restore();
}

function emitExhaust(p, angle) {
    for (let i = 0; i < 2; i++) {
        exhaust.push({
            x: p.x - Math.cos(angle) * 9,
            y: p.y - Math.sin(angle) * 9,
            vx: -Math.cos(angle) * (30 + Math.random() * 40) + (Math.random() - 0.5) * 14,
            vy: -Math.sin(angle) * (30 + Math.random() * 40) + (Math.random() - 0.5) * 14,
            life: 0.45 + Math.random() * 0.25,
            age: 0
        });
    }
    if (exhaust.length > 140) exhaust.splice(0, exhaust.length - 140);
}

function updateExhaust(dt) {
    exhaust = exhaust.filter(p => (p.age += dt) < p.life);
    for (const p of exhaust) {
        p.x += p.vx * dt;
        p.y += p.vy * dt;
        const f = 1 - p.age / p.life;
        ctx.globalAlpha = f * 0.5;
        ctx.fillStyle = f > 0.6 ? 'rgba(251,146,60,1)' : 'rgba(148,163,184,1)';
        ctx.beginPath();
        ctx.arc(p.x, p.y, 1 + f * 2, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.globalAlpha = 1;
}

function spawnBoom(p, color) {
    for (let i = 0; i < 34; i++) {
        const a = Math.random() * Math.PI * 2;
        const sp = 50 + Math.random() * 190;
        boom.push({ x: p.x, y: p.y, vx: Math.cos(a) * sp, vy: Math.sin(a) * sp, life: 0.6 + Math.random() * 0.35, age: 0, color });
    }
}

function updateBoom(dt) {
    boom = boom.filter(p => (p.age += dt) < p.life);
    for (const p of boom) {
        p.vy += 220 * dt;
        p.x += p.vx * dt;
        p.y += p.vy * dt;
        const f = 1 - p.age / p.life;
        ctx.globalAlpha = f;
        ctx.fillStyle = p.color;
        ctx.beginPath();
        ctx.arc(p.x, p.y, 1.2 + f * 2, 0, Math.PI * 2);
        ctx.fill();
    }
    ctx.globalAlpha = 1;
}

function shakeStage() {
    const stage = canvas?.closest('.cr-stage');
    if (!stage) return;
    stage.classList.add('cr-shake');
    clearTimeout(shakeTimer);
    shakeTimer = setTimeout(() => stage.classList.remove('cr-shake'), 480);
}

function pulseReadout() {
    if (!readout) return;
    readout.style.transition = 'transform 0.12s ease';
    readout.style.transform = 'translate(-50%, -50%) scale(1.14)';
    setTimeout(() => { if (readout) readout.style.transform = 'translate(-50%, -50%) scale(1)'; }, 130);
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

function colorA(color, a) {
    if (color.startsWith('#')) {
        const n = parseInt(color.slice(1), 16);
        return `rgba(${(n >> 16) & 255},${(n >> 8) & 255},${n & 255},${a})`;
    }
    return color;
}
