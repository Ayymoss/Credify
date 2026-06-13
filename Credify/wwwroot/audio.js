// Credify audio — bundled ES module, shared by every game page (import("/_content/credify/audio.js")).
// Game SFX are SYNTHESIZED with the Web Audio API (license-clean). The win effect plays a real short
// sample (sfx/money.ogg) spammed once per "tick" with a descending pitch — a GMod-Tower-style money count
// whose length scales with the amount won. Volume is global, persisted to localStorage, default 20%.

const VOLUME_KEY = 'credify-volume';
// The slider (0..1) scales UNDER this ceiling: slider 100% = MASTER_CEILING real gain. Full-scale audio was
// deafening (even 20% was loud), so the usable range is capped at 0.40 and the default slider sits at 40%
// (≈ 0.16 real gain).
const MASTER_CEILING = 0.4;
const DEFAULT_VOLUME = 0.4;
const MONEY_URL = '/_content/credify/sfx/money.ogg';
const ACHIEVEMENT_URL = '/_content/credify/sfx/AchievementUnlocked.ogg';

let ctx = null;
let master = null;
let volume = readStoredVolume();

// decoded sample cache (url -> { buffer, loading })
const sampleCache = new Map();

function readStoredVolume() {
    const v = parseFloat(localStorage.getItem(VOLUME_KEY));
    return isNaN(v) ? DEFAULT_VOLUME : Math.max(0, Math.min(1, v));
}

// Lazily create the audio graph and resume it (browsers block audio until a user gesture; every play()
// call goes through here so the first click/keypress unlocks it).
function ensure() {
    if (!ctx) {
        const AC = window.AudioContext || window.webkitAudioContext;
        if (!AC) return null;
        ctx = new AC();
        master = ctx.createGain();
        master.gain.value = volume * MASTER_CEILING;
        master.connect(ctx.destination);
        loadSample(MONEY_URL);       // preload on first unlock
        loadSample(ACHIEVEMENT_URL);
    }
    if (ctx.state === 'suspended') ctx.resume();
    return ctx;
}

// fetch + decode a sample once, cached for reuse
function loadSample(url) {
    let entry = sampleCache.get(url);
    if (entry?.buffer) return Promise.resolve(entry.buffer);
    if (!entry) {
        entry = { buffer: null, loading: null };
        sampleCache.set(url, entry);
    }
    if (!entry.loading) {
        entry.loading = fetch(url)
            .then(r => r.arrayBuffer())
            .then(b => ctx.decodeAudioData(b))
            .then(buf => { entry.buffer = buf; return buf; })
            .catch(() => null);
    }
    return entry.loading;
}

export function getVolume() { return volume; }

export function setVolume(v) {
    volume = Math.max(0, Math.min(1, v));
    localStorage.setItem(VOLUME_KEY, String(volume));
    if (master) master.gain.value = volume * MASTER_CEILING;
    ensure(); // a slider drag is a user gesture — unlock here too
}

// ── synth primitives ───────────────────────────────────────────────────────
function tone(freq, dur, type = 'sine', opts = {}) {
    if (!ensure()) return;
    const { gain = 0.3, attack = 0.005, slideTo = null, when = 0 } = opts;
    const t = ctx.currentTime + when;
    const osc = ctx.createOscillator();
    const g = ctx.createGain();
    osc.type = type;
    osc.frequency.setValueAtTime(freq, t);
    if (slideTo) osc.frequency.exponentialRampToValueAtTime(slideTo, t + dur);
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(gain, t + attack);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    osc.connect(g);
    g.connect(master);
    osc.start(t);
    osc.stop(t + dur + 0.02);
}

function noise(dur, opts = {}) {
    if (!ensure()) return;
    const { gain = 0.3, freq = 1000, type = 'lowpass', when = 0 } = opts;
    const t = ctx.currentTime + when;
    const len = Math.max(1, Math.floor(ctx.sampleRate * dur));
    const buf = ctx.createBuffer(1, len, ctx.sampleRate);
    const data = buf.getChannelData(0);
    for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    const src = ctx.createBufferSource();
    src.buffer = buf;
    const filter = ctx.createBiquadFilter();
    filter.type = type;
    filter.frequency.value = freq;
    const g = ctx.createGain();
    g.gain.setValueAtTime(gain, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    src.connect(filter);
    filter.connect(g);
    g.connect(master);
    src.start(t);
    src.stop(t + dur);
}

function clickAt(when, gain = 0.12, freq = 2200) {
    if (!ensure()) return;
    const t = ctx.currentTime + when;
    const o = ctx.createOscillator();
    o.type = 'square';
    o.frequency.value = freq;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0.0001, t);
    g.gain.linearRampToValueAtTime(gain, t + 0.002);
    g.gain.exponentialRampToValueAtTime(0.0001, t + 0.03);
    o.connect(g);
    g.connect(master);
    o.start(t);
    o.stop(t + 0.04);
}

// ── one-shot named effects ─────────────────────────────────────────────────
const EFFECTS = {
    click: () => tone(620, 0.05, 'triangle', { gain: 0.18 }),
    bet: () => { clickAt(0, 0.16, 1800); clickAt(0.04, 0.12, 2400); }, // chip clink
    deal: () => noise(0.13, { gain: 0.18, freq: 1600, type: 'bandpass' }), // card swish
    hit: () => noise(0.12, { gain: 0.18, freq: 1800, type: 'bandpass' }),
    stand: () => tone(300, 0.16, 'sine', { gain: 0.16 }),
    fold: () => { tone(400, 0.22, 'triangle', { gain: 0.14, slideTo: 200 }); },
    push: () => { tone(440, 0.18, 'sine', { gain: 0.12 }); tone(440, 0.18, 'sine', { gain: 0.1, when: 0.12 }); },
    lose: () => { tone(196, 0.45, 'square', { gain: 0.12, slideTo: 130 }); tone(155, 0.45, 'square', { gain: 0.1, when: 0.05 }); },
    cash: () => { clickAt(0, 0.16, 2600); tone(880, 0.18, 'triangle', { gain: 0.14, when: 0.02 }); }
};

export function play(name) {
    const fn = EFFECTS[name];
    if (fn) fn();
}

// rising ping that climbs with the Minefield multiplier (level = gems cleared)
export function gem(level) {
    const freq = 540 + Math.min(level, 24) * 55;
    tone(freq, 0.14, 'triangle', { gain: 0.16 });
    tone(freq * 2, 0.1, 'sine', { gain: 0.05, when: 0.01 });
}

// soft peg tick for Plinko — a short, randomly-pitched click so a run of bounces doesn't sound mechanical
export function peg() {
    clickAt(0, 0.05, 1300 + Math.random() * 700);
}

// Plinko floor impact: a weighty thud whose pitched 'ding' climbs with the bucket's win multiplier. A
// losing bucket (<1×) stays a dull low thump; winning buckets ring brighter/higher the bigger the win, and
// 10×+ adds a sparkle. In multi-ball runs the staggered landings make a satisfying cascade of tones.
export function plinkoLand(multiplier) {
    if (!ensure()) return;
    const m = Math.max(0, multiplier || 0);

    // the physical impact — a short low thump + a filtered noise tap (the ball's weight hitting the floor)
    tone(110, 0.16, 'sine', { gain: 0.20, slideTo: 60 });
    noise(0.05, { gain: 0.12, freq: 900, type: 'lowpass' });

    if (m < 1) {
        // losing bucket — keep it dull and low, no bright chime
        tone(180, 0.12, 'triangle', { gain: 0.08, slideTo: 120 });
        return;
    }

    // winning bucket — a bell whose pitch rises with the multiplier (1× … 100×+ → ~440 … ~1880 Hz)
    const level = Math.min(1, Math.log10(m) / 2); // m=1 → 0, m=100 → 1
    const freq = 440 * Math.pow(2, level * 2.1);
    const gain = 0.14 + level * 0.10;
    tone(freq, 0.22 + level * 0.12, 'triangle', { gain });
    tone(freq * 2, 0.14, 'sine', { gain: gain * 0.4, when: 0.01 }); // shimmer overtone

    // big hits sparkle with a quick rising fifth + a bright tick
    if (m >= 10) {
        tone(freq * 1.5, 0.18, 'sine', { gain: gain * 0.5, when: 0.06 });
        clickAt(0, 0.10, 3200);
    }
}

// Crash multiplier milestone (2× / 5× / 10× / …): a quick rising two-note chime whose pitch climbs with
// the milestone index, layered over the continuous rocket thrust.
export function milestone(level) {
    const f = 660 * Math.pow(1.4, Math.min(level, 5));
    tone(f, 0.12, 'triangle', { gain: 0.13 });
    tone(f * 1.5, 0.16, 'sine', { gain: 0.11, when: 0.07 });
}

// mine detonation
export function bomb() {
    noise(0.55, { gain: 0.5, freq: 1100, type: 'lowpass' });
    tone(90, 0.5, 'sine', { gain: 0.4, slideTo: 40 });
}

// The reel deceleration curve — must match the transition easing in slots.js (`cubic-bezier(0.16,1,0.3,1)`).
// We only need its y(t)/x(t) components (P0=(0,0), P1=(0.16,1), P2=(0.3,1), P3=(1,1)).
function reelBezierY(t) { const mt = 1 - t; return (3 * mt * mt * t + 3 * mt * t * t) * 1 + t * t * t; }
function reelBezierX(t) { const mt = 1 - t; return 3 * mt * mt * t * 0.16 + 3 * mt * t * t * 0.3 + t * t * t; }

// Time fraction (0..1) at which the reel has scrolled `progress` (0..1) of its total distance. Inverting the
// ease-out means equal progress steps map to growing time gaps — i.e. ticks decelerate exactly like the reels.
function reelTimeAtProgress(progress) {
    let lo = 0, hi = 1;
    for (let i = 0; i < 28; i++) {
        const m = (lo + hi) / 2;
        if (reelBezierY(m) < progress) lo = m; else hi = m;
    }
    return reelBezierX((lo + hi) / 2);
}

// slot machine: a lever ka-chunk + per-reel tick trains where each tick = one symbol crossing the
// window, so the ticks decelerate exactly with the reels. The choreography mirrors slots.js: reels
// 0 & 1 spin together (1.15s / 1.55s); reel 2 starts once they've stopped and — when `anticipate`
// is set (the first two reels match) — crawls for a longer beat with a heartbeat underneath before
// landing. Keep these timings in sync with spinReel() in slots.js.
export function reels(anticipate = false) {
    if (!ensure()) return;

    // lever pull / ka-chunk
    clickAt(0, 0.18, 1500);
    tone(190, 0.12, 'square', { gain: 0.14, slideTo: 110 });

    const lanes = [
        { start: 0.04, dur: 1.15 },
        { start: 0.04, dur: 1.55 },
        { start: 1.55, dur: anticipate ? 2.6 : 1.75 }, // final reel runs alone, after the first two
    ];

    for (const lane of lanes) {
        const ticks = 15;
        // one tick per equal slice of scroll distance → time gaps widen as the reel eases to a stop
        for (let k = 1; k < ticks; k++) {
            const progress = k / ticks;
            const when = lane.start + reelTimeAtProgress(progress) * lane.dur;
            clickAt(when, 0.05 * (1 - progress * 0.4), 2500);
        }

        // the reel slamming to its stop
        const stop = lane.start + lane.dur;
        clickAt(stop, 0.16, 1900);
        tone(150, 0.14, 'sine', { gain: 0.2, slideTo: 80, when: stop });
    }

    // anticipation heartbeat under the lone final reel
    if (anticipate) {
        for (let b = 0; b < 5; b++) {
            const when = 1.8 + b * 0.45;
            tone(95, 0.12, 'sine', { gain: 0.17, when });
            tone(72, 0.14, 'sine', { gain: 0.13, when: when + 0.12 });
        }
    }
}

// Single pocket-pass tick for the roulette wheel — fired by roulette.js as each pocket crosses the
// pointer, so the tick rate IS the wheel's real speed (a pre-baked schedule drifts out of sync with the
// animation). Intensity 0..1 follows the wheel speed: quieter and duller as it slows.
// Unlike clickAt (square wave, envelope scheduled at ctx.currentTime exactly), this voice is built for
// rapid rAF-driven firing: a 10ms scheduling lead so the attack ramp is never clamped into the past
// (a clamped ramp skips the fade-in and pops), a soft triangle instead of a square, and an asymptotic
// setTargetAtTime decay with no envelope corners to click on.
export function pocketTick(intensity = 1) {
    if (!ensure()) return;
    const i = Math.max(0, Math.min(1, intensity));
    const t = ctx.currentTime + 0.01;
    const o = ctx.createOscillator();
    o.type = 'triangle';
    o.frequency.value = 1500 + 600 * i;
    const g = ctx.createGain();
    g.gain.setValueAtTime(0, t);
    g.gain.linearRampToValueAtTime(0.035 + 0.095 * i, t + 0.004);
    g.gain.setTargetAtTime(0, t + 0.004, 0.012);
    o.connect(g);
    g.connect(master);
    o.start(t);
    o.stop(t + 0.1); // ~8 time-constants into the decay (≈ -70 dB), silent at the stop
}

// roulette wheel: a tick train whose spacing widens as the wheel "slows" over ~4s
// (legacy — live roulette now uses pocketTick() per real pocket crossing; kept for the /credify tester)
export function spin() {
    if (!ensure()) return;
    const total = 4.0;
    let t = 0;
    while (t < total) {
        const p = t / total;
        clickAt(t, 0.14 - p * 0.09, 2200);
        t += 0.05 + p * p * 0.3; // fast → slow
    }
}

// ball settling into a pocket
export function land() {
    clickAt(0, 0.18, 2000);
    clickAt(0.09, 0.14, 1700);
    tone(180, 0.18, 'sine', { gain: 0.22, slideTo: 90 });
}

// ── Crash rocket: a continuous thrust whose pitch & intensity climb with the multiplier ──────
// A looping filtered-noise hiss (the burn) layered with a low sawtooth (the engine body), driven live by
// crash.js's animation loop. rocketSet(multiplier) ramps the brightness/pitch/loudness up as the rocket
// climbs; rocketStop() fades it out on crash or cash-out.
let rocket = null;

export function rocketStart() {
    if (!ensure()) return;
    if (rocket) rocketStop();

    // 2s of white noise, looped, band-passed = the rushing burn
    const len = Math.floor(ctx.sampleRate * 2);
    const buf = ctx.createBuffer(1, len, ctx.sampleRate);
    const data = buf.getChannelData(0);
    for (let i = 0; i < len; i++) data[i] = Math.random() * 2 - 1;
    const src = ctx.createBufferSource();
    src.buffer = buf;
    src.loop = true;

    const filter = ctx.createBiquadFilter();
    filter.type = 'bandpass';
    filter.frequency.value = 420;
    filter.Q.value = 0.8;

    const osc = ctx.createOscillator(); // low engine rumble under the hiss
    osc.type = 'sawtooth';
    osc.frequency.value = 60;
    const oscGain = ctx.createGain();
    oscGain.gain.value = 0.09;

    const gain = ctx.createGain();
    gain.gain.value = 0.0001;

    src.connect(filter);
    filter.connect(gain);
    osc.connect(oscGain);
    oscGain.connect(gain);
    gain.connect(master);

    const t = ctx.currentTime;
    gain.gain.linearRampToValueAtTime(0.16, t + 0.25); // fade in
    src.start();
    osc.start();

    rocket = { src, osc, filter, gain };
}

export function rocketSet(multiplier) {
    if (!rocket || !ctx) return;
    const m = Math.max(1, multiplier || 1);
    // 0 at 1×, ~1 by 20× (log so it keeps climbing smoothly without ever maxing out)
    const level = Math.min(1, Math.log2(m) / Math.log2(20));
    const t = ctx.currentTime;
    rocket.filter.frequency.setTargetAtTime(420 + level * 2400, t, 0.08);
    rocket.filter.Q.setTargetAtTime(0.8 + level * 4.5, t, 0.1);
    rocket.osc.frequency.setTargetAtTime(60 + level * 200, t, 0.08);
    rocket.gain.gain.setTargetAtTime(0.16 + level * 0.2, t, 0.12);
}

export function rocketStop() {
    if (!rocket || !ctx) return;
    const r = rocket;
    rocket = null;
    const t = ctx.currentTime;
    r.gain.gain.cancelScheduledValues(t);
    r.gain.gain.setValueAtTime(Math.max(0.0001, r.gain.gain.value), t);
    r.gain.gain.linearRampToValueAtTime(0.0001, t + 0.18);
    try { r.src.stop(t + 0.22); } catch { /* already stopped */ }
    try { r.osc.stop(t + 0.22); } catch { /* already stopped */ }
}

// ── the money-count win effect ──────────────────────────────────────────────
// Spams the short money.ogg sample once per "tick", pitch descending GMod-Tower style. The number of ticks
// scales with the amount won. The pitch slide runs over an ABSOLUTE timeframe (curveDuration), measured from
// the first tap — NOT over the run's own tap count. So a short win only samples the high "front" of the curve
// (stays high-pitched), and only long, big-money counts get deep into the slide toward endRate.
// Defaults below are the live tuning; the /credify tester drives the same engine via previewMoney().
const MONEY = {
    interval: 35,       // ms between taps
    minPlays: 5,
    maxPlays: 60,       // caps runtime on huge wins
    perPlay: 50,        // 1 tap per this many credits (lower = each tap is less money, finer low-end)
    startRate: 1.2,     // pitch starts a touch high…
    endRate: 0.6,       // …and slides down toward this
    curve: 1,           // pitch-descent easing exponent (1 = linear, >1 holds high longer, <1 drops fast)
    startDelay: 0,      // ms before the first tap
    curveDuration: 2100, // ms over which the full pitch slide happens (absolute, from the first tap)
    achievementThreshold: 10000, // gross win must be >= this for the jingle (0 = off) — the absolute floor
    achievementMinMultiplier: 2, // …AND the gross payout must be at least this × the stake (relative gate)
    achievementMode: 'start',    // 'start' (with the first tap) | 'threshold' (when the count passes the
                                 // threshold) | 'after' (once the count finishes)
    achievementGap: 90,          // ms gap after the last tap, for 'after' mode
};

// Core engine, fully parameterised. Used by both the real win() path and previewMoney() (the /credify tester).
async function playMoney(o) {
    if (!ensure()) return;

    const amount = Math.max(0, Math.round(o.amount ?? 0));
    const plays = Math.max(1, Math.round(o.plays ?? 1));
    const interval = (o.interval ?? MONEY.interval) / 1000;
    const startDelay = (o.startDelay ?? 0) / 1000;
    const startRate = o.startRate ?? MONEY.startRate;
    const endRate = o.endRate ?? MONEY.endRate;
    const curve = Math.max(0.05, o.curve ?? MONEY.curve);
    const curveDuration = Math.max(0.001, (o.curveDuration ?? MONEY.curveDuration) / 1000); // seconds

    if (o.counter !== false && amount > 0) {
        moneyCounter(amount, startDelay * 1000 + plays * interval * 1000);
    }

    const buf = await loadSample(MONEY_URL);
    if (!buf || !ctx) return;

    const t0 = ctx.currentTime + startDelay;
    for (let i = 0; i < plays; i++) {
        // map this tap's ABSOLUTE time (since the first tap) onto the fixed-duration curve, then ease
        const frac = Math.min(1, (i * interval) / curveDuration);
        const eased = Math.pow(frac, curve);
        const src = ctx.createBufferSource();
        src.buffer = buf;
        src.playbackRate.value = startRate - (startRate - endRate) * eased;
        const g = ctx.createGain();
        g.gain.value = 0.9;
        src.connect(g);
        g.connect(master);
        src.start(t0 + i * interval);
    }

    // big-win reward: an achievement jingle. `amount` is the PROFIT; the gross payout is profit + stake.
    // It only fires on a strong win — the gross must be at least achievementMinMultiplier× the stake AND at
    // least the absolute threshold, so a small bet that happens to multiply (e.g. 50 → 100) stays silent.
    const threshold = o.achievementAt ?? MONEY.achievementThreshold;
    const bet = Math.max(0, o.bet ?? 0);
    const gross = amount + bet;
    const minMult = o.achievementMinMultiplier ?? MONEY.achievementMinMultiplier;
    const meetsMultiplier = bet <= 0 || gross >= minMult * bet;
    if (threshold > 0 && gross >= threshold && meetsMultiplier) {
        const mode = o.achievementMode ?? MONEY.achievementMode;
        let at;
        if (mode === 'threshold') {
            // fire on the tap where the running count crosses the threshold
            const idx = Math.max(0, Math.min(plays - 1, Math.round((threshold / amount) * plays)));
            at = t0 + idx * interval;
        } else if (mode === 'after') {
            at = t0 + plays * interval + MONEY.achievementGap / 1000;
        } else {
            // 'start' — alongside the first tap
            at = t0;
        }

        loadSample(ACHIEVEMENT_URL).then(jingle => {
            if (!jingle || !ctx) return;
            const src = ctx.createBufferSource();
            src.buffer = jingle;
            const g = ctx.createGain();
            g.gain.value = 0.9;
            src.connect(g);
            g.connect(master);
            // guard against the scheduled time having already passed during the async decode
            src.start(Math.max(ctx.currentTime, at));
        });
    }
}

// Tap count from the amount won: one tap per `perPlay` credits, clamped to [minPlays, maxPlays].
function playsForAmount(amount, perPlay, minPlays, maxPlays) {
    return Math.max(minPlays, Math.min(maxPlays, Math.round(amount / Math.max(1, perPlay))));
}

// The real win effect — amount = profit (what flies into the wallet); bet = the stake, used to gate the
// achievement jingle on a strong multiplier. Pass bet = 0 (or omit) where there's no clean stake (e.g. a
// poker pot) to fall back to the absolute threshold only. Tick count derives from the amount.
export function win(amount, bet = 0) {
    const amt = Math.max(0, Math.round(amount));
    if (amt <= 0) return;
    const plays = playsForAmount(amt, MONEY.perPlay, MONEY.minPlays, MONEY.maxPlays);
    playMoney({ amount: amt, bet, plays });
}

// Tester entry point — full control over every knob. opts: { amount, plays, perPlay, minPlays, maxPlays,
// interval, startDelay, startRate, endRate, curve, curveDuration, counter }. If plays is omitted it's
// derived from amount via perPlay/minPlays/maxPlays, exactly like a real win.
export async function previewMoney(opts) {
    const o = opts || {};
    const amount = Math.max(0, Math.round(o.amount ?? 500));
    const perPlay = o.perPlay ?? MONEY.perPlay;
    const minPlays = Math.max(1, Math.round(o.minPlays ?? MONEY.minPlays));
    const maxPlays = Math.max(minPlays, Math.round(o.maxPlays ?? MONEY.maxPlays));
    const plays = o.plays != null ? Math.round(o.plays) : playsForAmount(amount, perPlay, minPlays, maxPlays);
    await playMoney({ ...o, amount, plays });
}

// floating "+amount" that counts up (the money going into the wallet)
function moneyCounter(amount, dur) {
    const el = document.createElement('div');
    el.className = 'credify-money-pop';
    // This element lands on <body>, outside any host-stamped scope marker, so the plugin's scoped
    // CSS can't style it directly (inside @scope, bare selectors match only DESCENDANTS of the
    // scope root). Nest it under a marker wrapper — via the host helper when available (host
    // 2026.06.09+), else inline. Marker value is case-sensitive (bundle id).
    const scope = window.iw4m?.scopedOverlay
        ? window.iw4m.scopedOverlay('Credify')
        : (() => {
            const s = document.createElement('div');
            s.setAttribute('data-iw4m-plugin', 'Credify');
            s.style.display = 'contents';
            document.body.appendChild(s);
            return s;
        })();
    scope.appendChild(el);
    const start = performance.now();
    const ease = t => 1 - Math.pow(1 - t, 3);
    function frame(now) {
        const t = Math.min(1, (now - start) / dur);
        el.textContent = '+' + Math.floor(amount * ease(t)).toLocaleString();
        if (t < 1) {
            requestAnimationFrame(frame);
        } else {
            // landed: stamp the final value (punch + flare + shock ring), then drift out
            el.classList.add('credify-money-pop-done');
            setTimeout(() => el.classList.add('credify-money-pop-out'), 500);
            setTimeout(() => scope.remove(), 1150);
        }
    }
    requestAnimationFrame(frame);
}
