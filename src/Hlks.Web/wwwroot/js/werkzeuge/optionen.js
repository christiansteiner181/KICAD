// Optionen der Zeichen- und Ändern-Werkzeuge (Optionsleiste wie in Revit): Felddefinitionen je Werkzeug, aktuelle Werte,
// Merken über Sitzungen (localStorage über `merken`, darf nie stören). Keine Fachlogik und keine Normwerte:
// Pflichtfelder wie die Wanddicke haben keine Vorgabe – beim ersten Mal sind sie leer, danach gilt der zuletzt benutzte Wert.
//
// Feld: { id, titel, art: 'zahl'|'text'|'auswahl'|'bool', einheit?, pflicht?, platzhalter?, vorgabe?, werte?: [[wert, text]],
//         katalog?: [klasse, eigenschaft] (Auswahl aus dem Katalog), breite? (Zeichen), fluechtig? (nicht merken) }
// Annahmen: Raster 100 mm, Tiefe eines Schnitts 10000 mm (Vorgaben aus dem Auftrag, keine Norm).

import { merken, derKlasse } from '../zustand.js';
import { zahlAusText, RASTER_MM } from './fangen.js';
import { KALIBRIERBAR } from './kalibrierung.js';
import { familieFelder, definitionAus, typTeile } from '../familien/typauswahl.js';
import { serienListe, serieDef, serieLaden, haken as leitungHaken } from './leitung-daten.js';
import { groessenListe } from './leitung-logik.js';

const BEZUGSLINIEN = [['mitte', 'Mitte'], ['links', 'Links'], ['rechts', 'Rechts']];

/** Felder, die in jeder Zeichen-Optionsleiste hinten stehen. */
export const FANGFELDER = [{ id: 'raster', titel: 'Raster', art: 'zahl', einheit: 'mm', vorgabe: RASTER_MM, breite: 5, global: true }];

export const WERKZEUGE = {
  wand: {
    titel: 'Wand',
    felder: [
      { id: 'dicke', titel: 'Dicke', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'Pflicht', breite: 6 },
      { id: 'hoehe', titel: 'Höhe', art: 'zahl', einheit: 'mm', platzhalter: 'Geschosshöhe', breite: 9 },
      { id: 'bezugslinie', titel: 'Bezugslinie', art: 'auswahl', werte: BEZUGSLINIEN, vorgabe: 'mitte' },
      { id: 'kette', titel: 'Kette', art: 'bool', vorgabe: true },
      { id: 'material', titel: 'Material', art: 'auswahl', katalog: ['Wand', 'material'], leer: 'nicht gesetzt' },
    ],
  },
  tuer: {
    titel: 'Tür',
    felder: [
      { id: 'breite', titel: 'Breite', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'Pflicht', breite: 6 },
      { id: 'hoehe', titel: 'Höhe', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'Pflicht', breite: 6 },
      { id: 'seite', titel: 'Aufschlag', art: 'auswahl', werte: [['links', 'Links'], ['rechts', 'Rechts']], vorgabe: 'links' },
      { id: 'band', titel: 'Band', art: 'auswahl', werte: [['anfang', 'zum Wandanfang'], ['ende', 'zum Wandende']], vorgabe: 'anfang' },
    ],
    hinweis: 'Leertaste dreht den Aufschlag, Umschalt+Leertaste die Bandseite.',
  },
  fenster: {
    titel: 'Fenster',
    felder: [
      { id: 'breite', titel: 'Breite', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'Pflicht', breite: 6 },
      { id: 'hoehe', titel: 'Höhe', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'Pflicht', breite: 6 },
      { id: 'bruestung', titel: 'Brüstung', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'Pflicht', breite: 6 },
    ],
  },
  raum: {
    titel: 'Raum',
    felder: [
      { id: 'nummer', titel: 'Nummer', art: 'text', breite: 7, fluechtig: true },
      { id: 'nutzung', titel: 'Nutzung', art: 'text', breite: 10 },
    ],
  },
  achse: {
    titel: 'Achse',
    felder: [
      { id: 'art', titel: 'Bezeichnung', art: 'auswahl', werte: [['buchstaben', 'A, B, C …'], ['zahlen', '1, 2, 3 …']], vorgabe: 'buchstaben' },
      { id: 'bezeichnung', titel: 'Nächste', art: 'text', breite: 5, fluechtig: true },
    ],
  },
  bemassung: {
    titel: 'Bemassung',
    felder: [
      { id: 'richtung', titel: 'Richtung', art: 'auswahl', werte: [['ausgerichtet', 'ausgerichtet'], ['horizontal', 'horizontal'], ['vertikal', 'vertikal']], vorgabe: 'ausgerichtet' },
      { id: 'kette', titel: 'Kette', art: 'bool', vorgabe: false },
    ],
    hinweis: 'Messpunkte anklicken, dann die Lage der Masslinie.',
  },
  text: {
    titel: 'Text',
    felder: [
      { id: 'groesse', titel: 'Schrifthöhe', art: 'zahl', einheit: 'mm', platzhalter: 'Vorgabe', breite: 5 },
      { id: 'ausrichtung', titel: 'Ausrichtung', art: 'auswahl', katalog: ['Textnotiz', 'ausrichtung'], leer: 'Vorgabe' },
      { id: 'winkel', titel: 'Winkel', art: 'zahl', einheit: '°', platzhalter: '0', breite: 4 },
    ],
  },
  linie: {
    titel: 'Detaillinie',
    felder: [
      { id: 'stift', titel: 'Stift', art: 'auswahl', katalog: ['Detaillinie', 'stift'], leer: 'Vorgabe' },
      { id: 'linienart', titel: 'Linienart', art: 'auswahl', katalog: ['Detaillinie', 'linienart'], leer: 'Vorgabe' },
    ],
    hinweis: 'Punkte anklicken, Enter oder Doppelklick beendet.',
  },
  fuellbereich: {
    titel: 'Füllbereich',
    felder: [
      { id: 'muster', titel: 'Muster', art: 'auswahl', katalog: ['Fuellbereich', 'muster'], leer: 'Vorgabe' },
      { id: 'farbe', titel: 'Farbe', art: 'text', platzhalter: '#rrggbb', breite: 7 },
      { id: 'rand', titel: 'Rand', art: 'bool', vorgabe: true },
    ],
    hinweis: 'Eckpunkte anklicken, Enter oder Doppelklick beendet.',
  },
  kote: {
    titel: 'Höhenkote',
    felder: [
      { id: 'z', titel: 'Höhe z', art: 'zahl', einheit: 'mm', platzhalter: 'Geschosskote', breite: 8 },
      { id: 'wert', titel: 'Fester Wert', art: 'zahl', einheit: 'mm', platzhalter: 'gemessen', breite: 8 },
      { id: 'zusatz', titel: 'Zusatz', art: 'text', platzhalter: 'z. B. OK FB', breite: 9 },
    ],
  },
  nordpfeil: {
    titel: 'Nordpfeil',
    felder: [
      { id: 'winkel', titel: 'Winkel', art: 'zahl', einheit: '°', platzhalter: '0', breite: 4 },
      { id: 'groesse', titel: 'Grösse', art: 'zahl', einheit: 'mm', platzhalter: 'Vorgabe', breite: 4 },
    ],
  },
  schnitt: {
    titel: 'Schnitt',
    felder: [
      { id: 'tiefe', titel: 'Tiefe', art: 'zahl', einheit: 'mm', vorgabe: 10000, breite: 7 },
      { id: 'name', titel: 'Name', art: 'text', platzhalter: 'automatisch', breite: 10, fluechtig: true },
    ],
    hinweis: 'Zwei Punkte der Schnittlinie, dann die Blickseite anklicken.',
    ohneRaster: false,
  },
  kalibrieren: {
    titel: 'Kalibrieren',
    felder: [
      // Unterlagen des Projekts (DWG, DXF, PDF, Bild); die Liste wird beim Zeichnen der Optionsleiste frisch gelesen
      { id: 'unterlage', titel: 'Unterlage', art: 'auswahl', leer: 'wählen …', fluechtig: true,
        get werte() {
          return derKlasse('Unterlage').filter((u) => KALIBRIERBAR.has(u.werte?.art))
            .map((u) => [u.kennzeichen, `${u.name || u.kennzeichen} (${String(u.werte?.art).toUpperCase()}${u.geschoss ? ' · ' + u.geschoss : ''})`]);
        } },
      { id: 'nurVerschieben', titel: 'Nur verschieben', art: 'bool', vorgabe: false, fluechtig: true },
    ],
    hinweis: 'Zwei Punkte auf der Unterlage, dann die zwei Zielpunkte (anklicken oder Enter für Koordinaten).',
  },
  // Familie platzieren (Modul 2, C1): Typauswahl «Familie: Typ», Drehung und Exemplarparameter der gewählten Familie (Felder aus familien/typauswahl.js)
  familie: {
    titel: 'Familie platzieren',
    get felder() { return familieFelder(speicher.familie?.typ, definitionAus(typTeile(speicher.familie?.typ)?.familie)); },
    hinweis: 'Klick setzt den Einfügepunkt, Leertaste dreht um 90°.',
  },
  // Leitungen zeichnen (Modul 3, C1): Rohr (PI) und Kanal (DT)
  rohr: { titel: 'Rohr', get felder() { return leitungFelder('rohr', 'Rohr'); }, hinweis: 'H = Höhe ändern (Steigleitung), Zahl = Länge, Enter beendet.' },
  kanal: { titel: 'Kanal', get felder() { return leitungFelder('kanal', 'Kanal'); }, hinweis: 'H = Höhe ändern (Steigleitung), Zahl = Länge, Enter beendet.' },
  verschieben: { titel: 'Verschieben', felder: [], hinweis: 'Basispunkt, dann Zielpunkt anklicken. Strg+Klick wählt mehrere Elemente.' },
  kopieren: {
    titel: 'Kopieren',
    felder: [{ id: 'anzahl', titel: 'Anzahl', art: 'zahl', vorgabe: 1, breite: 3, fluechtig: true }],
    hinweis: 'Basispunkt, dann Zielpunkt anklicken; jede Kopie liegt um denselben Abstand weiter.',
  },
  drehen: { titel: 'Drehen', felder: [], hinweis: 'Drehpunkt anklicken, dann den Winkel mit zwei Klicks setzen oder in Grad eingeben (Enter).' },
  spiegeln: { titel: 'Spiegeln', felder: [], hinweis: 'Zwei Punkte der Spiegelachse anklicken.' },
};

// ------------------------------------------------------------------------------------------------ Leitungen (Modul 3, C1)

const kz = (e) => e.kennzeichen;
const systemOptionen = () => derKlasse('System').sort((a, b) => kz(a).localeCompare(kz(b), 'de', { numeric: true })).map((e) => [kz(e), e.name ? `${kz(e)} – ${e.name}` : kz(e)]);
const strangOptionen = (wid) => derKlasse('Strang').filter((e) => !wert(wid, 'system') || !e.teilVon || e.teilVon === wert(wid, 'system'))
  .sort((a, b) => kz(a).localeCompare(kz(b), 'de', { numeric: true })).map((e) => [kz(e), e.teilVon ? `${kz(e)} (${e.teilVon})` : kz(e)]);

/** Felder der Werkzeuge Rohr (PI) und Kanal (DT): Serie, Grösse, Höhe der Achse über OK Geschoss, Gefälle, System/Strang, Dämmdicke, Winkelfang. */
function leitungFelder(wid, kategorie) {
  const nachSerie = (el) => {
    el?.blur?.();
    const s = serieDef(wert(wid, 'serie'));
    if (wert(wid, 'serie')) serieLaden(wert(wid, 'serie'));
    if (wert(wid, 'groesse') && s && !groessenListe(s).some((g) => g.typ === wert(wid, 'groesse'))) setze(wid, 'groesse', '');
    leitungHaken.optionenNeu?.();
  };
  return [
    { id: 'serie', titel: 'Serie', art: 'auswahl', get werte() { return serienListe(kategorie).map((s) => [s.kz, s.name]); }, leer: 'wählen …', beiAenderung: nachSerie },
    { id: 'groesse', titel: 'Grösse', art: 'auswahl', leer: 'wählen …',
      get werte() {
        const s = serieDef(wert(wid, 'serie'));
        return s ? groessenListe(s).map((g) => [g.typ, g.typ]) : (serienListe(kategorie).find((x) => x.kz === wert(wid, 'serie'))?.typen ?? []).map((t) => [t, t]);
      } },
    { id: 'hoehe', titel: 'Höhe', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'ab OK Geschoss', breite: 12,
      beiAenderung: () => leitungHaken.hoeheGeaendert?.() },
    { id: 'gefaelle', titel: 'Gefälle', art: 'zahl', einheit: '%', platzhalter: 'kein', breite: 4, fluechtig: true },
    { id: 'system', titel: 'System', art: 'auswahl', get werte() { return systemOptionen(); }, leer: '–',
      beiAenderung(el) { el?.blur?.(); const st = derKlasse('Strang').find((e) => kz(e) === wert(wid, 'strang')); if (st && st.teilVon !== wert(wid, 'system')) setze(wid, 'strang', ''); leitungHaken.optionenNeu?.(); } },
    { id: 'strang', titel: 'Strang', art: 'auswahl', get werte() { return strangOptionen(wid); }, leer: '–',
      beiAenderung(el) { el?.blur?.(); const st = derKlasse('Strang').find((e) => kz(e) === wert(wid, 'strang')); if (st?.teilVon) setze(wid, 'system', st.teilVon); leitungHaken.optionenNeu?.(); } },
    { id: 'daemmdicke', titel: 'Dämmdicke', art: 'zahl', einheit: 'mm', platzhalter: 'keine', breite: 4, fluechtig: true },
    { id: 'winkelfang', titel: 'Winkelfang', art: 'bool', vorgabe: true },
    // Keine Serie der Kategorie im Projekt: Hinweis mit Weg zu den Familien
    { id: 'serieHinweis', art: 'hinweis', text: `Keine Leitungsserie der Kategorie ${kategorie} im Projekt.`, sichtbar: () => serienListe(kategorie).length === 0 },
    { id: 'serieLaden', art: 'knopf', titel: 'Leitungsserie laden …', aktion: 'familie.laden', sichtbar: () => serienListe(kategorie).length === 0 },
  ];
}

// ------------------------------------------------------------------------------------------------ Werte

const speicher = {};   // werkzeug-id → { feld: wert }
let geladen = false;

function laden() {
  if (geladen) return;
  geladen = true;
  const alt = merken.lesen('werkzeugOptionen', {});
  if (alt && typeof alt === 'object') for (const [k, v] of Object.entries(alt)) speicher[k] = { ...v };
}

function sichern() {
  const rein = {};
  for (const [wid, felder] of Object.entries(speicher)) {
    const def = wid === 'fangen' ? FANGFELDER : WERKZEUGE[wid]?.felder ?? [];
    const nurGemerkte = {};
    for (const [fid, v] of Object.entries(felder)) if (!def.find((f) => f.id === fid)?.fluechtig) nurGemerkte[fid] = v;
    if (Object.keys(nurGemerkte).length) rein[wid] = nurGemerkte;
  }
  merken.schreiben('werkzeugOptionen', rein);
}

export function feldDef(wid, fid) {
  return (wid === 'fangen' ? FANGFELDER : [...(WERKZEUGE[wid]?.felder ?? []), ...FANGFELDER]).find((f) => f.id === fid) ?? null;
}

/** Aktueller Wert eines Feldes (gesetzt › Vorgabe › null bzw. false bei bool). Zahlfelder liefern Zahl oder null. */
export function wert(wid, fid) {
  laden();
  const f = feldDef(wid, fid);
  const ort = f?.global ? 'fangen' : wid;
  const w = speicher[ort]?.[fid];
  if (w !== undefined) return w;
  if (f?.vorgabe !== undefined) return f.vorgabe;
  return f?.art === 'bool' ? false : f?.art === 'zahl' ? null : '';
}

/** Setzt einen Wert (Zahlfelder: Zahl oder null). */
export function setze(wid, fid, v) {
  laden();
  const f = feldDef(wid, fid);
  const ort = f?.global ? 'fangen' : wid;
  (speicher[ort] ??= {})[fid] = v;
  sichern();
}

/** Alle Werte eines Werkzeugs (einschliesslich Raster) als Objekt. */
export function werte(wid) {
  const o = {};
  for (const f of [...(WERKZEUGE[wid]?.felder ?? []), ...FANGFELDER]) o[f.id] = wert(wid, f.id);
  return o;
}

export function rasterWert() { const r = wert('fangen', 'raster'); return r === null ? 0 : Math.max(0, r); }

/** Text einer Eingabe → Wert des Feldes. */
export function ausText(f, text) {
  if (f.art === 'zahl') return zahlAusText(text);
  return String(text ?? '').trim();
}

// ------------------------------------------------------------------------------------------------ Auswahl aus dem Katalog

const katalogCache = new Map();   // «Klasse.eigenschaft» → [wert]

/** Auswahlwerte aus dem Katalog (zustand.katalog hat nur Kurzform; die volle Klasse holt `lade`). Leer, solange nicht geladen. */
export function katalogWerte(klasse, eigenschaft) {
  return katalogCache.get(`${klasse}.${eigenschaft}`) ?? [];
}

/** Lädt die Katalogauswahlen aller Felder eines Werkzeugs; ruft danach `fertig` (Optionsleiste neu zeichnen). */
export async function katalogLaden(wid, abfrage, fertig) {
  const fehlend = (WERKZEUGE[wid]?.felder ?? []).filter((f) => f.katalog && !katalogCache.has(`${f.katalog[0]}.${f.katalog[1]}`));
  if (fehlend.length === 0) return;
  let neu = false;
  for (const f of fehlend) {
    const [klasse, eig] = f.katalog;
    try {
      const k = await abfrage('katalog', { klasse });
      const e = (k?.eigenschaften ?? []).find((x) => x.name === eig);
      katalogCache.set(`${klasse}.${eig}`, Array.isArray(e?.auswahl) ? e.auswahl : []);
      neu = true;
    } catch (err) { console.warn('Katalogauswahl', klasse, eig, err?.message ?? err); katalogCache.set(`${klasse}.${eig}`, []); }
  }
  if (neu) fertig?.();
}

