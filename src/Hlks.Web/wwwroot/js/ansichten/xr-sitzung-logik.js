// Modul XR, Teil XR-H «Sitzungsraum mit Modell auf dem Tisch»: die Rechenregeln, rein und ohne DOM und three (in Node getestet: xr-sitzung.test.mjs).
// Einpassen und Massstab, Grenzen, Drehen und Verschieben mit einer Hand, Zoomen und Drehen mit zwei Händen um den Mittelpunkt, Etagenliste und Schnitthöhe
// aus den Geschossdaten, Tablet-Seite des Sitzungsraums.
//
// Koordinaten: Raum-lokal, Meter ({x, y, z}, y oben). Ursprung = Boden unter dem Kopf beim Betreten, der Blick geht nach −z. Das Modell liegt auf dem Tisch; seine Lage
// ist { massstab (Nenner n, 1:n), drehung (rad um die Senkrechte, gegen den Uhrzeigersinn von oben), x, z (Mitte des Modells relativ zur Tischmitte, m) }.
// Modellkoordinaten sind die der 3D-Ansicht (three, m: x Ost, y Höhe, z = −Nord); Geschossdaten (kote, hoehe, dicke) in mm wie im Kern.
//
// ANNAHMEN (am Brillentest zu bestätigen; alle Werte sind hier benannt):
//   Tisch 1.6 × 1.0 m, Plattenoberkante 0.75 m, Vorderkante 0.4 m vor dem Kopf (Mitte also 0.9 m vor dem Kopf); nutzbare Platte mit 0.1 m Rand.
//   Massstab zu Beginn 1:50 (grösser, wenn das Modell sonst nicht auf die Platte passt, höchstens 1:200); Zoom von 1:200 bis 1:20.
//   Griffe (Christian 21.10.2026 nach dem Brillentest: «das ist nicht praktikabel. es braucht griffe. halten nur mit daumen und zeigefinger.»): zwei sichtbare Griffe links und
//   rechts des Modells (GRIFF_ABSTAND_M ausserhalb der Modellbreite, GRIFF_HOEHE_M über der Platte), sie wandern mit Lage, Drehung und Massstab des Modells. Gegriffen wird nur
//   mit einem Pinch (Daumen- und Zeigefingerspitze) bzw. dem Abzug, der höchstens GRIFF_FANG_M vom Griff entfernt beginnt (Controller auch mit dem Zielstrahl), und bis zum
//   Loslassen. Bewegt wird das Modell nur, solange BEIDE Griffe gehalten sind (verschieben, drehen, zoomen um den Mittelpunkt der beiden Pinch-Punkte); ein Griff allein: Ruhe.
//   Die Faust greift nichts. Zoom: der Abstand der Hände, Drehung: die Richtung der Verbindungslinie, Verschieben: der Mittelpunkt.
//   Schnitt: unter der Decke des gewählten Geschosses = Unterkante der Decke (Oberkante der Decke minus Dicke), «Decke zeigen» = Oberkante der Decke. Oberkante der Decke
//   über einem Geschoss = Kote des Geschosses darüber, sonst Kote + Geschosshöhe (Regel von decke_erzeugen, Kern). Gibt es keine Decke im Modell, gilt deren Oberkante.

import { TABLET_PX, TABLET_KOPF_H, TABLET_ENDE } from './xr-greifen-logik.js';

export const TISCH = Object.freeze({ hoehe: 0.75, breite: 1.6, tiefe: 1.0, vorderkante: 0.4, rand: 0.1, halt: 0.05 });
/** Mitte der Tischplatte (Oberkante) im Raum. */
export const TISCH_MITTE = Object.freeze({ x: 0, y: TISCH.hoehe, z: -(TISCH.vorderkante + TISCH.tiefe / 2) });
export const RAUM = Object.freeze({ breite: 8, tiefe: 6, hoehe: 3.2, hinten: 1.5 });
export const MASSSTAB = Object.freeze({ start: 50, min: 20, max: 200 });
export const GRIFF_ABSTAND_M = 0.06;      // m: Abstand eines Griffs zur Modellbreite (aussen, in der Lage des Modells gemessen)
export const GRIFF_HOEHE_M = 0.05;         // m: Höhe der Griffe über der Tischplatte
export const GRIFF_DURCHMESSER_M = 0.025;  // m: Grösse der Kugel eines Griffs (Anzeige)
export const GRIFF_FANG_M = 0.05;          // m: so nah am Griff muss ein Pinch beginnen, damit er den Griff fasst
export const GRIFF_HOVER_M = 0.05;         // m: so nah an einem freien Griff leuchtet er heller (Annäherung)
export const GRIFF_MIN_HALB_M = 0.02;      // m: kleinster Abstand der Griffe von der Modellmitte (winziges Modell)
export const MIN_HANDABSTAND = 0.03;       // m: kleinster Abstand zweier Hände, der noch zählt (gegen Teilen durch fast null)
export const MIN_RICHTUNG = 0.03;          // m: kürzere Vektoren (Hand, Verbindungslinie) liefern keine Richtung
export const SPIEL_MM = 0.5;               // mm: Flächen genau auf der Schnittebene bleiben/entfallen sicher (wie SPIEL_M in schnittbereich3d.js)
export const DECKEN_TOLERANZ_MM = 2;       // mm: so genau muss die Oberkante einer Decke auf der Kote liegen, damit sie zum Geschoss zählt
export const MAX_ETAGEN = 8;               // so viele Geschosse (plus «Alle Etagen») passen auf das Tablet

const zahl = (x) => typeof x === 'number' && Number.isFinite(x);
const klemme = (x, a, b) => Math.min(b, Math.max(a, x));
/** Drehung (x, z) um die Senkrechte; theta > 0 = gegen den Uhrzeigersinn von oben (wie three rotation.y). */
export const dreheXZ = (p, theta) => ({ x: p.x * Math.cos(theta) + p.z * Math.sin(theta), z: -p.x * Math.sin(theta) + p.z * Math.cos(theta) });
/** Richtung (x, z) → Winkel, gleiche Zählung wie dreheXZ (Gieren, vr-logik: atan2(−z, x)). */
export const gierenVon = (d) => Math.atan2(-d.z, d.x);
/** Winkelunterschied b − a auf −π … π. */
export const winkelDiff = (b, a) => { let d = (b - a) % (2 * Math.PI); if (d > Math.PI) d -= 2 * Math.PI; if (d <= -Math.PI) d += 2 * Math.PI; return d; };

// ------------------------------------------------------------------------------------------------------ Massstab und Lage
export const massstabBegrenzen = (n) => (zahl(n) ? klemme(n, MASSSTAB.min, MASSSTAB.max) : MASSSTAB.start);
export const massstabText = (n) => `1:${Math.round(massstabBegrenzen(n))}`;

/** Nutzbare Platte (m). */
export const platte = () => ({ b: TISCH.breite - 2 * TISCH.rand, t: TISCH.tiefe - 2 * TISCH.rand });

/**
 * Startlage: 1:50 auf der Mitte der Platte; passt das Modell (grundfläche b × t in m) so nicht auf die nutzbare Platte, wird der Massstab aufgerundet
 * (höchstens 1:200, dann passt = false). Ungültige Grössen: 1:50.
 */
export function einpassen(groesse, start = MASSSTAB.start) {
  let n = start, passt = true;
  if (groesse && groesse.b > 0 && groesse.t > 0) {
    const p = platte();
    const noetig = Math.max(groesse.b / p.b, groesse.t / p.t);
    if (noetig > n) n = Math.ceil(noetig);
    if (n > MASSSTAB.max) { n = MASSSTAB.max; passt = noetig <= n; }
  }
  return { massstab: massstabBegrenzen(n), drehung: 0, x: 0, z: 0, passt };
}

/** Mitte des Modells bleibt auf der Platte (halt = Abstand zum Rand). */
export function lageBegrenzen(lage) {
  const gx = TISCH.breite / 2 - TISCH.halt, gz = TISCH.tiefe / 2 - TISCH.halt;
  return { ...lage, massstab: massstabBegrenzen(lage.massstab), x: klemme(lage.x, -gx, gx), z: klemme(lage.z, -gz, gz) };
}

/** Zurücksetzen = Startlage (neu eingepasst). */
export const zuruecksetzen = (groesse) => einpassen(groesse);

/** Mitte des Modells im Raum (x, z). */
export const modellMitte = (lage) => ({ x: TISCH_MITTE.x + lage.x, z: TISCH_MITTE.z + lage.z });

// ------------------------------------------------------------------------------------------------------ Griffe
const abstand3 = (p, q) => Math.hypot(p.x - q.x, p.y - q.y, p.z - q.z);

/**
 * Lage der beiden Griffe (Raum-lokal, m): links (index 0) und rechts (index 1) der Modellmitte, in der Drehung des Modells, GRIFF_ABSTAND_M ausserhalb der halben Modellbreite
 * (groesse.b, unverkleinert), GRIFF_HOEHE_M über der Platte. Beim Zoomen und Drehen wandern sie mit.
 */
export function griffPunkte(lage, groesse) {
  const halb = Math.max((zahl(groesse?.b) ? groesse.b : 0) / lage.massstab / 2, GRIFF_MIN_HALB_M) + GRIFF_ABSTAND_M;
  const m = modellMitte(lage);
  return [{ index: 0, seite: 'links', vz: -1 }, { index: 1, seite: 'rechts', vz: 1 }].map(({ index, seite, vz }) => {
    const d = dreheXZ({ x: vz * halb, z: 0 }, lage.drehung);
    return { index, seite, x: m.x + d.x, y: TISCH.hoehe + GRIFF_HOEHE_M, z: m.z + d.z };
  });
}

/** Abstand des Punkts p vom Strahl (Ursprung u, Richtung r), nur nach vorn; hinter dem Ursprung zählt der Abstand zum Ursprung. */
export function strahlAbstand(u, r, p) {
  const len = Math.hypot(r.x, r.y, r.z);
  if (!(len > 1e-9)) return abstand3(u, p);
  const d = { x: r.x / len, y: r.y / len, z: r.z / len }, w = { x: p.x - u.x, y: p.y - u.y, z: p.z - u.z };
  const t = w.x * d.x + w.y * d.y + w.z * d.z;
  if (t <= 0) return abstand3(u, p);
  return Math.hypot(w.x - t * d.x, w.y - t * d.y, w.z - t * d.z);
}

/**
 * Welcher Griff liegt in Fangreichweite? punkt = Pinch-Punkt bzw. Position des Controllers (Raum-lokal, mit y), strahl = Zielstrahl { u, r } des Controllers (Raum-lokal) oder null
 * (Hände greifen nur in der Nähe). Rückgabe der Index des nächsten Griffs (0 | 1) oder null.
 */
export function griffTreffer(punkt, strahl, griffe, fang = GRIFF_FANG_M) {
  if (!punkt) return null;
  let best = null, bd = fang;
  for (const g of griffe) {
    let d = abstand3(punkt, g);
    if (strahl?.u && strahl?.r) d = Math.min(d, strahlAbstand(strahl.u, strahl.r, g));
    if (d <= bd) { bd = d; best = g.index; }
  }
  return best;
}

// ------------------------------------------------------------------------------------------------------ Gesten
/** Richtung einer Hand um die Senkrechte (rad) aus den Gelenken (Handgelenk → Grundgelenk Mittelfinger, Raum-lokal) bzw. dem Zielstrahl; null, wenn senkrecht/unbekannt. */
export function handGieren(s) {
  const g = s?.gelenke;
  if (g?.wrist && g['middle-finger-phalanx-proximal']) {
    const d = { x: g['middle-finger-phalanx-proximal'].x - g.wrist.x, z: g['middle-finger-phalanx-proximal'].z - g.wrist.z };
    if (Math.hypot(d.x, d.z) >= 0.02) return gierenVon(d);
  }
  const r = s?.strahl?.r;
  if (r && Math.hypot(r.x, r.z) >= 0.3) return gierenVon(r);
  return null;
}

/** Eine Hand greift: Modell starr an der Hand. hand = { x, z, gieren|null } (Raum-lokal). */
export function einhandStart(lage, hand) {
  return { art: 'ein', lage0: { ...lage }, h0: { x: hand.x, z: hand.z }, g0: hand.gieren ?? null };
}
export function einhandSchritt(g, hand) {
  const dphi = g.g0 !== null && hand.gieren !== null && hand.gieren !== undefined ? winkelDiff(hand.gieren, g.g0) : 0;
  const m0 = modellMitte(g.lage0);
  const rel = dreheXZ({ x: m0.x - g.h0.x, z: m0.z - g.h0.z }, dphi);
  const m = { x: hand.x + rel.x, z: hand.z + rel.z };
  return lageBegrenzen({ ...g.lage0, drehung: g.lage0.drehung + dphi, x: m.x - TISCH_MITTE.x, z: m.z - TISCH_MITTE.z });
}

/** Zwei Hände greifen: a, b = { x, z } (Raum-lokal). */
export function zweihandStart(lage, a, b) {
  const mitte = { x: (a.x + b.x) / 2, z: (a.z + b.z) / 2 };
  const m0 = modellMitte(lage);
  const q = dreheXZ({ x: mitte.x - m0.x, z: mitte.z - m0.z }, -lage.drehung);   // Punkt des Modells unter dem Mittelpunkt der Hände (verkleinert)
  const d = { x: b.x - a.x, z: b.z - a.z };
  return { art: 'zwei', lage0: { ...lage }, abstand0: Math.max(Math.hypot(d.x, d.z), MIN_HANDABSTAND), phi0: Math.hypot(d.x, d.z) >= MIN_RICHTUNG ? gierenVon(d) : null, q0: { x: q.x * lage.massstab, z: q.z * lage.massstab } };
}
export function zweihandSchritt(g, a, b) {
  const d = { x: b.x - a.x, z: b.z - a.z };
  const abstand = Math.max(Math.hypot(d.x, d.z), MIN_HANDABSTAND);
  const n = massstabBegrenzen(g.lage0.massstab * g.abstand0 / abstand);
  const phi = Math.hypot(d.x, d.z) >= MIN_RICHTUNG ? gierenVon(d) : null;
  const theta = g.lage0.drehung + (phi !== null && g.phi0 !== null ? winkelDiff(phi, g.phi0) : 0);
  const mitte = { x: (a.x + b.x) / 2, z: (a.z + b.z) / 2 };
  const q = dreheXZ({ x: g.q0.x / n, z: g.q0.z / n }, theta);
  return lageBegrenzen({ massstab: n, drehung: theta, x: mitte.x - q.x - TISCH_MITTE.x, z: mitte.z - q.z - TISCH_MITTE.z });
}

/** besitzt = welchen Griff (0 | 1) eine Hand hält, sonst null; vorher = war der Pinch im letzten Schritt gesetzt (Flanke). */
export const gestenNeu = (lage) => ({ lage, modus: null, griff: null, besitzt: { links: null, rechts: null }, vorher: { links: false, rechts: false } });

/**
 * Ein Schritt der Gestenerkennung. haende = { links, rechts }, je null oder { griff, nah, x, z }:
 *   griff = Pinch (Daumen und Zeigefinger) bzw. Abzug ist gesetzt – die Faust zählt nicht (das entscheidet der Aufrufer, xr-sitzung.js);
 *   nah   = Index des Griffs (0 | 1) in Fangreichweite der Hand (griffTreffer), sonst null;
 *   x, z  = Pinch-Punkt der Hand (Raum-lokal), an dem gemessen wird.
 * Eine Hand hält einen Griff, wenn der Pinch in Fangreichweite beginnt (steigende Flanke, der Griff ist frei) – und bis sie loslässt. Bewegt wird das Modell **nur, solange beide
 * Griffe gehalten sind** (verschieben, drehen, zoomen); hält nur eine Hand, ruht es (modus 'wartet'). Jeder Wechsel setzt einen neuen Bezug, damit nichts springt.
 * einhandStart/einhandSchritt bleiben für eine spätere Wahl erhalten.
 */
export function gestenSchritt(g, haende) {
  const besitzt = { links: null, rechts: null }, vorher = { links: false, rechts: false };
  const andere = { links: 'rechts', rechts: 'links' };
  for (const s of ['links', 'rechts']) {
    const h = haende?.[s];
    vorher[s] = !!h?.griff;
    if (!h?.griff) continue;
    if (g.besitzt[s] !== null) besitzt[s] = g.besitzt[s];
    else if (!g.vorher[s] && (h.nah === 0 || h.nah === 1) && g.besitzt[andere[s]] !== h.nah && besitzt[andere[s]] !== h.nah) besitzt[s] = h.nah;
  }
  const n = (besitzt.links !== null ? 1 : 0) + (besitzt.rechts !== null ? 1 : 0);
  const gleich = besitzt.links === g.besitzt.links && besitzt.rechts === g.besitzt.rechts;
  let { lage, griff } = g;
  const modus = n === 0 ? null : n === 1 ? 'wartet' : 'zwei';
  if (n < 2) griff = null;
  else {
    // a = die Hand am Griff 0 (links am Modell), b = die Hand am Griff 1: die Richtung a → b ist die Richtung der Verbindungslinie der Griffe
    const a = besitzt.links === 0 ? haende.links : haende.rechts, b = besitzt.links === 0 ? haende.rechts : haende.links;
    if (!gleich || !griff) griff = zweihandStart(lage, a, b);
    else lage = zweihandSchritt(griff, a, b);
  }
  return { lage, modus, griff, besitzt, vorher };
}

// ------------------------------------------------------------------------------------------------------ Geschosse und Schnitthöhe
/**
 * Geschosse für den Sitzungsraum. geschosse: [{ kennzeichen, name, kote, hoehe, teilVon? }] (mm), decken: [{ oberkante, dicke }] (mm, nur Klasse Decke).
 * Nur Geschosse mit Kote und Höhe (wie «Auf Geschoss»). Rückgabe aufsteigend nach Kote: { kennzeichen, name, kote, hoehe, deckeOK, dicke (null = keine Decke gefunden), deckeUK, ohneDecke }.
 */
export function geschosseAufbereiten(geschosse, decken = []) {
  const gueltig = (geschosse ?? []).filter((g) => zahl(g?.kote) && zahl(g?.hoehe)).sort((a, b) => a.kote - b.kote || String(a.kennzeichen).localeCompare(String(b.kennzeichen)));
  return gueltig.map((g) => {
    const darueber = gueltig.find((x) => x !== g && x.kote > g.kote + 1 && (x.teilVon ?? null) === (g.teilVon ?? null));
    const deckeOK = darueber ? darueber.kote : g.kote + g.hoehe;
    const passend = (decken ?? []).filter((d) => zahl(d?.oberkante) && zahl(d?.dicke) && d.dicke > 0 && Math.abs(d.oberkante - deckeOK) <= DECKEN_TOLERANZ_MM);
    const dicke = passend.length ? Math.max(...passend.map((d) => d.dicke)) : null;
    return { kennzeichen: g.kennzeichen, name: g.name ?? '', kote: g.kote, hoehe: g.hoehe, deckeOK, dicke, deckeUK: dicke === null ? deckeOK : deckeOK - dicke, ohneDecke: dicke === null };
  });
}

/** Der Schnitt «unter der Decke» liegt so viel tiefer als die Unterkante der Decke (mm). Christian 21.10.2026 nach dem Brillentest: «etagenwahl zeigt decke. ev. 5cm
 * weiter unten schneiden.» (Die Decken kommen oft nur aus der IFC-Unterlage; KICAD kennt dann ihre Dicke nicht.) Nach dem nächsten Brillentest nachstellen. */
export const SCHNITT_TIEFER_MM = 50;

/** Höhe des Schnitts in mm (Modell, z oben): SCHNITT_TIEFER_MM unter der Unterkante der Decke bzw. mit deckeZeigen deren Oberkante. */
export const schnittHoeheMm = (geschoss, deckeZeigen = false) => (deckeZeigen ? geschoss.deckeOK : geschoss.deckeUK - SCHNITT_TIEFER_MM);

/**
 * Höhe der Clipping-Ebene im Raum (m): Plattenoberkante + (Schnitthöhe − tiefster Punkt des Modells) / Massstab. Das Spiel schiebt die Ebene um SPIEL_MM (im Modell) so,
 * dass Flächen genau auf der Schnitthöhe nicht flackern: unter der Decke entfällt, was in der Ebene liegt; mit «Decke zeigen» bleibt es.
 */
export function schnittEbeneY({ schnittMm, deckeZeigen, modellUnten, massstab, tischOben = TISCH.hoehe }) {
  const mm = schnittMm + (deckeZeigen ? SPIEL_MM : -SPIEL_MM);
  return tischOben + (mm / 1000 - modellUnten) / massstab;
}

/** Text für den Kopf des Tablets. */
export function kopfZeilen(lage, wahl, geschosse) {
  const z = [`Sitzungsraum · Massstab ${massstabText(lage.massstab)}`];
  const g = wahl?.geschoss ? geschosse.find((x) => x.kennzeichen === wahl.geschoss) : null;
  if (!g) z.push('Alle Etagen');
  else {
    const hoehe = (schnittHoeheMm(g, wahl.deckeZeigen) / 1000).toFixed(2);
    z.push(`${g.kennzeichen} · ${wahl.deckeZeigen ? 'Schnitt über der Decke' : 'Schnitt unter der Decke'} (${hoehe} m)`);
    if (g.ohneDecke && !wahl.deckeZeigen) z.push('Keine Decke im Modell: Schnitt auf Oberkante Geschoss');
  }
  return z;
}

// ------------------------------------------------------------------------------------------------------ Tablet-Seite
/** Beschriftung eines Geschosses auf der Taste. */
export const GESCHOSS_TEXT_MAX = 26;      // Zeichen: so lang darf die Beschriftung einer Etagentaste sein (die Tasten sind im Hochformat so breit wie das Tablet)
export const SITZUNG_TASTE_H_MAX = 72;     // Bildpunkte: höher werden die Tasten der Sitzungsraum-Seite bei wenigen Etagen nicht
export const geschossText = (g) => { const k = String(g.kennzeichen ?? ''); const t = k.length <= 8 ? k : String(g.name || k); return t.length > GESCHOSS_TEXT_MAX ? `${t.slice(0, GESCHOSS_TEXT_MAX - 1)}…` : t; };

/**
 * Tasten der Tablet-Seite im Sitzungsraum (Bildpunkte wie tabletLayout): oben «Alle Etagen» und die Geschosse (oberstes zuerst, höchstens MAX_ETAGEN),
 * darunter «Decke zeigen», «Zurücksetzen», «IFC-Unterlage», dann «Begehen» und unten «VR beenden».
 */
export function sitzungsLayout(geschosse) {
  const rand = 14, luecke = 8, fussH = 44;
  const eintraege = [{ id: 'alle', text: 'Alle Etagen', umschalter: true }, ...[...geschosse].reverse().slice(0, MAX_ETAGEN).map((g) => ({ id: `g:${g.kennzeichen}`, text: geschossText(g), umschalter: true }))];
  const zeilen = eintraege.length + 2;
  const raumH = TABLET_PX.h - TABLET_KOPF_H - fussH - rand;
  const bh = Math.min(SITZUNG_TASTE_H_MAX, (raumH - (zeilen - 1) * luecke) / zeilen), bw = TABLET_PX.b - 2 * rand, bw2 = (bw - luecke) / 2;
  const zelle = (spalte, zeile, zwei = false) => ({ x: rand + (zwei ? spalte * (bw2 + luecke) : 0), y: TABLET_KOPF_H + zeile * (bh + luecke), w: zwei ? bw2 : bw, h: bh });
  const tasten = eintraege.map((e, i) => ({ ...e, ...zelle(0, i) }));
  const z0 = eintraege.length;
  tasten.push({ id: 'decke', text: 'Decke zeigen', umschalter: true, ...zelle(0, z0, true) });
  tasten.push({ id: 'zuruecksetzen', text: 'Zurücksetzen', ...zelle(1, z0, true) });
  tasten.push({ id: 'ifc', text: 'IFC-Unterlage', umschalter: true, ...zelle(0, z0 + 1, true) });
  tasten.push({ id: 'raum', text: 'Begehen', ...zelle(1, z0 + 1, true) });
  tasten.push({ ...TABLET_ENDE, x: rand, y: TABLET_PX.h - fussH - rand / 2, w: TABLET_PX.b - 2 * rand, h: fussH - 4 });
  return tasten;
}

/** Zustand der Umschalter für die Tasten: gewählte Etage, «Decke zeigen», IFC. */
export function sitzungsTasten(wahl, ifcAn) {
  const z = { alle: !wahl.geschoss, decke: !!wahl.deckeZeigen, ifc: !!ifcAn };
  if (wahl.geschoss) z[`g:${wahl.geschoss}`] = true;
  return z;
}
