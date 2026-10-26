// Handskelett (Modul G, entwurf/modul-gesten.md): kleines Feld unten rechts, das aus den Ereignissen { art: 'punkte', punkte: [[x, y] × 21] | null } die Hand als Linien zeichnet –
// ohne Kamerabild. Daran sieht man, warum etwas nicht erkannt wird. Vorgabe ein (Einstellung «skelett»), nur sichtbar, solange die Gesten laufen.
// Die Linien rechnet gesten-text.js (skelettLinien, in Node getestet); hier wird nur gezeichnet. Es wird nichts gespeichert: jedes Bild ersetzt das vorige.

import { skelettLinien } from './gesten-text.js';

export const BREITE = 160;
export const HOEHE = 120;

/**
 * Richtet das Feld ein und gibt { zeichnen, sichtbar } zurück. doc ist das Dokument (für Tests austauschbar).
 * Das Feld liegt in #gesten-skelett, blockiert keine Mausklicks (pointer-events: none) und schiebt die Einblendungen unten rechts nach oben (CSS-Variable --meldungen-unten am Element #meldungen).
 */
export function skelettEinrichten(steuerung, doc = document) {
  const feld = doc.createElement('div');
  feld.id = 'gesten-skelett';
  feld.hidden = true;
  feld.setAttribute('aria-hidden', 'true');
  feld.title = 'Handskelett: so sieht die Gestenerkennung die Hand (ohne Kamerabild)';
  const leinwand = doc.createElement('canvas');
  leinwand.width = BREITE;
  leinwand.height = HOEHE;
  feld.appendChild(leinwand);
  doc.body.appendChild(feld);
  const g = leinwand.getContext?.('2d') ?? null;
  let punkte = null;
  let geplant = false;
  let meldungenVerschoben = false;

  const sichtbar = () => { const z = steuerung.zustand(); return steuerung.einstellung().skelett && z.laeuft; };

  function zeichnen() {
    geplant = false;
    const an = sichtbar();
    feld.hidden = !an;
    if (an !== meldungenVerschoben) {   // nur bei Änderung, und am Element der Einblendungen (nicht am Wurzelelement: das beobachtet das Menüband)
      meldungenVerschoben = an;
      doc.getElementById?.('meldungen')?.style.setProperty('--meldungen-unten', an ? `${HOEHE + 14}px` : '0px');
    }
    if (!g) return;
    g.clearRect(0, 0, BREITE, HOEHE);
    if (!an) { punkte = null; return; }
    const linien = skelettLinien(punkte, BREITE, HOEHE);
    if (!linien) return;
    g.strokeStyle = (typeof getComputedStyle === 'function' ? getComputedStyle(feld).color : '') || '#4aa8ff';
    g.lineWidth = 2;
    g.lineCap = 'round';
    g.beginPath();
    for (const [x1, y1, x2, y2] of linien) { g.moveTo(x1, y1); g.lineTo(x2, y2); }
    g.stroke();
  }
  const spaeter = () => {
    if (geplant) return;
    geplant = true;
    if (typeof requestAnimationFrame === 'function') requestAnimationFrame(zeichnen); else zeichnen();
  };

  const abmelden = steuerung.abonnieren((e) => {
    if (e.art === 'punkte') { punkte = Array.isArray(e.punkte) ? e.punkte : null; spaeter(); }
    else if (e.art === 'schalter' || e.art === 'einstellung') spaeter();
    else if (e.art === 'zustand' && e.zustand === 'aus') { punkte = null; spaeter(); }
  });
  zeichnen();
  return { zeichnen, sichtbar, abmelden, feld };
}
