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

// Cash-out celebration: a fountain of gold coins erupting from the bottom centre.
export function cashOut(big) {
    const count = big ? 160 : 90;
    const duration = big ? 2800 : 2100;

    const { canvas, ctx, done } = makeOverlayCanvas(9998);
    const w = window.innerWidth;
    const h = window.innerHeight;

    const GOLD = ['#fcd34d', '#fbbf24', '#f59e0b', '#fde68a'];
    const coins = Array.from({ length: count }, () => ({
        x: w / 2 + (Math.random() - 0.5) * 160,
        y: h + 20,
        vx: (Math.random() - 0.5) * 7,
        vy: -(Math.random() * 12 + (big ? 13 : 10)),
        r: Math.random() * 7 + 5,
        phase: Math.random() * Math.PI * 2,
        spin: (Math.random() - 0.5) * 0.3,
        color: GOLD[(Math.random() * GOLD.length) | 0]
    }));

    const start = performance.now();
    function frame(now) {
        const elapsed = now - start;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        const fade = elapsed > duration * 0.7 ? Math.max(0, 1 - (elapsed - duration * 0.7) / (duration * 0.3)) : 1;

        for (const c of coins) {
            c.vy += 0.3;        // gravity
            c.vx *= 0.99;
            c.x += c.vx;
            c.y += c.vy;
            c.phase += c.spin;

            // a spinning coin: an ellipse whose width pulses with the spin to fake 3D rotation
            const squash = Math.abs(Math.cos(c.phase));
            ctx.save();
            ctx.globalAlpha = fade;
            ctx.translate(c.x, c.y);
            ctx.scale(squash * 0.9 + 0.1, 1);
            ctx.beginPath();
            ctx.arc(0, 0, c.r, 0, Math.PI * 2);
            ctx.fillStyle = c.color;
            ctx.fill();
            ctx.lineWidth = 1.5;
            ctx.strokeStyle = 'rgba(180,120,10,0.7)';
            ctx.stroke();
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
