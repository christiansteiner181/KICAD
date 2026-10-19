// Verstellbare Seitenleiste (Breite) und Trenner zwischen Eigenschaften und Projektbrowser; Grössen werden gemerkt.

import { merken } from '../zustand.js';

export function seitenleisteVerstellbar() {
  const wurzel = document.documentElement;
  const breite = merken.lesen('seitenleisteBreite', null);
  if (breite) wurzel.style.setProperty('--breite-seitenleiste', breite + 'px');
  ziehen(document.getElementById('seitenleiste-griff'), (e) => {
    const b = Math.min(Math.max(e.clientX, 200), window.innerWidth * 0.5);
    wurzel.style.setProperty('--breite-seitenleiste', b + 'px');
    merken.schreiben('seitenleisteBreite', b);
  });
  const leiste = document.getElementById('seitenleiste');
  const anteil = merken.lesen('seitenleisteAnteil', 0.5);
  leiste.style.gridTemplateRows = `${anteil}fr 4px ${1 - anteil}fr`;
  ziehen(document.getElementById('seitenleiste-trenner'), (e) => {
    const r = leiste.getBoundingClientRect();
    const a = Math.min(Math.max((e.clientY - r.top) / r.height, 0.15), 0.85);
    leiste.style.gridTemplateRows = `${a}fr 4px ${1 - a}fr`;
    merken.schreiben('seitenleisteAnteil', a);
  });
}

function ziehen(griff, bewegen) {
  if (!griff) return;
  griff.addEventListener('pointerdown', (e) => {
    griff.setPointerCapture(e.pointerId);
    const weg = () => { griff.removeEventListener('pointermove', bewegen); griff.removeEventListener('pointerup', weg); window.dispatchEvent(new Event('resize')); };
    griff.addEventListener('pointermove', bewegen);
    griff.addEventListener('pointerup', weg);
  });
}
