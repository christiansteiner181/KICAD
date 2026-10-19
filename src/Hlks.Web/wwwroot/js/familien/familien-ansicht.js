// Familieneditor und Platzieren (Modul 2, C1): reine Hilfen für die Darstellung – ohne DOM, in Node getestet (familien.test.mjs).
// Die Grafik der Familie kommt vom Kern (Abfrage familie_ansicht); hier wird nur umgerechnet, was die Oberfläche zusätzlich zeigt:
//   - Ursprung (Einfügepunkt) und Achsen x/y/z in der Ansicht, Umrissrahmen des gewählten Körpers (Projektion der Netz-/Quaderdaten),
//   - die Zuordnung Körperliste ↔ Körperteile der 3D-Antwort (Reihenfolge der Definition),
//   - die Vorschau des Umrisses am Mauszeiger beim Platzieren (Umriss von oben, gedreht und verschoben).
// Ansichtskoordinaten (Spiegel von FamilienAnsicht.cs): u nach rechts, v nach oben, wie der Betrachter sieht;
//   vorne u = x, v = z · hinten u = −x, v = z · links u = −y, v = z · rechts u = y, v = z · oben u = x, v = y · unten u = −x, v = y.
// Annahme: Der Umrissrahmen eines Körpers ist der achsparallele Rahmen seiner Eckpunkte (Zylinder: Achse ± Radius); er ist eine Anzeigehilfe,
//   keine Geometrie des Kerns.

import * as S from '../werkzeuge/schicht.js';
import { koerperDetailgrade } from './familien-text.js';

export const ANSICHTEN = [['vorne', 'Vorne'], ['hinten', 'Hinten'], ['links', 'Links'], ['rechts', 'Rechts'], ['oben', 'Oben'], ['unten', 'Unten'], ['3d', '3D']];
export const VIER_FENSTER = ['vorne', 'oben', 'links', '3d'];
const f2 = (x) => Math.round(x * 100) / 100;

/** Punkt der Familie [x, y, z] → [u, v] in der gewählten Ansicht. */
export function projektion(ansicht, p) {
  const [x, y, z] = [p[0], p[1], p[2] ?? 0];
  const neg = (v) => (v === 0 ? 0 : -v);   // kein «−0»
  switch (ansicht) {
    case 'hinten': return [neg(x), z];
    case 'links': return [neg(y), z];
    case 'rechts': return [y, z];
    case 'oben': return [x, y];
    case 'unten': return [neg(x), y];
    default: return [x, z];
  }
}

/** Achsen x, y, z als [u, v]-Richtung in der Ansicht (null, wenn die Achse auf den Betrachter zeigt). */
export function achsenRichtungen(ansicht) {
  const r = {};
  for (const [name, v] of [['x', [1, 0, 0]], ['y', [0, 1, 0]], ['z', [0, 0, 1]]]) {
    const [u, w] = projektion(ansicht, v);
    r[name] = Math.hypot(u, w) < 1e-9 ? null : [u, w];
  }
  return r;
}

/** Achsparalleler Rahmen eines Körperteils des Kerns (quader | zylinder | kugel | koerper) in Familienkoordinaten, sonst null. */
export function teilBox(t) {
  if (!t) return null;
  let pts = null;
  if (t.art === 'quader' && t.mitte && t.masse) {
    const [a, b, c] = t.masse;
    const m = t.mitte;
    // Drehung um z im Familiensystem tritt nicht auf (Teile liegen in Familienkoordinaten); sicherheitshalber der Rahmen der gedrehten Grundfläche
    const w = ((t.drehung ?? 0) * Math.PI) / 180, ca = Math.abs(Math.cos(w)), sa = Math.abs(Math.sin(w));
    const hx = (a * ca + b * sa) / 2, hy = (a * sa + b * ca) / 2;
    pts = [[m[0] - hx, m[1] - hy, m[2] - c / 2], [m[0] + hx, m[1] + hy, m[2] + c / 2]];
  } else if (t.art === 'zylinder' && t.von && t.nach && t.radius > 0) {
    const d = [t.nach[0] - t.von[0], t.nach[1] - t.von[1], t.nach[2] - t.von[2]];
    const l = Math.hypot(...d) || 1;
    // Rahmen eines schrägen Zylinders: Achse ± Radius · Betrag der Querkomponente je Koordinate
    const q = d.map((c) => Math.sqrt(Math.max(0, 1 - (c / l) ** 2)) * t.radius);
    pts = [t.von.map((c, i) => c - q[i]), t.von.map((c, i) => c + q[i]), t.nach.map((c, i) => c - q[i]), t.nach.map((c, i) => c + q[i])];
  } else if (t.art === 'kugel' && t.mitte && t.radius > 0) {
    pts = [t.mitte.map((c) => c - t.radius), t.mitte.map((c) => c + t.radius)];
  } else if (t.art === 'koerper' && Array.isArray(t.ecken) && t.ecken.length) {
    pts = t.ecken;
  }
  if (!pts) return null;
  const min = [Infinity, Infinity, Infinity], max = [-Infinity, -Infinity, -Infinity];
  for (const p of pts) for (let i = 0; i < 3; i++) { min[i] = Math.min(min[i], p[i]); max[i] = Math.max(max[i], p[i]); }
  return { min, max };
}

/** Rahmen in einer Ansicht: { minu, minv, maxu, maxv } aus den acht Ecken des Quaders. */
export function boxProjektion(ansicht, box) {
  if (!box) return null;
  const r = { minu: Infinity, minv: Infinity, maxu: -Infinity, maxv: -Infinity };
  for (const x of [box.min[0], box.max[0]]) for (const y of [box.min[1], box.max[1]]) for (const z of [box.min[2], box.max[2]]) {
    const [u, v] = projektion(ansicht, [x, y, z]);
    r.minu = Math.min(r.minu, u); r.maxu = Math.max(r.maxu, u); r.minv = Math.min(r.minv, v); r.maxv = Math.max(r.maxv, v);
  }
  return r;
}

/**
 * Ordnet die Körperteile der 3D-Antwort den Körpern der Definition zu (gleiche Reihenfolge, nur Körper im Detailgrad).
 * Ergebnis: Liste der Körper-Ids je Teil – oder null, wenn die Zuordnung nicht eindeutig ist (z. B. wegen einer Formel in «sichtbar»,
 * die einen Körper ausblendet). Der Kern liefert keine Körper-Id am Teil (Befund).
 */
export function teilZuKoerper(def, detailgrad, anzahlTeile) {
  const kandidaten = (def?.koerper ?? []).filter((k) => koerperDetailgrade(k).includes(detailgrad));
  if (kandidaten.length === anzahlTeile) return kandidaten.map((k) => k.id);
  const ohneAus = kandidaten.filter((k) => k.sichtbar !== false && k.sichtbar !== 0 && k.sichtbar !== '0');
  if (ohneAus.length === anzahlTeile) return ohneAus.map((k) => k.id);
  return null;
}

/** Index des Teils, das zum gewählten Körper gehört (oder -1). */
export function teilIndexVon(def, detailgrad, anzahlTeile, koerperId) {
  const zu = teilZuKoerper(def, detailgrad, anzahlTeile);
  return zu ? zu.indexOf(koerperId) : -1;
}

/** Länge der Achspfeile in mm: ein Zehntel der grössten Ausdehnung, mindestens 50. */
export function achsenLaenge(begrenzung) {
  if (!Array.isArray(begrenzung) || begrenzung.length < 4) return 100;
  return Math.max(50, Math.round(Math.max(begrenzung[2] - begrenzung[0], begrenzung[3] - begrenzung[1]) / 10));
}

/**
 * SVG-Überlagerung der 2D-Ansicht in Ansichtskoordinaten (y nach oben, Gruppe wird wie die Plangrafik gespiegelt):
 * Ursprung als Kreuz mit Kreis, Achsen x (rot), y (grün), z (blau) mit Pfeil und Buchstabe, Rahmen des gewählten Körpers (gestrichelt).
 * s = Ansichts-mm je Bildschirmpixel (für Textgrösse und Pfeilspitzen).
 */
export function ueberlagerungSvg(ansicht, { laenge = 100, rahmen = null, s = 1 } = {}) {
  let h = '';
  if (rahmen) {
    h += `<rect class="fe-rahmen" x="${f2(rahmen.minu)}" y="${f2(rahmen.minv)}" width="${f2(rahmen.maxu - rahmen.minu)}" height="${f2(rahmen.maxv - rahmen.minv)}"/>`;
  }
  const farben = { x: 'fe-achse-x', y: 'fe-achse-y', z: 'fe-achse-z' };
  const a = achsenRichtungen(ansicht);
  for (const [n, r] of Object.entries(a)) {
    if (!r) continue;
    const e = [r[0] * laenge, r[1] * laenge];
    const k = 6 * s, n2 = [-r[1], r[0]];
    h += `<g class="${farben[n]}"><line x1="0" y1="0" x2="${f2(e[0])}" y2="${f2(e[1])}"/>`
      + `<path d="M${f2(e[0] - r[0] * 2 * k + n2[0] * k)},${f2(e[1] - r[1] * 2 * k + n2[1] * k)} L${f2(e[0])},${f2(e[1])} L${f2(e[0] - r[0] * 2 * k - n2[0] * k)},${f2(e[1] - r[1] * 2 * k - n2[1] * k)}"/>`
      + S.text([e[0] + r[0] * 10 * s, e[1] + r[1] * 10 * s], n, s, { groessePx: 12, anker: 'middle', versatzPx: [0, -4], klasse: 'fe-achstext' })
      + '</g>';
  }
  const q = 7 * s;
  h += `<g class="fe-ursprung"><circle cx="0" cy="0" r="${f2(q)}"/><path d="M${f2(-2 * q)},0 L${f2(2 * q)},0 M0,${f2(-2 * q)} L0,${f2(2 * q)}"/></g>`;
  h += S.text([0, 0], 'Ursprung (Einfügepunkt)', s, { groessePx: 11, versatzPx: [10, -16], anker: 'start', klasse: 'fe-achstext' });
  return h;
}

// ------------------------------------------------------------------------------------------------ Passpunkte anklicken (Modul 2, C2)

/**
 * Nächster Eckpunkt der Plangrafik (Linien, Polylinien, Polygone samt Löchern, Kreismitten; ohne die Marken der Anschlüsse) zum Punkt (u, v) in
 * Ansichtskoordinaten, höchstens radius mm entfernt. Ergebnis [u, v] oder null. Für das Anklicken der Passpunkte im Kontrollbild.
 */
export function naechsterPunkt(plan, u, v, radius) {
  let beste = null, d2 = radius * radius;
  const pruefen = (p) => {
    if (!Array.isArray(p) || !Number.isFinite(p[0]) || !Number.isFinite(p[1])) return;
    const d = (p[0] - u) ** 2 + (p[1] - v) ** 2;
    if (d <= d2) { d2 = d; beste = [p[0], p[1]]; }
  };
  for (const t of plan?.teile ?? []) {
    if (t.ebene === 'symbol') continue;
    for (const pr of t.primitive ?? []) {
      if (['linie', 'polylinie', 'polygon'].includes(pr.art)) { (pr.punkte ?? []).forEach(pruefen); for (const lo of pr.loecher ?? []) lo.forEach(pruefen); }
      else if (pr.art === 'kreis') pruefen((pr.punkte ?? [])[0]);
    }
  }
  return beste;
}

/** SVG der angeklickten Passpunkte (Kreuz und Beschriftung) in Ansichtskoordinaten; punkte: [{ u, v, label }], s = mm je Pixel. */
export function pickPunkteSvg(punkte, s = 1) {
  let h = '';
  for (const p of punkte ?? []) {
    const r = 6 * s;
    h += `<g class="fu-pickpunkt"><circle cx="${f2(p.u)}" cy="${f2(p.v)}" r="${f2(r)}"/><path d="M${f2(p.u - 1.6 * r)},${f2(p.v)} L${f2(p.u + 1.6 * r)},${f2(p.v)} M${f2(p.u)},${f2(p.v - 1.6 * r)} L${f2(p.u)},${f2(p.v + 1.6 * r)}"/>`
      + S.text([p.u, p.v], p.label ?? '', s, { groessePx: 11, anker: 'start', versatzPx: [9, 9], klasse: 'fu-pickname' }) + '</g>';
  }
  return h;
}

// ------------------------------------------------------------------------------------------------ Vorschau beim Platzieren

/** Drehung auf 0 … < 360 Grad. */
export function drehungNormieren(grad) {
  const g = Number(grad);
  if (!Number.isFinite(g)) return 0;
  return ((Math.round(g * 1000) / 1000) % 360 + 360) % 360;
}
/** Leertaste: um 90° weiter (Umschalt: zurück). */
export const drehungWeiter = (grad, zurueck = false) => drehungNormieren((Number(grad) || 0) + (zurueck ? -90 : 90));

/** Punkt der Familie (x, y) → Weltpunkt: um die Drehung (Grad, Gegenuhrzeigersinn) gedreht und um den Einfügepunkt verschoben. */
export function weltPunkt(p, grad, einfuege) {
  const w = (grad * Math.PI) / 180, c = Math.cos(w), s = Math.sin(w);
  return [einfuege[0] + p[0] * c - p[1] * s, einfuege[1] + p[0] * s + p[1] * c];
}

/** Umrissprimitive aus der Antwort familie_ansicht (ansicht oben): nur Linien, Polygone, Kreise; Marken der Anschlüsse (Ebene symbol) entfallen. */
export function umrissPrimitive(antwort) {
  const aus = [];
  for (const t of antwort?.plangrafik?.teile ?? []) {
    if (t.ebene === 'symbol') continue;
    for (const p of t.primitive ?? []) if (['linie', 'polylinie', 'polygon', 'kreis', 'bogen'].includes(p.art)) aus.push(p);
  }
  return aus;
}

/** Bogen als Punktfolge (höchstens 7.5° je Strecke). */
function bogenPunkte(m, r, von, bis) {
  let sw = (bis - von) % 360;
  if (sw <= 0) sw += 360;
  const n = Math.max(2, Math.ceil(sw / 7.5));
  return Array.from({ length: n + 1 }, (_, i) => { const w = ((von + (sw * i) / n) * Math.PI) / 180; return [m[0] + r * Math.cos(w), m[1] + r * Math.sin(w)]; });
}

/** SVG-Vorschau des Umrisses (Modellkoordinaten, Werkzeugschicht) für Einfügepunkt, Drehung; Pfeil zeigt die Vorderseite (−y der Familie). */
export function vorschauSvg(primitive, grad, einfuege, s = 1) {
  if (!einfuege) return '';
  const W = (p) => weltPunkt(p, grad, einfuege);
  let h = '';
  for (const p of primitive ?? []) {
    const pts = (p.punkte ?? []).filter((q) => Array.isArray(q) && Number.isFinite(q[0]) && Number.isFinite(q[1]));
    if (p.art === 'linie' || p.art === 'polylinie') { if (pts.length >= 2) h += S.polylinie(pts.map(W), 'wz-vorschau wz-offen'); }
    else if (p.art === 'polygon') { if (pts.length >= 3) h += S.polylinie(pts.map(W), 'wz-vorschau', true); }
    else if (p.art === 'kreis') { if (pts.length && p.radius > 0) h += S.kreis(W(pts[0]), p.radius, 'wz-vorschau'); }
    else if (p.art === 'bogen') { if (pts.length && p.radius > 0) h += S.polylinie(bogenPunkte(pts[0], p.radius, p.von ?? 0, p.bis ?? 360).map(W), 'wz-vorschau wz-offen'); }
  }
  // Einfügepunkt und Vorderseite (Pfeil nach −y der Familie)
  const k = 6 * s;
  h += S.linie([einfuege[0] - k, einfuege[1]], [einfuege[0] + k, einfuege[1]], 'wz-hilf') + S.linie([einfuege[0], einfuege[1] - k], [einfuege[0], einfuege[1] + k], 'wz-hilf');
  const v = weltPunkt([0, -40 * s], grad, [0, 0]);
  h += S.linie(einfuege, [einfuege[0] + v[0], einfuege[1] + v[1]], 'wz-oeffnung');
  return h;
}

/** Umfang (Rahmen) der Vorschau in Weltkoordinaten – für Tests und Einpassen. */
export function vorschauRahmen(primitive, grad, einfuege) {
  const r = { minx: Infinity, miny: Infinity, maxx: -Infinity, maxy: -Infinity };
  for (const p of primitive ?? []) for (const q of p.punkte ?? []) {
    const w = weltPunkt(q, grad, einfuege);
    r.minx = Math.min(r.minx, w[0]); r.maxx = Math.max(r.maxx, w[0]); r.miny = Math.min(r.miny, w[1]); r.maxy = Math.max(r.maxy, w[1]);
  }
  return Number.isFinite(r.minx) ? r : null;
}

// ------------------------------------------------------------------------------------------------ Sinnbild-Vorschau

/** Primitiv eines Sinnbildteils für die Vorschau im Editor – nur, wenn alle Koordinaten Zahlen sind (Formeln rechnet nur der Kern); sonst null. */
export function sinnbildPrimitive(teil) {
  if (!teil || !teil.art) return null;
  const zahl = (v) => typeof v === 'number' && Number.isFinite(v);
  const punkte = (teil.punkte ?? []).map((p) => [p[0], p[1]]);
  if (!punkte.every((p) => zahl(p[0]) && zahl(p[1]))) return null;
  const stil = { stift: teil.stil?.stift ?? 'duenn', linie: teil.stil?.linie ?? 'voll', ...(teil.stil?.fuellung ? { fuellung: teil.stil.fuellung } : {}) };
  switch (teil.art) {
    case 'linie': case 'polylinie': return punkte.length >= 2 ? { art: teil.art, punkte, stil } : null;
    case 'polygon': return punkte.length >= 3 ? { art: 'polygon', punkte, stil } : null;
    case 'kreis': return punkte.length >= 1 && zahl(teil.radius) && teil.radius > 0 ? { art: 'kreis', punkte: [punkte[0]], radius: teil.radius, stil } : null;
    case 'bogen': return punkte.length >= 1 && zahl(teil.radius) && teil.radius > 0 && zahl(teil.von) && zahl(teil.bis) ? { art: 'bogen', punkte: [punkte[0]], radius: teil.radius, von: teil.von, bis: teil.bis, stil } : null;
    case 'text': return punkte.length >= 1 && teil.text ? { art: 'text', punkte: [punkte[0]], text: teil.text, groesse: teil.groesse ?? 2.5, ausrichtung: 'links', stil: {} } : null;
    default: return null;
  }
}
