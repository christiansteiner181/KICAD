// SVG-Bausteine für die Werkzeugschicht der Ansicht (`ansicht.werkzeugSchicht`): Vorschau, Gummiband, Fangsymbol, temporäre
// Bemassung, Griffe. Reine Funktionen, die Markup (Text) liefern – in Node testbar. Die Schicht liegt in Modellkoordinaten
// (y wie im Modell, die Ansicht spiegelt): Linienbreiten bleiben dank `vector-effect: non-scaling-stroke` (CSS .wz-schicht)
// in Bildschirmpixeln, Texte werden gegengespiegelt (scale(1,-1)), Grössen in Pixel werden mit `s` (mm je Pixel) umgerechnet.
// Farben nur über Klassen (css/oberflaeche.css → CSS-Variablen).
// Annahme: Die Ansicht spiegelt die y-Achse der Schicht (Schnittstelle Ansicht ↔ Werkzeug in entwurf/modul-1a.md).

import { winkelGrad, abstand, mitteVon, laengeText, winkelText, FANGNAMEN } from './fangen.js';

const f = (x) => (Math.round(x * 1000) / 1000).toString();
const xy = (p) => `${f(p[0])},${f(p[1])}`;
export const esc = (t) => String(t ?? '').replace(/[&<>"]/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' }[c]));

// N9: Drehung der Ansicht (Grad, Gegenuhrzeigersinn, Modell → Ansicht). Die Schicht liegt in der gedrehten Gruppe der Ansicht; damit Texte,
// Fangsymbole und Griffe auf dem Bildschirm waagrecht bleiben, drehen sie hier gegen. Die Werkzeuge setzen sie vor jedem Zeichnen
// (basis.js, griffe.js); Winkelangaben der temporären Masse gelten in den Achsen der Ansicht.
let dreh = 0;
export function drehungSetzen(d) { dreh = Number.isFinite(Number(d)) ? Number(d) : 0; }
export const drehung = () => dreh;
const um = (v, grad) => { const w = grad * Math.PI / 180, c = Math.cos(w), s = Math.sin(w); return [v[0] * c - v[1] * s, v[0] * s + v[1] * c]; };
/** Lesbarer Winkel auf (−90, 90]. */
const lesbar = (w) => { let n = ((w % 360) + 360) % 360; if (n > 180) n -= 360; if (n > 90) n -= 180; else if (n <= -90) n += 180; return n; };
/** Gruppe, die ihren Inhalt um p gegen die Drehung der Ansicht dreht (bleibt auf dem Bildschirm waagrecht). */
const gegen = (p, inhalt) => (dreh ? `<g transform="rotate(${f(-dreh)} ${f(p[0])} ${f(p[1])})">${inhalt}</g>` : inhalt);

export function linie(a, b, klasse = 'wz-linie') {
  return `<line class="${klasse}" x1="${f(a[0])}" y1="${f(a[1])}" x2="${f(b[0])}" y2="${f(b[1])}"/>`;
}

export function polylinie(punkte, klasse = 'wz-linie', geschlossen = false) {
  if (!punkte || punkte.length < 2) return '';
  return `<${geschlossen ? 'polygon' : 'polyline'} class="${klasse}" points="${punkte.map(xy).join(' ')}"/>`;
}

export function kreis(mitte, radius, klasse = 'wz-linie') {
  return `<circle class="${klasse}" cx="${f(mitte[0])}" cy="${f(mitte[1])}" r="${f(radius)}"/>`;
}

/** Viertelkreis o. ä.: Bogen um m von p1 nach p2 mit Radius r (kleiner Bogen). sweep 0|1 in Bildschirmrichtung der Schicht. */
export function bogen(p1, p2, r, sweep, klasse = 'wz-linie') {
  return `<path class="${klasse}" d="M${xy(p1)} A${f(r)},${f(r)} 0 0 ${sweep} ${xy(p2)}"/>`;
}

/**
 * Text bei p; groessePx = Schrifthöhe in Bildschirmpixeln, s = mm je Pixel. Die Schicht ist gespiegelt, daher scale(1,-1).
 * versatzPx verschiebt den Text in Bildschirmpixeln (x nach rechts, y nach oben). winkel: Winkel auf dem Bildschirm (0 = waagrecht); mit
 * ausgerichtet ist winkel die Richtung im Modell (z. B. eines Segments) – der Text folgt ihr und bleibt lesbar (N9: auch gedreht).
 */
export function text(p, inhalt, s, { groessePx = 11, anker = 'start', versatzPx = [0, 0], winkel = 0, klasse = 'wz-text', ausgerichtet = false } = {}) {
  const v = um([versatzPx[0] * s, versatzPx[1] * s], -dreh);   // Versatz in Bildschirmrichtung → Modell
  const x = p[0] + v[0], y = p[1] + v[1];
  const blatt = ausgerichtet ? lesbar(winkel + dreh) : winkel;   // Winkel auf dem Bildschirm
  const lokal = blatt - dreh;                                     // in der gedrehten Gruppe
  const r = Math.abs(lokal) > 1e-9 ? ` rotate(${f(-lokal)})` : '';
  return `<text class="${klasse}" transform="translate(${f(x)} ${f(y)}) scale(1 -1)${r}" font-size="${f(groessePx * s)}" text-anchor="${anker}">${esc(inhalt)}</text>`;
}

/** Winkel, bei dem ein Text entlang der Linie a → b lesbar bleibt (nicht auf dem Kopf). */
export function lesbarerWinkel(a, b) {
  let w = winkelGrad(a, b);
  if (w > 90 && w <= 270) w -= 180;
  return w > 180 ? w - 360 : w;
}

/** Fangsymbol nach Art (Revit): Quadrat = Endpunkt, Dreieck = Mitte, X = Schnittpunkt, ⊥ = Lot, Sanduhr = Nächster, + = Raster. */
export function fangsymbol(fang, s) {
  if (!fang || !fang.punkt || fang.art === 'frei') return '';
  const [x, y] = fang.punkt, h = 5 * s;
  let g = '';
  switch (fang.art) {
    case 'endpunkt': g = `<rect class="wz-fang" x="${f(x - h)}" y="${f(y - h)}" width="${f(2 * h)}" height="${f(2 * h)}"/>`; break;
    case 'mitte': g = `<polygon class="wz-fang" points="${xy([x - h, y - h])} ${xy([x + h, y - h])} ${xy([x, y + h])}"/>`; break;
    case 'schnittpunkt': g = `<path class="wz-fang" d="M${xy([x - h, y - h])} L${xy([x + h, y + h])} M${xy([x - h, y + h])} L${xy([x + h, y - h])}"/>`; break;
    case 'lot': g = `<path class="wz-fang" d="M${xy([x - h, y - h])} L${xy([x + h, y - h])} M${xy([x, y - h])} L${xy([x, y + h])}"/>`; break;
    case 'naechster': g = `<polygon class="wz-fang" points="${xy([x - h, y + h])} ${xy([x + h, y + h])} ${xy([x - h, y - h])} ${xy([x + h, y - h])}"/>`; break;
    case 'raster': g = `<path class="wz-fang wz-fang-leise" d="M${xy([x - h * 0.7, y])} L${xy([x + h * 0.7, y])} M${xy([x, y - h * 0.7])} L${xy([x, y + h * 0.7])}"/>`; break;
    default: return '';
  }
  const name = FANGNAMEN[fang.art] ?? '';
  const t = name && fang.art !== 'raster' ? text([x, y], name, s, { versatzPx: [10, -16], klasse: 'wz-text wz-fangtext' }) : '';
  return gegen([x, y], g) + t;   // N9: Symbol auf dem Bildschirm achsparallel
}

/** Temporäre Bemassung eines Segments: Länge in mm und Winkel (wie Revit) als Text neben der Mitte, mit kurzer Masslinie. */
export function tempMass(a, b, s, { mitWinkel = true, seite = 1 } = {}) {
  const l = abstand(a, b);
  if (l < 1e-6) return '';
  let w = (((winkelGrad(a, b) + dreh) % 360) + 360) % 360;   // N9: Winkel in den Achsen der Ansicht (waagrecht gezeichnet = 0°)
  if (w >= 359.95) w = 0;                                       // Rundungsrest der Drehung: nicht «360.0°»
  const d = [(b[0] - a[0]) / l, (b[1] - a[1]) / l], n = [-d[1] * seite, d[0] * seite];
  const v = 14 * s;   // Abstand der Masslinie vom Segment (14 px)
  const a2 = [a[0] + n[0] * v, a[1] + n[1] * v], b2 = [b[0] + n[0] * v, b[1] + n[1] * v];
  const m = mitteVon(a2, b2);
  const tw = lesbarerWinkel(a, b);
  const beschr = `${laengeText(l)}${mitWinkel ? `  ${winkelText(w)}` : ''}`;
  const tm = [m[0] + n[0] * 6 * s, m[1] + n[1] * 6 * s];
  return linie(a, a2, 'wz-hilf') + linie(b, b2, 'wz-hilf') + linie(a2, b2, 'wz-mass')
    + text(tm, beschr, s, { anker: 'middle', winkel: tw, ausgerichtet: true, klasse: 'wz-text wz-masstext' });
}

/** Beschriftung am Mauszeiger (z. B. getippte Länge). */
export function eingabeAnzeige(p, textInhalt, s) {
  return text(p, textInhalt, s, { versatzPx: [14, 14], klasse: 'wz-text wz-eingabe', groessePx: 12 });
}

/** Griff (Quadrat in Pixelgrösse). */
export function griff(p, s, klasse = 'wz-griff', px = 8) {
  const h = px / 2 * s;
  return gegen(p, `<rect class="${klasse}" x="${f(p[0] - h)}" y="${f(p[1] - h)}" width="${f(2 * h)}" height="${f(2 * h)}"/>`);   // N9: achsparallel
}

/** Kleiner Doppelpfeil (Griff zum Umdrehen) um p in Richtung d (Einheitsvektor), Länge in Pixeln. */
export function doppelpfeil(p, d, s, px = 16) {
  const h = px / 2 * s, k = 3.5 * s, n = [-d[1], d[0]];
  const a = [p[0] - d[0] * h, p[1] - d[1] * h], b = [p[0] + d[0] * h, p[1] + d[1] * h];
  const spitze = (q, r) => `M${xy([q[0] - r[0] * k + n[0] * k, q[1] - r[1] * k + n[1] * k])} L${xy(q)} L${xy([q[0] - r[0] * k - n[0] * k, q[1] - r[1] * k - n[1] * k])}`;
  return `<path class="wz-pfeil" d="M${xy(a)} L${xy(b)} ${spitze(a, [-d[0], -d[1]])} ${spitze(b, d)}"/><circle class="wz-pfeil-flaeche" cx="${f(p[0])}" cy="${f(p[1])}" r="${f(px * 0.7 * s)}"/>`;
}

/** Band (Rechteck) um das Segment a → b mit Dicke dicke, um q nach links versetzt (Vorschau einer Wand). */
export function band(a, b, dicke, q = 0) {
  const l = abstand(a, b);
  if (l < 1e-6) return '';
  const n = [-(b[1] - a[1]) / l, (b[0] - a[0]) / l];
  const h = dicke / 2;
  const p = (pt, o) => [pt[0] + n[0] * (q + o), pt[1] + n[1] * (q + o)];
  return polylinie([p(a, h), p(b, h), p(b, -h), p(a, -h)], 'wz-band', true);
}

// ------------------------------------------------------------------------------------------------ Umriss von Elementen (Vorschau beim Ändern)

/** Vereinfachter Umriss eines Elements (aus seiner Darstellung) für die Vorschau: { art: wand|linie|flaeche|punkt, punkte, dicke? } oder null. */
export function umriss(el) {
  const g = el?.geometrie;
  if (!g?.punkte?.length) return null;
  const pts = g.punkte.map((p) => [p[0], p[1]]);
  if (g.art === 'linie' && el.klasse === 'Wand' && g.dicke > 0 && pts.length >= 2) return { art: 'wand', punkte: pts.slice(0, 2), dicke: g.dicke };
  if (g.art === 'linie' || g.art === 'polylinie') return { art: 'linie', punkte: pts };
  if (g.art === 'polygon') return { art: 'flaeche', punkte: pts };
  if (g.art === 'punkt') return { art: 'punkt', punkte: pts.slice(0, 1) };
  return null;
}

/** Zeichnet die Umrisse von Elementen, jeden Punkt über die Abbildung abb (Verschieben, Drehen, Spiegeln). */
export function umrisseSvg(elemente, abb, s, klasse = 'wz-vorschau') {
  let h = '';
  for (const el of elemente) {
    const u = umriss(el);
    if (!u) continue;
    const pts = u.punkte.map(abb);
    if (u.art === 'wand') h += band(pts[0], pts[1], u.dicke, 0).replace('wz-band', klasse);
    else if (u.art === 'flaeche') h += polylinie(pts, klasse, true);
    else if (u.art === 'linie') h += polylinie(pts, `${klasse} wz-offen`);
    else { const [x, y] = pts[0], k = 4 * s; h += linie([x - k, y], [x + k, y], klasse) + linie([x, y - k], [x, y + k], klasse); }
  }
  return h;
}
