// Credify audio — bundled ES module, shared by every game page (import("/_content/credify/audio.js")).
// Game SFX are SYNTHESIZED with the Web Audio API (license-clean). The win effect plays a real short
// sample (sfx/money.ogg) spammed once per "tick" with a descending pitch — a GMod-Tower-style money count
// whose length scales with the amount won. Volume is global, persisted to localStorage, default 20%.

const VOLUME_KEY = 'credify-volume';
const DEFAULT_VOLUME = 0.2;
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
        master.gain.value = volume;
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
    if (master) master.gain.value = volume;
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

// mine detonation
export function bomb() {
    noise(0.55, { gain: 0.5, freq: 1100, type: 'lowpass' });
    tone(90, 0.5, 'sine', { gain: 0.4, slideTo: 40 });
}

// roulette wheel: a tick train whose spacing widens as the wheel "slows" over ~4s
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
    achievementThreshold: 10000, // win >= this plays the achievement jingle (0 = off)
    achievementMode: 'start',    // 'start' (with the first tap) | 'threshold' (when the count passes the
                                 // threshold) | 'after' (once the count finishes)
    achievementGap: 90,          // ms gap after the last tap, for 'after' mode
};

// Core engine, fully parameterised. Used by both win() (live) and previewMoney() (tester).
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

    // big-win reward: an achievement jingle, timed by mode
    const threshold = o.achievementAt ?? MONEY.achievementThreshold;
    if (threshold > 0 && amount >= threshold) {
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

export async function win(amount, big = false) {
    if (!ensure()) return;
    const amt = Math.max(0, Math.round(amount));
    const plays = playsForAmount(amt, MONEY.perPlay, MONEY.minPlays, MONEY.maxPlays);
    await playMoney({ amount: amt, plays, counter: true });
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
    document.body.appendChild(el);
    const start = performance.now();
    const ease = t => 1 - Math.pow(1 - t, 3);
    function frame(now) {
        const t = Math.min(1, (now - start) / dur);
        el.textContent = '+' + Math.floor(amount * ease(t)).toLocaleString();
        if (t < 1) {
            requestAnimationFrame(frame);
        } else {
            el.classList.add('credify-money-pop-out');
            setTimeout(() => el.remove(), 700);
        }
    }
    requestAnimationFrame(frame);
}
