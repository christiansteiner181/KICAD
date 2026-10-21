// Tests der Leitungswerkzeuge Rohr und Kanal (Modul 3, C1) ohne Browser: Winkelfang nach den erlaubten Winkeln der Serie, Anschlussfang,
// Höhenwechsel, Aufbau der Befehlsparameter (von/nach, Fliessrichtung, System/Strang), Zifferneingabe, Zustand der Optionsleiste, Texte der
// Vorschau-Zeile aus den Hinweisen des Kerns, Palette. Dazu Lesetests gegen den Kern-Quelltext (Parameternamen, Hinweisformat).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/werkzeuge/leitung.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const F = await import('./fangen.js');
const L = await import('./leitung-logik.js');
const P = await import('./leitung-palette.js');
const O = await import('./optionen.js');
const D = await import('./leitung-daten.js');

const nah = (a, b, eps = 1e-6) => assert.ok(Math.abs(a - b) <= eps, `${a} ≠ ${b}`);
const KERN = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../Hlks.Kern');
const quelle = (datei) => readFileSync(path.join(KERN, datei), 'utf8');

// Antwort der Abfrage leitungsserie (Kürzung der echten Antwort für die fiktive Testserie, Modul 3)
const serieRohr = {
  familie: 'FAM-4', name: 'Testrohrserie (fiktiv)', kategorie: 'Rohr', querschnitt: 'rund', winkel: [15, 30, 45, 90], winkelRegel: 'liste',
  groessen: [
    { typ: 'DN 15', gueltig: true, dn: 15, aussen: 20, innen: 16, wand: 2 },
    { typ: 'DN 20', gueltig: true, dn: 20, aussen: 25, innen: 21, wand: 2 },
    { typ: 'DN 99', gueltig: false, dn: 99, aussen: null },
  ],
};
const serieKanal = { familie: 'FAM-9', kategorie: 'Kanal', querschnitt: 'rechteck', winkel: [45, 90], winkelRegel: 'liste', groessen: [{ typ: '500x300', gueltig: true, b: 500, h: 300, aussen: 500, innen: null, wand: null }] };

// ============================================================================================== Winkel

test('Winkel: erlaubte Winkel der Serie (Liste, frei, keine Regel)', () => {
  assert.deepEqual(L.erlaubteWinkel(serieRohr), [15, 30, 45, 90]);
  assert.deepEqual(L.erlaubteWinkel({ ...serieRohr, winkel: [90, 45, 45] }), [45, 90]);
  assert.equal(L.erlaubteWinkel({ winkelRegel: 'frei', winkel: null }), null);
  assert.equal(L.erlaubteWinkel({ winkelRegel: 'keine' }), null);
  assert.equal(L.erlaubteWinkel(null), null);
});

test('Winkel: Unterschied und Normierung', () => {
  nah(L.winkelDiff(350, 10), 20);
  nah(L.winkelDiff(0, 180), 180);
  nah(L.winkelDiff(90, 85), 5);
  nah(L.normWinkel(-90), 270);
});

test('Winkelfang: Kandidaten ohne Vorgänger sind die Achsen, mit Vorgänger dessen Winkel der Serie (ohne Kehrtwende)', () => {
  assert.deepEqual(L.richtungsKandidaten({}), [0, 90, 180, 270]);
  const k = L.richtungsKandidaten({ vorher: 0, winkel: [15, 30, 45, 90] });
  for (const w of [0, 15, 30, 45, 90, 270, 315, 330, 345]) assert.ok(k.includes(w), `${w} fehlt`);
  assert.ok(!k.includes(180), 'Kehrtwende');
  assert.ok(!k.includes(60), '60° ist nicht in der Serie');
  const k2 = L.richtungsKandidaten({ vorher: 90, winkel: [45] });
  assert.ok(k2.includes(135) && k2.includes(45) && !k2.includes(270));
  const frei = L.richtungsKandidaten({ vorher: 0, winkel: null });
  assert.ok(frei.includes(45) && frei.includes(315) && frei.includes(90));
});

test('Winkelfang: am Anschluss und beim Abzweig gelten nur die vorgegebenen Richtungen', () => {
  assert.deepEqual(L.richtungsKandidaten({ erster: 270 }), [270]);
  assert.deepEqual(L.richtungsKandidaten({ nur: [90, 270] }), [90, 270]);
  assert.deepEqual(L.abzweigRichtungen(0), [90, 270]);
  assert.deepEqual(L.abzweigRichtungen(null), []);
});

test('Winkelfang: rastet innerhalb der Toleranz auf den Kandidaten ein, behält die Länge', () => {
  const kand = [0, 45, 90, 270];
  const r = L.winkelFang({ letzter: [0, 0], maus: [1000, 20], kandidaten: kand });         // ≈ 1.15° über 0°
  assert.equal(r.winkel, 0);
  nah(r.punkt[0], Math.hypot(1000, 20), 1e-6);
  nah(r.punkt[1], 0, 1e-9);
  const d = L.winkelFang({ letzter: [100, 100], maus: [100 + 700, 100 + 720], kandidaten: kand });   // ≈ 45.8° → 45°
  assert.equal(d.winkel, 45);
  nah(d.punkt[1] - 100, d.punkt[0] - 100, 1e-6);
  nah(Math.hypot(d.punkt[0] - 100, d.punkt[1] - 100), Math.hypot(700, 720), 1e-6);
  assert.equal(L.winkelFang({ letzter: [0, 0], maus: [1000, 200], kandidaten: kand }), null, '11.3° ist nicht in Reichweite');
  assert.equal(L.winkelFang({ letzter: [0, 0], maus: [0, 0], kandidaten: kand }), null, 'keine Richtung');
  assert.equal(L.winkelFang({ letzter: [0, 0], maus: [10, 1], kandidaten: [] }), null);
});

test('Winkelfang: Länge auf das Raster', () => {
  const r = L.winkelFang({ letzter: [0, 0], maus: [1234, 10], kandidaten: [0], raster: 100 });
  assert.deepEqual(r.punkt.map((x) => Math.round(x)), [1200, 0]);
  const kurz = L.winkelFang({ letzter: [0, 0], maus: [30, 0], kandidaten: [0], raster: 100 });
  assert.equal(Math.round(kurz.punkt[0]), 100, 'mindestens ein Rasterschritt');
});

test('Winkelfang: Zifferneingabe gilt in der gefangenen Richtung', () => {
  const w = L.winkelFang({ letzter: [0, 0], maus: [800, 830], kandidaten: [0, 45, 90] });
  const p = F.punktNachLaenge([0, 0], w.punkt, 1500);
  nah(p[0], 1500 * Math.SQRT1_2, 1e-6);
  nah(p[1], 1500 * Math.SQRT1_2, 1e-6);
  nah(F.abstand([0, 0], p), 1500, 1e-6);
});

test('Winkelfang: Richtung der letzten waagrechten Geraden (senkrechte Stücke zählen nicht)', () => {
  nah(L.letzteRichtung([[0, 0, 300], [1000, 0, 300]]), 0);
  nah(L.letzteRichtung([[0, 0, 300], [0, 1000, 300]]), 90);
  assert.equal(L.letzteRichtung([[0, 0, 300], [1000, 0, 300], [1000, 0, 2800]]), null, 'nach der Steigleitung gibt es keine Richtung');
  assert.equal(L.letzteRichtung([[0, 0, 300]]), null);
});

// ============================================================================================== Anschlussfang

const hk = { element: 'HK-1', name: 'rl', medium: 'HZ-RL', fluss: 'raus', dn: 15, lage: [5900, 3050, 120], richtung: [0, 0, -1], verbunden: null, p: [5900, 3050] };
const antwortHk = {
  element: 'HK-1',
  anschluesse: [
    { name: 'vl', medium: 'HZ-VL', fluss: 'rein', dn: 15, lage: [5950, 3050, 120], richtung: [0, 0, -1], verbunden: null },
    { name: 'rl', medium: 'HZ-RL', fluss: 'raus', dn: 15, lage: [5900, 3050, 120], richtung: [0, 0, -1], verbunden: 'RO-3.anfang' },
    { name: 'ohne', medium: '*', fluss: 'beide', lage: null, richtung: null, verbunden: null },
  ],
};

test('Anschlüsse: Antwort anschlusslagen → Fangpunkte (ohne Lage nicht fangbar), Text mit Name, Medium, Grösse, frei/belegt', () => {
  const a = L.anschluesseAus(antwortHk);
  assert.equal(a.length, 2);
  assert.deepEqual(a[0].p, [5950, 3050]);
  assert.equal(L.anschlussText(a[0]), 'HK-1.vl · HZ-VL · rein · DN 15 · frei');
  assert.equal(L.anschlussText(a[1]), 'HK-1.rl · HZ-RL · raus · DN 15 · belegt (RO-3.anfang)');
  assert.equal(L.anschlussFrei(a[0]), true);
  assert.equal(L.anschlussFrei(a[1]), false);
  assert.equal(L.anschlussGroesse({ b: 500, h: 300 }), '500×300');
  assert.equal(L.anschlussGroesse({ d: 160 }), 'Ø160');
  assert.equal(L.anschlussGroesse({}), '–');
  assert.deepEqual(L.anschlussEnde(a[0]), { element: 'HK-1', anschluss: 'vl' });
  assert.deepEqual(L.anschluesseAus(null), []);
});

test('Anschlüsse: Richtung im Grundriss (senkrechter Anschluss hat keine)', () => {
  assert.equal(L.anschlussRichtungPlan(hk), null);
  nah(L.anschlussRichtungPlan({ richtung: [0, 1, 0] }), 90);
  nah(L.anschlussRichtungPlan({ richtung: [-1, 0, 0] }), 180);
  assert.equal(L.anschlussRichtungPlan({ richtung: null }), null);
});

test('Fangen: ein Anschluss geht allen anderen Fängen vor (auch einem näheren Endpunkt)', () => {
  const daten = { punkte: [{ p: [5905, 3050], id: 'x' }], kanten: [] };
  const extra = L.anschlussFangpunkte(L.anschluesseAus(antwortHk));
  const f = F.fangen({ punkt: [5908, 3050], daten, radius: 12, extraPunkte: extra, raster: 100 });
  assert.equal(f.art, 'anschluss');
  assert.deepEqual(f.punkt, [5900, 3050]);
  assert.equal(f.anschluss.name, 'rl');
  const weit = F.fangen({ punkt: [5908, 3050], daten, radius: 1, extraPunkte: extra, raster: 0 });
  assert.notEqual(weit.art, 'anschluss', 'ausserhalb des Radius');
  assert.equal(F.FANGNAMEN.anschluss, 'Anschluss');
});

test('Fangen: Anschluss mit Shift nur auf der waagrechten/senkrechten Linie; ohne Anschluss-Art bleibt alles wie vorher', () => {
  const extra = L.anschlussFangpunkte(L.anschluesseAus(antwortHk));
  const a = F.fangen({ punkt: [5902, 3048], daten: { punkte: [], kanten: [] }, radius: 12, extraPunkte: extra, letzter: [3000, 3050], ortho: true });
  assert.equal(a.art, 'anschluss');
  assert.deepEqual(a.punkt, [5900, 3050], 'beide Anschlüsse liegen auf der waagrechten Linie, rl ist näher');
  const ohne = F.fangen({ punkt: [5902, 3048], daten: { punkte: [{ p: [5900, 3050], id: 'k' }], kanten: [] }, radius: 12, extraPunkte: [[5900, 3050]], raster: 100 });
  assert.equal(ohne.art, 'endpunkt', 'gewöhnliche Zusatzpunkte (Arrays) bleiben Endpunkte');
  const nur = F.fangen({ punkt: [5902, 3048], daten: { punkte: [], kanten: [] }, radius: 12, extraPunkte: extra, arten: ['mitte'] });
  assert.notEqual(nur.art, 'anschluss', 'Art abschaltbar');
});

test('Anschlüsse: nahe Anschlüsse nach Abstand', () => {
  const liste = L.anschluesseAus(antwortHk);
  assert.equal(L.anschluesseNahe(liste, [5910, 3050], 100).length, 2);
  assert.equal(L.anschluesseNahe(liste, [5910, 3050], 100)[0].name, 'rl');
  assert.equal(L.anschluesseNahe(liste, [9000, 9000], 100).length, 0);
});

// ============================================================================================== Fliessrichtung und Parameter

test('Fliessrichtung: Start an «rein» oder Ende an «raus» dreht die Zeichnung um (Verlauf in Fliessrichtung)', () => {
  const pts = [[0, 0, 0], [1000, 0, 0]];
  const vl = { element: 'HK-1', name: 'vl', fluss: 'rein' };
  const rl = { element: 'HK-1', name: 'rl', fluss: 'raus' };
  const r1 = L.flussRichtung({ punkte: pts, von: vl });
  assert.equal(r1.umgedreht, true);
  assert.deepEqual(r1.punkte, [[1000, 0, 0], [0, 0, 0]]);
  assert.equal(r1.von, null);
  assert.equal(r1.nach, vl);
  const r2 = L.flussRichtung({ punkte: pts, von: rl });
  assert.equal(r2.umgedreht, false);
  assert.equal(r2.von, rl);
  const r3 = L.flussRichtung({ punkte: pts, nach: rl });
  assert.equal(r3.umgedreht, true);
  assert.equal(r3.von, rl);
  const r4 = L.flussRichtung({ punkte: pts, von: vl, nach: rl });                // gedreht passt beides
  assert.equal(r4.umgedreht, true);
  assert.equal(r4.von, rl);
  assert.equal(r4.nach, vl);
  const beide = L.flussRichtung({ punkte: pts, von: { fluss: 'beide' }, nach: { fluss: 'beide' } });
  assert.equal(beide.umgedreht, false);
  const falsch = L.flussRichtung({ punkte: pts, von: rl, nach: rl });            // nichts passt: der Kern meldet es
  assert.equal(falsch.umgedreht, false);
});

const opt = { serie: 'FAM-4', groesse: 'DN 20', hoehe: 300, gefaelle: null, system: 'KW', strang: '', daemmdicke: null };

test('leitung_zeichnen: Parameter mit Serie, Grösse, gerundeten Punkten, System und Geschoss', () => {
  const { parameter: p } = L.leitungParameter({ opt, punkte: [[1000.004, 1000, 300], [3000, 1000, 300], [3000, 3000, 300]], geschoss: 'EG' });
  assert.deepEqual(p, { serie: 'FAM-4', groesse: 'DN 20', punkte: [[1000, 1000, 300], [3000, 1000, 300], [3000, 3000, 300]], system: 'KW', geschoss: 'EG' });
  assert.equal('von' in p, false);
  assert.equal('gefaelle' in p, false);
});

test('leitung_zeichnen: Gefälle, Dämmdicke und Strang gehen mit; Strang und System beide, wenn gewählt', () => {
  const { parameter: p } = L.leitungParameter({ opt: { ...opt, gefaelle: 2, daemmdicke: 20, strang: 'KW-V1' }, punkte: [[0, 0, 0], [1000, 0, 0]] });
  assert.equal(p.gefaelle, 2);
  assert.deepEqual(p.eigenschaften, { daemmdicke: 20 });
  assert.equal(p.strang, 'KW-V1');
  assert.equal(p.system, 'KW');
  const o = L.leitungParameter({ opt: { ...opt, gefaelle: 0, daemmdicke: 0 }, punkte: [[0, 0, 0], [1000, 0, 0]] }).parameter;
  assert.equal('gefaelle' in o, false);
  assert.equal('eigenschaften' in o, false);
});

test('leitung_zeichnen: von und nach als Objekte {element, anschluss} (nie die Textform «KZ.anschluss»)', () => {
  const rl = { element: 'HK-1', name: 'rl', fluss: 'raus' };
  const ziel = { element: 'WC-E.02', name: 'zu', fluss: 'rein' };
  const { parameter: p, umgedreht } = L.leitungParameter({ opt: { ...opt, system: '' }, punkte: [[5900, 3050, 120], [5900, 3050, 20], [8000, 3050, 20]], von: rl, nach: ziel });
  assert.equal(umgedreht, false);
  assert.deepEqual(p.von, { element: 'HK-1', anschluss: 'rl' });
  assert.deepEqual(p.nach, { element: 'WC-E.02', anschluss: 'zu' });
  assert.equal('system' in p, false, 'das System liefert der Anschluss');
});

test('leitung_zeichnen: gegen die Fliessrichtung gezeichnet → umgedreht, von/nach vertauscht, Punkte rückwärts', () => {
  const vl = { element: 'HK-1', name: 'vl', fluss: 'rein' };
  const { parameter: p, umgedreht } = L.leitungParameter({ opt, punkte: [[1, 2, 3], [4, 5, 6]], von: vl });
  assert.equal(umgedreht, true);
  assert.deepEqual(p.punkte, [[4, 5, 6], [1, 2, 3]]);
  assert.deepEqual(p.nach, { element: 'HK-1', anschluss: 'vl' });
  assert.equal('von' in p, false);
});

test('leitung_zeichnen: fehlende Angaben → Fehler mit Vorschlag', () => {
  assert.throws(() => L.leitungParameter({ opt, punkte: [[0, 0, 0]] }), /mindestens zwei Punkte/);
  assert.throws(() => L.leitungParameter({ opt: { ...opt, serie: '' }, punkte: [[0, 0, 0], [1, 0, 0]] }), /Serie fehlt/);
  assert.throws(() => L.leitungParameter({ opt: { ...opt, groesse: '' }, punkte: [[0, 0, 0], [1, 0, 0]] }), /Grösse fehlt/);
  try { L.leitungParameter({ opt: { ...opt, system: '' }, punkte: [[0, 0, 0], [1000, 0, 0]] }); assert.fail(); }
  catch (e) { assert.match(e.message, /System oder Strang/); assert.match(e.vorschlag, /Anschluss/); }
});

test('Pflichtangaben vor dem ersten Klick: Serie, Grösse und Höhe (keine erfundene Vorgabe)', () => {
  const serien = [{ kz: 'FAM-4', name: 'x', typen: ['DN 20'] }];
  assert.equal(L.pflichtFehlt({ serie: 'FAM-4', groesse: 'DN 20', hoehe: 0 }, { serien }), null, 'Höhe 0 ist eine gültige Eingabe');
  assert.equal(L.pflichtFehlt({ serie: 'FAM-4', groesse: 'DN 20', hoehe: null }, { serien }).feld, 'hoehe');
  assert.equal(L.pflichtFehlt({ serie: 'FAM-4', groesse: '', hoehe: 300 }, { serien }).feld, 'groesse');
  assert.equal(L.pflichtFehlt({ serie: '', groesse: '', hoehe: 300 }, { serien }).feld, 'serie');
  const keine = L.pflichtFehlt({ serie: '', groesse: '', hoehe: 300 }, { serien: [], kategorie: 'Kanal' });
  assert.match(keine.text, /Kategorie Kanal/);
  assert.match(keine.vorschlag, /Familie laden/);
});

test('leitung_abzweigen: Fusspunkt, Verlauf oder Richtung, Serie und Grösse der Optionsleiste', () => {
  const p = L.abzweigParameter({ opt, leitung: 'RO-1', punkt: [2000.001, 1000, 300], punkte: [[2000, 1500, 300], [2000, 2500, 300]], geschoss: 'EG' });
  assert.deepEqual(p, { leitung: 'RO-1', punkt: [2000, 1000, 300], punkte: [[2000, 1500, 300], [2000, 2500, 300]], serie: 'FAM-4', groesse: 'DN 20', geschoss: 'EG' });
  const t = L.abzweigParameter({ opt, leitung: 'RO-1', punkt: [2000, 1000, 300], richtung: [0, 1, 0] });
  assert.deepEqual(t.richtung, [0, 1, 0]);
  assert.equal('punkte' in t, false);
  assert.throws(() => L.abzweigParameter({ opt, leitung: 'RO-1', punkt: [0, 0, 0] }), /keine Richtung/);
  assert.throws(() => L.abzweigParameter({ opt, leitung: '', punkt: [0, 0, 0], richtung: [0, 1, 0] }), /Keine Leitung/);
  const e = L.abzweigParameter({ opt, leitung: 'RO-1', punkt: [0, 0, 0], punkte: [[0, 100, 0]], nach: { element: 'HK-1', name: 'vl' } });
  assert.deepEqual(e.ende, { element: 'HK-1', anschluss: 'vl' });
});

test('Abzweig: Fusspunkt auf der Geraden, Gerade unter der Maus, Seite des T-Stücks', () => {
  const pr = L.projektionAufGerade([2000, 40], [1000, 1000, 300], [3000, 1000, 300]);
  nah(pr.t, 0.5);
  assert.deepEqual(pr.punkt.map((x) => Math.round(x)), [2000, 1000, 300]);
  nah(pr.abstand, 960, 1e-6);
  const schraeg = L.projektionAufGerade([1500, 1000], [1000, 1000, 300], [3000, 1000, 500]);
  nah(schraeg.punkt[2], 300 + 0.25 * 200, 1e-6);
  assert.equal(L.projektionAufGerade([5, 5], [0, 0, 0], [0, 0, 3000]).senkrecht, true);

  const el = (kz, a, b, klasse = 'Rohr', serie = 'FAM-4') => ({ kennzeichen: kz, klasse, bezuege: serie ? { familie: serie } : undefined, geometrie: { punkte: [a, b] } });
  const l = [el('RO-1', [0, 0, 300], [2000, 0, 300]), el('RO-2', [0, 500, 300], [2000, 500, 300]), el('RO-3', [0, 100, 0], [2000, 100, 0], 'Rohr', null)];
  const treffer = L.leitungUnterMaus([1000, 480], l, 50, 'Rohr');
  assert.equal(treffer.element.kennzeichen, 'RO-2', 'die nächste');
  assert.equal(L.leitungUnterMaus([1000, 150], l, 30, 'Rohr'), null, 'RO-3 hat keine Serie (Altbestand)');
  assert.equal(L.leitungUnterMaus([1000, 20], l, 50, 'Kanal'), null, 'falsche Kategorie');
  assert.equal(L.leitungUnterMaus([0.5, 5], l, 50, 'Rohr'), null, 'weniger als 1 mm vom Ende: dort sitzt der Anschluss');

  nah(L.leitungRichtungPlan(l[0]), 0);
  assert.equal(L.leitungRichtungPlan(el('S', [0, 0, 0], [0, 0, 100])), null);
  const seite = L.abzweigSeite(0, [1000, 0], [1000, 300]);
  assert.deepEqual(seite, [0, 1, 0]);
  assert.deepEqual(L.abzweigSeite(0, [1000, 0], [1000, -300]), [0, -1, 0]);
});

// ============================================================================================== Höhe

test('Höhenwechsel: senkrechter Punkt auf der neuen Höhe (Steigleitung)', () => {
  const r = L.hoehenWechsel([[0, 0, 300], [1000, 0, 300]], 2800);
  assert.equal(r.angehaengt, true);
  assert.deepEqual(r.punkte, [[0, 0, 300], [1000, 0, 300], [1000, 0, 2800]]);
  const gleich = L.hoehenWechsel([[0, 0, 300]], 300.2);
  assert.equal(gleich.angehaengt, false);
  assert.equal(L.hoehenWechsel([], 100).angehaengt, false);
  assert.equal(L.hoehenWechsel([[0, 0, 0]], null).angehaengt, false);
  const abwaerts = L.hoehenWechsel([[5, 5, 3200]], -300);
  assert.deepEqual(abwaerts.punkte.at(-1), [5, 5, -300]);
});

test('Höhe: absolut = Kote des Geschosses + Höhe über OK Geschoss, und zurück', () => {
  assert.equal(L.hoeheAbsolut(2900, 300), 3200);
  assert.equal(L.hoeheAbsolut(-2700, 300), -2400);
  assert.equal(L.hoeheAbsolut(null, 300), 300, 'ohne Kote ±0.00');
  assert.equal(L.hoeheAbsolut(0, null), null);
  assert.equal(L.hoeheRelativ(2900, 3200), 300);
  assert.equal(L.hoeheRelativ(0, 120), 120);
});

// ============================================================================================== Optionsleiste

const familien = [
  { kennzeichen: 'FAM-4', name: 'Testrohrserie (fiktiv)', kategorie: 'Rohr', gueltig: true, typen: ['DN 15', 'DN 20'] },
  { kennzeichen: 'FAM-9', name: 'Testkanalserie rechteckig (fiktiv)', kategorie: 'Kanal', gueltig: true, typen: ['500x300'] },
  { kennzeichen: 'FAM-1', name: 'Testbogen (fiktiv)', kategorie: 'Bogen', gueltig: true, typen: ['DN 15'] },
  { kennzeichen: 'FAM-X', name: 'Kaputt', kategorie: 'Rohr', gueltig: false, typen: ['DN 15'] },
  { kennzeichen: 'FAM-L', name: 'Leer', kategorie: 'Rohr', gueltig: true, typen: [] },
];

test('Optionsleiste: nur Familien der Kategorie Rohr bzw. Kanal, gültig und mit Typen', () => {
  assert.deepEqual(L.serienDerKategorie(familien, 'Rohr').map((s) => s.kz), ['FAM-4']);
  assert.deepEqual(L.serienDerKategorie(familien, 'Kanal').map((s) => s.kz), ['FAM-9']);
  assert.deepEqual(L.serienDerKategorie([], 'Rohr'), []);
});

test('Optionsleiste: Zustand – keine Serie geladen, gültige Serie, Grössen der Serie', () => {
  const keine = L.optionenZustand({ serien: [], serie: '', groesse: '' });
  assert.equal(keine.keineSerie, true);
  assert.deepEqual(keine.serieOptionen, []);
  const serien = L.serienDerKategorie(familien, 'Rohr');
  const z = L.optionenZustand({ serien, serie: 'FAM-4', serieDef: serieRohr, groesse: 'DN 20' });
  assert.equal(z.keineSerie, false);
  assert.deepEqual(z.serieOptionen, [['FAM-4', 'Testrohrserie (fiktiv)']]);
  assert.deepEqual(z.groessenOptionen, [['DN 15', 'DN 15'], ['DN 20', 'DN 20']], 'ungültige Grössen fehlen');
  assert.equal(z.groesse, 'DN 20');
  assert.deepEqual(z.korrektur, {});
});

test('Optionsleiste: gemerkte Werte, die nicht mehr stimmen, werden korrigiert', () => {
  const serien = L.serienDerKategorie(familien, 'Rohr');
  const z = L.optionenZustand({ serien, serie: 'FAM-ALT', groesse: 'DN 20' });
  assert.equal(z.serie, 'FAM-4');
  assert.equal(z.korrektur.serie, 'FAM-4');
  const g = L.optionenZustand({ serien, serie: 'FAM-4', serieDef: serieRohr, groesse: 'DN 50' });
  assert.equal(g.groesse, '');
  assert.equal(g.korrektur.groesse, '');
  const straenge = [{ kz: 'KW-V1', system: 'KW' }, { kz: 'SW-F1', system: 'SW' }];
  const s = L.optionenZustand({ serien, serie: 'FAM-4', serieDef: serieRohr, groesse: 'DN 20', systemKz: 'KW', strangKz: 'SW-F1', straenge });
  assert.equal(s.strang, '');
  assert.equal(s.korrektur.strang, '');
  assert.deepEqual(s.strangOptionen.map((x) => x[0]), ['KW-V1'], 'nur Stränge des gewählten Systems');
  assert.equal(L.optionenZustand({ serien, serie: '', serieDef: null, groesse: '' }).serie, 'FAM-4', 'die einzige Serie wird vorgewählt');
});

test('Optionsleiste: Grössen und Breite der Vorschau aus der Serie (Rohr: aussen, Kanal: Breite)', () => {
  assert.deepEqual(L.groessenListe(serieRohr).map((g) => g.typ), ['DN 15', 'DN 20']);
  assert.equal(L.vorschauBreite(serieRohr, 'DN 20'), 25);
  assert.equal(L.vorschauBreite(serieKanal, '500x300'), 500);
  assert.equal(L.vorschauBreite(serieRohr, 'DN 77'), null);
  assert.equal(L.vorschauBreite(null, 'DN 20'), null);
});

test('Optionen: Felder von Rohr und Kanal (Serie, Grösse, Höhe als Pflicht, Gefälle, System, Strang, Dämmdicke, Winkelfang)', () => {
  for (const wid of ['rohr', 'kanal']) {
    const ids = O.WERKZEUGE[wid].felder.map((f) => f.id);
    // Nachtrag freie Grösse / Abwasser-Serie: Zahlenfelder und Hinweise hinter der Grösse (sichtbar nur bei den passenden Serien, siehe frei-abwasser.test.mjs)
    assert.deepEqual(ids, ['serie', 'groesse', 'freiB', 'freiH', 'freiD', 'freiHinweis', 'abwasserHinweis', 'hoehe', 'gefaelle', 'system', 'strang', 'daemmdicke', 'winkelfang', 'serieHinweis', 'serieLaden']);
    const hoehe = O.feldDef(wid, 'hoehe');
    assert.equal(hoehe.pflicht, true);
    assert.equal(hoehe.art, 'zahl');
    assert.equal(O.wert(wid, 'hoehe'), null, 'keine erfundene Vorgabe');
    assert.equal(O.wert(wid, 'winkelfang'), true, 'Winkelfang ist vorgabemässig an');
    assert.equal(O.feldDef(wid, 'gefaelle').fluechtig, true);
    assert.equal(O.feldDef(wid, 'serie').art, 'auswahl');
  }
  assert.equal(O.WERKZEUGE.rohr.titel, 'Rohr');
  assert.equal(O.WERKZEUGE.kanal.titel, 'Kanal');
});

test('Optionen: Hinweis «Leitungsserie laden» erscheint, solange keine Serie der Kategorie geladen ist', () => {
  const hinweis = O.feldDef('rohr', 'serieHinweis');
  const knopf = O.feldDef('rohr', 'serieLaden');
  assert.equal(D.serienListe('Rohr').length, 0);
  assert.equal(hinweis.sichtbar(), true);
  assert.equal(knopf.sichtbar(), true);
  assert.equal(knopf.aktion, 'familie.laden', 'der Weg zu den Familien');
  assert.match(hinweis.text, /Kategorie Rohr/);
  assert.match(O.feldDef('kanal', 'serieHinweis').text, /Kategorie Kanal/);
  assert.match(knopf.titel, /Leitungsserie laden/);
});

// ============================================================================================== Vorschau-Zeile

// Hinweise, wie sie der Kern liefert (aus einem echten Trockenlauf von leitung_zeichnen mit der fiktiven Testserie)
const hinweiseKern = [
  'leitung_zeichnen: 2 Gerade(n), zusammen 3900 mm, und 1 Formteil(e) – Serie FAM-4 «Testrohrserie (fiktiv)».',
  'Gerade RO-1 DN 20: 1950 mm von [1000, 1000, 300] bis [2950, 1000, 300]',
  'Bogen BO-1 Typ «DN 20», 90° bei [3000, 1000, 300], Einbaulängen a 50 mm, b 50 mm',
  'Gerade RO-2 DN 20: 1950 mm von [3000, 1050, 300] bis [3000, 3000, 300]',
];

test('Vorschau: Geraden mit Länge und Formteile mit Typ und Winkel aus den Hinweisen des Kerns', () => {
  const v = L.vorschauAus({ hinweise: hinweiseKern });
  assert.equal(v.geraden.length, 2);
  assert.deepEqual(v.geraden[0], { kz: 'RO-1', groesse: 'DN 20', laenge: 1950 });
  assert.deepEqual(v.formteile, [{ art: 'Bogen', kz: 'BO-1', typ: 'DN 20', winkel: 90 }]);
  assert.equal(v.summe, 3900);
  assert.ok(v.kopf.startsWith('leitung_zeichnen: 2 Gerade(n)'));
  assert.equal(L.vorschauZeile(v), '2 Geraden, 3900 mm · 1 Bogen 90°');
  assert.deepEqual(L.vorschauListe(v), ['Gerade RO-1 · DN 20 · 1950 mm', 'Gerade RO-2 · DN 20 · 1950 mm', 'Bogen BO-1 · Typ «DN 20» · 90°']);
});

test('Vorschau: Abzweig mit T-Stück, Reduktion und weiteren Hinweisen (nichts geht verloren)', () => {
  const v = L.vorschauAus({ hinweise: [
    'leitung_abzweigen: RO-1 geteilt in RO-1 (900 mm) und RO-1-2 (950 mm); T-Stück TS-1 Typ «DN 20» bei [2000, 1000, 300], Einbaulängen a 25 mm, b 25 mm, c 25 mm.',
    'Abzweig: 1 Gerade(n), zusammen 975.5 mm, und 1 Formteil(e) – Serie FAM-4 «Testrohrserie (fiktiv)».',
    'Gerade RO-3 DN 15: 975.5 mm von [2000, 1025, 300] bis [2000, 2000, 300]',
    'Reduktion RE-1 Typ «DN 20/15», bei [2000, 1050, 300], Einbaulängen a 0 mm, b 40 mm',
    'Gefälle 2 %: z von 300 bis 280 mm.',
  ] });
  assert.equal(v.geraden.length, 1);
  assert.equal(v.formteile[0].art, 'Reduktion');
  assert.equal(L.vorschauZeile(v), '1 Gerade, 975.5 mm · 1 Reduktion');
  assert.equal(v.sonst.length, 2, 'die Zeile des T-Stücks und das Gefälle bleiben als weitere Hinweise');
  assert.ok(L.vorschauListe(v).some((t) => t.startsWith('Gefälle')));
  assert.equal(L.vorschauAus({ hinweise: [] }).geraden.length, 0);
  assert.equal(L.vorschauZeile(L.vorschauAus({ hinweise: [] })), 'Keine Änderung.');
});

test('Vorschau: Ablehnung des Kerns mit Text und Vorschlag', () => {
  const a = L.ablehnung({ angenommen: false, fehler: 'Der Knick von 60° passt zu keinem Winkel der Serie.', vorschlag: 'Winkel 45° oder 90° wählen.' });
  assert.equal(a.text, 'Der Knick von 60° passt zu keinem Winkel der Serie.');
  assert.equal(a.vorschlag, 'Winkel 45° oder 90° wählen.');
  assert.equal(L.ablehnung({}).vorschlag, null);
  assert.match(L.ablehnung({}).text, /abgelehnt/);
});

test('Befunde nach dem Zeichnen: Prüfungen P13–P18 und alles, was mehr als ein Hinweis ist', () => {
  const t = L.neueBefundeTexte({ neueBefunde: [
    { code: 'P01', schwere: 'hinweis', element: 'RO-1', text: 'Anschluss «anfang» ist offen' },
    { code: 'P13', schwere: 'hinweis', element: 'RO-2', text: 'Ende nicht an seinem Anschluss' },
    { code: 'P15', schwere: 'warnung', element: 'RO-3', text: 'Knick ohne Formteil' },
  ] });
  assert.deepEqual(t, ['P13 RO-2: Ende nicht an seinem Anschluss', 'P15 RO-3: Knick ohne Formteil']);
  assert.deepEqual(L.neueBefundeTexte({}), []);
});

// ============================================================================================== Palette

const H = { zeile: (label, feld) => `[${label}|${feld}]`, gruppe: (name, zeilen) => `<${name}>${zeilen.join('')}</${name}>`, nurLesen: (t) => `«${t}»` };
const rohrEl = { klasse: 'Rohr', kennzeichen: 'RO-1', bezuege: { familie: 'FAM-4' }, werte: { familientyp: 'DN 20' } };
const lesen = { eigenschaften: { familientyp: { wert: 'DN 20' }, gefaelle: { wert: 2 }, daemmung: { wert: 'Mineralwolle' }, daemmdicke: { wert: 20 } } };

test('Palette: Gruppe Leitung mit Serie, Grösse, Gefälle, Dämmung, Dämmdicke und Leitungszug', () => {
  const zug = { summe: { leitungen: 2, formteile: 1, laengeMm: 3900 } };
  const h = P.leitungGruppe({ el: rohrEl, l: lesen, f: { kennzeichen: 'FAM-4', name: 'Testrohrserie (fiktiv)' }, serie: serieRohr, serien: [{ kz: 'FAM-4', name: 'Testrohrserie (fiktiv)' }], zug }, H);
  assert.match(h, /^<Leitung>/);
  assert.match(h, /data-ziel="lt:serie"/);
  assert.match(h, /data-ziel="lt:groesse"/);
  assert.match(h, /<option value="DN 20" selected>DN 20<\/option>/);
  assert.ok(!h.includes('DN 99'), 'ungültige Grösse');
  assert.match(h, /data-ziel="eig:gefaelle"[^>]*value="2"/);
  assert.match(h, /data-ziel="eig:daemmung"[^>]*value="Mineralwolle"/);
  assert.match(h, /data-ziel="eig:daemmdicke"[^>]*value="20"/);
  assert.match(h, /aussen 25 · innen 21 · Wand 2 mm/);
  assert.match(h, /2 Geraden, 1 Formteil, 3900 mm/);
  assert.match(h, /data-aktion="fam-editor" data-fam="FAM-4"/);
});

test('Palette: Grösse, die es in der Serie nicht gibt, bleibt sichtbar mit Hinweis', () => {
  const h = P.leitungGruppe({ el: rohrEl, l: { eigenschaften: { familientyp: { wert: 'DN 77' } } }, f: { kennzeichen: 'FAM-4', name: 'x' }, serie: serieRohr, serien: [], zug: null }, H);
  assert.match(h, /DN 77 \(gibt es nicht mehr\)/);
});

test('Palette: Altbestand ohne Serie bietet «Aufteilen mit Serie …»; Formteil zeigt Art, Familie, Typ und Winkel', () => {
  const alt = P.altbestandGruppe({ el: { klasse: 'Rohr', kennzeichen: 'KW-1.01-SP' } }, H);
  assert.match(alt, /Aufteilen mit Serie …/);
  assert.match(alt, /data-aktion="lt-aufteilen"/);
  assert.ok(!alt.includes('fam-editor'));
  const f = P.formteilGruppe({ el: { klasse: 'TStueck', kennzeichen: 'TS-1', werte: {} }, l: { eigenschaften: { familientyp: { wert: 'DN 20' } } }, f: { name: 'Test-T-Stück egal (fiktiv)' } }, H);
  assert.match(f, /T-Stück/);
  assert.match(f, /Test-T-Stück egal/);
  const b = P.formteilGruppe({ el: { klasse: 'Bogen', kennzeichen: 'BO-1', werte: { winkel: 90, familientyp: 'DN 20' } }, l: { eigenschaften: { winkel: { wert: 90 }, familientyp: { wert: 'DN 20' } } }, f: { name: 'Testbogen (fiktiv)' } }, H);
  assert.match(b, /Winkel\|«90°»/);
  assert.match(b, /Typ\|«DN 20»/);
});

test('Palette: welche Gruppen zu welchem Element gehören', () => {
  assert.equal(P.leitungsArt(rohrEl), 'leitung');
  assert.equal(P.leitungsArt({ klasse: 'Kanal' }), 'altbestand');
  assert.equal(P.leitungsArt({ klasse: 'Bogen' }), 'formteil');
  assert.equal(P.leitungsArt({ klasse: 'Reduktion' }), 'formteil');
  assert.equal(P.leitungsArt({ klasse: 'Heizkoerper' }), null);
  assert.deepEqual(P.LEITUNG_EIGENSCHAFTEN, ['dn', 'gefaelle', 'daemmung', 'daemmdicke']);
  assert.equal(P.zugText(null), '');
});

test('Palette: Parameter von leitung_groesse, leitung_serie und leitung_aufteilen', () => {
  assert.deepEqual(L.groessenParameter({ leitung: 'RO-1', groesse: 'DN 25', zug: false }), { leitung: 'RO-1', groesse: 'DN 25' });
  assert.deepEqual(L.groessenParameter({ leitung: 'RO-1', groesse: 'DN 25', zug: true }), { leitung: 'RO-1', groesse: 'DN 25', zug: true });
  assert.deepEqual(L.serienParameter({ leitung: 'RO-1', serie: 'FAM-9', zug: true }), { leitung: 'RO-1', serie: 'FAM-9', zug: true });
  assert.deepEqual(L.aufteilenParameter({ leitung: 'KW-1', serie: 'FAM-4' }), { leitung: 'KW-1', serie: 'FAM-4' });
  assert.deepEqual(L.aufteilenParameter({ leitung: 'KW-1', serie: 'FAM-4', groesse: 'DN 20' }), { leitung: 'KW-1', serie: 'FAM-4', groesse: 'DN 20' });
  assert.throws(() => L.aufteilenParameter({ leitung: 'KW-1', serie: '' }), /Serie fehlt/);
});

test('Elemente: Altbestand = Leitung ohne Familie', () => {
  assert.equal(L.istAltbestand({ klasse: 'Rohr' }), true);
  assert.equal(L.istAltbestand(rohrEl), false);
  assert.equal(L.istAltbestand({ klasse: 'Bogen' }), false);
  assert.equal(L.hatSerie(rohrEl), true);
  assert.equal(L.kategorieVon('Kanal'), 'Kanal');
  assert.equal(L.kategorieVon('Rohr'), 'Rohr');
});

// ============================================================================================== Gegen den Kern-Quelltext

test('Kern: Parameternamen der Befehle, die das Werkzeug sendet, gibt es', () => {
  const q = quelle('Leitungen/RoutingBefehle.cs');
  for (const name of ['serie', 'groesse', 'punkte', 'von', 'nach', 'gefaelle', 'system', 'strang', 'geschoss', 'praefix', 'eigenschaften']) assert.ok(q.includes(`new("${name}"`), `leitung_zeichnen: ${name}`);
  for (const name of ['leitung', 'punkt', 'richtung', 'laenge', 'punkte', 'groesse', 'serie', 'ende', 'gefaelle']) assert.ok(q.includes(`new("${name}"`), `leitung_abzweigen: ${name}`);
  for (const name of ['leitungen', 'zug']) assert.ok(q.includes(`new("${name}"`), `leitung_groesse: ${name}`);
  for (const b of ['leitung_zeichnen', 'leitung_abzweigen', 'leitung_aufteilen', 'leitung_groesse', 'leitung_serie']) assert.ok(q.includes(`"${b}"`), b);
});

test('Kern: Format der Hinweise, das die Vorschau-Zeile liest (Routing.Uebersicht)', () => {
  const q = quelle('Leitungen/Routing.cs');
  assert.ok(q.includes('Gerade(n), zusammen'), 'Kopfzeile');
  assert.ok(q.includes('$"Gerade {e.Kennzeichen} {g.Groesse.Groesse}: {Z(g.Laenge)} mm von {P(g.Von)} bis {P(g.Bis)}"'), 'Zeile je Gerade');
  assert.ok(q.includes('{Leitungsserie.Artname(f.Wahl.Art)} {e.Kennzeichen} Typ «{f.Wahl.Typ}»'), 'Zeile je Formteil');
  const s = quelle('Leitungen/Leitungsserie.cs') + quelle('Leitungen/LeitungsserieAbfrage.cs');   // Abfrage seit 21.10.2026 in LeitungsserieAbfrage.cs
  for (const name of ['winkelRegel', 'groessen', 'aussen', 'innen', 'wand', 'formteilFamilien']) assert.ok(s.includes(`["${name}"]`), `leitungsserie: ${name}`);
  assert.ok(s.includes('"liste"') && s.includes('"frei"'), 'winkelRegel liste | frei');
  const a = quelle('Leitungen/Anschlusslagen.cs');
  assert.ok(a.includes('["lage"]') && a.includes('["richtung"]') && a.includes('["fluss"]') && a.includes('["name"]'), 'anschlusslagen: name, fluss, lage, richtung');
  assert.ok(quelle('Familien/FamilienAbfragen.cs').includes('o["verbunden"] = g is null ? null : $"{m.Finde(g.Element)?.Kennzeichen}.{g.Anschluss}"'), 'verbunden = «KZ.anschluss» oder null');
});

test('Kern: Fluss «rein» nimmt auf, «raus» speist (Umdrehen der Zeichnung folgt dem Kern)', () => {
  const q = quelle('Leitungen/RoutingBefehle.cs');
  assert.ok(q.includes('s.Fluss != (anfang ? "rein" : "raus")'), 'Ende(): am Anfang nicht «rein», am Ende nicht «raus»');
});

// ============================================================================================== Zwischenspeicher (Elemente in der Nähe)

test('Daten: welche Elemente Anschlüsse haben können und welche in der Nähe des Mauszeigers liegen', async () => {
  const Z = await import('../zustand.js');
  Z.zustand.katalog = { klassen: [{ name: 'Rohr', anschluesse: ['anfang (*, rein)', 'ende (*, raus)'] }, { name: 'Wand', anschluesse: [] }, { name: 'Heizkoerper', anschluesse: ['vl (HZ-VL, rein)'] }] };
  const el = (id, kz, klasse, punkte, extra = {}) => ({ id, kennzeichen: kz, klasse, schicht: 'system', geschoss: 'EG', geometrie: { art: 'punkt', punkte }, ...extra });
  const liste = [
    el('hk', 'HK-1', 'Heizkoerper', [[17000, 7000, 0]], { bezuege: { familie: 'FAM-5' } }),
    el('ro', 'RO-1', 'Rohr', [[14000, 3000, 300], [15950, 3000, 300]]),
    el('wa', 'W-1', 'Wand', [[0, 0, 0], [5000, 0, 0]], { schicht: 'huelle' }),
    el('ob', 'RO-9', 'Rohr', [[17000, 7000, 3200]], { geschoss: '1.OG' }),
  ];
  Z.zustand.elemente.clear(); Z.zustand.nachKennzeichen.clear();
  for (const e of [...liste, { id: 'eg', kennzeichen: 'EG', klasse: 'Geschoss', werte: { kote: 0 } }, { id: 'og', kennzeichen: '1.OG', klasse: 'Geschoss', werte: { kote: 2900 } }]) { Z.zustand.elemente.set(e.id, e); Z.zustand.nachKennzeichen.set(e.kennzeichen.toLowerCase(), e.id); }
  assert.equal(D.hatAnschluesse(liste[0]), true, 'Familienelement');
  assert.equal(D.hatAnschluesse(liste[1]), true, 'Leitung (Katalogklasse mit Anschlüssen)');
  assert.equal(D.hatAnschluesse(liste[2]), false, 'Wand');
  assert.equal(D.hatAnschluesse({ id: 'x', klasse: 'Rohr', geometrie: { punkte: [] } }), false, 'ohne Geometrie (nicht platziert) gibt es keine Lage');
  assert.deepEqual(D.elementeNahe([17050, 7050], 3000, 'EG').map((e) => e.kennzeichen), ['HK-1'], 'nur Elemente dieses Geschosses in der Nähe');
  assert.deepEqual(D.elementeNahe([15000, 3000], 100, 'EG').map((e) => e.kennzeichen), ['RO-1']);
  assert.deepEqual(D.elementeNahe([1000, 1000], 100, 'EG'), [], 'die Wand hat keine Anschlüsse');
  assert.deepEqual(D.elementeNahe([17000, 7000], 3000, '1.OG').map((e) => e.kennzeichen), ['RO-9']);
  Z.zustand.elemente.clear(); Z.zustand.nachKennzeichen.clear(); Z.zustand.katalog = null;
});

test('Daten: Serien der Kategorie kommen aus der Familienliste, die Haken sind leer, solange kein Werkzeug läuft', () => {
  assert.equal(D.haken.hoeheGeaendert, null);
  assert.equal(D.haken.optionenNeu, null);
  assert.deepEqual(D.serienListe('Rohr'), []);
  assert.deepEqual(D.anschluesseVon('gibt-es-nicht'), null);
  assert.deepEqual(D.alleAnschluesse(), []);
});
