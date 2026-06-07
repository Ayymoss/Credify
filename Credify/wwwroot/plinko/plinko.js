// Credify Plinko — bundled ES module (import("/_content/credify/plinko/plinko.js")). Owns a single canvas:
// it draws the static peg pyramid and animates one or more balls falling down PRE-DECIDED paths. The server
// settles each landing bucket before this runs; the balls just replay the path[]s they're handed, so the
// visuals can never disagree with the payout. Bucket geometry (gap = width/(rows+1)) lines up exactly with the
// rows+1 DOM multiplier cells the page renders directly under the canvas.
//
// The motion is a real (if lightweight) physics sim rather than a scripted tween: each peg-to-peg segment is a
// true projectile arc under constant gravity — a ball is launched up off a peg and accelerates back down, so it
// visibly gains speed (weight) and the bounces are asymmetric like the real thing. Gravity scales with the row
// height (G = k·rowH) so the timing/feel are identical at every board size. We still solve each arc to land
// exactly on the next peg the path demands, so determinism is preserved.
//
// Multi-ball: balls are spawned one after another (staggered) and animate concurrently. Each ball is fully
// independent — there's no inter-ball collision — so a stagger reads as "one after the other" without them
// physically interacting. Self-contained bar a soft, throttled peg-tick from audio.js.

import * as audio from '/_content/credify/audio.js';

// gravity = GRAVITY_K * rowH (px/s²). This constant alone sets the timing (the per-row fall time works out
// independent of board size): lower = slower/heavier. ~130 gives a natural ~0.22s per row drop.
const GRAVITY_K = 130;

let canvas = null, ctx = null;
let rows = 12;
let geom = null;
let raf = 0;
let balls = [];
let lastPegSound = 0;

export function mount(canvasEl) {
    canvas = canvasEl;
    ctx = canvas.getContext('2d');
    window.addEventListener('resize', onResize);
}

function onResize() {
    if (!canvas) return;
    layout();
    drawStatic(null);
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

// (re)build the board for a given row count
export function render(rowCount) {
    rows = Math.max(1, rowCount | 0);
    balls = [];
    layout();
    drawStatic(null);
}

function pegX(rowIndex, j) { return geom.cx + (2 * j - rowIndex) * geom.gap / 2; }
function pegY(rowIndex) { return geom.topPad + rowIndex * geom.rowH; }

function drawBoard() {
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

function drawStatic(ball) {
    if (!ctx || !geom) return;
    drawBoard();
    if (ball) drawBall(ball);
}

function drawBall(b) {
    // motion trail — a few fading after-images sell the speed/weight
    if (b.trail) {
        for (let k = 0; k < b.trail.length; k++) {
            const p = b.trail[k];
            const f = (k + 1) / b.trail.length;
            ctx.beginPath();
            ctx.arc(p.x, p.y, geom.ballR * (0.45 + 0.55 * f), 0, Math.PI * 2);
            ctx.fillStyle = `rgba(252,211,77,${f * 0.22})`;
            ctx.fill();
        }
    }

    const { x, y } = b;

    const grad = ctx.createRadialGradient(x, y, 0, x, y, geom.ballR * 2.6);
    grad.addColorStop(0, 'rgba(252,211,77,0.5)');
    grad.addColorStop(1, 'rgba(252,211,77,0)');
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
    ctx.fillStyle = '#fde68a';
    ctx.fill();
    ctx.lineWidth = 1.5;
    ctx.strokeStyle = 'rgba(180,120,10,0.85)';
    ctx.stroke();
    ctx.beginPath();
    ctx.arc(geom.ballR * 0.42, 0, geom.ballR * 0.22, 0, Math.PI * 2);
    ctx.fillStyle = 'rgba(180,120,10,0.65)';
    ctx.fill();
    ctx.restore();
}

// turn a path into a ball with a solved projectile plan; `spawn` is the ms delay before it's released,
// `mult` is the multiplier of the bucket it lands in (drives the floor-impact sound's pitch)
function buildBall(path, spawn, mult) {
    const g = GRAVITY_K * geom.rowH;
    const off = geom.pegR + geom.ballR * 0.15;
    const floorY = geom.boardH - geom.ballR - 2;

    const pts = [{ x: geom.cx, y: -geom.ballR }];
    let rights = 0;
    for (let i = 0; i < rows; i++) {
        pts.push({ x: geom.cx + (2 * rights - i) * geom.gap / 2, y: pegY(i) - off });
        if (path[i]) rights++;
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
        g, segs, totalT: acc, finalPt, spawn, mult: mult ?? 1,
        started: false, startTime: 0, last: 0, segIndex: 0, squashStart: -1,
        x: pts[0].x, y: pts[0].y, angle: 0, sx: 1, sy: 1, trail: [], landed: false
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

    while (b.segIndex < b.segs.length && elapsed >= b.segs[b.segIndex].t1) {
        b.squashStart = now;
        pegTick(now);
        b.segIndex++;
    }

    if (b.segIndex >= b.segs.length || elapsed >= b.totalT) {
        // settle with a touch of jitter so balls sharing a bucket form a small visible heap
        b.x = b.finalPt.x + (Math.random() - 0.5) * geom.gap * 0.35;
        b.y = b.finalPt.y - Math.random() * geom.ballR * 0.8;
        b.sx = 1; b.sy = 1; b.trail = [];
        b.landed = true;
        try { audio.plinkoLand(b.mult); } catch { /* audio optional */ } // floor impact, scaled by the win
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

// drop one ball (kept for parity; delegates to the multi-ball path)
export function drop(path, mult) {
    return dropMany([path], 0, [mult ?? 1]);
}

// drop several balls, released `staggerMs` apart. `mults` (one per path) sets each landing sound's pitch.
// Resolves once every ball has settled.
export function dropMany(paths, staggerMs, mults) {
    return new Promise(resolve => {
        if (!ctx || !geom || !paths || paths.length === 0) { resolve(); return; }
        if (raf) { cancelAnimationFrame(raf); raf = 0; }

        const stagger = staggerMs ?? 320;
        balls = paths.map((p, i) => buildBall(p, i * stagger, mults?.[i] ?? 1));

        const wallStart = performance.now();
        let done = false;

        function frame(now) {
            drawBoard();

            for (const b of balls) {
                if (b.landed) { drawBall(b); continue; }
                if (now - wallStart < b.spawn) continue; // not released yet
                if (!b.started) { b.started = true; b.startTime = now; b.last = now; }
                updateBall(b, now);
                drawBall(b);
            }

            if (!done && balls.every(b => b.landed)) {
                done = true;
                raf = 0;
                resolve();
                return;
            }

            raf = requestAnimationFrame(frame);
        }

        raf = requestAnimationFrame(frame);
    });
}

export function dispose() {
    if (raf) cancelAnimationFrame(raf);
    raf = 0;
    balls = [];
    window.removeEventListener('resize', onResize);
    canvas = ctx = geom = null;
}
