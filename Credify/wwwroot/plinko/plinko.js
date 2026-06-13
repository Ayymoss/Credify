// Credify Plinko — bundled ES module (import("/_content/credify/plinko/plinko.js")). Owns a single canvas:
// it draws the static peg pyramid and animates balls falling down PRE-DECIDED paths. The server settles each
// landing bucket before this runs; the balls just replay the path[]s they're handed, so the visuals can never
// disagree with the payout. Bucket geometry (gap = width/(rows+1)) lines up exactly with the rows+1 DOM
// multiplier cells the page renders directly under the canvas.
//
// The motion is a real (if lightweight) physics sim rather than a scripted tween: each peg-to-peg segment is a
// true projectile arc under constant gravity — a ball is launched up off a peg and accelerates back down, so it
// visibly gains speed (weight) and the bounces are asymmetric like the real thing. Gravity scales with the row
// height (G = k·rowH) so the timing/feel are identical at every board size. We still solve each arc to land
// exactly on the next peg the path demands, so determinism is preserved.
//
// Drops are ADDITIVE: each dropMany() call is its own group of balls animating concurrently with any earlier
// groups still in flight (rapid fire / auto-drop), and resolves its own promise when its balls settle. Extras:
// peg-hit flash rings, floating per-ball payout pops, jackpot sparks + board shake on 10×+, a bucket
// "anticipation" glow over the still-reachable buckets in the final rows, per-risk ball themes, and a
// localStorage-persisted turbo mode.

import * as audio from '/_content/credify/audio.js';

// gravity = GRAVITY_K * rowH (px/s²). This constant alone sets the timing (the per-row fall time works out
// independent of board size): lower = slower/heavier. ~130 gives a natural ~0.22s per row drop.
const GRAVITY_K = 130;
const TURBO_FACTOR = 2.4;          // turbo multiplies gravity (≈ 1.55× faster fall) and halves the stagger
const TURBO_KEY = 'credify-plinko-turbo';

const BALL_LINGER = 1100;          // ms a settled ball stays visible (fading) before it's removed
const FLASH_MS = 240;              // peg-hit ring lifetime
const POP_MS = 1000;               // floating payout text lifetime
const SPARK_MS = 750;              // jackpot particle lifetime
const ANTICIPATE_ROWS = 3;         // glow the reachable buckets once a ball is within this many rows

// per-risk ball/effect palettes (set via setTheme): cool indigo → house gold → hot rose
const THEMES = {
    low:    { fill: '#c7d2fe', stroke: 'rgba(67,56,202,0.9)',  dot: 'rgba(67,56,202,0.7)',  fxRgb: '129,140,248' },
    medium: { fill: '#fde68a', stroke: 'rgba(180,120,10,0.85)', dot: 'rgba(180,120,10,0.65)', fxRgb: '252,211,77' },
    high:   { fill: '#fecdd3', stroke: 'rgba(190,18,60,0.9)',  dot: 'rgba(190,18,60,0.7)',  fxRgb: '251,113,133' }
};

let canvas = null, ctx = null;
let rows = 12;
let geom = null;
let raf = 0;
let groups = [];      // concurrent drop batches; each resolves its own promise when its balls settle
let flashes = [];     // peg-hit rings
let pops = [];        // floating per-ball payout texts
let particles = [];   // jackpot sparks
let lastPegSound = 0;
let shakeTimer = 0;
let theme = THEMES.medium;
let turbo = false;
try { turbo = localStorage.getItem(TURBO_KEY) === '1'; } catch { /* storage unavailable */ }

export function mount(canvasEl) {
    canvas = canvasEl;
    ctx = canvas.getContext('2d');
    window.addEventListener('resize', onResize);
}

export function setTheme(name) { theme = THEMES[name] ?? THEMES.medium; }
export function getTurbo() { return turbo; }
export function setTurbo(v) {
    turbo = !!v;
    try { localStorage.setItem(TURBO_KEY, turbo ? '1' : '0'); } catch { /* storage unavailable */ }
}

function onResize() {
    if (!canvas) return;
    layout();
    if (!raf) drawBoard();
}

// recompute geometry from the canvas's CSS width and the row count, then size the backing store for DPR
function layout() {
    const w = Math.max(1, canvas.clientWidth);
    const gap = w / (rows + 1);
    const topPad = gap * 0.95;
    const rowH = gap * 0.82;
    const boardH = topPad + rows * rowH + gap * 1.05;

    canvas.style.height = `${boardH}px`;

    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.round(w * dpr));
    canvas.height = Math.max(1, Math.round(boardH * dpr));
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

    geom = {
        w, gap, cx: w / 2, topPad, rowH, boardH,
        pegR: Math.max(2, Math.min(5, gap * 0.085)),
        ballR: Math.max(4, Math.min(9, gap * 0.18))
    };
}

// (re)build the board for a given row count — abandons anything still in flight
export function render(rowCount) {
    rows = Math.max(1, rowCount | 0);
    for (const g of groups) if (!g.resolved) { g.resolved = true; g.resolve(); }
    groups = [];
    flashes = [];
    pops = [];
    particles = [];
    layout();
    clearAnticipation();
    drawBoard();
}

function pegX(rowIndex, j) { return geom.cx + (2 * j - rowIndex) * geom.gap / 2; }
function pegY(rowIndex) { return geom.topPad + rowIndex * geom.rowH; }

function drawBoard() {
    if (!ctx || !geom) return;
    ctx.clearRect(0, 0, geom.w, geom.boardH);
    for (let i = 0; i < rows; i++) {
        const y = pegY(i);
        for (let j = 0; j <= i; j++) {
            ctx.beginPath();
            ctx.arc(pegX(i, j), y, geom.pegR, 0, Math.PI * 2);
            ctx.fillStyle = 'rgba(226,232,240,0.85)';
            ctx.fill();
        }
    }
}

function drawBall(b, now) {
    // settled balls hold briefly, then fade out and stop being drawn
    let alpha = 1;
    if (b.landed) {
        alpha = Math.max(0, 1 - Math.max(0, (now - b.landedAt - 350) / (BALL_LINGER - 350)));
        if (alpha <= 0) return;
    }
    ctx.globalAlpha = alpha;

    // motion trail — a few fading after-images sell the speed/weight
    if (b.trail) {
        for (let k = 0; k < b.trail.length; k++) {
            const p = b.trail[k];
            const f = (k + 1) / b.trail.length;
            ctx.beginPath();
            ctx.arc(p.x, p.y, geom.ballR * (0.45 + 0.55 * f), 0, Math.PI * 2);
            ctx.fillStyle = `rgba(${theme.fxRgb},${f * 0.22})`;
            ctx.fill();
        }
    }

    const { x, y } = b;

    const grad = ctx.createRadialGradient(x, y, 0, x, y, geom.ballR * 2.6);
    grad.addColorStop(0, `rgba(${theme.fxRgb},0.5)`);
    grad.addColorStop(1, `rgba(${theme.fxRgb},0)`);
    ctx.fillStyle = grad;
    ctx.beginPath();
    ctx.arc(x, y, geom.ballR * 2.6, 0, Math.PI * 2);
    ctx.fill();

    ctx.save();
    ctx.translate(x, y);
    ctx.rotate(b.angle || 0);
    ctx.scale(b.sx || 1, b.sy || 1);
    ctx.beginPath();
    ctx.arc(0, 0, geom.ballR, 0, Math.PI * 2);
    ctx.fillStyle = theme.fill;
    ctx.fill();
    ctx.lineWidth = 1.5;
    ctx.strokeStyle = theme.stroke;
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(geom.ballR * 0.42, 0, geom.ballR * 0.22, 0, Math.PI * 2);
    ctx.fillStyle = theme.dot;
    ctx.fill();
    ctx.restore();

    ctx.globalAlpha = 1;
}

// turn a path into a ball with a solved projectile plan; `spawn` is the ms delay before it's released,
// `mult` drives the floor-impact sound's pitch and the jackpot theatre, `label` is the floating payout pop
function buildBall(path, spawn, mult, label) {
    const g = GRAVITY_K * (turbo ? TURBO_FACTOR : 1) * geom.rowH;
    const off = geom.pegR + geom.ballR * 0.15;
    const floorY = geom.boardH - geom.ballR - 2;

    const pts = [{ x: geom.cx, y: -geom.ballR }];
    const prefixRights = [0]; // rights decided after k rows — for the bucket anticipation glow
    let rights = 0;
    for (let i = 0; i < rows; i++) {
        pts.push({ x: geom.cx + (2 * rights - i) * geom.gap / 2, y: pegY(i) - off });
        if (path[i]) rights++;
        prefixRights.push(rights);
    }
    pts.push({ x: geom.cx + (2 * rights - rows) * geom.gap / 2, y: floorY });

    const segs = [];
    for (let s = 0; s < pts.length - 1; s++) {
        const a = pts[s], b = pts[s + 1];
        const dy = b.y - a.y;

        let hop;
        if (s === 0) hop = 0;
        else if (s === pts.length - 2) hop = geom.rowH * 0.12;
        else hop = Math.max(geom.rowH * 0.18, geom.rowH * (0.55 - 0.22 * (s / rows)));

        const vy0 = hop > 0 ? -Math.sqrt(2 * g * hop) : 0;
        const t = (-vy0 + Math.sqrt(vy0 * vy0 + 2 * g * dy)) / g;
        segs.push({ a, vy0, vx: (b.x - a.x) / t, t });
    }

    const finalPt = pts[pts.length - 1];
    for (const hop of [geom.rowH * 0.22, geom.rowH * 0.08]) {
        const vy0 = -Math.sqrt(2 * g * hop);
        segs.push({ a: finalPt, vy0, vx: 0, t: -2 * vy0 / g });
    }

    let acc = 0;
    for (const sg of segs) { sg.t0 = acc; acc += sg.t; sg.t1 = acc; }

    return {
        g, segs, totalT: acc, finalPt, spawn, mult: mult ?? 1, label, prefixRights,
        started: false, startTime: 0, last: 0, segIndex: 0, squashStart: -1,
        x: pts[0].x, y: pts[0].y, angle: 0, sx: 1, sy: 1, trail: [], landed: false, landedAt: 0
    };
}

function pegTick(now) {
    if (now - lastPegSound > 26) { // throttle so a shower of balls doesn't machine-gun the ticks
        lastPegSound = now;
        try { audio.peg(); } catch { /* audio optional */ }
    }
}

function updateBall(b, now) {
    const elapsed = (now - b.startTime) / 1000;

    let crossed = false;
    while (b.segIndex < b.segs.length && elapsed >= b.segs[b.segIndex].t1) {
        b.squashStart = now;
        pegTick(now);
        b.segIndex++;
        crossed = true;
        // flash the peg we just bounced off (segments 1..rows start at a peg; 0 is the drop-in)
        if (b.segIndex >= 1 && b.segIndex <= rows) {
            const a = b.segs[b.segIndex].a;
            flashes.push({ x: a.x, y: a.y, t0: now });
        }
    }
    if (crossed) refreshAnticipation();

    if (b.segIndex >= b.segs.length || elapsed >= b.totalT) {
        // settle with a touch of jitter so balls sharing a bucket form a small visible heap
        b.x = b.finalPt.x + (Math.random() - 0.5) * geom.gap * 0.35;
        b.y = b.finalPt.y - Math.random() * geom.ballR * 0.8;
        b.sx = 1; b.sy = 1; b.trail = [];
        b.landed = true;
        b.landedAt = now;

        if (b.label) {
            pops.push({ x: b.finalPt.x, y: b.finalPt.y - geom.rowH * 0.5, text: b.label.text, win: b.label.win, t0: now });
        }
        if (b.mult >= 10) {
            spawnSparks(b.finalPt.x, b.finalPt.y, now);
            shakeBoard();
        }

        try { audio.plinkoLand(b.mult); } catch { /* audio optional */ } // floor impact, scaled by the win
        refreshAnticipation();
        return;
    }

    const sg = b.segs[b.segIndex];
    const tl = elapsed - sg.t0;
    const x = sg.a.x + sg.vx * tl;
    const y = sg.a.y + sg.vy0 * tl + 0.5 * b.g * tl * tl;

    const dt = (now - b.last) / 1000;
    b.last = now;
    b.angle += sg.vx * dt * 0.03;

    if (b.squashStart >= 0) {
        const f = Math.max(0, 1 - (now - b.squashStart) / 90);
        b.sx = 1 + 0.4 * f;
        b.sy = 1 - 0.32 * f;
    }

    b.trail.push({ x, y });
    if (b.trail.length > 7) b.trail.shift();
    b.x = x;
    b.y = y;
}

// ── effects ─────────────────────────────────────────────────────────────────
function drawFlashes(now) {
    flashes = flashes.filter(f => now - f.t0 < FLASH_MS);
    for (const f of flashes) {
        const t = (now - f.t0) / FLASH_MS;
        ctx.beginPath();
        ctx.arc(f.x, f.y, geom.pegR * (1 + t * 3.2), 0, Math.PI * 2);
        ctx.strokeStyle = `rgba(${theme.fxRgb},${(1 - t) * 0.7})`;
        ctx.lineWidth = 1.6;
        ctx.stroke();
    }
}

function drawPops(now) {
    pops = pops.filter(p => now - p.t0 < POP_MS);
    for (const p of pops) {
        const t = (now - p.t0) / POP_MS;
        const y = p.y - t * geom.rowH * 0.9;
        ctx.font = `800 ${Math.max(11, geom.gap * 0.34)}px ui-sans-serif, system-ui, sans-serif`;
        ctx.textAlign = 'center';
        ctx.globalAlpha = 1 - t * t;
        ctx.lineWidth = 3;
        ctx.strokeStyle = 'rgba(0,0,0,0.55)';
        ctx.strokeText(p.text, p.x, y);
        ctx.fillStyle = p.win ? '#6ee7b7' : '#fda4af';
        ctx.fillText(p.text, p.x, y);
        ctx.globalAlpha = 1;
    }
}

function spawnSparks(x, y, now) {
    for (let i = 0; i < 26; i++) {
        particles.push({
            x, y,
            vx: (Math.random() - 0.5) * geom.rowH * 9,
            vy: -(0.4 + Math.random()) * geom.rowH * 7,
            t0: now, last: now
        });
    }
}

function drawParticles(now) {
    particles = particles.filter(p => now - p.t0 < SPARK_MS);
    const g = GRAVITY_K * geom.rowH;
    for (const p of particles) {
        const dt = Math.min(0.05, (now - p.last) / 1000);
        p.last = now;
        p.vy += g * dt;
        p.x += p.vx * dt;
        p.y += p.vy * dt;
        const t = (now - p.t0) / SPARK_MS;
        ctx.globalAlpha = 1 - t;
        ctx.fillStyle = `rgb(${theme.fxRgb})`;
        ctx.beginPath();
        ctx.arc(p.x, p.y, 1.4 + (1 - t) * 1.2, 0, Math.PI * 2);
        ctx.fill();
        ctx.globalAlpha = 1;
    }
}

// brief CSS shake on the board frame for a 10×+ landing
function shakeBoard() {
    const board = canvas?.closest('.pl-board');
    if (!board) return;
    board.classList.add('pl-shake');
    clearTimeout(shakeTimer);
    shakeTimer = setTimeout(() => board.classList.remove('pl-shake'), 480);
}

// ── bucket anticipation: glow the buckets still reachable from each airborne ball's position ──
function bucketStrip() { return canvas?.parentElement?.querySelector('.pl-buckets') ?? null; }

function refreshAnticipation() {
    const strip = bucketStrip();
    if (!strip) return;
    const mark = new Array(strip.children.length).fill(false);
    for (const grp of groups) {
        for (const b of grp.balls) {
            if (b.landed || !b.started) continue;
            const k = Math.min(b.segIndex, rows);
            const remaining = rows - k;
            if (k < 1 || remaining > ANTICIPATE_ROWS) continue;
            const r = b.prefixRights[k];
            for (let i = r; i <= r + remaining && i < mark.length; i++) mark[i] = true;
        }
    }
    for (let i = 0; i < strip.children.length; i++) {
        strip.children[i].classList.toggle('pl-bucket-maybe', mark[i]);
    }
}

function clearAnticipation() {
    const strip = bucketStrip();
    if (!strip) return;
    for (const cell of strip.children) cell.classList.remove('pl-bucket-maybe');
}

// ── animation loop ──────────────────────────────────────────────────────────
function ensureLoop() {
    if (!raf) raf = requestAnimationFrame(frame);
}

function frame(now) {
    drawBoard();
    drawFlashes(now);

    for (const grp of groups) {
        for (const b of grp.balls) {
            if (b.landed) { drawBall(b, now); continue; }
            if (now - grp.wallStart < b.spawn) continue; // not released yet
            if (!b.started) { b.started = true; b.startTime = now; b.last = now; }
            updateBall(b, now);
            drawBall(b, now);
        }
        if (!grp.resolved && grp.balls.every(b => b.landed)) {
            grp.resolved = true;
            grp.resolve();
        }
    }
    // a group lingers (balls fading in their buckets) a moment after it resolves
    groups = groups.filter(grp => !grp.resolved || grp.balls.some(b => now - b.landedAt < BALL_LINGER));

    drawParticles(now);
    drawPops(now);

    if (groups.length || flashes.length || pops.length || particles.length) {
        raf = requestAnimationFrame(frame);
    } else {
        raf = 0;
        clearAnticipation();
        drawBoard();
    }
}

// drop one ball (kept for parity; delegates to the multi-ball path)
export function drop(path, mult) {
    return dropMany([path], 0, [mult ?? 1], null);
}

// Drop several balls, released `staggerMs` apart (halved in turbo). `mults` (one per path) drives each
// landing sound's pitch + jackpot theatre; `labels` ({text, win} per path) are the floating payout pops.
// ADDITIVE: coexists with earlier in-flight drops. Resolves once every ball in THIS batch has settled.
export function dropMany(paths, staggerMs, mults, labels) {
    return new Promise(resolve => {
        if (!ctx || !geom || !paths || paths.length === 0) { resolve(); return; }

        const stagger = (staggerMs ?? 320) * (turbo ? 0.5 : 1);
        groups.push({
            wallStart: performance.now(),
            resolved: false,
            resolve,
            balls: paths.map((p, i) => buildBall(p, i * stagger, mults?.[i] ?? 1, labels?.[i]))
        });
        ensureLoop();
    });
}

export function dispose() {
    for (const g of groups) if (!g.resolved) { g.resolved = true; g.resolve(); }
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    groups = [];
    flashes = [];
    pops = [];
    particles = [];
    clearTimeout(shakeTimer);
    window.removeEventListener('resize', onResize);
    canvas = ctx = geom = null;
}
