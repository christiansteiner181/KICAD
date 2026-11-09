// Optionen der Zeichen- und Ändern-Werkzeuge (Optionsleiste wie in Revit): Felddefinitionen je Werkzeug, aktuelle Werte,
// Merken über Sitzungen (localStorage über `merken`, darf nie stören). Keine Fachlogik und keine Normwerte:
// Pflichtfelder wie die Wanddicke haben keine Vorgabe – beim ersten Mal sind sie leer, danach gilt der zuletzt benutzte Wert.
//
// Feld: { id, titel, art: 'zahl'|'text'|'auswahl'|'bool', einheit?, pflicht?, platzhalter?, vorgabe?, werte?: [[wert, text]],
//         katalog?: [klasse, eigenschaft] (Auswahl aus dem Katalog), breite? (Zeichen), fluechtig? (nicht merken) }
// Annahmen: Raster 100 mm, Tiefe eines Schnitts 10000 mm (Vorgaben aus dem Auftrag, keine Norm).

import { merken, derKlasse, zustand, element } from '../zustand.js';
import { zahlAusText, RASTER_MM } from './fangen.js';
import { KALIBRIERBAR } from './kalibrierung.js';
import { familieFelder, definitionAus, typTeile } from '../familien/typauswahl.js';
import { serienListe, serieDef, serieLaden, haken as leitungHaken } from './leitung-daten.js';
import { groessenListe } from './leitung-logik.js';
import { elementWerte } from './vorwand-typen.js';
import { istFrei, istRechteck, istAbwasser, groessenOptionen, vorbelegung, freiHinweis, abwasserHinweis, beschriftungVon } from './leitung-frei.js';
import { lagenFuer, ukdFuer } from './lagen-daten.js';

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
  vorwand: {   // Geberit Vorwand (04.11.2026): System, Tiefe, Höhe Pflicht; Bereiche im Hinweis (Planungshandbuch S. 175, 250)
    titel: 'Vorwand',
    felder: [
      { id: 'system', titel: 'System', art: 'auswahl', werte: [['gis', 'Geberit GIS'], ['duofix', 'Geberit Duofix']], vorgabe: 'gis' },
      // kein festes Tiefenfeld (Christian 09.11.2026: «der tiefenwert aus dem klick wird nicht übernommen» – ein gemerkter Wert überstimmte den Klick);
      // die Tiefe kommt aus dem Klick oder aus einer eingetippten Zahl
      { id: 'ausfuehrung', titel: 'Ausführung', art: 'auswahl', werte: [['teilhoch', 'teilhoch'], ['raumhoch', 'raumhoch']], vorgabe: 'teilhoch' },
      { id: 'hoehe', titel: 'Höhe', art: 'zahl', einheit: 'mm', platzhalter: 'teilhoch Pflicht', breite: 7 },
      { id: 'bezugslinie', titel: 'Lage', art: 'auswahl', werte: [['links', 'an der Rohbauwand'], ['mitte', 'frei stehend']], vorgabe: 'links' },
      { id: 'bauwerk', titel: 'Bauwerk', art: 'auswahl', werte: [['massiv', 'massiv'], ['trockenbau', 'Trockenbau']], vorgabe: 'massiv' },
      { id: 'bodenaufbau', titel: 'Bodenaufbau', art: 'zahl', einheit: 'mm', platzhalter: '0', breite: 5 },
      { id: 'bauweise', titel: 'Bauweise', art: 'auswahl', werte: [['', 'nach System'], ['vorgefertigt', 'vorgefertigt'], ['vor_ort', 'vor Ort']], vorgabe: '' },
      { id: 'bestuecken', titel: 'Bestücken', art: 'bool', vorgabe: true },
      // Christian 09.11.2026: «nach dem absetzen der Vorwandlänge sollte mit dem nächsten mausklick die tiefe abgegriffen werden» → Kette aus;
      // eigene id, damit ein gemerktes «kette: an» nicht weiter gilt
      { id: 'mehrteilig', titel: 'Kette', art: 'bool', vorgabe: false },
    ],
    hinweis: '1. Aussenkante: Anfangs- und Endpunkt anklicken, 2. nächster Klick greift die Tiefe ab – die Seite des Klicks gibt die Ausrichtung (oder Tiefe eintippen, Maus auf der Wandseite). Mit «Kette»: weitere Punkte, Enter beendet die Kante. Tiefe inkl. Beplankung: GIS 100–700 mm, Duofix 135–220 mm (420 mit Verlängerung). Höhe ab OK Fertigboden; raumhoch ohne Höhe = Raumhöhe.',
  },
  montageelement: {
    titel: 'Montageelement',
    felder: [
      { id: 'element', titel: 'Element', art: 'auswahl', get werte() { return elementWerte(); }, leer: 'wählen …' },
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
  // Fallleitung (Christian 06.11.2026): Serie, Grösse, System wie beim Rohr; Höhe, Lage und Gefälle entfallen (senkrecht durch alle Geschosse)
  // Christian 06.11.2026: «eine fallleitung ist immer abwasser» – nur Abwasser-Serien, kein System/Medium (der Kern nimmt das Schmutzwasser-System)
  fallleitung: {
    titel: 'Fallleitung',
    get felder() {
      return leitungFelder('fallleitung', 'Rohr').filter((f) => !['lage', 'hoehe', 'gefaelle', 'winkelfang', 'daemmdicke', 'system', 'medium'].includes(f.id))
        .map((f) => (f.id === 'serie' ? { ...f, get werte() { return serienListe('Rohr').filter((x) => { const d = serieDef(x.kz); return !d || istAbwasser(d); }).map((x) => [x.kz, x.name]); } } : f));
    },
    hinweis: 'Immer Abwasser (System Schmutzwasser, Produktlinien Abwasser). Lage anklicken – senkrecht durch alle Geschosse, je Geschoss ein Rohr, verbunden; Abgänge daran mit 88.5°.',
  },
  steigleitung: { titel: 'Steigleitung', get felder() { return steigFelder('steigleitung'); }, hinweis: 'Leitungen anhaken, je Produktlinie und Grösse; Steigzone Breite × Tiefe (Abstände daraus) oder Achsabstand. 1. Klick: Anfang der Steigzone bzw. erste Leitung, 2. Klick: Richtung. Zirk und RL fallen, die übrigen steigen.' },
  steigleitungLueftung: { titel: 'Steigleitung Lüftung', get felder() { return steigFelder('steigleitungLueftung'); }, hinweis: 'AUL, FOL, ZUL, ABL mit Produktlinie rund oder eckig; Steigzone Breite × Tiefe oder Achsabstand. Zentrale unten: ZUL und FOL steigen, ABL und AUL fallen (oben umgekehrt).' },
  verschieben: {
    titel: 'Verschieben',
    felder: [{ id: 'trennen', titel: 'Trennen', art: 'bool', vorgabe: false }],   // Bearbeitungswerkzeuge A1: ohne folgen Leitungen und Formteile
    hinweis: 'Basispunkt, dann Zielpunkt anklicken. Strg+Klick wählt mehrere Elemente. «Trennen» löst die Verbindungen nach aussen, sonst folgen Leitungen und Formteile.',
  },
  kopieren: {
    titel: 'Kopieren',
    felder: [{ id: 'anzahl', titel: 'Anzahl', art: 'zahl', vorgabe: 1, breite: 3, fluechtig: true }, { id: 'mehrfach', titel: 'Mehrfach', art: 'bool', vorgabe: false }],   // B6: Mehrfach
    hinweis: 'Basispunkt, dann Zielpunkt anklicken; jede Kopie liegt um denselben Abstand weiter.',
  },
  drehen: { titel: 'Drehen', felder: [{ id: 'kopieren', titel: 'Kopieren', art: 'bool', vorgabe: false }], hinweis: 'Drehpunkt anklicken, dann den Winkel mit zwei Klicks setzen oder in Grad eingeben (Enter).' },   // B6: Kopieren
  spiegeln: { titel: 'Spiegeln – Achse zeichnen', felder: [{ id: 'kopieren', titel: 'Kopieren', art: 'bool', vorgabe: true }], hinweis: 'Zwei Punkte der Spiegelachse anklicken.' },   // B6: DM, Kopieren wie Revit vorgewählt
  spiegelnWaehlen: { titel: 'Spiegeln – Achse wählen', felder: [{ id: 'kopieren', titel: 'Kopieren', art: 'bool', vorgabe: true }], hinweis: 'Eine Gerade (Leitung, Wand, Achse, Detaillinie) als Spiegelachse anklicken.' },   // B6: MM
  // Bearbeitungswerkzeuge A2
  parallel: {   // B2
    titel: 'Parallele Leitungen',
    felder: [
      { id: 'anzahl', titel: 'Anzahl', art: 'zahl', vorgabe: 1, breite: 3 },
      { id: 'abstand', titel: 'Abstand', art: 'zahl', einheit: 'mm', vorgabe: 150, breite: 6 },
      { id: 'lage', titel: 'Lage', art: 'auswahl', werte: [['waagrecht', 'waagrecht'], ['oben', 'darüber'], ['unten', 'darunter']], vorgabe: 'waagrecht' },
      { id: 'nurGerade', titel: 'Nur Gerade', art: 'bool', vorgabe: false },
    ],
    hinweis: 'Leitung anklicken (waagrecht: auf der Seite der Parallelen); der ganze Leitungszug wird parallel gezeichnet.',
  },
  ausrichten: { titel: 'Ausrichten', felder: [], hinweis: 'Zuerst den Bezug (bei Wänden auf der Seite der Fläche), dann die Elemente anklicken; Esc beendet.' },   // B1
  versatz: {   // B1
    titel: 'Versatz',
    felder: [{ id: 'abstand', titel: 'Abstand', art: 'zahl', einheit: 'mm', vorgabe: 100, breite: 6 }, { id: 'kopieren', titel: 'Kopieren', art: 'bool', vorgabe: false }],
    hinweis: 'Gerade auf der Seite anklicken, zu der versetzt wird; mit «Kopieren» entsteht eine parallele Kopie.',
  },
  ecke: { titel: 'Stutzen/Dehnen für Ecke', felder: [], hinweis: 'Zwei Geraden anklicken, jeweils auf dem Teil, das bleibt; zwei Leitungen bekommen einen Bogen.' },   // A4
  dehnen: { titel: 'Dehnen bis', felder: [], hinweis: 'Zuerst die Bezugsgerade, dann die Elemente anklicken; Leitung an Leitung ergibt ein T-Stück.' },   // A4
  teilen: { titel: 'Teilen', felder: [], hinweis: 'Leitung an der Stelle anklicken, an der sie geteilt wird; das Werkzeug bleibt aktiv.' },   // A3
  verbinden: { titel: 'Verbinden', felder: [], hinweis: 'Zwei freie Anschlüsse nacheinander anklicken; das Werkzeug bleibt für das nächste Paar aktiv.' },
};

// ------------------------------------------------------------------------------------------------ Leitungen (Modul 3, C1)

const kz = (e) => e.kennzeichen;
const systemOptionen = () => derKlasse('System').sort((a, b) => kz(a).localeCompare(kz(b), 'de', { numeric: true })).map((e) => [kz(e), e.name ? `${kz(e)} – ${e.name}` : kz(e)]);
/** Medien des Katalogs (Kennung – Name) für Leitungen ohne System (Christian 02.11.2026: «leitungen gehören einem medium»). */
const mediumOptionen = () => (zustand.katalog?.medien ?? []).slice().sort((a, b) => a.kennung.localeCompare(b.kennung, 'de', { numeric: true }))
  .map((m) => [m.kennung, m.name ? `${m.kennung} – ${m.name}` : m.kennung]);
const strangOptionen = (wid) => derKlasse('Strang').filter((e) => !wert(wid, 'system') || !e.teilVon || e.teilVon === wert(wid, 'system'))
  .sort((a, b) => kz(a).localeCompare(kz(b), 'de', { numeric: true })).map((e) => [kz(e), e.teilVon ? `${kz(e)} (${e.teilVon})` : kz(e)]);

/** Zahlenfelder der freien Grösse mit der Vorlage der Serie füllen (nur freie Serien). nurLeere: nur, was noch leer ist (Start des Werkzeugs);
 *  sonst immer (die Serie wurde gewechselt). Bei einer Serie ohne freie Grösse werden die Felder geleert (nicht bei nurLeere). */
export function freiVorbelegen(wid, serie, nurLeere = false) {
  const v = vorbelegung(serie);
  for (const f of ['freiB', 'freiH', 'freiD']) {
    if (!v) { if (!nurLeere) setze(wid, f, null); continue; }
    if (!(f in v)) { setze(wid, f, null); continue; }
    if (!nurLeere || wert(wid, f) === null) setze(wid, f, v[f]);
  }
}

/** Felder der Werkzeuge Rohr (PI) und Kanal (DT): Serie, Grösse (bei einer freien Serie Breite × Höhe und die Normgrösse als Vorschlag), Höhe der Achse über OK Geschoss,
 *  Gefälle, System/Strang, Dämmdicke, Winkelfang. */
/** Leitungen der Steigleitung (Christian 06.11.2026): Medium, Kurzname, Vorgabe angehakt; Lüftung («es soll auch steigleitung lüftung geben AUL, FOL,
 *  ZUL, ABL rund und Eckig») mit Kanalserien rund oder eckig. */
/** Minimale Dämmdicke in mm einer Dämmregel (daemmung.json) für eine Nennweite – gleiche Regel wie DaemmRegel.Dicke im Kern: Stufe mit DN im
 *  Bereich bzw. die nächstgrössere (ANNAHME), unter der kleinsten deren Wert, über der grössten null. */
export function daemmDicke(regel, dn) {
  const st = [...(regel?.stufen ?? [])].sort((a, b) => a.dnVon - b.dnVon);
  if (!st.length || !(dn > 0) || dn > st[st.length - 1].dnBis) return null;
  return st.find((s) => dn <= s.dnBis)?.dicke ?? null;
}

export const STEIG_LEITUNGEN = [['KW', 'KW', true], ['WW', 'WW', true], ['ZIR', 'Zirk', true], ['HZ-VL', 'VL', false], ['HZ-RL', 'RL', false]];
export const STEIG_LUEFTUNG = [['AUL', 'AUL', false], ['FOL', 'FOL', false], ['ZUL', 'ZUL', true], ['ABL', 'ABL', true]];
export const steigId = (m) => m.toLowerCase().replace(/[^a-z]/g, '');
export const steigListe = (wid) => (wid === 'steigleitungLueftung' ? STEIG_LUEFTUNG : STEIG_LEITUNGEN);

/** Felder der Steigleitung: Steigzone (Breite × Tiefe; daraus die Abstände – Christian 06.11.2026 «bei allen steigleitungen soll eine Steigzonen
 *  Grösse angegeben werden können»), sonst Achsabstand; Lüftung mit Lage der Zentrale; je Leitung Häkchen, Serie und Grösse. */
function steigFelder(wid) {
  const lueftung = wid === 'steigleitungLueftung';
  const kategorie = lueftung ? 'Kanal' : 'Rohr';
  const felder = [
    { id: 'zoneB', titel: 'Steigzone', art: 'zahl', einheit: '×', platzhalter: 'Breite', breite: 5 },
    { id: 'zoneT', titel: '', art: 'zahl', einheit: 'mm', platzhalter: 'Tiefe', breite: 5 },
    { id: 'abstand', titel: 'oder Achsabstand', art: 'zahl', einheit: 'mm', platzhalter: '–', breite: 5, sichtbar: () => !(wert(wid, 'zoneB') > 0) },
  ];
  if (lueftung) felder.push({ id: 'zentrale', titel: 'Zentrale', art: 'auswahl', werte: [['unten', 'unten'], ['oben', 'oben']], vorgabe: 'unten' });
  for (const [m, kurz, an] of steigListe(wid)) {
    const id = steigId(m);
    felder.push(
      { id, titel: kurz, art: 'bool', vorgabe: an, beiAenderung: () => leitungHaken.optionenNeu?.() },
      { id: id + 'Serie', titel: 'Produktlinie', art: 'auswahl', leer: 'wählen …', sichtbar: () => !!wert(wid, id), get werte() { return serienListe(kategorie).map((x) => [x.kz, x.name]); },
        beiAenderung(el) { el?.blur?.(); const kz = wert(wid, id + 'Serie'); if (kz) serieLaden(kz).then(() => leitungHaken.optionenNeu?.()); leitungHaken.optionenNeu?.(); } },
      { id: id + 'Groesse', titel: 'Grösse', art: 'auswahl', leer: 'wählen …', sichtbar: () => !!wert(wid, id), get werte() { const d = serieDef(wert(wid, id + 'Serie')); return d ? groessenOptionen(d) : []; } },
    );
  }
  return felder;
}

function leitungFelder(wid, kategorie) {
  const aktuelleSerie = () => serieDef(wert(wid, 'serie'));
  const frei = () => istFrei(aktuelleSerie());
  const nachSerie = (el) => {
    el?.blur?.();
    const kzS = wert(wid, 'serie');
    const s = serieDef(kzS);
    if (kzS) {
      // Die Serie wird (leicht oder vollständig) geladen; danach Grösse prüfen und bei einer freien Serie die Vorlage eintragen
      serieLaden(kzS).then((d) => {
        if (!d || wert(wid, 'serie') !== kzS) return;
        if (wert(wid, 'groesse') && !groessenListe(d).some((g) => g.typ === wert(wid, 'groesse'))) setze(wid, 'groesse', '');
        freiVorbelegen(wid, d);
        ausUkd();
        daemmVorschlag();
        leitungHaken.optionenNeu?.();
      });
    }
    if (wert(wid, 'groesse') && s && !groessenListe(s).some((g) => g.typ === wert(wid, 'groesse'))) setze(wid, 'groesse', '');
    leitungHaken.optionenNeu?.();
  };
  // Lüftung (Christian 09.11.2026: «Lüftungen beziehen sich nicht auf lagen. diese werden für Rohre immer als Achse = -xy UKD und bei kanälen immer
  // OK = -xy UKD angegeben und gezeichnet.»): Feld «unter UKD» setzt die Achshöhe – rund: Achse = UKD − x, eckig: OK = UKD − x (Achse = OK − h/2)
  const eckig = () => istRechteck(aktuelleSerie());
  const kanalH = () => {
    const s = aktuelleSerie();
    if (frei()) return wert(wid, 'freiH');
    const g = s ? groessenListe(s).find((x) => x.typ === wert(wid, 'groesse')) : null;
    if (g?.h > 0) return g.h;
    const m = String(wert(wid, 'groesse') ?? '').match(/(d+(?:.d+)?)s*[x×]s*(d+(?:.d+)?)/);
    return m ? Number(m[2]) : null;
  };
  const ukdGeschoss = () => ukdFuer(element(zustand.aktiveAnsicht)?.geschoss);
  const ausUkd = () => {
    const x = wert(wid, 'unterUkd'), ukd = ukdGeschoss();
    if (kategorie !== 'Kanal' || !(x >= 0) || x === null || !Number.isFinite(ukd)) return;
    const h = eckig() ? kanalH() : 0;
    if (!(h >= 0) || h === null) return;
    setze(wid, 'hoehe', Math.round(ukd - x - h / 2));
    leitungHaken.hoeheGeaendert?.();
  };
  // Normgrösse gewählt (freie Serie): die Zahlenfelder zeigen ihre Grösse, geändert werden sie von Hand
  // Dämmdicke nach Energiegesetz (Christian 09.11.2026: «dämmdicken gem. energiegesetzt»): Dämmregel des Systems (MuKEn 2014 Anhang 4,
  // Katalog daemmung.json über die Abfrage katalog) und DN der gewählten Grösse; sonst die feste Vorgabe-Dämmdicke des Systems
  const daemmVorschlag = () => {
    const sy = derKlasse('System').find((e) => e.id === wert(wid, 'system') || kz(e) === wert(wid, 'system'));
    if (!sy) return;
    const w = (n) => { const x = sy.werte?.[n]; return x && typeof x === 'object' && 'wert' in x ? x.wert : x; };
    const regel = (zustand.katalog?.daemmregeln ?? []).find((r) => r.name === w('daemmregel'));
    if (regel) {
      const s = aktuelleSerie();
      const g = s ? groessenListe(s).find((x) => x.typ === wert(wid, 'groesse')) : null;
      const dn = g?.dn ?? Number(/DNs*(d+)/i.exec(String(wert(wid, 'groesse') ?? ''))?.[1]);
      const d = daemmDicke(regel, dn);
      if (d) { setze(wid, 'daemmdicke', d); return; }
    }
    if (w('daemmdicke') > 0) setze(wid, 'daemmdicke', w('daemmdicke'));
  };
  const nachNormgroesse = (el) => {
    el?.blur?.();
    ausUkd();
    daemmVorschlag();
    const s = aktuelleSerie();
    const g = istFrei(s) ? groessenListe(s).find((x) => x.typ === wert(wid, 'groesse')) : null;
    if (g) { setze(wid, 'freiB', g.b); setze(wid, 'freiH', g.h); setze(wid, 'freiD', g.d); }
    leitungHaken.optionenNeu?.();
  };
  return [
    { id: 'serie', titel: 'Produktlinie', art: 'auswahl', get werte() { return serienListe(kategorie).map((s) => [s.kz, s.name]); }, leer: 'wählen …', beiAenderung: nachSerie },
    { id: 'groesse', get titel() { return frei() ? 'Normgrösse' : 'Grösse'; }, art: 'auswahl', get leer() { return frei() ? 'Vorschlag …' : 'wählen …'; },
      get werte() {
        const s = aktuelleSerie();
        return s ? groessenOptionen(s) : (serienListe(kategorie).find((x) => x.kz === wert(wid, 'serie'))?.typen ?? []).map((t) => [t, t]);
      },
      beiAenderung: nachNormgroesse },
    // Freie Grösse (Nachtrag): Zahlenfelder statt der Typwahl, vorbelegt mit der Vorlage; Grenzen und Schritt prüft der Kern
    { id: 'freiB', titel: 'Breite', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'b', breite: 5, sichtbar: () => frei() && istRechteck(aktuelleSerie()) },
    { id: 'freiH', titel: 'Kanalhöhe', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'h', breite: 5, sichtbar: () => frei() && istRechteck(aktuelleSerie()), beiAenderung: () => ausUkd() },
    { id: 'freiD', titel: 'Durchmesser', art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'd', breite: 5, sichtbar: () => frei() && !istRechteck(aktuelleSerie()) },
    { id: 'freiHinweis', art: 'hinweis', klasse: 'ol-info', get text() { return freiHinweis(aktuelleSerie()); }, sichtbar: frei },
    { id: 'abwasserHinweis', art: 'hinweis', klasse: 'ol-info', get text() { const s = aktuelleSerie(); return abwasserHinweis(s, s ? beschriftungVon(s, wert(wid, 'groesse')) : null); }, sichtbar: () => istAbwasser(aktuelleSerie()) },
    // Lage (Lagemanager, Christian 05.11.2026: «Lage wird beim zeichnen bestimmt»): nur Rohre; setzt die Höhe = UK roh Decke − Abstand der Lage
    { id: 'lage', titel: 'Lage', art: 'auswahl', leer: '–', sichtbar: () => kategorie === 'Rohr',
      get werte() { return lagenFuer(element(zustand.aktiveAnsicht)?.geschoss).map((l) => [String(l.nr), `L${l.nr} (UKD −${l.abstand})`]); },
      beiAenderung(el) { el?.blur?.(); const l = lagenFuer(element(zustand.aktiveAnsicht)?.geschoss).find((x) => String(x.nr) === wert(wid, 'lage')); if (l) { setze(wid, 'hoehe', Math.round(l.hoehe)); leitungHaken.hoeheGeaendert?.(); } leitungHaken.optionenNeu?.(); } },
    { id: 'unterUkd', get titel() { return eckig() ? 'OK unter UKD' : 'Achse unter UKD'; }, art: 'zahl', einheit: 'mm', breite: 5, sichtbar: () => kategorie === 'Kanal',
      get platzhalter() { const u = ukdGeschoss(); return Number.isFinite(u) ? `UKD ${Math.round(u)}` : 'UKD fehlt'; },
      beiAenderung: () => ausUkd() },
    { id: 'hoehe', get titel() { return kategorie === 'Kanal' ? 'Achshöhe' : 'Höhe'; }, art: 'zahl', einheit: 'mm', pflicht: true, platzhalter: 'ab OK Geschoss', breite: 12,
      beiAenderung: () => { if (kategorie === 'Kanal') setze(wid, 'unterUkd', null); leitungHaken.hoeheGeaendert?.(); } },
    { id: 'gefaelle', titel: 'Gefälle', art: 'zahl', einheit: '%', platzhalter: 'kein', breite: 4, fluechtig: true },
    { id: 'system', titel: 'System', art: 'auswahl', get werte() { return systemOptionen(); }, leer: '–',
      beiAenderung(el) {
        el?.blur?.();
        const st = derKlasse('Strang').find((e) => kz(e) === wert(wid, 'strang')); if (st && st.teilVon !== wert(wid, 'system')) setze(wid, 'strang', '');
        // Vorgaben des Systems (Mustersysteme, Christian 09.11.2026): Produktlinie und Dämmdicke vorschlagen
        const sy = derKlasse('System').find((e) => e.id === wert(wid, 'system') || kz(e) === wert(wid, 'system'));
        const w = (n) => { const x = sy?.werte?.[n]; return x && typeof x === 'object' && 'wert' in x ? x.wert : x; };
        daemmVorschlag();
        const pl = w('produktlinie');
        if (pl && pl !== wert(wid, 'serie') && serienListe(kategorie).some((s) => s.kz === pl)) { setze(wid, 'serie', pl); nachSerie(); return; }
        leitungHaken.optionenNeu?.();
      } },
    { id: 'medium', titel: 'Medium', art: 'auswahl', get werte() { return mediumOptionen(); }, leer: '–',
      beiAenderung(el) { el?.blur?.(); leitungHaken.optionenNeu?.(); } },   // ohne System: Medium der Leitung (Christian 02.11.2026)
    { id: 'strang', titel: 'Strang', art: 'auswahl', get werte() { return strangOptionen(wid); }, leer: '–',
      beiAenderung(el) { el?.blur?.(); const st = derKlasse('Strang').find((e) => kz(e) === wert(wid, 'strang')); if (st?.teilVon) setze(wid, 'system', st.teilVon); leitungHaken.optionenNeu?.(); } },
    { id: 'daemmdicke', titel: 'Dämmdicke', art: 'zahl', einheit: 'mm', platzhalter: 'keine', breite: 4, fluechtig: true },
    { id: 'winkelfang', titel: 'Winkelfang', art: 'bool', vorgabe: true },
    // Keine Serie der Kategorie im Projekt: Hinweis mit Weg zu den Familien
    { id: 'serieHinweis', art: 'hinweis', text: `Keine Produktlinie der Kategorie ${kategorie} im Projekt.`, sichtbar: () => serienListe(kategorie).length === 0 },
    { id: 'serieLaden', art: 'knopf', titel: 'Produktlinie laden …', aktion: 'familie.laden', sichtbar: () => serienListe(kategorie).length === 0 },
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

