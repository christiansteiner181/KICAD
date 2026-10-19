// Netzdatei einer IFC-Unterlage lesen (Modul 1b, C2). Reine Funktionen ohne DOM und ohne three.js (in Node testbar).
// Der Kern schreibt die Datei (Hlks.Kern/Import/IfcNetz.cs, IfcNetzErzeuger); GET /api/unterlagen/{id}/netz liefert sie roh.
//
// Format (binär, little-endian; dieselbe Beschreibung steht im Dateikopf):
//   8 Byte «HLKSNETZ», int32 Fassung (1, 2 oder 3), int32 Länge L, L Byte Beschreibung (UTF-8), int32 Anzahl Datensätze N,
//   6 × float64 Begrenzung (minx, miny, minz, maxx, maxy, maxz, mm),
//   je Datensatz: int32 EntityLabel, 22 Byte GlobalId (ASCII), uint8 n + n Byte Entität (ASCII), float64 Geschosskote mm (NaN = ohne),
//   uint8 Kennzeichen (1 = übernommen, 0 = Hintergrund),
//   [nur Fassung 2 (Modul 3b): uint8 Rolle (0 = bauteil, 1 = teil, 2 = oeffnung), 22 Byte Bezug (GlobalId des Ganzen bzw. Wirts,
//   sonst Leerzeichen), uint8 f + f × 22 Byte GlobalIds der Füllungen],
//   [nur Fassung 3 (Nachtrag B3c, Material): uint8 m + m × (uint16 L + L Byte UTF-8) Materialnamen, uint16 L + L Byte UTF-8 Typname,
//   uint16 L + L Byte UTF-8 Fundstelle des Materials (L = 0: keine Angabe)],
//   6 × float32 Begrenzung, int32 Punkte p, int32 Dreiecke d,
//   p × 3 float32 Punkte (x Ost, y Nord, z, mm), d × 3 int32 Dreiecke (Punktindizes je Produkt, 0-basiert).
// Öffnungen (Rolle oeffnung) werden nie gezeichnet – die Bauteilnetze sind nicht ausgeschnitten, Öffnungen dienen nur der
// Kollisionsprüfung. Teile (Rolle teil) zeichnen sich wie in Fassung 1 nach ihrem Kennzeichen.
//
// Für die Anzeige führt `leseNetz` die gewünschten Produkte zu EINEM Netz zusammen (eine Geometrie statt Objekte je
// Produkt, damit auch 1.7 Mio. Dreiecke flüssig drehbar bleiben). Die Punkte kommen dabei gleich in three-Koordinaten
// (Meter; x = x, y = z, z = −y – wie koerper3d.js). Das Netz ist nicht orientiert: gezeichnet wird beidseitig.

export const KENNUNG = 'HLKSNETZ';
export const GLOBALID_LAENGE = 22;
/** Fassungen, die leseNetz kennt (der Kern schreibt die höchste: IfcNetzErzeuger.Fassung). */
export const FASSUNGEN = Object.freeze([1, 2, 3]);
/** Rollen der Datensätze nach ihrer Nummer (Fassung 2; Fassung 1: immer bauteil). */
export const ROLLEN = Object.freeze(['bauteil', 'teil', 'oeffnung']);
const M = 1 / 1000;

/** Welche Produkte ins zusammengeführte Netz kommen: Hintergrund (Kennzeichen 0), übernommene (1) oder alle. */
export const AUSWAHL = Object.freeze({ hintergrund: 'hintergrund', uebernommen: 'uebernommen', alle: 'alle' });

class NetzFehler extends Error {}

/**
 * Liest eine Netzdatei.
 * @param {ArrayBuffer|ArrayBufferView} daten  Inhalt der Datei
 * @param {{ nur?: 'hintergrund'|'uebernommen'|'alle' }} [optionen]  nur: Vorgabe «hintergrund»
 * @returns {{ fassung:number, beschreibung:string, begrenzung:number[], produkte:object[], punkte:number, dreiecke:number,
 *             positionen:Float32Array, indizes:Uint32Array }}
 *   produkte: Kopfdaten ALLER Datensätze (label, globalId, entitaet, kote, uebernommen, rolle, bezug, fuellungen, begrenzung,
 *   punkte, dreiecke, materialien, typname, materialquelle; Fassung 1: rolle «bauteil», bezug null, fuellungen [];
 *   Fassungen 1 und 2: materialien [], typname und materialquelle null);
 *   positionen/indizes: nur die gewählten Produkte, nie Öffnungen, zusammengeführt (positionen in three-Metern).
 *   Wirft Error mit deutscher Meldung bei unlesbarer oder abgeschnittener Datei.
 */
export function leseNetz(daten, optionen = {}) {
  const nur = optionen.nur ?? AUSWAHL.hintergrund;
  if (!Object.values(AUSWAHL).includes(nur)) throw new NetzFehler(`Unbekannte Auswahl «${nur}» (erlaubt: hintergrund, uebernommen, alle).`);
  const bytes = ArrayBuffer.isView(daten) ? new Uint8Array(daten.buffer, daten.byteOffset, daten.byteLength) : new Uint8Array(daten);
  const dv = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const ende = bytes.byteLength;
  let pos = 0;
  const brauche = (n, was) => { if (pos + n > ende) throw new NetzFehler(`Die Netzdatei ist abgeschnitten (${was} bei Byte ${pos} von ${ende}).`); };
  const i32 = (was) => { brauche(4, was); const v = dv.getInt32(pos, true); pos += 4; return v; };
  const ascii = (n) => { let s = ''; for (let i = 0; i < n; i++) s += String.fromCharCode(bytes[pos + i]); pos += n; return s; };
  const utf8 = new TextDecoder('utf-8');
  const text16 = (was) => {   // Fassung 3: uint16 Länge + UTF-8; leer → null
    brauche(2, was); const n = dv.getUint16(pos, true); pos += 2;
    brauche(n, was); const t = utf8.decode(bytes.subarray(pos, pos + n)).trim(); pos += n;
    return t || null;
  };

  brauche(8, 'Kennung');
  if (ascii(8) !== KENNUNG) throw new NetzFehler('Das ist keine HLKS-Netzdatei (Kennung «HLKSNETZ» fehlt).');
  const fassung = i32('Fassung');
  if (!FASSUNGEN.includes(fassung)) throw new NetzFehler(`Netzdatei Fassung ${fassung} wird nicht unterstützt (bekannt: ${FASSUNGEN.join(', ')}).`);
  const l = i32('Länge der Beschreibung');
  if (l < 0) throw new NetzFehler('Die Netzdatei ist beschädigt (Länge der Beschreibung).');
  brauche(l, 'Beschreibung');
  const beschreibung = new TextDecoder('utf-8').decode(bytes.subarray(pos, pos + l));
  pos += l;
  const anzahl = i32('Anzahl Produkte');
  if (anzahl < 0) throw new NetzFehler('Die Netzdatei ist beschädigt (Anzahl Produkte).');
  brauche(48, 'Begrenzung');
  const begrenzung = [];
  for (let i = 0; i < 6; i++) { begrenzung.push(dv.getFloat64(pos, true)); pos += 8; }

  // Erster Durchgang: Köpfe lesen und Punkte/Dreiecke überspringen; so ist die Grösse des Ergebnisses vorab bekannt.
  const produkte = [];
  const gewaehlt = [];   // { punkteAb, dreieckeAb, p, d }
  let summeP = 0, summeD = 0;
  for (let n = 0; n < anzahl; n++) {
    const label = i32('Produkt');
    brauche(GLOBALID_LAENGE + 1, 'GlobalId');
    const globalId = ascii(GLOBALID_LAENGE);
    const el = bytes[pos++];
    brauche(el + 8 + 1, 'Produktkopf');
    const entitaet = ascii(el);
    const kote = dv.getFloat64(pos, true); pos += 8;
    const uebernommen = bytes[pos++] === 1;
    let rolle = 'bauteil', bezug = null;
    const fuellungen = [];
    if (fassung >= 2) {
      brauche(1 + GLOBALID_LAENGE + 1, 'Rolle und Bezug');
      const r = bytes[pos++];
      if (r >= ROLLEN.length) throw new NetzFehler(`Die Netzdatei ist beschädigt (Produkt ${n + 1}: unbekannte Rolle ${r}).`);
      rolle = ROLLEN[r];
      bezug = ascii(GLOBALID_LAENGE).trim() || null;
      const f = bytes[pos++];
      brauche(f * GLOBALID_LAENGE, 'Füllungen');
      for (let i = 0; i < f; i++) fuellungen.push(ascii(GLOBALID_LAENGE).trim());
    }
    const materialien = [];
    let typname = null, materialquelle = null;
    if (fassung >= 3) {
      brauche(1, 'Materialien');
      const mz = bytes[pos++];
      for (let i = 0; i < mz; i++) { const t = text16('Materialname'); if (t) materialien.push(t); }
      typname = text16('Typname');
      materialquelle = text16('Fundstelle des Materials');
    }
    brauche(24 + 8, 'Produktkopf');
    const bb = [];
    for (let i = 0; i < 6; i++) { bb.push(dv.getFloat32(pos, true)); pos += 4; }
    const p = i32('Punktzahl'), d = i32('Dreieckszahl');
    if (p < 0 || d < 0) throw new NetzFehler(`Die Netzdatei ist beschädigt (Produkt ${n + 1}: negative Anzahl).`);
    brauche(12 * p + 12 * d, `Punkte und Dreiecke von Produkt ${n + 1}`);
    produkte.push({ label, globalId, entitaet, kote: Number.isNaN(kote) ? null : kote, uebernommen, rolle, bezug, fuellungen, materialien, typname, materialquelle, begrenzung: bb, punkte: p, dreiecke: d });
    const mit = rolle !== 'oeffnung' && (nur === AUSWAHL.alle || (nur === AUSWAHL.uebernommen ? uebernommen : !uebernommen));
    if (mit && p > 0 && d > 0) { gewaehlt.push({ punkteAb: pos, dreieckeAb: pos + 12 * p, p, d }); summeP += p; summeD += d; }
    pos += 12 * p + 12 * d;
  }

  // Zweiter Durchgang: zusammenführen, Punkte nach three (Meter), Indizes um den Versatz des Produkts verschieben
  const positionen = new Float32Array(summeP * 3);
  const indizes = new Uint32Array(summeD * 3);
  let vp = 0, vi = 0, versatz = 0;
  for (const g of gewaehlt) {
    for (let k = 0, o = g.punkteAb; k < g.p; k++, o += 12) {
      positionen[vp++] = dv.getFloat32(o, true) * M;          // x
      const y = dv.getFloat32(o + 4, true), z = dv.getFloat32(o + 8, true);
      positionen[vp++] = z * M;                                // three-y = Höhe z
      positionen[vp++] = -y * M;                               // three-z = −y
    }
    for (let k = 0, o = g.dreieckeAb; k < g.d * 3; k++, o += 4) {
      const i = dv.getInt32(o, true);
      indizes[vi++] = i >= 0 && i < g.p ? versatz + i : versatz;   // ungültiger Index → entartetes Dreieck statt Fremdpunkt
    }
    versatz += g.p;
  }
  return { fassung, beschreibung, begrenzung, produkte, punkte: summeP, dreiecke: summeD, positionen, indizes };
}
