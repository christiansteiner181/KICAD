// Gemeinsame Mausbedienung verschiebbarer Fenster (Auftrag W1): Dialoge (dialoge.js) und schwebende Paletten (paletten.js) benutzen dieselben Griffe.
// Die Rechenregeln stehen in fenstergeometrie.js (ohne DOM, getestet); hier nur das Verdrahten mit der Maus.
//   sichtbereich()                         Browserfenster als Rechteck
//   rechteck(el)                           Lage und Grösse eines Elements als { x, y, b, h }
//   rechteckSetzen(el, r)                  schreibt left/top/width/height
//   griffeAnlegen(el, klasse)              acht Griffe (Ränder und Ecken) als Kindelemente mit data-kante
//   ziehen(e, opt)                         Zug mit der Maus ab pointerdown e: beiBeginn(m), beiBewegung(dx, dy, m), beiEnde({ begonnen, abgebrochen })

import { groesseAendern, verschieben } from './fenstergeometrie.js';

export const KANTEN = ['n', 's', 'e', 'w', 'ne', 'nw', 'se', 'sw'];
export const CURSOR = { n: 'ns-resize', s: 'ns-resize', e: 'ew-resize', w: 'ew-resize', ne: 'nesw-resize', sw: 'nesw-resize', nw: 'nwse-resize', se: 'nwse-resize' };

export const sichtbereich = () => ({ x: 0, y: 0, b: window.innerWidth, h: window.innerHeight });

export function rechteck(el) {
  const r = el.getBoundingClientRect();
  return { x: r.left, y: r.top, b: r.width, h: r.height };
}

export function rechteckSetzen(el, r) {
  el.style.left = Math.round(r.x) + 'px';
  el.style.top = Math.round(r.y) + 'px';
  el.style.width = Math.round(r.b) + 'px';
  el.style.height = Math.round(r.h) + 'px';
}

/** Legt acht Griffe an (n, s, e, w, ne, nw, se, sw); klasse z. B. 'dlg-griff'. */
export function griffeAnlegen(el, klasse) {
  for (const k of KANTEN) {
    const g = document.createElement('div');
    g.className = `${klasse} ${klasse}-${k}`;
    g.dataset.kante = k;
    g.setAttribute('aria-hidden', 'true');
    el.appendChild(g);
  }
}

/**
 * Zug mit der Maus ab dem pointerdown-Ereignis e. Nach schwelle Pixeln Weg beginnt der Zug (beiBeginn), dann kommt jede Bewegung als (dx, dy) zum Start.
 * Esc bricht ab. beiEnde({ begonnen, abgebrochen, ereignis }) kommt genau einmal, auch wenn der Zug nie begann.
 */
export function ziehen(e, { schwelle = 0, beiBeginn = null, beiBewegung = null, beiEnde = null } = {}) {
  const x0 = e.clientX, y0 = e.clientY;
  let begonnen = false, fertig = false;
  const bewegt = (m) => {
    const dx = m.clientX - x0, dy = m.clientY - y0;
    if (!begonnen) {
      if (Math.hypot(dx, dy) < schwelle) return;
      begonnen = true;
      beiBeginn?.(m);
    }
    beiBewegung?.(dx, dy, m);
  };
  const ende = (m, abgebrochen) => {
    if (fertig) return;
    fertig = true;
    window.removeEventListener('pointermove', bewegt, true);
    window.removeEventListener('pointerup', hoch, true);
    window.removeEventListener('pointercancel', ab, true);
    window.removeEventListener('keydown', taste, true);
    window.removeEventListener('blur', ab);
    beiEnde?.({ begonnen, abgebrochen, ereignis: m });
  };
  const hoch = (m) => ende(m, false);
  const ab = (m) => ende(m, true);
  const taste = (k) => { if (k.key === 'Escape') { k.stopPropagation(); k.preventDefault(); ende(k, true); } };
  window.addEventListener('pointermove', bewegt, true);
  window.addEventListener('pointerup', hoch, true);
  window.addEventListener('pointercancel', ab, true);
  window.addEventListener('keydown', taste, true);
  window.addEventListener('blur', ab);
}

/**
 * Zieht an einem Griff (kante) die Grösse von start (Rechteck beim Beginn) und meldet jedes neue Rechteck.
 * opt: min { b, h }, beiAenderung(r), beiEnde(r, geaendert).
 */
export function groesseZiehen(e, kante, start, { min = { b: 120, h: 80 }, beiAenderung, beiEnde } = {}) {
  let letztes = start, geaendert = false;
  ziehen(e, {
    beiBewegung(dx, dy) {
      letztes = groesseAendern(start, kante, dx, dy, min, sichtbereich());
      geaendert = true;
      beiAenderung?.(letztes);
    },
    beiEnde({ abgebrochen }) {
      if (abgebrochen && geaendert) { letztes = start; beiAenderung?.(start); geaendert = false; }
      beiEnde?.(letztes, geaendert);
    },
  });
}

/** Verschiebt ein Rechteck mit der Maus und meldet jedes neue Rechteck (ganz im Browserfenster). Schwelle in Pixeln. */
export function bewegenZiehen(e, start, { schwelle = 3, beiBeginn, beiAenderung, beiEnde } = {}) {
  let letztes = start, geaendert = false;
  ziehen(e, {
    schwelle,
    beiBeginn,
    beiBewegung(dx, dy) {
      letztes = verschieben(start, dx, dy, sichtbereich());
      geaendert = true;
      beiAenderung?.(letztes);
    },
    beiEnde({ abgebrochen }) {
      if (abgebrochen && geaendert) { letztes = start; beiAenderung?.(start); geaendert = false; }
      beiEnde?.(letztes, geaendert);
    },
  });
}
