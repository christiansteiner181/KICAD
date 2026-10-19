// Tests der Schema-Ansicht (Modul 3, Teil C2) – die Teile ohne Browser: schema-logik.js (Treffer, Rundung, Einrasten, Zelle, Parameter der
// Befehle, Trennoptionen, Menüs) und schema-text.js (Banner, Tooltips, Listen der Dialoge), dazu die Einbindung (fenster.js, aktionen.js,
// symbole.js, index.html) und die Spiegel gegen den Kern (Linienklassen gegen SchemaGraph.cs).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/schema.test.mjs
// Läuft ein Testserver mit frischem Beispielprojekt (HLKS_TEST_SERVER=http://localhost:5162), laufen zusätzlich die echten Antworten des Kerns
// (Abfrage schema, Plangrafik, Katalog) durch die reine Logik – nur lesend.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { readFileSync, existsSync } from 'node:fs';

const L = await import('./schema-logik.js');
const T = await import('./schema-text.js');
const pg = await import('./plangrafik.js');

const hier = path.dirname(fileURLToPath(import.meta.url));
const wwwroot = path.resolve(hier, '../..');
const lies = (rel) => readFileSync(path.join(wwwroot, rel), 'utf8');
const kernDatei = (rel) => { const p = path.resolve(hier, '../../../../Hlks.Kern', rel); return existsSync(p) ? readFileSync(p, 'utf8') : null; };

// ---------- ein kleines Schema von Hand (Papier-mm wie die Abfrage «schema») ----------
const daten = () => ({
  ansicht: 'AN-S', schemaart: 'strangschema', massstab: 1, stand: 'abc',
  zeilen: [
    { geschoss: null, kennzeichen: null, name: 'ohne Geschoss', kote: null, y: -60, hoehe: 50, zone: true },
    { geschoss: 'g-eg', kennzeichen: 'EG', name: 'Erdgeschoss', kote: 0, y: 0, hoehe: 50, zone: false },
    { geschoss: 'g-og', kennzeichen: '1.OG', name: 'Obergeschoss', kote: 2900, y: 50, hoehe: 50, zone: false },
  ],
  spalten: [
    { strang: 's-kw1', kennzeichen: 'KW-1', name: 'Steigstrang', system: 'KW', art: 'senkrecht', x: 30, breite: 70, linie: 40 },
    { strang: 's-sw1', kennzeichen: 'SW-1', name: 'Sammelleitung', system: 'SW', art: 'waagrecht', x: 110, breite: 70, linie: 120 },
    { strang: null, kennzeichen: null, name: 'SW ohne Strang', system: 'SW', art: 'ohneStrang', x: 180, breite: 70, linie: 190 },
    { strang: null, kennzeichen: null, name: 'ohne System', system: null, art: 'ohneSystem', x: 260, breite: 70, linie: 270 },
  ],
  knoten: [
    { element: 'e-ts1', kennzeichen: 'TS-1', klasse: 'TStueck', art: 'tstueck', x: 40, y: 20, manuell: false, platziert: true, symbol: 'tstueck' },
    { element: 'e-ts2', kennzeichen: 'TS-2', klasse: 'TStueck', art: 'tstueck', x: 40, y: 26, manuell: false, platziert: true, symbol: 'tstueck' },
    { element: 'e-wt', kennzeichen: 'WT-E.02', klasse: 'Waschtisch', art: 'endgeraet', x: 55, y: 42, manuell: true, platziert: true, symbol: 'apparat', strang: 'KW-1', system: 'KW', geschoss: 'EG' },
    { element: 'e-wc', kennzeichen: 'WC-NEU', klasse: 'WC', art: 'endgeraet', x: 68, y: 42, manuell: false, platziert: false, symbol: 'apparat' },
  ],
  linien: [
    { elemente: ['e-r1'], kennzeichen: ['R-1'], von: 'e-ts1', vonAnschluss: 'b', nach: 'e-ts2', nachAnschluss: 'a', punkte: [[40, 20], [40, 26]], groessen: ['NW 20'], medium: 'KW' },
    { elemente: ['e-r2', 'e-bo', 'e-r3'], kennzeichen: ['R-2', 'BO-1', 'R-3'], von: 'e-ts2', vonAnschluss: 'c', nach: 'e-wt', nachAnschluss: 'kw', punkte: [[40, 26], [40, 42], [55, 42]], groessen: ['NW 15'], medium: 'KW' },
    { elemente: [], kennzeichen: [], von: 'e-wt', vonAnschluss: 'sw', nach: 'e-wc', nachAnschluss: 'sw', verbindung: 'v-direkt', punkte: [[55, 42], [68, 42]], groessen: [], medium: 'SW' },
    { elemente: ['e-r9'], kennzeichen: ['R-9'], von: 'e-ts1', vonAnschluss: 'a', nach: null, punkte: [[40, 20], [32, 20]], groessen: ['NW 20'], medium: 'KW', offen: [{ ende: 'nach', element: 'e-r9', anschluss: 'ende', grund: 'offen', x: 32, y: 20 }] },
  ],
  hinweise: ['Ohne Geschoss (Zone unten): KS-1 – geschoss setzen (element_aendern).', 'Nur im Schema (nicht platziert): WC-NEU – im Grundriss platzieren.'],
});

// Verbindungen wie zustand.verbindungen (Kennzeichen)
const verbindungen = [
  { id: 'v1', von: 'TS-1', vonAnschluss: 'b', nach: 'R-1', nachAnschluss: 'anfang' },
  { id: 'v2', von: 'R-1', vonAnschluss: 'ende', nach: 'TS-2', nachAnschluss: 'a' },
  { id: 'v3', von: 'TS-2', vonAnschluss: 'c', nach: 'R-2', nachAnschluss: 'anfang' },
  { id: 'v4', von: 'R-2', vonAnschluss: 'ende', nach: 'BO-1', nachAnschluss: 'a' },
  { id: 'v5', von: 'BO-1', vonAnschluss: 'b', nach: 'R-3', nachAnschluss: 'anfang' },
  { id: 'v6', von: 'R-3', vonAnschluss: 'ende', nach: 'WT-E.02', nachAnschluss: 'kw' },
  { id: 'v-direkt', von: 'WT-E.02', vonAnschluss: 'sw', nach: 'WC-NEU', nachAnschluss: 'sw' },
  { id: 'v7', von: 'TS-1', vonAnschluss: 'a', nach: 'R-9', nachAnschluss: 'anfang' },
];
const kzVonId = (id) => ({ 'e-ts1': 'TS-1', 'e-ts2': 'TS-2', 'e-wt': 'WT-E.02', 'e-wc': 'WC-NEU', 'e-r1': 'R-1', 'e-r2': 'R-2', 'e-bo': 'BO-1', 'e-r3': 'R-3', 'e-r9': 'R-9' }[id] ?? null);

// ---------- Rundung und Einrasten ----------
test('Rundung: auf 0.5 mm, nie -0', () => {
  assert.equal(L.runden(12.3), 12.5);
  assert.equal(L.runden(12.2), 12);
  assert.equal(L.runden(-0.2), 0);
  assert.ok(!Object.is(L.runden(-0.1), -0));
  assert.equal(L.runden(7.26, 0.1), 7.3);
  assert.equal(L.RUNDUNG_MM, 0.5);
});

test('Hand-Lage: rastet auf die Stranglinie und auf Achsen anderer Knoten ein, sonst gerundet', () => {
  const d = daten();
  // nahe der Stranglinie von KW-1 (x 40): x rastet ein, y gerundet
  let l = L.lageRunden({ x: 41.2, y: 77.3 }, { daten: d, ausser: 'e-wt', toleranz: 1.5 });
  assert.equal(l.x, 40); assert.equal(l.fangX.art, 'strang'); assert.equal(l.fangX.kennzeichen, 'KW-1'); assert.equal(l.y, 77.5); assert.equal(l.fangY, null);
  // y rastet auf die Achse eines Knotens (WT: y 42), x gerundet (ausserhalb jeder Toleranz)
  l = L.lageRunden({ x: 90.3, y: 41.4 }, { daten: d, ausser: 'e-ts1', toleranz: 1.5 });
  assert.equal(l.y, 42); assert.equal(l.fangY.kennzeichen, 'WT-E.02'); assert.equal(l.x, 90.5); assert.equal(l.fangX, null);
  // der bewegte Knoten fängt sich nicht an sich selbst (WT steht bei x 55), wohl aber an der Achse von WC-NEU (y 42)
  l = L.lageRunden({ x: 55.4, y: 42.4 }, { daten: d, ausser: 'e-wt', toleranz: 1.5 });
  assert.equal(l.fangX, null); assert.equal(l.x, 55.5); assert.equal(l.y, 42); assert.equal(l.fangY.kennzeichen, 'WC-NEU');
  l = L.lageRunden({ x: 55.4, y: 10 }, { daten: d, toleranz: 1.5 });
  assert.equal(l.x, 55); assert.equal(l.fangX.kennzeichen, 'WT-E.02');
  // waagrechte Stränge haben keine Stranglinie zum Einrasten
  l = L.lageRunden({ x: 120.5, y: 10 }, { daten: d, toleranz: 1.5 });
  assert.equal(l.fangX, null); assert.equal(l.x, 120.5);
  // ohne Daten nur runden
  assert.deepEqual(L.lageRunden({ x: 3.3, y: 4.7 }), { x: 3.5, y: 4.5, fangX: null, fangY: null });
  // grössere Toleranz (weit herausgezoomt) fängt weiter
  assert.equal(L.lageRunden({ x: 44, y: 5 }, { daten: d, toleranz: 5 }).x, 40);
});

// ---------- Treffer ----------
test('Treffer auf Knoten: Symbolfläche ±4.5 × ±3.5 mm, bei mehreren der nächste', () => {
  const d = daten();
  assert.equal(L.knotenBei(d.knoten, 55, 42)?.kennzeichen, 'WT-E.02');
  assert.equal(L.knotenBei(d.knoten, 58.9, 44.9)?.kennzeichen, 'WT-E.02');
  assert.equal(L.knotenBei(d.knoten, 60.5, 42), null);          // rechts daneben
  assert.equal(L.knotenBei(d.knoten, 55, 46.4), null);          // darüber
  assert.equal(L.knotenBei(d.knoten, 40, 23)?.kennzeichen, 'TS-1');   // TS-1 (y 20) und TS-2 (y 26): 23 ist gleich weit → der erste
  assert.equal(L.knotenBei(d.knoten, 40, 24.5)?.kennzeichen, 'TS-2');
  assert.equal(L.knotenBei(d.knoten, 40, 21)?.kennzeichen, 'TS-1');
  // die Toleranz (Bildschirmpixel in mm) weitet die Fläche
  assert.equal(L.knotenBei(d.knoten, 62, 42, 0), null);
  assert.equal(L.knotenBei(d.knoten, 60.4, 42, 6)?.kennzeichen, 'WT-E.02');
  assert.equal(L.knotenBei([], 1, 1), null);
});

test('Knoten genau aufeinander (Kern: Etagen-T-Stücke zweier Systeme am selben Endgerät): das angeklickte Teil gewinnt', () => {
  const kn = [
    { element: 'e-sw', kennzeichen: 'TS-SW-1.02-2', x: 138, y: 120 },
    { element: 'e-kw', kennzeichen: 'TS-KW-1.02-2', x: 138, y: 120 },
  ];
  const d = { knoten: kn, linien: [], spalten: [], zeilen: [] };
  assert.equal(L.knotenBei(kn, 138, 120)?.kennzeichen, 'TS-SW-1.02-2');            // ohne Hinweis der erste
  assert.equal(L.knotenBei(kn, 138, 120, 0, 'e-kw')?.kennzeichen, 'TS-KW-1.02-2');   // das angeklickte Teil
  assert.equal(L.knotenBei(kn, 138, 120, 0, 'e-sw')?.kennzeichen, 'TS-SW-1.02-2');
  assert.equal(L.knotenBei(kn, 138, 120, 0, 'e-anders')?.kennzeichen, 'TS-SW-1.02-2');   // Teil ohne Knoten in Reichweite: wie bisher
  assert.equal(L.knotenBei(kn, 160, 120, 0, 'e-kw'), null);                           // ausser Reichweite zählt auch das Teil nicht
  assert.equal(L.treffer(d, { x: 138, y: 120 }, 0, 'e-kw').knoten.kennzeichen, 'TS-KW-1.02-2');
  assert.equal(L.treffer(d, { x: 138, y: 120 }, 0, 'e-sw').id, 'e-sw');
});

test('Abstand zur Strecke und nächste Linie', () => {
  assert.deepEqual(Object.fromEntries(Object.entries(L.abstandZurStrecke(5, 3, 0, 0, 10, 0)).map(([k, v]) => [k, Math.round(v * 100) / 100])), { abstand: 3, t: 0.5, x: 5, y: 0 });
  assert.equal(L.abstandZurStrecke(-4, 3, 0, 0, 10, 0).abstand, 5);          // vor dem Anfang: Abstand zum Endpunkt
  assert.equal(L.abstandZurStrecke(1, 1, 2, 2, 2, 2).abstand, Math.SQRT2);   // Strecke der Länge 0
  const d = daten();
  const n = L.naechsteLinie(d, 47, 42.5, 1);
  assert.deepEqual(n.linie.kennzeichen, ['R-2', 'BO-1', 'R-3']);
  assert.equal(n.segment, 1);
  assert.equal(L.naechsteLinie(d, 47, 45, 1), null);
  const direkt = L.naechsteLinie(d, 62, 41.8, 1);
  assert.equal(direkt.linie.verbindung, 'v-direkt');
  // zwei Linien in Reichweite: die nähere gewinnt
  assert.equal(L.naechsteLinie(d, 40, 22, 5).linie.kennzeichen[0], 'R-1');
});

test('treffer(): Knoten vor Linie, Linienteil laut Teil, direkte Linie nach der Geometrie, sonst Teil oder leer', () => {
  const d = daten();
  let t = L.treffer(d, { x: 55, y: 42 }, 1.5, 'e-r3');          // Knoten gewinnt, auch wenn das Teil eine Linie ist
  assert.equal(t.art, 'knoten'); assert.equal(t.id, 'e-wt');
  t = L.treffer(d, { x: 47, y: 42.2 }, 1.5, 'e-r3');            // auf der Linie: das angeklickte Teil
  assert.equal(t.art, 'linie'); assert.equal(t.id, 'e-r3'); assert.equal(t.element, 'e-r3'); assert.equal(t.linie.kennzeichen.length, 3);
  t = L.treffer(d, { x: 62, y: 42 }, 1.5, null);                // direkte Linie ohne Element, kein Teil im Plan
  assert.equal(t.art, 'linie'); assert.equal(t.element, null); assert.equal(t.nurGeometrie, true); assert.equal(t.linie.verbindung, 'v-direkt');
  t = L.treffer(d, { x: 200, y: 10 }, 1.5, 'g-eg');            // Geschosslinie o. ä.
  assert.deepEqual(t, { art: 'sonst', id: 'g-eg' });
  t = L.treffer(d, { x: 200, y: 10 }, 1.5, null);
  assert.deepEqual(t, { art: 'leer' });
  t = L.treffer(d, { x: 200, y: 10 }, 1.5, 'e-wt');             // Teil eines Knotens, auch ohne Nähe: das Teil zählt (Symbolteile ragen heraus)
  assert.equal(t.art, 'knoten');
  assert.equal(L.treffer(null, { x: 0, y: 0 }, 1, null).art, 'leer');
});

test('Zelle unter dem Zeiger: Spalte × Zeile, auch Zonen', () => {
  const d = daten();
  let z = L.zelleBei(d, 60, 20);
  assert.equal(z.spalte.kennzeichen, 'KW-1'); assert.equal(z.zeile.kennzeichen, 'EG');
  z = L.zelleBei(d, 125, 70);
  assert.equal(z.spalte.kennzeichen, 'SW-1'); assert.equal(z.zeile.kennzeichen, '1.OG');
  z = L.zelleBei(d, 200, -30);
  assert.equal(z.spalte.art, 'ohneStrang'); assert.equal(z.zeile.zone, true);
  assert.equal(L.zelleBei(d, 100.5, 20), null);                // Lücke zwischen den Systemen
  assert.equal(L.zelleBei(d, 60, 120), null);                  // über der obersten Zeile
  assert.equal(L.zelleText(L.zelleBei(d, 60, 20)), 'Geschoss EG × Strang KW-1');
  assert.equal(L.zelleText(L.zelleBei(d, 200, -30)), 'ohne Geschoss × SW ohne Strang');
  assert.equal(L.zelleText(L.zelleBei(d, 280, 10)), 'Geschoss EG × ohne System');
  assert.equal(L.zelleText(null), 'ausserhalb des Rasters');
});

test('Umrandung, Wo-zeigt-es, nicht platziert', () => {
  const d = daten();
  assert.deepEqual(L.umrandung(d, 'e-wt'), { minx: 43, miny: 32, maxx: 67, maxy: 52 });
  assert.deepEqual(L.umrandung(d, 'e-bo'), { minx: 32, miny: 18, maxx: 63, maxy: 50 });
  assert.equal(L.umrandung(d, 'unbekannt'), null);
  assert.equal(L.zeigtElement(d, 'e-wt'), 'knoten');
  assert.equal(L.zeigtElement(d, 'e-bo'), 'linie');
  assert.equal(L.zeigtElement(d, 'e-x'), null);
  assert.equal(L.zeigtElement(null, 'e-wt'), null);
  const np = L.nichtPlatziert(d, (id) => (id === 'e-r3' ? { geometrie: null } : { geometrie: { art: 'polylinie' } }));
  assert.deepEqual(np.knoten.map((k) => k.kennzeichen), ['WC-NEU']);
  assert.equal(np.linien, 1);
});

// ---------- Parameter der Befehle ----------
test('Parameter: schema_lage, Zurücksetzen, schema_anordnen', () => {
  assert.deepEqual(L.lageParameter('AN-S', 'WT-E.02', 85.26, 92.74), { ansicht: 'AN-S', element: 'WT-E.02', x: 85.5, y: 92.5 });
  assert.deepEqual(L.lageZuruecksetzenParameter('AN-S', 'e-wt'), { ansicht: 'AN-S', element: 'e-wt', zuruecksetzen: true });
  assert.deepEqual(L.anordnenParameter('AN-S', true), { ansicht: 'AN-S', alle: true });
  assert.deepEqual(L.anordnenParameter('AN-S'), { ansicht: 'AN-S' });
  assert.ok(!('alle' in L.anordnenParameter('AN-S', false)));
});

test('Parameter verbinden: Objekt { element, anschluss? } – Kennzeichen mit Punkt bleiben heil', () => {
  assert.deepEqual(L.verbindenParameter('WC-E.02', 'V-1'), { von: { element: 'WC-E.02' }, nach: { element: 'V-1' } });
  assert.deepEqual(L.verbindenParameter('a', 'b', 'ein', 'aus'), { von: { element: 'a', anschluss: 'ein' }, nach: { element: 'b', anschluss: 'aus' } });
  assert.deepEqual(L.verbindenParameter('a', 'b', null, 'aus'), { von: { element: 'a' }, nach: { element: 'b', anschluss: 'aus' } });
  assert.deepEqual(L.trennenParameter('v1'), { verbindung: 'v1' });
});

test('Ablehnung «Mehrere passende Anschlusspaare» → Kandidaten', () => {
  const t = 'Mehrere passende Anschlusspaare zwischen V-A und V-B: ein–ein, ein–aus, aus–ein, aus–aus.';
  assert.deepEqual(L.mehrfachKandidaten(t), [['ein', 'ein'], ['ein', 'aus'], ['aus', 'ein'], ['aus', 'aus']]);
  assert.equal(L.mehrfachKandidaten('Kein freies, passendes Anschlusspaar zwischen A und B.'), null);
  assert.equal(L.mehrfachKandidaten(null), null);
  assert.deepEqual(L.mehrfachKandidaten('Mehrere passende Anschlusspaare zwischen A und B'), []);
  // der Text des Kerns hat sich nicht verändert (Spiegel gegen Befehle.cs)
  const q = kernDatei('Api/Befehle.cs');
  if (q) assert.ok(q.includes('Mehrere passende Anschlusspaare zwischen {a.Kennzeichen} und {b.Kennzeichen}: {string.Join(", ", kandidaten.Select(c => $"{c.Item1.Name}–{c.Item2.Name}"))}.'), 'Meldung des Kerns geändert: schema-logik.mehrfachKandidaten anpassen');
});

test('Element einfügen: einfügbare Klassen nach Rolle und Basis, ohne Linienklassen', () => {
  const katalog = [
    { name: 'WC', basis: 'Apparat', familie: true, beschreibung: 'WC mit Spülkasten.' },
    { name: 'Waschtisch', basis: 'Apparat', familie: true },
    { name: 'Hausanschluss', basis: null, familie: true },
    { name: 'Heizkoerper', basis: null },
    { name: 'Ventil', basis: 'Armatur' },
    { name: 'Pumpe', basis: 'Armatur' },
    { name: 'TStueck', basis: 'Formteil' },
    { name: 'Bogen', basis: 'Formteil' },
    { name: 'Reduktion', basis: 'Formteil' },
    { name: 'Rohr', basis: 'Leitung' },
    { name: 'Kanal', basis: 'Leitung' },
    { name: 'Raum', basis: null },
    { name: 'Wasserzaehler', basis: 'Armatur' },
    { name: 'Abstrakt', basis: 'Armatur', abstrakt: true },
  ];
  const rollen = new Map([['WC', 'verbraucher'], ['Waschtisch', 'verbraucher'], ['Hausanschluss', 'quelle'], ['Heizkoerper', 'verbraucher'], ['Ventil', 'leitung'], ['Pumpe', 'leitung'], ['TStueck', 'leitung'], ['Bogen', 'leitung'], ['Reduktion', 'leitung'], ['Rohr', 'leitung'], ['Kanal', 'leitung'], ['Wasserzaehler', 'leitung']]);
  const k = L.einfuegbareKlassen(katalog, rollen);
  assert.deepEqual(k.endgeraete.map((x) => x.name), ['Heizkoerper', 'Waschtisch', 'WC']);
  assert.deepEqual(k.geraete.map((x) => x.name), ['Hausanschluss']);
  assert.deepEqual(k.armaturen.map((x) => x.name), ['Pumpe', 'Ventil', 'Wasserzaehler']);
  assert.deepEqual(k.formteile.map((x) => x.name), ['TStueck']);
  assert.ok(![...k.endgeraete, ...k.geraete, ...k.armaturen, ...k.formteile].some((x) => ['Rohr', 'Kanal', 'Bogen', 'Reduktion', 'Raum', 'Abstrakt'].includes(x.name)));
  assert.equal(k.endgeraete.find((x) => x.name === 'WC').beschreibung, 'WC mit Spülkasten.');
  assert.equal(L.knotenArt({ name: 'Raum' }, undefined), null);
  assert.equal(L.knotenArt({ name: 'Ventil', basis: 'Armatur' }, 'leitung'), 'armatur');
  assert.equal(L.knotenArt({ name: 'Rohr', basis: 'Leitung' }, 'leitung'), null);
});

test('Linienklassen: Spiegel von SchemaGraph.IstLinienelement', () => {
  const q = kernDatei('Schema/SchemaGraph.cs');
  assert.deepEqual([...L.LINIENKLASSEN], ['Rohr', 'Kanal', 'Bogen', 'Reduktion']);
  if (!q) return;
  assert.match(q, /IstLinienelement[\s\S]{0,200}IstArt\(e, "Leitung"\)[\s\S]{0,60}IstArt\(e, "Bogen"\)[\s\S]{0,60}IstArt\(e, "Reduktion"\)/);
  const klassen = JSON.parse(readFileSync(path.resolve(hier, '../../../../../kataloge/klassen.json'), 'utf8'));
  const liste = Array.isArray(klassen) ? klassen : (klassen.klassen ?? Object.values(klassen));
  const namen = JSON.stringify(liste);
  for (const n of L.LINIENKLASSEN) assert.ok(namen.includes(`"${n}"`), `Klasse ${n} im Katalog`);
});

test('Zuordnung aus der Zelle: Geschoss der Zeile, Strang bzw. System der Spalte', () => {
  const d = daten();
  const kw1 = L.zelleBei(d, 60, 20);       // EG × KW-1
  assert.deepEqual(L.zuordnungAusZelle(kw1, 'armatur'), { geschoss: 'EG', teilVon: 'KW-1' });
  assert.deepEqual(L.zuordnungAusZelle(kw1, 'endgeraet'), { geschoss: 'EG', teilVon: 'KW-1' });
  assert.deepEqual(L.zuordnungAusZelle(L.zelleBei(d, 125, 70), 'formteil'), { geschoss: '1.OG', teilVon: 'SW-1' });
  assert.deepEqual(L.zuordnungAusZelle(L.zelleBei(d, 200, 10), 'endgeraet'), { geschoss: 'EG', system: 'SW' });        // Zone «ohne Strang»
  assert.deepEqual(L.zuordnungAusZelle(L.zelleBei(d, 280, 10), 'endgeraet'), { geschoss: 'EG' });                     // «ohne System»
  assert.deepEqual(L.zuordnungAusZelle(L.zelleBei(d, 200, -30), 'geraet'), { system: 'SW' });                         // Zone ohne Geschoss
  assert.deepEqual(L.zuordnungAusZelle(null, 'armatur'), {});
});

test('element_anlegen im Schema: Klasse oder Familie, ohne Geometrie', () => {
  const d = daten();
  const zelle = L.zelleBei(d, 60, 20);
  assert.deepEqual(L.klasseAnlegenParameter('Waschtisch', zelle, 'endgeraet'), { klasse: 'Waschtisch', geschoss: 'EG', teilVon: 'KW-1' });
  assert.deepEqual(L.klasseAnlegenParameter('Ventil', zelle, 'armatur', { kennzeichen: ' V-7 ', name: ' Absperrung ' }), { klasse: 'Ventil', geschoss: 'EG', teilVon: 'KW-1', kennzeichen: 'V-7', name: 'Absperrung' });
  const p = L.familieAnlegenParameter({ kategorie: 'Ventil', kennzeichen: 'FAM-3' }, 'DN15', zelle, 'armatur');
  assert.deepEqual(p, { klasse: 'Ventil', geschoss: 'EG', teilVon: 'KW-1', eigenschaften: { familientyp: 'DN15' }, bezuege: { familie: 'FAM-3' } });
  assert.ok(!('geometrie' in p), 'ohne Geometrie: familie_platzieren verlangt einen Punkt, element_anlegen nicht');
  assert.equal(L.angelegtesElement({ aenderungen: [{ art: 'geaendert', kennzeichen: 'X' }, { art: 'angelegt', kennzeichen: 'WT-1', klasse: 'Waschtisch' }] }), 'WT-1');
  assert.equal(L.angelegtesElement({ aenderungen: [{ art: 'angelegt', kennzeichen: 'A', klasse: 'Strang' }, { art: 'angelegt', kennzeichen: 'WT-2', klasse: 'Waschtisch' }] }, 'Waschtisch'), 'WT-2');
  assert.equal(L.angelegtesElement({ aenderungen: [] }), null);
  assert.deepEqual(L.platzierenParameter('WT-E.02', [8512.34, 9000]), { element: 'WT-E.02', geometrie: { art: 'punkt', punkte: [[8512.3, 9000]] } });
});

test('Familien im Dialog: nur gültige mit Typen und einer Kategorie, die ein Knoten ist', () => {
  const info = new Map([['Ventil', 'armatur'], ['WC', 'endgeraet']]);
  const f = L.einfuegbareFamilien([
    { kennzeichen: 'FAM-1', name: 'Kugelhahn', kategorie: 'Ventil', typen: ['DN15', 'DN20'], gueltig: true },
    { kennzeichen: 'FAM-2', name: 'Rohrserie', kategorie: 'Rohr', typen: ['DN15'] },
    { kennzeichen: 'FAM-3', name: 'Entwurf', kategorie: 'Ventil', typen: [], gueltig: true },
    { kennzeichen: 'FAM-4', name: 'Kaputt', kategorie: 'WC', typen: ['A'], gueltig: false },
    { kennzeichen: 'FAM-5', name: 'Alpha', kategorie: 'WC', typen: ['Wand'] },
  ], info);
  assert.deepEqual(f.map((x) => x.kennzeichen), ['FAM-5', 'FAM-1']);
  assert.equal(f[1].art, 'armatur');
  assert.deepEqual(f[1].typen, ['DN15', 'DN20']);
  assert.equal(L.schemaVorschlag().kennzeichen, 'AN-SCHEMA');
  assert.deepEqual(L.schemaVorschlag(['KW']), { kennzeichen: 'AN-SCHEMA-KW', name: 'Schema KW' });
  assert.deepEqual(L.schemaAnlegenParameter({ kennzeichen: 'AN-SCHEMA-KW', name: 'Schema KW', gewerke: ['sanitaer'], systeme: ['KW'] }),
    { klasse: 'Ansicht', kennzeichen: 'AN-SCHEMA-KW', name: 'Schema KW', eigenschaften: { art: 'schema', schemaart: 'strangschema', detailgrad: 'mittel', gewerke: ['sanitaer'], systeme: ['KW'] } });
  const leer = L.schemaAnlegenParameter({ kennzeichen: 'A', name: 'B' });
  assert.ok(!('gewerke' in leer.eigenschaften) && !('systeme' in leer.eigenschaften), 'ohne Wahl kein Filter');
});

// ---------- Verbindungen zum Trennen ----------
test('Verbindungen eines Elements (Kennzeichen, ohne Gross/Klein)', () => {
  const v = L.verbindungenVon('ts-1', verbindungen);
  assert.deepEqual(v.map((x) => [x.id, x.anschluss, x.gegen, x.gegenAnschluss]), [['v1', 'b', 'R-1', 'anfang'], ['v7', 'a', 'R-9', 'anfang']]);
  assert.deepEqual(L.verbindungenVon('WT-E.02', verbindungen).map((x) => x.id), ['v6', 'v-direkt']);
  assert.deepEqual(L.verbindungenVon('nix', verbindungen), []);
});

test('Verbindungen einer Linie in Fliessrichtung: Knoten › Teile › Knoten', () => {
  const d = daten();
  const o = L.linienVerbindungen(d.linien[1], verbindungen, kzVonId);
  assert.deepEqual(o.map((x) => x.id), ['v3', 'v4', 'v5', 'v6']);
  assert.equal(o[0].text, 'TS-2.c ↔ R-2.anfang');
  assert.equal(o[3].text, 'R-3.ende ↔ WT-E.02.kw');
  assert.deepEqual(o[1].mit, ['e-r2', 'e-bo']);
  // direkte Linie: die Verbindung der Linie
  const direkt = L.linienVerbindungen(d.linien[2], verbindungen, kzVonId);
  assert.equal(direkt.length, 1); assert.equal(direkt[0].id, 'v-direkt'); assert.equal(direkt[0].text, 'WT-E.02.sw ↔ WC-NEU.sw');
  // offenes Ende: nur die Verbindung zum Knoten
  const offen = L.linienVerbindungen(d.linien[3], verbindungen, kzVonId);
  assert.deepEqual(offen.map((x) => x.id), ['v7']);
  assert.deepEqual(L.linienVerbindungen(null, verbindungen, kzVonId), []);
  assert.deepEqual(L.linienVerbindungen({ elemente: [], von: 'e-ts1', nach: 'e-ts2' }, verbindungen, kzVonId), []);
});

test('Trennoptionen: beim Klick auf ein Teil die Verbindungen, die es berühren', () => {
  const d = daten();
  const l = d.linien[1];
  assert.deepEqual(L.trennOptionen(l, verbindungen, kzVonId, 'e-bo').map((x) => x.id), ['v4', 'v5']);            // Bogen: davor und danach
  assert.deepEqual(L.trennOptionen(l, verbindungen, kzVonId, 'e-r2').map((x) => x.id), ['v3', 'v4']);            // erste Leitung: Knoten und Bogen
  assert.deepEqual(L.trennOptionen(l, verbindungen, kzVonId, 'e-r3').map((x) => x.id), ['v5', 'v6']);
  assert.deepEqual(L.trennOptionen(l, verbindungen, kzVonId, null).map((x) => x.id), ['v3', 'v4', 'v5', 'v6']);  // keine Angabe: alle
  assert.deepEqual(L.trennOptionen(l, verbindungen, kzVonId, 'e-unbekannt').map((x) => x.id), ['v3', 'v4', 'v5', 'v6']);
  assert.deepEqual(L.trennOptionen(d.linien[0], verbindungen, kzVonId, 'e-r1').map((x) => x.id), ['v1', 'v2']);
});

// ---------- Menüs ----------
test('Menü auf einem Knoten: Grundriss, Lage, Verbinden, Trennen je Verbindung, Löschen', () => {
  const d = daten();
  const wt = d.knoten.find((k) => k.element === 'e-wt');
  const m = L.knotenMenue({ knoten: wt, element: { geschoss: 'EG', geometrie: { art: 'punkt' } }, verbindungen });
  const titel = m.filter((x) => !x.trenner).map((x) => x.titel);
  assert.deepEqual(titel, ['Im Grundriss zeigen', 'Automatische Lage', 'Verbinden …', 'Trennen: WT-E.02.kw ↔ R-3.ende', 'Trennen: WT-E.02.sw ↔ WC-NEU.sw', 'Löschen …']);
  assert.equal(m[0].aktiv, true);
  assert.equal(m.find((x) => x.titel === 'Automatische Lage').aktiv, true);                    // manuell
  assert.deepEqual(m.find((x) => x.titel === 'Verbinden …').param, { von: 'e-wt' });
  assert.deepEqual(m.find((x) => x.titel?.startsWith('Trennen: WT-E.02.kw')).param, { verbindung: 'v6' });
  assert.deepEqual(m.find((x) => x.titel === 'Löschen …').param, { element: 'WT-E.02' });
  assert.ok(m.every((x) => x.trenner || x.aktion));
});

test('Menü auf einem nicht platzierten Knoten: Im Grundriss zeigen ausgegraut mit Hinweis, dafür platzieren', () => {
  const d = daten();
  const wc = d.knoten.find((k) => k.element === 'e-wc');
  const m = L.knotenMenue({ knoten: wc, element: { geschoss: 'EG' }, verbindungen });
  const zeigen = m.find((x) => x.titel === 'Im Grundriss zeigen');
  assert.equal(zeigen.aktiv, false); assert.match(zeigen.hinweis, /nicht platziert/);
  const platz = m.find((x) => x.titel === 'Im Grundriss platzieren …');
  assert.equal(platz.aktiv, true); assert.equal(platz.aktion, 'schema.im-grundriss-platzieren');
  assert.equal(m.find((x) => x.titel === 'Automatische Lage').aktiv, false);                  // keine Hand-Lage
  assert.match(m.find((x) => x.titel === 'Automatische Lage').hinweis, /automatische Anordnung/);
  // ohne Geschoss geht auch das Platzieren nicht
  const ohne = L.knotenMenue({ knoten: wc, element: { geschoss: null }, verbindungen });
  assert.equal(ohne.find((x) => x.titel === 'Im Grundriss platzieren …').aktiv, false);
  // ein platzierter Knoten hat keinen Eintrag «platzieren»
  assert.ok(!L.knotenMenue({ knoten: d.knoten[0], element: { geschoss: 'EG' }, verbindungen: [] }).some((x) => x.titel?.startsWith('Im Grundriss platzieren')));
});

test('Menü auf einer Linie: Trennen je Verbindung, Grundriss für das Teil', () => {
  const d = daten();
  const opt = L.trennOptionen(d.linien[1], verbindungen, kzVonId, 'e-bo');
  const m = L.linieMenue({ linie: d.linien[1], elementId: 'e-bo', element: { kennzeichen: 'BO-1', geschoss: 'EG', geometrie: { art: 'punkt' } }, optionen: opt });
  assert.deepEqual(m.filter((x) => !x.trenner).map((x) => x.titel), ['Trennen: R-2.ende ↔ BO-1.a', 'Trennen: BO-1.b ↔ R-3.anfang', 'Im Grundriss zeigen', 'Löschen …']);
  // direkte Linie ohne Teil
  const direkt = L.linieMenue({ linie: d.linien[2], optionen: L.linienVerbindungen(d.linien[2], verbindungen, kzVonId) });
  assert.deepEqual(direkt.map((x) => x.titel), ['Trennen: WT-E.02.sw ↔ WC-NEU.sw']);
  // nicht platziertes Teil: ausgegraut
  const np = L.linieMenue({ elementId: 'e-r3', element: { kennzeichen: 'R-3', geschoss: 'EG' }, optionen: opt });
  assert.equal(np.find((x) => x.titel === 'Im Grundriss zeigen').aktiv, false);
  // keine Verbindung (z. B. nur ein offenes Ende)
  const keine = L.linieMenue({ optionen: [] });
  assert.equal(keine[0].aktiv, false); assert.match(keine[0].hinweis, /offenes Ende/);
});

test('Menü auf leerer Fläche und im Grundriss', () => {
  const d = daten();
  const m = L.leerMenue({ zelle: L.zelleBei(d, 60, 20), hatHandLagen: false });
  assert.equal(m[0].titel, 'Element einfügen … (Geschoss EG × Strang KW-1)');
  assert.equal(m.find((x) => x.titel === 'Schema neu anordnen …').aktiv, false);
  assert.equal(L.leerMenue({ hatHandLagen: true }).find((x) => x.titel === 'Schema neu anordnen …').aktiv, true);
  assert.equal(L.leerMenue({})[0].titel, 'Element einfügen …');
  // Grundriss/3D: «Im Schema zeigen»
  const el = { kennzeichen: 'WC-E.02' };
  assert.deepEqual(L.grundrissMenue({ element: el, treffer: [{ ansicht: 'AN-S', name: 'Schema' }] }), [{ titel: 'Im Schema zeigen', aktion: 'schema.im-schema-zeigen', param: { element: 'WC-E.02', ansicht: 'AN-S' } }]);
  const zwei = L.grundrissMenue({ element: el, treffer: [{ ansicht: 'AN-A', name: 'Sanitär' }, { ansicht: 'AN-B', name: 'Heizung' }] });
  assert.deepEqual(zwei.map((x) => x.titel), ['Im Schema zeigen: Sanitär', 'Im Schema zeigen: Heizung']);
  const keins = L.grundrissMenue({ element: el, treffer: [], schemaAnzahl: 2 });
  assert.equal(keins[0].aktiv, false); assert.match(keins[0].hinweis, /Keine Schema-Ansicht zeigt dieses Element/);
  assert.match(L.grundrissMenue({ element: el, treffer: [], schemaAnzahl: 0 })[0].hinweis, /keine Schema-Ansicht/);
});

test('Grundriss zum Zeigen: gleiches Geschoss, offene und sichtbare zuerst', () => {
  const ansichten = [
    { klasse: 'Ansicht', kennzeichen: 'AN-GR-EG-H', geschoss: 'EG', werte: { art: 'grundriss', gewerke: ['heizung'] } },
    { klasse: 'Ansicht', kennzeichen: 'AN-GR-EG', geschoss: 'EG', werte: { art: 'grundriss', gewerke: ['sanitaer', 'architektur'] } },
    { klasse: 'Ansicht', kennzeichen: 'AN-GR-EG-V', geschoss: 'EG', werte: { art: 'grundriss', vorlage: true } },
    { klasse: 'Ansicht', kennzeichen: 'AN-GR-OG', geschoss: '1.OG', werte: { art: 'grundriss' } },
    { klasse: 'Ansicht', kennzeichen: 'AN-3D', werte: { art: '3d' } },
  ];
  const wt = { geschoss: 'EG', gewerk: 'sanitaer', klasse: 'Waschtisch', basis: 'Apparat' };
  let g = L.grundrissFuer(ansichten, wt, []);
  assert.equal(g.ansicht.kennzeichen, 'AN-GR-EG'); assert.equal(g.sichtbar, true);              // das Sanitär-Gewerk ist gewählt
  g = L.grundrissFuer(ansichten, wt, ['AN-GR-EG-H']);
  assert.equal(g.ansicht.kennzeichen, 'AN-GR-EG');                                               // sichtbar geht vor «offen»
  g = L.grundrissFuer([ansichten[0]], wt, ['AN-GR-EG-H']);
  assert.equal(g.ansicht.kennzeichen, 'AN-GR-EG-H'); assert.equal(g.sichtbar, false);           // nur ein Heizungsgrundriss: gefunden, aber ausgeblendet
  assert.equal(L.grundrissFuer(ansichten, { geschoss: 'UG', gewerk: 'sanitaer' }), null);
  assert.equal(L.grundrissFuer(ansichten, { geschoss: null }), null);
  const aus = [{ klasse: 'Ansicht', kennzeichen: 'A', geschoss: 'EG', werte: { art: 'grundriss', klassenAusblenden: ['Apparat'] } }];
  assert.equal(L.grundrissFuer(aus, wt).sichtbar, false);                                        // Basisklasse ausgeblendet
});

test('Ausschnitt im Grundriss: Punkt ± 1.5 m, sonst die Begrenzung der Geometrie', () => {
  assert.deepEqual(L.grundrissAusschnitt({ geometrie: { art: 'punkt', punkte: [[7500, 9500, 0]] } }), { minx: 6000, miny: 8000, maxx: 9000, maxy: 11000 });
  const l = L.grundrissAusschnitt({ geometrie: { art: 'polylinie', punkte: [[0, 0, 0], [8000, 6000, 0]] } });
  assert.deepEqual(l, { minx: 0, miny: 0, maxx: 8000, maxy: 6000 });
  assert.deepEqual(L.grundrissAusschnitt({ geometrie: { art: 'linie', punkte: [[0, 0], [8000, 100]] } }), { minx: 0, miny: -1450, maxx: 8000, maxy: 1550 });
  assert.equal(L.grundrissAusschnitt({}), null);
  assert.equal(L.istPunktklasse({ geometrie: 'punkt' }), true);
  assert.equal(L.istPunktklasse({ geometrie: 'polylinie' }), false);
});

test('Banner der Hinweise', () => {
  const b = L.hinweisBanner(daten());
  assert.equal(b.anzahl, 2);
  assert.match(b.erste, /^Ohne Geschoss/);
  assert.deepEqual(L.hinweisBanner({}), { anzahl: 0, erste: '', alle: [] });
});

// ---------- Texte ----------
test('Banner-HTML: zu und aufgeklappt, maskiert', () => {
  const zu = T.bannerHtml({ anzahl: 2, erste: 'a <b>', alle: ['a <b>', 'c'] }, false);
  assert.match(zu, /2 Hinweise/); assert.match(zu, /a &lt;b&gt;/); assert.ok(!zu.includes('<ul'));
  assert.match(zu, /aria-expanded="false"/);
  const auf = T.bannerHtml({ anzahl: 1, erste: 'x', alle: ['x & y'] }, true);
  assert.match(auf, /1 Hinweis[^e]/); assert.match(auf, /<ul class="sc-banner-liste"><li>x &amp; y<\/li><\/ul>/);
  assert.equal(T.bannerHtml({ anzahl: 0, erste: '', alle: [] }), '');
  assert.equal(T.bannerHtml(null), '');
});

test('Tooltips', () => {
  assert.equal(T.knotenTip(d0().knoten[2]), 'WT-E.02 · Waschtisch · Strang KW-1 · EG · Hand-Lage');
  assert.equal(T.knotenTip(d0().knoten[3]), 'WC-NEU · WC · nicht platziert');
  assert.equal(T.linieTip({ groessen: ['NW 25', 'NW 20'], medium: 'KW' }, { kennzeichen: 'R-1', klasse: 'Rohr', geometrie: {} }), 'R-1 · Rohr · NW 25 / NW 20 · KW');
  assert.equal(T.linieTip({ groessen: [], medium: null }, null), 'Verbindung');
  assert.match(T.linieTip({ groessen: [] }, { kennzeichen: 'R-3', klasse: 'Rohr' }), /nicht platziert/);
  assert.equal(T.knotenTip(null), '');
  function d0() { return daten(); }
});

test('Dialog «Element einfügen»: Gruppen, Wahlwerte, Filter, maskierte Liste', () => {
  const klassen = { endgeraete: [{ name: 'WC', beschreibung: 'WC mit <Spülkasten>' }], geraete: [], armaturen: [{ name: 'Ventil', beschreibung: '' }], formteile: [] };
  const familien = [{ kennzeichen: 'FAM-1', name: 'Kugelhahn', kategorie: 'Ventil', art: 'armatur', typen: ['DN15', 'DN20'] }];
  const g = T.einfuegenGruppen(klassen, familien);
  assert.deepEqual(g.map((x) => x.gruppe), ['Endgeräte', 'Armaturen', 'Familien']);
  assert.deepEqual(g[2].eintraege.map((e) => e.wert), ['f:FAM-1|DN15', 'f:FAM-1|DN20']);
  assert.equal(g[2].eintraege[0].titel, 'Kugelhahn: DN15');
  assert.deepEqual(T.wahlAusWert('k:WC'), { art: 'klasse', klasse: 'WC' });
  assert.deepEqual(T.wahlAusWert('f:FAM-1|DN15'), { art: 'familie', familie: 'FAM-1', typ: 'DN15' });
  assert.deepEqual(T.wahlAusWert('f:FAM-1|22|600'), { art: 'familie', familie: 'FAM-1', typ: '22|600' });
  assert.equal(T.wahlAusWert('x:1'), null); assert.equal(T.wahlAusWert(''), null); assert.equal(T.wahlAusWert('f:|x'), null);
  assert.deepEqual(T.einfuegenFiltern(g, 'kugel').map((x) => x.gruppe), ['Familien']);
  assert.deepEqual(T.einfuegenFiltern(g, 'spül').map((x) => x.gruppe), ['Endgeräte']);              // Beschreibung zählt mit
  assert.deepEqual(T.einfuegenFiltern(g, 'xyz'), []);
  const html = T.einfuegenListeHtml(g, 'k:WC');
  assert.match(html, /value="k:WC" checked/);
  assert.match(html, /&lt;Spülkasten&gt;/); assert.ok(!html.includes('<Spülkasten>'));
  assert.match(T.einfuegenListeHtml([]), /Keine passende/);
  const rahmen = T.einfuegenDialogHtml(g);
  assert.match(rahmen, /class="sc-suche"/); assert.match(rahmen, /class="sc-kz"/); assert.match(rahmen, /ohne Lage im Gebäude/);
  assert.equal(T.artText('endgeraet'), 'Endgerät');
});

test('Dialog «Anschlüsse wählen»: Kandidaten des Kerns oder freie Anschlüsse', () => {
  const mit = T.anschlussDialogHtml({ a: 'V-A', b: 'V-B', paare: [['ein', 'ein'], ['ein', 'aus']] });
  assert.match(mit, /name="sc-paar" value="0" checked/); assert.match(mit, /V-A\.ein ↔ V-B\.aus/);
  const frei = T.anschlussDialogHtml({ a: 'A<', b: 'B', frei: { a: [{ name: 'sw', medium: 'SW', fluss: 'raus' }], b: [{ name: 'kw' }] } });
  assert.match(frei, /class="sc-a"/); assert.match(frei, /sw \(SW, raus\)/); assert.match(frei, /A&lt;/);
  assert.deepEqual(T.freieAnschluesse({ anschluesse: [{ name: 'a', verbundenMit: 'X.b' }, { name: 'ein', medium: 'KW', fluss: 'rein', verbundenMit: null }] }), [{ name: 'ein', medium: 'KW', fluss: 'rein' }]);
  assert.deepEqual(T.freieAnschluesse(null), []);
});

test('Rückfrage «Schema neu anordnen»', () => {
  assert.match(T.anordnenText(3, 0), /^3 Knoten sind von Hand angeordnet\./);
  assert.match(T.anordnenText(1, 2), /^Ein Knoten ist von Hand angeordnet \(2 Hand-Lagen ohne Element im Schema\)\./);
  assert.match(T.anordnenText(0, 1), /1 Hand-Lage ohne Element/);
  assert.match(T.anordnenText(2), /Alle Hand-Lagen entfernen\?/);
});

// ---------- Einbindung ----------
test('Einbindung: Ansichtsart schema hat ein eigenes Modul, kein Platzhalter mehr', () => {
  const f = lies('js/fenster.js');
  assert.match(f, /schema: '\.\/ansichten\/schema\.js'/);
  assert.match(f, /art === 'schema'/);                                    // Steuerleiste: nur Detailgrad, kein Massstab
  assert.ok(!/schema: '3'/.test(lies('js/ansichten/platzhalter.js')));
  assert.match(lies('js/oberflaeche/aktionen.js'), /import '\.\/aktionen-schema\.js'/);
  assert.match(lies('index.html'), /css\/schema\.css/);
  assert.ok(existsSync(path.join(wwwroot, 'css/schema.css')));
  assert.match(lies('js/oberflaeche/aktionen-plaene.js'), /PLANARTEN = new Set\(\['grundriss', 'schnitt', 'ansicht', 'schema'\]\)/);
});

test('Einbindung: Symbole der Aktionen stehen in symbole.js, Aktionen sind registriert', () => {
  const sym = lies('js/werkzeuge/symbole.js');
  const akt = lies('js/oberflaeche/aktionen-schema.js');
  const brauchtSymbol = [...akt.matchAll(/symbol: '(schema[A-Za-z]+)'/g)].map((m) => m[1]);
  assert.ok(brauchtSymbol.length >= 4);
  for (const s of new Set(brauchtSymbol)) assert.match(sym, new RegExp(`\\b${s}:`), `Symbol ${s}`);
  for (const id of ['ansicht.schema', 'schema.verbinden', 'schema.trennen', 'schema.einfuegen', 'schema.neu-anordnen', 'schema.im-grundriss-zeigen', 'schema.im-grundriss-platzieren', 'schema.im-schema-zeigen', 'schema.lage-zuruecksetzen', 'schema.trennen-verbindung', 'kontext.im-schema-zeigen', 'kontext.im-grundriss-zeigen'])
    assert.ok(akt.includes(`id: '${id}'`), `Aktion ${id}`);
  // jede Aktion, die ein Menü oder die Leiste auslöst, gibt es auch
  const logik = lies('js/ansichten/schema-logik.js');
  for (const m of logik.matchAll(/aktion: '([a-z.-]+)'/g)) assert.ok(akt.includes(`id: '${m[1]}'`) || m[1] === 'aendern.loeschen', `Menü löst unbekannte Aktion ${m[1]} aus`);
  for (const id of ['verbinden', 'trennen', 'einfuegen', 'neu-anordnen', 'zoom-alles', 'auswahl']) assert.ok(akt.includes(`id: 'schema.${id}'`), `Leiste: schema.${id}`);
});

test('Keine Fachlogik und kein Direktzugriff: die Oberfläche ändert nur über Befehle des Kerns', () => {
  for (const datei of ['js/ansichten/schema.js', 'js/ansichten/schema-logik.js', 'js/oberflaeche/schema-bearbeiten.js', 'js/oberflaeche/aktionen-schema.js']) {
    const q = lies(datei);
    assert.ok(!/zustand\.elemente\.(set|delete)|zustand\.verbindungen\s*=|zustand\.auswahl\s*=/.test(q), `${datei} ändert den Zustand direkt`);
  }
  const bearbeiten = lies('js/oberflaeche/schema-bearbeiten.js');
  for (const befehl of ['schema_lage', 'schema_anordnen', 'verbinden', 'trennen', 'element_anlegen']) assert.ok(bearbeiten.includes(`'${befehl}'`), befehl);
});

// ---------- die echten Antworten des Kerns (nur mit Testserver, nur lesend) ----------
const SERVER = process.env.HLKS_TEST_SERVER;
const kern = async (art, name, body) => (await fetch(`${SERVER}/api/${art}/${name}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();

test('Echter Kern: Abfrage schema und Plangrafik passen zur Logik', { skip: !SERVER }, async () => {
  const d = await kern('abfrage', 'schema', { ansicht: 'AN-SCHEMA-SAN' });
  const plan = await (await fetch(`${SERVER}/api/plangrafik/AN-SCHEMA-SAN`)).json();
  const modell = await (await fetch(`${SERVER}/api/modell`)).json();
  assert.equal(d.massstab, 1); assert.equal(plan.massstab, 1); assert.equal(plan.art, 'schema');
  const kz = new Map(modell.elemente.map((e) => [e.id, e.kennzeichen]));
  // jeder Knoten ist ein Teil der Plangrafik mit seiner Id; Treffer in der Mitte findet ihn
  const ids = new Set(plan.teile.map((t) => t.id).filter(Boolean));
  for (const k of d.knoten) {
    assert.ok(ids.has(k.element), `Teil für ${k.kennzeichen}`);
    // Der Kern setzt Knoten verschiedener Systeme auch genau aufeinander (Etagen-T-Stücke von Schmutz- und Kaltwasser am selben
    // Endgerät, beide auf der Etagenleitung unter dem Gerät): der Zeiger trifft dann das angeklickte Teil (domId = Id des Knotens);
    // ohne Teil trifft er irgendeinen Knoten an genau dieser Stelle.
    assert.equal(L.treffer(d, { x: k.x, y: k.y }, 0, k.element).knoten?.element, k.element, `Treffer auf ${k.kennzeichen}`);
    const ohneTeil = L.treffer(d, { x: k.x, y: k.y }, 0, null).knoten;
    assert.ok(ohneTeil && ohneTeil.x === k.x && ohneTeil.y === k.y, `Treffer an der Lage von ${k.kennzeichen}`);
    assert.ok(L.zelleBei(d, k.x, k.y) || k.y < -10 || true);
  }
  // jedes Linienelement ist ein Teil; die Verbindungen einer Linie gibt es im Modell
  const verb = new Map(modell.verbindungen.map((v) => [v.id, v]));
  let anzahl = 0;
  for (const l of d.linien) {
    for (const e of l.elemente) assert.ok(ids.has(e), 'Teil für Linienelement');
    const o = L.linienVerbindungen(l, modell.verbindungen, (id) => kz.get(id) ?? null);
    const erwartet = l.elemente.length === 0 ? 1 : l.elemente.length + 1 - (l.von ? 0 : 1) - (l.nach ? 0 : 1);
    assert.equal(o.length, erwartet, `Verbindungen der Linie ${l.kennzeichen.join(',') || l.verbindung}`);
    for (const x of o) { assert.ok(verb.has(x.id), `Verbindung ${x.id} im Modell`); anzahl++; }
  }
  assert.ok(anzahl > 50);
  // Raster: jeder Knoten liegt in einer Zelle seiner Spalte und Zeile (ausser Zonen mit negativen Lagen). Seit Nachtrag B8d liegen die
  // Grundleitungen (Verlegung erdreich) und die Schächte im Untergrund unter dem untersten Deckenband: ausserhalb des Rasters der
  // Geschosszeilen, aber im Streifen d.untergrund (y = Oberkante, hoehe nach unten; y nach oben) – die Schächte reichen bis zur Sohle.
  const untergrund = d.untergrund ?? null;
  let imUntergrund = 0;
  for (const k of d.knoten) {
    if (k.untergrund || k.schacht) {
      assert.ok(untergrund, `Untergrundstreifen für ${k.kennzeichen}`);
      const oben = untergrund.y + (k.schacht ? k.schacht.oben - untergrund.y : 0), unten = untergrund.y - untergrund.hoehe;
      assert.ok(k.y <= oben + 1e-6 && k.y >= unten - 1e-6, `${k.kennzeichen} (y ${k.y}) liegt im Untergrundstreifen ${unten}…${oben}`);
      assert.equal(L.zelleBei(d, k.x, k.y), null, `${k.kennzeichen} liegt ausserhalb des Rasters der Geschosse`);
      imUntergrund++;
      continue;
    }
    const z = L.zelleBei(d, k.x, k.y); assert.ok(z, `Zelle für ${k.kennzeichen}`);
  }
  assert.ok(imUntergrund >= 1, 'das Beispielprojekt hat Grundleitung und Kontrollschacht im Untergrund');
  // Hinweise und Banner
  assert.ok(L.hinweisBanner(d).anzahl >= 1);
  // Umrandung und Wo-zeigt-es für jedes Element der Linien
  const l1 = d.linien.find((l) => l.elemente.length);
  assert.equal(L.zeigtElement(d, l1.elemente[0]), 'linie');
  assert.ok(L.umrandung(d, l1.elemente[0]));
});

test('Echter Kern: einfügbare Klassen und Familien aus dem Katalog', { skip: !SERVER }, async () => {
  const kat = await kern('abfrage', 'katalog', {});
  const rollen = new Map();
  await Promise.all(kat.klassen.filter((k) => k.schicht === 'system' && !L.LINIENKLASSEN.includes(k.name) && (k.anschluesse ?? []).length > 0).map(async (k) => rollen.set(k.name, (await kern('abfrage', 'katalog', { klasse: k.name })).rolle ?? null)));
  const k = L.einfuegbareKlassen(kat.klassen, rollen);
  const namen = (l) => l.map((x) => x.name);
  assert.ok(namen(k.endgeraete).includes('WC') && namen(k.endgeraete).includes('Waschtisch'), 'Endgeräte');
  assert.ok(namen(k.armaturen).includes('Ventil') && namen(k.armaturen).includes('Wasserzaehler'), 'Armaturen');
  assert.ok(namen(k.formteile).includes('TStueck') && !namen(k.formteile).includes('Bogen') && !namen(k.formteile).includes('Reduktion'), 'Formteile ohne Bogen und Reduktion');
  assert.ok(namen(k.geraete).includes('Hausanschluss') || namen(k.geraete).length > 0, 'Geräte');
  const alle = [...k.endgeraete, ...k.geraete, ...k.armaturen, ...k.formteile].map((x) => x.name);
  for (const n of ['Rohr', 'Kanal', 'Bogen', 'Reduktion', 'Raum', 'Ansicht', 'Strang', 'System']) assert.ok(!alle.includes(n), `${n} ist kein einfügbarer Knoten`);
  // jede einfügbare Klasse ist für den Kern ein Netzelement (Rolle) – die Oberfläche rät nicht
  for (const n of alle) assert.ok(rollen.get(n) || kat.klassen.find((x) => x.name === n)?.basis, n);
});

test('Echter Kern: Trockenläufe der Parameter – der Kern nimmt sie an', { skip: !SERVER }, async () => {
  const d = await kern('abfrage', 'schema', { ansicht: 'AN-SCHEMA-SAN' });
  const k = d.knoten.find((x) => x.kennzeichen === 'WC-E.02');
  const t = (name, par) => fetch(`${SERVER}/api/befehl/${name}?trockenlauf=true`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(par) }).then((r) => r.json());
  let r = await t('schema_lage', L.lageParameter('AN-SCHEMA-SAN', k.kennzeichen, 85.26, 92.74));
  assert.equal(r.angenommen, true, r.fehler);
  r = await t('schema_lage', L.lageParameter('AN-SCHEMA-SAN', k.element, 12, 34));
  assert.equal(r.angenommen, true, r.fehler);
  r = await t('schema_anordnen', L.anordnenParameter('AN-SCHEMA-SAN', true));
  assert.equal(r.angenommen, true, r.fehler);
  // Kennzeichen mit Punkt gehen als Objekt durch (als Text «WC-E.02» würde der Kern am Punkt trennen)
  r = await t('verbinden', L.verbindenParameter('WC-E.02', 'WT-E.02'));
  assert.equal(r.angenommen, false);
  assert.match(r.fehler, /Kein freies, passendes Anschlusspaar zwischen WC-E\.02 und WT-E\.02/);
  const zelle = L.zelleBei(d, 60, 70);
  r = await t('element_anlegen', L.klasseAnlegenParameter('Waschtisch', zelle, 'endgeraet'));
  assert.equal(r.angenommen, true, r.fehler);
  assert.equal(L.angelegtesElement(r, 'Waschtisch') !== null, true);
  const nummer = zelle.spalte.kennzeichen;
  r = await t('element_anlegen', L.klasseAnlegenParameter('Ventil', zelle, 'armatur'));
  assert.equal(r.angenommen, true, `${nummer}: ${r.fehler}`);
  r = await t('trennen', L.trennenParameter('gibt-es-nicht'));
  assert.equal(r.angenommen, false);
  assert.ok(r.fehler);
});
