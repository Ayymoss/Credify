// Credify Blackjack — bundled ES module, loaded by the page via JS interop
// (import("/_content/credify/blackjack/blackjack.js")). Self-contained canvas confetti, no dependencies.
// Demonstrates that bundled plugin JS + .NET↔JS interop works for in-memory plugin pages.

const COLORS = ['#fcd34d', '#6ee7b7', '#818cf8', '#f472b6', '#f87171', '#ffffff'];

export function celebrate(big) {
    const count = big ? 180 : 100;
    const duration = big ? 2800 : 2000;

    const canvas = document.createElement('canvas');
    canvas.style.cssText =
        'position:fixed;inset:0;width:100vw;height:100vh;pointer-events:none;z-index:9998;';
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

    const w = window.innerWidth;
    const particles = Array.from({ length: count }, () => ({
        x: w / 2 + (Math.random() - 0.5) * 120,
        y: -20,
        vx: (Math.random() - 0.5) * 9,
        vy: Math.random() * 6 + 3,
        size: Math.random() * 7 + 4,
        rot: Math.random() * Math.PI,
        vr: (Math.random() - 0.5) * 0.3,
        color: COLORS[(Math.random() * COLORS.length) | 0]
    }));

    const start = performance.now();
    function frame(now) {
        const elapsed = now - start;
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        const fade = Math.max(0, 1 - elapsed / duration);

        for (const p of particles) {
            p.vy += 0.12;          // gravity
            p.vx *= 0.99;          // drag
            p.x += p.vx;
            p.y += p.vy;
            p.rot += p.vr;

            ctx.save();
            ctx.globalAlpha = fade;
            ctx.translate(p.x, p.y);
            ctx.rotate(p.rot);
            ctx.fillStyle = p.color;
            ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.6);
            ctx.restore();
        }

        if (elapsed < duration) {
            requestAnimationFrame(frame);
        } else {
            window.removeEventListener('resize', resize);
            canvas.remove();
        }
    }

    requestAnimationFrame(frame);
}
