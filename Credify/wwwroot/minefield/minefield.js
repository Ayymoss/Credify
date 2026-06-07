// Credify Minefield — bundled ES module, loaded by the page via JS interop
// (import("/_content/credify/minefield/minefield.js")). Self-contained canvas effects, no dependencies.
// Demonstrates bundled plugin JS + .NET↔JS interop driving casino-grade flair on an in-memory plugin page.

function makeOverlayCanvas(z) {
    const canvas = document.createElement('canvas');
    canvas.style.cssText =
        `position:fixed;inset:0;width:100vw;height:100vh;pointer-events:none;z-index:${z};`;
    document.body.appendChild(canvas);

    const ctx = canvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;
    const resize = () => {
        canvas.width = window.innerWidth * dpr;
        canvas.height = window.innerHeight * dpr;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    };
    resize();
    window.addEventListener('resize', resize);

    return {
        canvas,
        ctx,
        done() {
            window.removeEventListener('resize', resize);
            canvas.remove();
        }
    };
}

// Mine detonation: a red flash, a screen shake, and a burst of dark debris + embers.
export function boom() {
    // red flash
    const flash = document.createElement('div');
    flash.style.cssText =
        'position:fixed;inset:0;pointer-events:none;z-index:9997;background:radial-gradient(circle at 50% 55%,' +
        'rgba(248,113,113,0.55),rgba(127,29,29,0.0) 60%);opacity:0;';
    document.body.appendChild(flash);
    flash.animate(
        [{ opacity: 0 }, { opacity: 1, offset: 0.15 }, { opacity: 0 }],
        { duration: 600, easing: 'ease-out' }
    ).onfinish = () => flash.remove();

    // screen shake (animate the whole document body)
    document.body.animate(
        [
            { transform: 'translate(0,0)' },
            { transform: 'translate(-8px,5px)' },
            { transform: 'translate(7px,-4px)' },
            { transform: 'translate(-5px,3px)' },
            { transform: 'translate(4px,-2px)' },
            { transform: 'translate(0,0)' }
        ],
        { duration: 480, easing: 'ease-in-out' }
    );

    const { canvas, ctx, done } = makeOverlayCanvas(9998);
    const w = window.innerWidth;
    const h = window.innerHeight;
    const cx = w / 2;
    const cy = h * 0.5;
    const duration = 1100;

    const EMBER = ['#fb923c', '#f87171', '#fbbf24', '#facc15'];
    const debris = Array.from({ length: 130 }, () => {
        const angle = Math.random() * Math.PI * 2;
        const speed = Math.random() * 13 + 4;
        const ember = Math.random() < 0.45;
        return {
            x: cx + (Math.random() - 0.5) * 30,
            y: cy + (Math.random() - 0.5) * 30,
            vx: Math.cos(angle) * speed,
            vy: Math.sin(angle) * speed - 3,
            size: Math.random() * (ember ? 5 : 7) + 2,
            rot: Math.random() * Math.PI,
            vr: (Math.random() - 0.5) * 0.4,
            color: ember ? EMBER[(Math.random() * EMBER.length) | 0] : '#1f2937'
        };
    });

    const start = performance.now();
    function frame(now) {
        const elapsed = now - start;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        const fade = Math.max(0, 1 - elapsed / duration);

        for (const p of debris) {
            p.vy += 0.22;       // gravity
            p.vx *= 0.985;      // drag
            p.x += p.vx;
            p.y += p.vy;
            p.rot += p.vr;

            ctx.save();
            ctx.globalAlpha = fade;
            ctx.translate(p.x, p.y);
            ctx.rotate(p.rot);
            ctx.fillStyle = p.color;
            ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size);
            ctx.restore();
        }

        if (elapsed < duration) {
            requestAnimationFrame(frame);
        } else {
            done();
        }
    }

    requestAnimationFrame(frame);
}
