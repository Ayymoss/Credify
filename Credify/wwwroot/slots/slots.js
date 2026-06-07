// Cosmetic reel animation. The spin result is already decided server-side before this runs.
// Each reel scrolls a strip of symbols (one GPU-composited translateY) that decelerates and lands
// on its final symbol, staggered per reel. Returns a promise the Blazor caller awaits so the result
// banner only reveals once every reel has stopped.
export function spin(finals, allGlyphs) {
    const reels = Array.from(document.querySelectorAll('#sl-machine .sl-reel'));
    if (reels.length === 0 || !allGlyphs || allGlyphs.length === 0) {
        return Promise.resolve();
    }

    const pick = () => allGlyphs[(Math.random() * allGlyphs.length) | 0];

    const promises = reels.map((reel, i) => new Promise(resolve => {
        const glyph = reel.querySelector('.sl-reel-glyph');
        const cellHeight = reel.clientHeight || 96;
        const final = (finals && finals[i] != null)
            ? finals[i]
            : (glyph ? glyph.textContent : pick());

        // strip = [target, filler, filler, ...]. We start showing the bottom filler and scroll the
        // whole strip downward until the target (index 0) lands in the window. More fillers on later
        // reels = a touch more travel, which reads as them "still going" after the first stops.
        const fillers = 16 + i * 5;
        const cells = [final];
        for (let k = 0; k < fillers; k++) {
            cells.push(pick());
        }

        const strip = document.createElement('div');
        strip.className = 'sl-strip';
        for (const symbol of cells) {
            const cell = document.createElement('div');
            cell.className = 'sl-strip-cell';
            cell.style.height = `${cellHeight}px`;
            cell.textContent = symbol;
            strip.appendChild(cell);
        }

        reel.appendChild(strip);
        if (glyph) {
            glyph.style.visibility = 'hidden';
        }

        const duration = 1150 + i * 360; // staggered stop
        const travel = (cells.length - 1) * cellHeight;

        // prime at the bottom filler, no transition, then flip to the target with an ease-out decel
        strip.style.setProperty('--sl-dur', `${duration}ms`);
        strip.style.transition = 'none';
        strip.style.transform = `translateY(${-travel}px)`;
        void strip.offsetHeight; // commit the start position before transitioning
        strip.style.transition = `transform ${duration}ms cubic-bezier(0.16, 1, 0.3, 1)`;
        strip.classList.add('sl-strip-spin');
        strip.style.transform = 'translateY(0)';

        let settled = false;
        const finish = () => {
            if (settled) return;
            settled = true;
            if (glyph) {
                glyph.textContent = final;
                glyph.style.visibility = '';
                glyph.classList.remove('sl-settled');
                void glyph.offsetWidth; // restart the landing pop
                glyph.classList.add('sl-settled');
            }
            strip.remove();
            resolve();
        };

        strip.addEventListener('transitionend', event => {
            if (event.propertyName === 'transform') finish();
        }, { once: true });
        setTimeout(finish, duration + 150); // safety net if transitionend is missed
    }));

    return Promise.all(promises);
}
