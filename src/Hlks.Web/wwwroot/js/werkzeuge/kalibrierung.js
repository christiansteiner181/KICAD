// Kalibrieren einer Unterlage (Modul 1b, C1): reine Funktionen ohne Browser (in Node testbar).
// Die Unterlage hat eine `transformation` { faktor, drehung (Grad, Gegenuhrzeigersinn), dx, dy } von Unterlagen-Koordinaten
// in Modell-mm (klassen.json): Modell = Drehung(faktor · u) + [dx, dy]; PDF und Bild spiegeln y zuerst (Pixel, y nach unten).
// Die Oberfläche braucht die Umkehrung: ein Klick im Plan (Modell-mm) → Punkt in Unterlagen-Koordinaten, weil der Befehl
// unterlage_kalibrieren `von` in Unterlagen-Koordinaten verlangt. Den neuen Faktor, die Drehung und die Verschiebung rechnet
// der Kern (Ähnlichkeitstransformation aus den zwei Punktpaaren); hier steht nur die Umrechnung der Klickpunkte.
// Annahme (aus klassen.json): «spiegeln y zuerst» heisst u' = (u, −v) vor Skalierung und Drehung. Weicht die Umsetzung
// des Kerns ab, ändert sich nur `SPIEGELT_Y` bzw. `spiegeln()` in dieser Datei.

import { rund } from './fangen.js';
import { WerkzeugFehler } from './parameter.js';

/** Arten, deren Koordinaten Pixel mit y nach unten sind. */
export const SPIEGELT_Y = new Set(['pdf', 'bild']);
export const KALIBRIERBAR = new Set(['dwg', 'dxf', 'pdf', 'bild']);

const istZahl = (x) => typeof x === 'number' && Number.isFinite(x);

/** Transformation einer Unterlage (Darstellung des Kerns, werte.transformation) oder null, wenn sie keine gültige hat. */
export function transformationVon(unterlage) {
  const t = unterlage?.werte?.transformation;
  if (!t || typeof t !== 'object') return null;
  const faktor = Number(t.faktor), drehung = Number(t.drehung ?? 0), dx = Number(t.dx ?? 0), dy = Number(t.dy ?? 0);
  if (!(faktor > 0) || ![drehung, dx, dy].every(Number.isFinite)) return null;
  return { faktor, drehung, dx, dy };
}

const spiegeln = (art, p) => (SPIEGELT_Y.has(art) ? [p[0], -p[1]] : [p[0], p[1]]);

/** Unterlagen-Koordinaten [u, v] → Modell-mm [x, y]. */
export function unterlageNachModell(t, art, uv) {
  const [u, v] = spiegeln(art, uv);
  const w = t.drehung * Math.PI / 180, c = Math.cos(w), s = Math.sin(w);
  return [t.dx + t.faktor * (u * c - v * s), t.dy + t.faktor * (u * s + v * c)];
}

/** Modell-mm [x, y] → Unterlagen-Koordinaten [u, v] (Umkehrung von unterlageNachModell). */
export function modellNachUnterlage(t, art, xy) {
  const w = -t.drehung * Math.PI / 180, c = Math.cos(w), s = Math.sin(w);
  const dx = xy[0] - t.dx, dy = xy[1] - t.dy;
  const u = (dx * c - dy * s) / t.faktor, v = (dx * s + dy * c) / t.faktor;
  return spiegeln(art, [u, v]);   // Spiegelung ist ihre eigene Umkehrung
}

const gleich = (a, b, eps) => Math.hypot(a[0] - b[0], a[1] - b[1]) < eps;

/**
 * Parameter für unterlage_kalibrieren. von = Punkte in Unterlagen-Koordinaten, nach = Zielpunkte in Modell-mm.
 * Normal zwei Punktpaare; mit nurVerschieben genau eines (Verschiebung ohne Drehung und Massstab).
 */
export function kalibrierParameter({ unterlage, von, nach, nurVerschieben = false }) {
  if (!unterlage) throw new WerkzeugFehler('Keine Unterlage gewählt.', 'In der Optionsleiste eine Unterlage wählen.');
  const n = nurVerschieben ? 1 : 2;
  if (von.length !== n || nach.length !== n) {
    throw new WerkzeugFehler(nurVerschieben ? 'Zum Verschieben braucht es einen Punkt auf der Unterlage und einen Zielpunkt.' : 'Zum Kalibrieren braucht es zwei Punkte auf der Unterlage und zwei Zielpunkte.',
      'Die Punkte der Reihe nach anklicken.');
  }
  if (!nurVerschieben) {
    if (gleich(von[0], von[1], 1e-9)) throw new WerkzeugFehler('Die beiden Punkte auf der Unterlage sind derselbe Punkt.', 'Zwei verschiedene Punkte wählen, möglichst weit auseinander.');
    if (gleich(nach[0], nach[1], 0.5)) throw new WerkzeugFehler('Die beiden Zielpunkte sind derselbe Punkt.', 'Zwei verschiedene Zielpunkte wählen.');
  }
  if (![...von.flat(), ...nach.flat()].every(istZahl)) throw new WerkzeugFehler('Ein Punkt hat keine gültigen Koordinaten.', 'Den Punkt neu setzen.');
  const p = {
    unterlage,
    von: von.map((q) => [rund(q[0], 6), rund(q[1], 6)]),
    nach: nach.map((q) => [rund(q[0], 3), rund(q[1], 3)]),
  };
  if (nurVerschieben) p.nurVerschieben = true;
  return p;
}
