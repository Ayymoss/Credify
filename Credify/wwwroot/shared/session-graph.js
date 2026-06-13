// Session P/L graph — a live sparkline of the player's cumulative net across every Credify game this
// browser session, rendered inside the history rail's card (GameHistoryPanel → SessionGraphPanel).
// Blazor owns only the empty .credify-sgraph slot div; everything inside it is built and updated here,
// so Blazor's diffing and this DOM never meet. Instance-keyed because enhanced navigation overlaps
// lifetimes: the OLD page's dispose() can run after the NEW page's mount() and must not tear down the
// new instance. Module state survives navigation, so `carryNet` lets the headline number on the next
// page tween from where the last one left off instead of snapping from zero.

const W = 280, H = 60, PAD = 5;          // viewBox space; the SVG stretches to the card width
const EMERALD = '#34d399', ROSE = '#fb7185', SLATE = '#71717a';

let nextId = 1;
const instances = new Map(); // id -> { els, host, netTween, shownNet, clockTimer, startedAt }
let carryNet = 0;            // headline continuity across page navigations

export function mount(host, data) {
    if (!host) return -1;
    const id = nextId++;
    const inst = build(host);
    instances.set(id, inst);
    render(inst, data, true);
    return id;
}

export function update(id, data) {
    const inst = instances.get(id);
    if (inst) render(inst, data, false);
}

export function dispose(id) {
    const inst = instances.get(id);
    if (!inst) return;
    instances.delete(id);
    carryNet = inst.shownNet;
    if (inst.netTween) cancelAnimationFrame(inst.netTween);
    clearInterval(inst.clockTimer);
    inst.host.replaceChildren(); // usually moot — the slot div dies with the page
}

// ── construction ─────────────────────────────────────────────────────────────
function build(host) {
    const ns = 'http://www.w3.org/2000/svg';
    host.innerHTML = `
        <div class="credify-sgraph-head">
            <span class="credify-sgraph-net" data-r="net">±0</span>
            <span class="credify-sgraph-time" data-r="time"></span>
        </div>
        <div class="credify-sgraph-chartwrap">
            <svg data-r="svg" viewBox="0 0 ${W} ${H}" preserveAspectRatio="none" aria-hidden="true"></svg>
        </div>
        <div class="credify-sgraph-foot">
            <span title="Session best"><i class="ph-fill ph-trend-up"></i> <b data-r="peak">0</b></span>
            <span title="Session worst"><i class="ph-fill ph-trend-down"></i> <b data-r="low">0</b></span>
            <span class="credify-sgraph-plays" data-r="plays">0 plays</span>
        </div>`;

    // SVG internals (namespace-correct, so not via innerHTML on the svg element). Gradient ids are
    // instance-unique: two instances briefly coexist during an enhanced navigation.
    const svg = host.querySelector('[data-r="svg"]');
    const uid = `credify-sgraph-${nextId}`;
    const defs = document.createElementNS(ns, 'defs');
    const gradLine = splitGradient(ns, `${uid}-line`, EMERALD, ROSE);
    const gradArea = splitGradient(ns, `${uid}-area`, EMERALD, ROSE);
    defs.append(gradLine.el, gradArea.el);

    const area = document.createElementNS(ns, 'path');
    area.setAttribute('fill', `url(#${uid}-area)`);
    area.setAttribute('opacity', '0.18');

    const zero = document.createElementNS(ns, 'line');
    zero.setAttribute('x1', '0');
    zero.setAttribute('x2', String(W));
    zero.setAttribute('stroke', 'rgba(255,255,255,0.16)');
    zero.setAttribute('stroke-dasharray', '3 4');
    zero.setAttribute('vector-effect', 'non-scaling-stroke');

    const line = document.createElementNS(ns, 'path');
    line.setAttribute('fill', 'none');
    line.setAttribute('stroke', `url(#${uid}-line)`);
    line.setAttribute('stroke-width', '2');
    line.setAttribute('stroke-linejoin', 'round');
    line.setAttribute('stroke-linecap', 'round');
    line.setAttribute('vector-effect', 'non-scaling-stroke');

    const pulse = document.createElementNS(ns, 'circle');
    pulse.setAttribute('class', 'credify-sgraph-pulse');
    const dot = document.createElementNS(ns, 'circle');
    dot.setAttribute('class', 'credify-sgraph-dot');
    dot.setAttribute('r', '3');

    svg.append(defs, area, zero, line, pulse, dot);

    const inst = {
        host,
        shownNet: carryNet,
        netTween: null,
        startedAt: 0,
        els: {
            svg, area, zero, line, dot, pulse, gradLine, gradArea,
            net: host.querySelector('[data-r="net"]'),
            time: host.querySelector('[data-r="time"]'),
            plays: host.querySelector('[data-r="plays"]'),
            peak: host.querySelector('[data-r="peak"]'),
            low: host.querySelector('[data-r="low"]'),
        },
    };
    inst.clockTimer = setInterval(() => tickClock(inst), 30000);
    return inst;
}

// a vertical two-color gradient with a HARD stop we slide to the zero line each render — the curve and
// its fill are emerald above breakeven and rose below, with a knife-edge split exactly at 0
function splitGradient(ns, id, top, bottom) {
    const el = document.createElementNS(ns, 'linearGradient');
    el.setAttribute('id', id);
    el.setAttribute('gradientUnits', 'userSpaceOnUse');
    el.setAttribute('x1', '0'); el.setAttribute('y1', '0');
    el.setAttribute('x2', '0'); el.setAttribute('y2', String(H));
    const a = document.createElementNS(ns, 'stop');
    a.setAttribute('stop-color', top);
    const b = document.createElementNS(ns, 'stop');
    b.setAttribute('stop-color', bottom);
    el.append(a, b);
    return { el, a, b };
}

// ── rendering ────────────────────────────────────────────────────────────────
function render(inst, data, initial) {
    const { els } = inst;
    const pts = data.points || [];
    const n = pts.length;
    inst.startedAt = data.startedAt || inst.startedAt;
    tickClock(inst);

    els.plays.textContent = `${data.plays} ${data.plays === 1 ? 'play' : 'plays'}`;

    const net = n ? pts[n - 1] : 0;
    const peak = n ? Math.max(...pts) : 0;
    const low = n ? Math.min(...pts) : 0;
    els.peak.textContent = fmt(peak, true);
    els.low.textContent = fmt(low, true);
    setNet(inst, net, initial);

    // y-scale always brackets zero so the breakeven line stays in frame
    const top = Math.max(peak, 0), bot = Math.min(low, 0);
    const span = Math.max(1, top - bot);
    const y = v => PAD + ((top - v) / span) * (H - 2 * PAD);
    const x = i => n > 1 ? (i / (n - 1)) * W : W;
    const yZero = y(0);

    els.zero.setAttribute('y1', String(yZero));
    els.zero.setAttribute('y2', String(yZero));

    // slide both gradients' hard stop onto the zero line
    const split = String(Math.max(0, Math.min(1, yZero / H)));
    for (const g of [els.gradLine, els.gradArea]) {
        g.a.setAttribute('offset', split);
        g.b.setAttribute('offset', split);
    }

    // before the first play: just the dashed breakeven line, dimmed ("No plays yet" already sits below)
    const empty = n < 2;
    inst.host.classList.toggle('credify-sgraph--empty', empty);
    if (empty) {
        els.line.setAttribute('d', '');
        els.area.setAttribute('d', '');
        els.dot.style.display = els.pulse.style.display = 'none';
        return;
    }

    let d = `M${x(0).toFixed(1)} ${y(pts[0]).toFixed(1)}`;
    for (let i = 1; i < n; i++) d += ` L${x(i).toFixed(1)} ${y(pts[i]).toFixed(1)}`;
    els.line.setAttribute('d', d);
    els.area.setAttribute('d', `${d} V${yZero.toFixed(1)} L${x(0).toFixed(1)} ${yZero.toFixed(1)} Z`);

    const cx = x(n - 1), cy = y(net);
    const tone = net > 0 ? EMERALD : net < 0 ? ROSE : SLATE;
    for (const c of [els.dot, els.pulse]) {
        c.style.display = '';
        c.setAttribute('cx', String(cx));
        c.setAttribute('cy', String(cy));
    }
    els.dot.setAttribute('fill', tone);
    els.pulse.style.color = tone; // the ring strokes currentColor (its CSS keeps fill:none)

    // restart the pulse ring so every settled play visibly "lands" on the curve
    els.pulse.style.animation = 'none';
    void els.pulse.getBoundingClientRect();
    els.pulse.style.animation = '';
}

// headline number: tween from the currently shown value so rapid-fire wins read as one climbing figure
function setNet(inst, target, instant) {
    const { els } = inst;
    if (inst.netTween) cancelAnimationFrame(inst.netTween);
    const apply = v => {
        els.net.textContent = fmt(Math.round(v), true);
        els.net.classList.toggle('pos', target > 0);
        els.net.classList.toggle('neg', target < 0);
    };
    if ((instant && inst.shownNet === 0) || inst.shownNet === target) {
        inst.shownNet = target;
        apply(target);
        return;
    }

    const from = inst.shownNet, t0 = performance.now(), dur = 450;
    const ease = t => 1 - Math.pow(1 - t, 3);
    const step = now => {
        const t = Math.min(1, (now - t0) / dur);
        const v = from + (target - from) * ease(t);
        apply(v);
        if (t < 1) inst.netTween = requestAnimationFrame(step);
        else { inst.netTween = null; inst.shownNet = target; }
    };
    inst.netTween = requestAnimationFrame(step);
}

function tickClock(inst) {
    if (!inst.startedAt) return;
    const mins = Math.max(0, Math.floor((Date.now() - inst.startedAt) / 60000));
    inst.els.time.textContent = mins < 60 ? `${mins}m` : `${Math.floor(mins / 60)}h ${mins % 60}m`;
}

function fmt(v, signed) {
    const s = Math.abs(v).toLocaleString();
    return v > 0 ? (signed ? `+${s}` : s) : v < 0 ? `−${s}` : signed ? '±0' : '0';
}
