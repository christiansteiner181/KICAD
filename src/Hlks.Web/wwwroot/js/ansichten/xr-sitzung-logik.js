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
//   Greifen: eine Hand muss beim Zufassen bis NAH_ABSTAND (0.25 m) an das Modell herankommen (Umkreis auf dem Tisch, Höhe von 0.15 m unter der Platte bis 0.35 m über dem
//   Modell); eine Hand, die schon greift, bleibt dran. Mit einer Hand hängt das Modell starr an der Hand (verschiebt sich mit ihr und dreht sich mit ihrer Drehung um die
//   Senkrechte); mit zwei Händen zoomt der Abstand um den Mittelpunkt, die Drehung der Verbindungslinie dreht das Modell, der Mittelpunkt verschiebt es.
//   Schnitt: unter der Decke des gewählten Geschosses = Unterkante der Decke (Oberkante der Decke minus Dicke), «Decke zeigen» = Oberkante der Decke. Oberkante der Decke
//   über einem Geschoss = Kote des Geschosses darüber, sonst Kote + Geschosshöhe (Regel von decke_erzeugen, Kern). Gibt es keine Decke im Modell, gilt deren Oberkante.

import { TABLET_PX, TABLET_KOPF_H, TABLET_ENDE } from './xr-greifen-logik.js';

export const TISCH = Object.freeze({ hoehe: 0.75, breite: 1.6, tiefe: 1.0, vorderkante: 0.4, rand: 0.1, halt: 0.05 });
/** Mitte der Tischplatte (Oberkante) im Raum. */
export const TISCH_MITTE = Object.freeze({ x: 0, y: TISCH.hoehe, z: -(TISCH.vorderkante + TISCH.tiefe / 2) });
export const RAUM = Object.freeze({ breite: 8, tiefe: 6, hoehe: 3.2, hinten: 1.5 });
export const MASSSTAB = Object.freeze({ start: 50, min: 20, max: 200 });
export const NAH_ABSTAND = 0.25;
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

/** Ist ein Punkt p (Raum-lokal) nahe genug am Modell, um zuzugreifen? groesse = { b, t, h } des Modells in m (unverkleinert). */
export function nahAmModell(p, lage, groesse, abstand = NAH_ABSTAND) {
  const s = 1 / lage.massstab, m = modellMitte(lage);
  const r = Math.max(0.5 * Math.hypot(groesse.b, groesse.t) * s, 0.12);
  const hoehe = (groesse.h ?? 0) * s;
  return Math.hypot(p.x - m.x, p.z - m.z) <= r + abstand && p.y >= TISCH.hoehe - 0.15 && p.y <= TISCH.hoehe + hoehe + 0.35;
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

export const gestenNeu = (lage) => ({ lage, modus: null, griff: null, besitzt: { links: false, rechts: false }, vorher: { links: false, rechts: false } });

/**
 * Ein Schritt der Gestenerkennung. haende = { links, rechts }, je null oder { griff, nah, x, z, gieren }. Eine Hand «besitzt» das Modell, wenn sie zufasst, während sie nah
 * ist (steigende Flanke), und bis sie loslässt. Bewegt wird das Modell **nur, solange beide Hände greifen** (Christian 21.10.2026 nach dem Brillentest: «greifen soll nur
 * mit zwei händen funktionieren und nur wenn gegriffen. zoomen nur wenn gegriffen.»): dann verschieben, drehen und zoomen; greift nur eine Hand, ruht das Modell
 * (modus 'wartet'). Jeder Wechsel setzt einen neuen Bezug, damit nichts springt. einhandStart/einhandSchritt bleiben für eine spätere Wahl erhalten.
 */
export function gestenSchritt(g, haende) {
  const besitzt = { links: false, rechts: false }, vorher = { links: false, rechts: false };
  for (const s of ['links', 'rechts']) {
    const h = haende?.[s];
    vorher[s] = !!h?.griff;
    besitzt[s] = !!h?.griff && (g.besitzt[s] || (!g.vorher[s] && !!h.nah));
  }
  const n = (besitzt.links ? 1 : 0) + (besitzt.rechts ? 1 : 0);
  const gleich = besitzt.links === g.besitzt.links && besitzt.rechts === g.besitzt.rechts;
  let { lage, griff } = g;
  const modus = n === 0 ? null : n === 1 ? 'wartet' : 'zwei';
  if (n < 2) griff = null;
  else if (!gleich || !griff) griff = zweihandStart(lage, haende.links, haende.rechts);
  else lage = zweihandSchritt(griff, haende.links, haende.rechts);
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
export const geschossText = (g) => { const k = String(g.kennzeichen ?? ''); const t = k.length <= 8 ? k : String(g.name || k); return t.length > 14 ? `${t.slice(0, 13)}…` : t; };

/**
 * Tasten der Tablet-Seite im Sitzungsraum (Bildpunkte wie tabletLayout): oben «Alle Etagen» und die Geschosse (oberstes zuerst, höchstens MAX_ETAGEN),
 * darunter «Decke zeigen», «Zurücksetzen», «IFC-Unterlage», dann «Begehen» und unten «VR beenden».
 */
export function sitzungsLayout(geschosse) {
  const rand = 14, spalten = 3, luecke = 8, fussH = 38;
  const eintraege = [{ id: 'alle', text: 'Alle Etagen', umschalter: true }, ...[...geschosse].reverse().slice(0, MAX_ETAGEN).map((g) => ({ id: `g:${g.kennzeichen}`, text: geschossText(g), umschalter: true }))];
  const etagenZeilen = Math.ceil(eintraege.length / spalten);
  const zeilen = etagenZeilen + 2;
  const raumH = TABLET_PX.h - TABLET_KOPF_H - fussH - rand;
  const bw = (TABLET_PX.b - 2 * rand - (spalten - 1) * luecke) / spalten, bh = (raumH - (zeilen - 1) * luecke) / zeilen;
  const zelle = (spalte, zeile, breit = 1) => ({ x: rand + spalte * (bw + luecke), y: TABLET_KOPF_H + zeile * (bh + luecke), w: bw * breit + luecke * (breit - 1), h: bh });
  const tasten = eintraege.map((e, i) => ({ ...e, ...zelle(i % spalten, Math.floor(i / spalten)) }));
  tasten.push({ id: 'decke', text: 'Decke zeigen', umschalter: true, ...zelle(0, etagenZeilen) });
  tasten.push({ id: 'zuruecksetzen', text: 'Zurücksetzen', ...zelle(1, etagenZeilen) });
  tasten.push({ id: 'ifc', text: 'IFC-Unterlage', umschalter: true, ...zelle(2, etagenZeilen) });
  tasten.push({ id: 'raum', text: 'Begehen', ...zelle(0, etagenZeilen + 1, 3) });
  tasten.push({ ...TABLET_ENDE, x: rand, y: TABLET_PX.h - fussH - rand / 2, w: TABLET_PX.b - 2 * rand, h: fussH - 4 });
  return tasten;
}

/** Zustand der Umschalter für die Tasten: gewählte Etage, «Decke zeigen», IFC. */
export function sitzungsTasten(wahl, ifcAn) {
  const z = { alle: !wahl.geschoss, decke: !!wahl.deckeZeigen, ifc: !!ifcAn };
  if (wahl.geschoss) z[`g:${wahl.geschoss}`] = true;
  return z;
}
