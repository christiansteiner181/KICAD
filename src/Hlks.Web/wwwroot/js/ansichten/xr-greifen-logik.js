// Modul XR, Teile XR-B (Greifen) und XR-C (Tablet an der linken Hand): die Rechenregeln, rein und ohne DOM und three (in Node getestet: xr-b.test.mjs).
// Greif-Erkennung aus Gelenken (Hand-Tracking) bzw. Tasten (Controller), Reichweite, Querverschiebung einer Leitung in 1-cm-Schritten, Handfläche zum Kopf,
// Lage des Tablets über der Hand, Treffer auf dem Tablet und das Tablet-Layout.
//
// Koordinaten: three-Welt, Meter ({x, y, z}, y oben). Gelenke: Namen der WebXR-Hand-API ('wrist', 'index-finger-tip' …) → {x, y, z} in der Welt.
//
// ANNAHMEN (am Brillentest zu bestätigen; alle Schwellen sind hier benannt und leicht zu ändern):
//   Faust = Mittel der Entfernungen der vier Fingerspitzen zum Handgelenk, geteilt durch die Handgrösse (Handgelenk → Grundgelenk des Mittelfingers, ≈ 0.09 m).
//     Offene Hand ≈ 1.9, Faust ≈ 1.0. Greifen ab < 1.3, Loslassen ab > 1.55 (Hysterese).
//   Pinch = Daumen- und Zeigefingerspitze unter 2.5 cm (los ab 4 cm).
//   Tablet: Hand offen (Verhältnis > 1.65) und die Handflächen-Normale zeigt höchstens 60° neben den Kopf (wer auf die Hand blickt, hält sie etwa 50° unter der Blickrichtung). Schliessen: Faust oder Wegdrehen (> 80°, Hysterese).
//   Berühren: Fingerspitze höchstens 1.5 cm vor der Tablet-Ebene bzw. dahinter (bis 4 cm), im Rechteck; ein Druck zählt beim Eintritt.

import { v3, plus, minus, mal, skalar, kreuz, laenge, abstand3, einheit } from './xr-mess-logik.js';

const GRAD = Math.PI / 180;
export const GREIF_REICHWEITE = 0.6;          // m: so nah muss das Element sein, damit es greifbar ist
export const SCHRITT_M = 0.01;                // Querverschiebung in 1-cm-Schritten
export const FAUST_AN = 1.3;
export const FAUST_LOS = 1.55;
export const OFFEN_AB = 1.65;
export const PINCH_AN = 0.025;
export const PINCH_LOS = 0.04;
export const PALM_AN_GRAD = 60;
export const PALM_AUS_GRAD = 80;
export const HERANHOLEN_DAUER = 0.5;          // s, bis ein per Strahl geholtes Element bei der Hand ist
export const HERANHOLEN_ABSTAND = 0.4;        // m Abstand zur Hand nach dem Heranholen

const zahl = (x) => typeof x === 'number' && Number.isFinite(x);
const hat = (g, n) => g && g[n] && zahl(g[n].x) && zahl(g[n].y) && zahl(g[n].z);
const SPITZEN = ['index-finger-tip', 'middle-finger-tip', 'ring-finger-tip', 'pinky-finger-tip'];
const NOETIG = ['wrist', 'middle-finger-phalanx-proximal', ...SPITZEN, 'thumb-tip', 'index-finger-metacarpal', 'pinky-finger-metacarpal'];

/** Sind alle Gelenke da, die die Erkennung braucht? */
export const gelenkeVollstaendig = (g) => !!g && NOETIG.every((n) => hat(g, n));

/** Handgrösse (m): Handgelenk bis Grundgelenk des Mittelfingers. */
export const handGroesse = (g) => abstand3(g['wrist'], g['middle-finger-phalanx-proximal']);

/** Mittlere Entfernung der Fingerspitzen zum Handgelenk, in Handgrössen (offen ≈ 1.9, Faust ≈ 1.0); null, wenn Gelenke fehlen. */
export function faustVerhaeltnis(g) {
  if (!gelenkeVollstaendig(g)) return null;
  const h = handGroesse(g);
  if (!(h > 1e-4)) return null;
  return SPITZEN.reduce((s, n) => s + abstand3(g[n], g['wrist']), 0) / SPITZEN.length / h;
}

/** Faust mit Hysterese: vorher = war schon gegriffen. */
export function istFaust(g, vorher = false) {
  const v = faustVerhaeltnis(g);
  if (v === null) return false;
  return vorher ? v < FAUST_LOS : v < FAUST_AN;
}

export const istOffen = (g) => { const v = faustVerhaeltnis(g); return v !== null && v > OFFEN_AB; };

/** Pinch (Daumen- und Zeigefingerspitze) mit Hysterese. */
export function istPinch(g, vorher = false) {
  if (!hat(g, 'thumb-tip') || !hat(g, 'index-finger-tip')) return false;
  const d = abstand3(g['thumb-tip'], g['index-finger-tip']);
  return vorher ? d < PINCH_LOS : d < PINCH_AN;
}

/** Mitte der Handfläche. */
export function handflaechenMitte(g) {
  if (!hat(g, 'wrist') || !hat(g, 'middle-finger-phalanx-proximal')) return null;
  return mal(plus(g['wrist'], g['middle-finger-phalanx-proximal']), 0.5);
}

/** Normale der Handfläche (zeigt aus der Handfläche heraus); seite 'left' | 'right'. */
export function handflaechenNormale(g, seite) {
  if (!gelenkeVollstaendig(g)) return null;
  const a = minus(g['index-finger-metacarpal'], g['wrist']), b = minus(g['pinky-finger-metacarpal'], g['wrist']);
  const n = einheit(kreuz(a, b));
  return seite === 'left' ? mal(n, -1) : n;   // linke Hand: cross(b, a)
}

/** Winkel (Grad) zwischen der Handflächen-Normale und der Richtung zum Kopf; null, wenn nicht bestimmbar. */
export function palmWinkelZumKopf(g, seite, kopf) {
  const n = handflaechenNormale(g, seite), m = handflaechenMitte(g);
  if (!n || !m || !kopf) return null;
  const z = einheit(minus(kopf, m));
  return Math.acos(Math.min(1, Math.max(-1, skalar(n, z)))) / GRAD;
}

/**
 * Tablet-Geste der linken Hand: offene Hand, Handfläche zum Kopf. vorher = Tablet war offen (Hysterese: das Wegdrehen zählt erst ab PALM_AUS_GRAD,
 * das Schliessen bei Faust oder Verhältnis unter OFFEN_AB − 0.25).
 */
export function tabletGeste(g, seite, kopf, vorher = false) {
  const v = faustVerhaeltnis(g), w = palmWinkelZumKopf(g, seite, kopf);
  if (v === null || w === null) return false;
  return vorher ? (v > OFFEN_AB - 0.25 && w < PALM_AUS_GRAD) : (v > OFFEN_AB && w < PALM_AN_GRAD);
}

// ---------------------------------------------------------------------------------------------------- Greifen
/**
 * Abstand eines Punkts zu einem Element: k = { id, strecken: [{a, b, radius}] } (Leitung: Achse minus Radius) bzw. { id, box: {min, max} }.
 * Rückgabe in m (0 = im Element).
 */
export function abstandZuElement(p, k) {
  if (k.strecken?.length) {
    let best = Infinity;
    for (const s of k.strecken) {
      const ab = minus(s.b, s.a), l2 = skalar(ab, ab);
      const t = l2 < 1e-12 ? 0 : Math.min(1, Math.max(0, skalar(minus(p, s.a), ab) / l2));
      best = Math.min(best, Math.max(0, abstand3(p, plus(s.a, mal(ab, t))) - (s.radius ?? 0)));
    }
    return best;
  }
  const b = k.box;
  return Math.hypot(Math.max(b.min.x - p.x, 0, p.x - b.max.x), Math.max(b.min.y - p.y, 0, p.y - b.max.y), Math.max(b.min.z - p.z, 0, p.z - b.max.z));
}

/** Das nächste Element in Reichweite des Greifpunkts: { id, abstand } oder null; Leitungen, Formteile, Apparate haben Vorrang vor Wänden und Decken (wie das Anklicken). */
export function elementInReichweite(p, kandidaten, reichweite = GREIF_REICHWEITE) {
  let best = null;
  for (const k of kandidaten) {
    const d = abstandZuElement(p, k);
    if (d > reichweite) continue;
    const vorrang = k.vorrang ?? 1;
    if (!best || vorrang > best.vorrang || (vorrang === best.vorrang && d < best.abstand)) best = { id: k.id, abstand: d, vorrang };
  }
  return best ? { id: best.id, abstand: best.abstand } : null;
}

/** Heranholen per Strahl: Anteil (0 … 1), um den sich der Abstand zur Hand nach t Sekunden verkleinert hat. */
export const heranholAnteil = (t) => { const x = Math.min(1, Math.max(0, t / HERANHOLEN_DAUER)); return x * x * (3 - 2 * x); };

// ---------------------------------------------------------------------------------------------------- Querverschiebung einer Leitung
/**
 * Querachsen zu einer Längsachse: u = «oben» (die Senkrechte, senkrecht zur Achse gemacht), s = «seitlich» (waagrecht quer). Bei einer (fast) senkrechten Leitung
 * gelten x (Ost) und die Querrichtung dazu.
 */
export function querAchsen(achse) {
  const d = einheit(achse);
  const hoch = v3(0, 1, 0);
  let u = minus(hoch, mal(d, skalar(hoch, d)));
  if (laenge(u) < 0.2) { const ost = v3(1, 0, 0); u = minus(ost, mal(d, skalar(ost, d))); }
  u = einheit(u);
  const s = einheit(kreuz(d, u));
  return { u, s };
}

/** Rundet auf ganze Schritte (Vorgabe 1 cm). */
export const aufSchritt = (x, schritt = SCHRITT_M) => { const r = Math.round(x / schritt) * schritt; return Math.abs(r) < 1e-12 ? 0 : Math.round(r * 1e6) / 1e6; };

/**
 * Verschiebung der ganzen Leitung quer zur Achse aus der Handbewegung delta: nur der Anteil quer zur Achse zählt, in den Querachsen (oben, seitlich) auf 1 cm gerundet.
 * Rückgabe { offset (three, m), oben, seitlich, achsen }.
 */
export function querVerschiebung(achse, delta, schritt = SCHRITT_M) {
  const { u, s } = querAchsen(achse);
  const oben = aufSchritt(skalar(delta, u), schritt), seitlich = aufSchritt(skalar(delta, s), schritt);
  return { offset: plus(mal(u, oben), mal(s, seitlich)), oben, seitlich, achsen: { u, s } };
}

/** Three-Verschiebung (m) → Parameter der Befehle des Kerns (mm, x Ost, y Nord, z oben), auf ganze mm gerundet. */
export function befehlsMasse(offset) {
  const r = (x) => { const v = Math.round(x * 1000); return v === 0 ? 0 : v; };
  return { dx: r(offset.x), dy: r(-offset.z), dz: r(offset.y) };
}

// ---------------------------------------------------------------------------------------------------- Tablet
export const TABLET_B = 0.26;          // m
export const TABLET_H = 0.195;         // m
export const TABLET_ABSTAND = 0.14;    // m über der Handfläche
export const TABLET_PX = { b: 512, h: 384 };
export const BERUEHR_VOR = 0.015;
export const BERUEHR_HINTER = 0.04;

/** Lage aus Mitte und (Blick-)Normale des Tablets: aufrecht, rechts/oben daraus abgeleitet. */
export function tabletLageAus(mitte, normale) {
  const n = einheit(normale);
  const hoch = v3(0, 1, 0);
  let rechts = kreuz(hoch, n);
  if (laenge(rechts) < 1e-6) rechts = v3(1, 0, 0);
  rechts = einheit(rechts);
  const oben = einheit(kreuz(n, rechts));
  return { mitte, normale: n, rechts, oben, b: TABLET_B, h: TABLET_H };
}

/** Lage des Tablets: über der Handfläche (in Richtung ihrer Normale), dem Kopf zugewandt, aufrecht. */
export function tabletLage(mitte, normale, kopf) {
  const m = plus(mitte, mal(einheit(normale), TABLET_ABSTAND));
  return tabletLageAus(m, minus(kopf, m));
}

// ---- Beruhigen des Tablets (Rückmeldung nach dem ersten Brillentest, 21.10.2026: «tablet ist etwas nervös»). Alle Werte stellt Christian nach dem nächsten Brillentest nach.
export const TABLET_TOTZONE_M = 0.015;     // m: weicht die Mitte weniger ab, bleibt das Tablet stehen
export const TABLET_TOTZONE_GRAD = 4;      // Grad: dasselbe für die Normale des Tablets
export const TABLET_RUHE_ANTEIL = 0.3;     // das Nachführen endet, sobald die Abweichung unter diesem Anteil der Totzone liegt
export const TABLET_FOLGE_ZEIT = 0.04;     // s: Zeitkonstante, mit der das Tablet beim Nachführen das gefilterte Ziel einholt
/** One-Euro-Filter der Tablet-Mitte (Hz; beta: zusätzliche Grenzfrequenz je m/s). Langsam bei kleinen, zügig bei grossen Bewegungen. */
export const TABLET_FILTER_MITTE = Object.freeze({ minCut: 1.2, beta: 40, dCut: 3 });
/** One-Euro-Filter der Tablet-Normale (Einheitsvektor; beta je 1/s). */
export const TABLET_FILTER_NORMALE = Object.freeze({ minCut: 1.2, beta: 8, dCut: 3 });
/** One-Euro-Filter der Handflächen-Normale (Hauptquelle des Zitterns: Hebel TABLET_ABSTAND). */
export const TABLET_FILTER_PALM = Object.freeze({ minCut: 0.8, beta: 8, dCut: 3 });

/** Neuer, leerer Zustand der Beruhigung (je Sitzung; beim Öffnen des Tablets neu anlegen). */
export const neuerTabletZustand = () => ({ mitte: null, normale: null, palm: null, folgt: false });

/** One-Euro-Filter für Vektoren. z = { x (gefiltert), roh (letzter Rohwert), dx (gefilterte Geschwindigkeit) }; liefert den gefilterten Wert. */
export function einEuroVektor(z, wert, dt, par) {
  const d = Math.min(0.1, Math.max(1e-3, dt));
  if (!z.x) { z.x = { ...wert }; z.roh = { ...wert }; z.dx = v3(); return z.x; }
  const alpha = (fc) => 1 / (1 + 1 / (2 * Math.PI * fc) / d);
  const geschw = mal(minus(wert, z.roh), 1 / d);
  z.dx = plus(z.dx, mal(minus(geschw, z.dx), alpha(par.dCut)));
  const fc = par.minCut + par.beta * laenge(z.dx);
  z.x = plus(z.x, mal(minus(wert, z.x), alpha(fc)));
  z.roh = { ...wert };
  return z.x;
}

/** Normale der Handfläche vor tabletLage glätten (Zustand je Sitzung); gibt einen Einheitsvektor zurück. */
export function tabletPalmGlaetten(zustand, normale, dt) {
  zustand.palm ??= {};
  return einheit(einEuroVektor(zustand.palm, einheit(normale), dt, TABLET_FILTER_PALM));
}

/** Winkel (Grad) zwischen zwei Einheitsvektoren. */
export const winkelGrad = (a, b) => Math.acos(Math.min(1, Math.max(-1, skalar(einheit(a), einheit(b))))) / GRAD;

/**
 * Beruhigt die Lage des Tablets. alt = bisher gezeigte Lage (null: erste Lage, sofort übernommen), neu = Soll aus tabletLage, dt in s,
 * zustand aus neuerTabletZustand(). Das Soll wird je Bild gefiltert (One-Euro); das Tablet bleibt stehen, solange das gefilterte Soll weniger als die Totzone
 * (TABLET_TOTZONE_M, TABLET_TOTZONE_GRAD) abweicht, folgt darüber weich und bleibt wieder stehen, sobald es angekommen ist (Hysterese TABLET_RUHE_ANTEIL).
 */
export function tabletNachfuehren(alt, neu, dt, zustand) {
  if (!alt || !zustand.mitte) {
    zustand.folgt = false;
    zustand.mitte = {}; zustand.normale = {};
    einEuroVektor(zustand.mitte, neu.mitte, dt, TABLET_FILTER_MITTE);
    einEuroVektor(zustand.normale, neu.normale, dt, TABLET_FILTER_NORMALE);
    return neu;
  }
  const d = Math.min(0.1, Math.max(1e-3, dt));
  const fm = einEuroVektor(zustand.mitte, neu.mitte, d, TABLET_FILTER_MITTE);
  const fn = einheit(einEuroVektor(zustand.normale, neu.normale, d, TABLET_FILTER_NORMALE));
  const abwM = abstand3(fm, alt.mitte), abwN = winkelGrad(fn, alt.normale);
  const ueber = abwM > TABLET_TOTZONE_M || abwN > TABLET_TOTZONE_GRAD;
  const ruhig = abwM < TABLET_TOTZONE_M * TABLET_RUHE_ANTEIL && abwN < TABLET_TOTZONE_GRAD * TABLET_RUHE_ANTEIL;
  if (ueber) zustand.folgt = true; else if (ruhig) zustand.folgt = false;
  if (!zustand.folgt) return alt;
  const k = 1 - Math.exp(-d / TABLET_FOLGE_ZEIT);
  return tabletLageAus(plus(alt.mitte, mal(minus(fm, alt.mitte), k)), plus(alt.normale, mal(minus(fn, alt.normale), k)));
}

/** Punkt (Welt) → Ebenenkoordinaten des Tablets { u, v (0 … 1, links oben = 0/0), tiefe (vor der Ebene > 0) } oder null, wenn ausserhalb. */
export function tabletKoordinaten(lage, p) {
  const w = minus(p, lage.mitte);
  const x = skalar(w, lage.rechts), y = skalar(w, lage.oben), tiefe = skalar(w, lage.normale);
  const u = x / lage.b + 0.5, v = 0.5 - y / lage.h;
  return { u, v, tiefe, innen: u >= 0 && u <= 1 && v >= 0 && v <= 1 };
}
/** Schnitt eines Strahls mit der Tablet-Ebene: { u, v, t } oder null (parallel, dahinter, ausserhalb). */
export function tabletStrahl(lage, ursprung, richtung) {
  const nr = skalar(lage.normale, richtung);
  if (Math.abs(nr) < 1e-6) return null;
  const t = skalar(minus(lage.mitte, ursprung), lage.normale) / nr;
  if (!(t > 0)) return null;
  const k = tabletKoordinaten(lage, plus(ursprung, mal(richtung, t)));
  return k.innen ? { u: k.u, v: k.v, t } : null;
}
/** Berührt die Fingerspitze das Tablet? (Eintritt in die Zone von BERUEHR_VOR bis BERUEHR_HINTER) → { u, v } oder null. */
export function tabletBeruehrung(lage, spitze) {
  const k = tabletKoordinaten(lage, spitze);
  if (!k.innen) return null;
  return k.tiefe <= BERUEHR_VOR && k.tiefe >= -BERUEHR_HINTER ? { u: k.u, v: k.v, tiefe: k.tiefe } : null;
}

/** Tasten des Tablets in Bildpunkten (512 × 384): Kopf mit Eigenschaften oben, darunter Raster 3 × 4. */
export const TABLET_TASTEN = Object.freeze([
  { id: 'rueckgaengig', text: 'Rückgängig', spalte: 0, zeile: 0 },
  { id: 'wiederholen', text: 'Wiederholen', spalte: 1, zeile: 0 },
  { id: 'messen', text: 'Messen', spalte: 2, zeile: 0, umschalter: true },
  { id: 'hoeher', text: 'Augenhöhe +', spalte: 0, zeile: 1 },
  { id: 'tiefer', text: 'Augenhöhe −', spalte: 1, zeile: 1 },
  { id: 'messungenLoeschen', text: 'Messungen löschen', spalte: 2, zeile: 1 },
  { id: 'schnitt', text: 'Schnittbereich', spalte: 0, zeile: 2, umschalter: true },
  { id: 'ifc', text: 'IFC-Unterlage', spalte: 1, zeile: 2, umschalter: true },
  { id: 'lot', text: 'Lot zur Fläche', spalte: 2, zeile: 2, umschalter: true },
  { id: 'familie', text: 'Familie platzieren', spalte: 0, zeile: 3, platzhalter: true },
  { id: 'problem', text: 'Problem markieren', spalte: 1, zeile: 3, platzhalter: true },
  { id: 'raum', text: 'Raum wechseln', spalte: 2, zeile: 3, platzhalter: true },
]);
export const TABLET_KOPF_H = 112;     // px für Titel und Eigenschaften
export const TABLET_ENDE = { id: 'ende', text: 'VR beenden' };

/** Rechtecke aller Tasten in Bildpunkten (zuzüglich der Taste «VR beenden» unten). */
export function tabletLayout() {
  const rand = 14, spalten = 3, zeilen = 4, fussH = 38, luecke = 8;
  const raumH = TABLET_PX.h - TABLET_KOPF_H - fussH - rand;
  const bw = (TABLET_PX.b - 2 * rand - (spalten - 1) * luecke) / spalten, bh = (raumH - (zeilen - 1) * luecke) / zeilen;
  const tasten = TABLET_TASTEN.map((t) => ({ ...t, x: rand + t.spalte * (bw + luecke), y: TABLET_KOPF_H + t.zeile * (bh + luecke), w: bw, h: bh }));
  tasten.push({ ...TABLET_ENDE, x: rand, y: TABLET_PX.h - fussH - rand / 2, w: TABLET_PX.b - 2 * rand, h: fussH - 4 });
  return tasten;
}
const LAYOUT = tabletLayout();
/** Taste an der Stelle (u, v) in 0 … 1; Platzhalter zählen als Treffer (sie tun nichts). */
export function tasteBei(u, v, layout = LAYOUT) {
  const x = u * TABLET_PX.b, y = v * TABLET_PX.h;
  return layout.find((t) => x >= t.x && x <= t.x + t.w && y >= t.y && y <= t.y + t.h) ?? null;
}

/** Zeilen für den Kopf des Tablets aus dem Element (lesend): Kennzeichen, Klasse, System, Geschoss + wichtige Werte. */
export function eigenschaftenZeilen(el, max = 3) {
  if (!el) return ['Kein Element gewählt'];
  const z = [`${el.kennzeichen ?? ''} · ${el.klasse ?? ''}`.trim()];
  const teil = [el.system && `System ${el.system}`, el.geschoss && `Geschoss ${el.geschoss}`, el.raum && `Raum ${el.raum}`].filter(Boolean);
  if (teil.length) z.push(teil.join(' · '));
  const w = el.werte ?? {};
  const wichtig = Object.entries(w).filter(([k, v]) => ['string', 'number', 'boolean'].includes(typeof v) && v !== '' && !/^(definition|geometrie|netz)$/i.test(k)).slice(0, 6)
    .map(([k, v]) => `${k} ${typeof v === 'number' ? Math.round(v * 100) / 100 : v}`);
  if (wichtig.length) z.push(wichtig.slice(0, 3).join(' · '));
  return z.slice(0, max);
}

/** Grösse, auf die das Tablet beim Öffnen wächst (0 … 1 nach t Sekunden; Aufbau von der Hand aus, 0.35 s, mit leichtem Überschwingen). */
export const AUFBAU_DAUER = 0.35;
export function aufbauAnteil(t) {
  const x = Math.min(1, Math.max(0, t / AUFBAU_DAUER));
  const e = 1 - Math.pow(1 - x, 3);
  return x >= 1 ? 1 : e * (1 + 0.08 * Math.sin(x * Math.PI));
}
/** Aufleuchten einer Taste nach dem Antippen: 1 → 0 über 0.35 s. */
export const AUFLEUCHT_DAUER = 0.35;
export const aufleuchten = (t) => Math.max(0, 1 - t / AUFLEUCHT_DAUER);
