// Credify Slots reel engine. The spin result is decided server-side before this runs — the reels
// just land on it. Each reel is a 3-row window (the centre row is the payline); a tall strip of
// symbols scrolls down and decelerates so the result symbol settles in the centre, with the
// neighbours above/below visible. Blazor renders the empty .sl-reel windows; this module owns
// everything inside them.
//
// The signature beat: reels 1 & 2 stop first, THEN the final reel spins on its own — and if the
// first two already match, it crawls under a spotlight (anticipation) before landing.

const SPIN_EASE = 'cubic-bezier(0.16, 1, 0.3, 1)';

function pick(glyphs) {
    return glyphs && glyphs.length ? glyphs[(Math.random() * glyphs.length) | 0] : '7';
}

function buildStrip(cells, cellHeight) {
    const strip = document.createElement('div');
    strip.className = 'sl-strip';
    for (const symbol of cells) {
        const cell = document.createElement('div');
        cell.className = 'sl-cell';
        cell.style.height = `${cellHeight}px`;
        cell.textContent = symbol;
        strip.appendChild(cell);
    }
    return strip;
}

// Render a reel at rest showing [neighbour, centre, neighbour] with no motion.
function rest(reel, centre, glyphs) {
    const cellHeight = (reel.clientHeight || 180) / 3;
    reel.replaceChildren(buildStrip([pick(glyphs), centre, pick(glyphs)], cellHeight));
}

// Spin one reel: build a long strip ending in [top, FINAL, bottom, ...fillers], prime it scrolled
// to the bottom, then ease it back to the top so FINAL lands in the centre row. Resolves when the
// reel has visually stopped (the resting strip is left in place).
function spinReel(reel, final, glyphs, fillers, duration, anticipate) {
    return new Promise(resolve => {
        const cellHeight = (reel.clientHeight || 180) / 3;
        const cells = [pick(glyphs), final, pick(glyphs)];
        for (let k = 0; k < fillers; k++) cells.push(pick(glyphs));

        const strip = buildStrip(cells, cellHeight);
        reel.replaceChildren(strip);

        const travel = (cells.length - 3) * cellHeight; // scroll past every filler

        strip.style.setProperty('--sl-dur', `${duration}ms`);
        strip.style.transition = 'none';
        strip.style.transform = `translateY(${-travel}px)`; // start showing the deep fillers
        void strip.offsetHeight;                            // commit before transitioning
        strip.style.transition = `transform ${duration}ms ${SPIN_EASE}`;
        strip.classList.add('sl-strip-spin');
        strip.style.transform = 'translateY(0)';            // … settle so [top, FINAL, bottom] shows

        if (anticipate) reel.classList.add('sl-reel-anticipate');

        let settled = false;
        const finish = () => {
            if (settled) return;
            settled = true;
            reel.classList.remove('sl-reel-anticipate');
            resolve();
        };
        strip.addEventListener('transitionend', e => { if (e.propertyName === 'transform') finish(); }, { once: true });
        setTimeout(finish, duration + 220); // safety net
    });
}

function applyWin(machineEl, reels, finals, opts) {
    if (!opts || !opts.win) return;

    // single payline: glow the centre cells whose symbols match (all three, or the matching pair)
    let winners = [];
    if (finals[0] === finals[1] && finals[1] === finals[2]) winners = [0, 1, 2];
    else if (finals[0] === finals[1]) winners = [0, 1];
    else if (finals[1] === finals[2]) winners = [1, 2];
    else if (finals[0] === finals[2]) winners = [0, 2];

    for (const i of winners) {
        const centre = reels[i].querySelector('.sl-cell:nth-child(2)');
        if (centre) centre.classList.add('sl-cell-win');
    }
    machineEl.classList.add(opts.jackpot ? 'sl-jackpot' : 'sl-win');
}

// Build the resting reels on first paint so the cabinet isn't empty before the first pull.
export function mount(machineEl, glyphs) {
    const reels = Array.from(machineEl.querySelectorAll('.sl-reel'));
    const seed = ['7', 'BAR', '🔔'];
    reels.forEach((reel, i) => rest(reel, seed[i] ?? pick(glyphs), glyphs));
}

// Drive a full spin to the server-decided finals. opts = { win, jackpot }.
export async function spin(machineEl, finals, glyphs, opts) {
    const reels = Array.from(machineEl.querySelectorAll('.sl-reel'));
    if (reels.length < 3) return;

    machineEl.classList.remove('sl-win', 'sl-jackpot');

    // reels 1 & 2 spin together and stop left-to-right
    await Promise.all([
        spinReel(reels[0], finals[0], glyphs, 16, 1150, false),
        spinReel(reels[1], finals[1], glyphs, 22, 1550, false),
    ]);

    // the final reel spins alone — and crawls under a spotlight when the first two already match
    const tease = finals[0] === finals[1];
    await spinReel(reels[2], finals[2], glyphs, tease ? 34 : 26, tease ? 2600 : 1750, tease);

    applyWin(machineEl, reels, finals, opts);
}
