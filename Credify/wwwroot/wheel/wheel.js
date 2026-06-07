// Credify Wheel — rotates the money wheel so the fixed top pointer lands on the result slice. The result is
// already decided server-side; this is cosmetic. spin(midAngle) takes the slice's mid-angle (degrees,
// clockwise from 12 o'clock) and turns the rotor several times before settling there.
let current = 0; // accumulated rotation (deg), so successive spins keep turning forward

export function spin(midAngle) {
    const rotor = document.querySelector('#wh-wheel .wh-rotor');
    if (!rotor) return Promise.resolve();

    // we want absolute rotation R with R ≡ -midAngle (mod 360) so the slice's mid sits under the top pointer,
    // and R at least ~5 full turns beyond the current angle.
    const turns = 5;
    const k = Math.ceil((current + turns * 360 + midAngle) / 360);
    const target = k * 360 - midAngle;
    current = target;

    rotor.style.transition = 'transform 4.2s cubic-bezier(0.12, 0.62, 0.12, 1)';
    rotor.style.transform = `rotate(${target}deg)`;

    return new Promise(resolve => {
        let done = false;
        const finish = () => { if (done) return; done = true; resolve(); };
        rotor.addEventListener('transitionend', event => {
            if (event.propertyName === 'transform') finish();
        }, { once: true });
        setTimeout(finish, 4500); // safety net if transitionend is missed
    });
}
