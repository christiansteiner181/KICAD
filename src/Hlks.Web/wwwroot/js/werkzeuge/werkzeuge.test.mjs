// Tests der Zeichenwerkzeuge (C3) ohne Browser: Fangen, Parameterbildung der Befehle, Schicht-Markup, Optionen, Mehrfachauswahl.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/werkzeuge/werkzeuge.test.mjs
// Läuft ein Server (Umgebungsvariable HLKS_TEST_SERVER, z. B. http://localhost:5135) mit dem Beispielprojekt, prüfen zusätzliche
// Tests die gebildeten Parameter im Trockenlauf gegen den echten Kern.

import { test } from 'node:test';
import assert from 'node:assert/strict';

const F = await import('./fangen.js');
const P = await import('./parameter.js');
const S = await import('./schicht.js');
const O = await import('./optionen.js');

const nah = (a, b, eps = 1e-6) => assert.ok(Math.abs(a - b) <= eps, `${a} ≠ ${b}`);
const punktGleich = (p, q, eps = 1e-6) => { nah(p[0], q[0], eps); nah(p[1], q[1], eps); };

// ============================================================================================== Fangen

const daten = {
  punkte: [{ p: [0, 0], id: 'w1' }, { p: [5000, 0], id: 'w1' }],
  kanten: [{ a: [0, 0], b: [5000, 0], id: 'w1' }, { a: [2000, -1000], b: [2000, 1000], id: 'w2' }],
};

test('Fangen: Endpunkt gewinnt vor allem anderen', () => {
  const f = F.fangen({ punkt: [4, 3], daten, radius: 10, raster: 100 });
  assert.equal(f.art, 'endpunkt');
  assert.deepEqual(f.punkt, [0, 0]);
  assert.equal(f.id, 'w1');
});

test('Fangen: Schnittpunkt zweier Kanten vor Mitte und Nächster', () => {
  const f = F.fangen({ punkt: [2004, 5], daten, radius: 12, raster: 100 });
  assert.equal(f.art, 'schnittpunkt');
  punktGleich(f.punkt, [2000, 0]);
});

test('Fangen: Mitte einer Kante', () => {
  const f = F.fangen({ punkt: [2503, 4], daten, radius: 10, raster: 0 });
  assert.equal(f.art, 'mitte');
  punktGleich(f.punkt, [2500, 0]);
  assert.equal(f.id, 'w1');
});

test('Fangen: Lot vom letzten Punkt auf eine Kante', () => {
  const d = { punkte: [], kanten: [{ a: [1000, -200], b: [1000, 800], id: 'k' }] };
  const f = F.fangen({ punkt: [1003, 4], daten: d, radius: 10, letzter: [0, 0] });
  assert.equal(f.art, 'lot');
  punktGleich(f.punkt, [1000, 0]);
});

test('Fangen: Lot nur, wenn der Fusspunkt auf der Kante liegt', () => {
  const d = { punkte: [], kanten: [{ a: [1000, 500], b: [1000, 800], id: 'k' }] };
  const f = F.fangen({ punkt: [1003, 4], daten: d, radius: 10, letzter: [0, 0], raster: 0 });
  assert.notEqual(f.art, 'lot');
});

test('Fangen: Nächster Punkt auf der Kante', () => {
  const d = { punkte: [], kanten: [{ a: [0, 0], b: [1000, 0], id: 'k' }] };
  const f = F.fangen({ punkt: [370, 4], daten: d, radius: 10, raster: 0 });
  assert.equal(f.art, 'naechster');
  punktGleich(f.punkt, [370, 0]);
});

test('Fangen: ausserhalb des Radius gilt das Raster, ohne Raster frei', () => {
  const r = F.fangen({ punkt: [1234, 5678], daten, radius: 10, raster: 100 });
  assert.equal(r.art, 'raster');
  assert.deepEqual(r.punkt, [1200, 5700]);
  const fr = F.fangen({ punkt: [1234, 5678], daten, radius: 10, raster: 0 });
  assert.equal(fr.art, 'frei');
  assert.deepEqual(fr.punkt, [1234, 5678]);
});

test('Fangen: Fangradius ist in Modell-mm (Pixel / Zoom)', () => {
  // 10 px bei 0.1 px/mm = 100 mm
  const f = F.fangen({ punkt: [60, 0], daten, radius: 10 * (1 / 0.1), raster: 0 });
  assert.equal(f.art, 'endpunkt');
  const g = F.fangen({ punkt: [60, 0], daten, radius: 10 * (1 / 10), raster: 0 });
  assert.notEqual(g.art, 'endpunkt');
});

test('Fangen: Arten lassen sich abschalten', () => {
  const f = F.fangen({ punkt: [4, 3], daten, radius: 10, raster: 0, arten: ['mitte'] });
  assert.notEqual(f.art, 'endpunkt');
});

test('Fangen: eigene Zusatzpunkte (Anfang der Kette) sind Endpunkte', () => {
  const f = F.fangen({ punkt: [9003, 7001], daten, radius: 10, extraPunkte: [[9000, 7000]], raster: 100 });
  assert.equal(f.art, 'endpunkt');
  assert.deepEqual(f.punkt, [9000, 7000]);
});

test('Fangen: Shift = orthogonal, Länge auf das Raster', () => {
  const f = F.fangen({ punkt: [1043, 300], daten: { punkte: [], kanten: [] }, radius: 10, letzter: [0, 0], ortho: true, raster: 100 });
  assert.equal(f.ortho, true);
  assert.deepEqual(f.punkt, [1000, 0]);
  const v = F.fangen({ punkt: [200, 2480], daten: { punkte: [], kanten: [] }, radius: 10, letzter: [0, 0], ortho: true, raster: 100 });
  assert.deepEqual(v.punkt, [0, 2500]);
});

test('Fangen: Shift fängt Endpunkte nur auf der waagrechten/senkrechten Linie', () => {
  const d = { punkte: [{ p: [3000, 4], id: 'a' }, { p: [3000, 900], id: 'b' }], kanten: [] };
  const f = F.fangen({ punkt: [3002, 150], daten: d, radius: 10, letzter: [0, 0], ortho: true, raster: 0 });
  assert.equal(f.art, 'endpunkt');
  assert.deepEqual(f.punkt, [3000, 0]);          // auf die Linie gelegt, nicht der Punkt bei y = 4 verfälscht
  const g = F.fangen({ punkt: [3002, 150], daten: { punkte: [{ p: [3000, 900], id: 'b' }], kanten: [] }, radius: 10, letzter: [0, 0], ortho: true, raster: 0 });
  assert.notEqual(g.art, 'endpunkt');
});

test('Fangen: Shift fängt den Schnittpunkt einer Kante mit der Linie', () => {
  const d = { punkte: [], kanten: [{ a: [2000, -1000], b: [2000, 1000], id: 'k' }] };
  const f = F.fangen({ punkt: [2004, 30], daten: d, radius: 10, letzter: [0, 0], ortho: true, raster: 0 });
  assert.equal(f.art, 'schnittpunkt');
  punktGleich(f.punkt, [2000, 0]);
});

test('Fangen: Länge per Zifferneingabe', () => {
  punktGleich(F.punktNachLaenge([0, 0], [3, 4], 10), [6, 8]);
  punktGleich(F.punktNachLaenge([100, 100], [100, 100], 50), [150, 100]);   // keine Richtung: nach Osten
  punktGleich(F.punktNachLaenge([0, 0], [0, -7], 2500), [0, -2500]);
});

test('Hilfen: Zahl aus Text (Komma, Apostroph), Winkel, Strecken', () => {
  assert.equal(F.zahlAusText('3,5'), 3.5);
  assert.equal(F.zahlAusText("1'250"), 1250);
  assert.equal(F.zahlAusText('-45'), -45);
  assert.equal(F.zahlAusText(''), null);
  assert.equal(F.zahlAusText('-'), null);
  assert.equal(F.zahlAusText('1.2.3'), null);
  assert.equal(F.winkelGrad([0, 0], [1, 0]), 0);
  assert.equal(F.winkelGrad([0, 0], [0, 1]), 90);
  assert.equal(F.winkelGrad([0, 0], [-1, 0]), 180);
  assert.equal(F.winkelGrad([0, 0], [0, -1]), 270);
  punktGleich(F.schnittStrecken([0, 0], [10, 10], [0, 10], [10, 0]), [5, 5]);
  assert.equal(F.schnittStrecken([0, 0], [1, 1], [5, 5], [6, 5]), null);       // ausserhalb
  assert.equal(F.schnittStrecken([0, 0], [10, 0], [0, 5], [10, 5]), null);     // parallel
  assert.equal(F.laengeText(3249.6), '3250 mm');
  assert.equal(F.winkelText(45), '45.0°');
});

// ============================================================================================== Parameter

test('Wandkette → wand_zeichnen', () => {
  const p = P.wandParameter({ geschoss: 'EG', punkte: [[0, 0], [5000, 0], [5000, 4000]], dicke: 300, hoehe: null, bezugslinie: 'mitte', material: '' });
  assert.deepEqual(p, { geschoss: 'EG', punkte: [[0, 0], [5000, 0], [5000, 4000]], dicke: 300, bezugslinie: 'mitte' });
});

test('Wandkette schliesst sich, wenn der letzte Punkt den ersten trifft', () => {
  const p = P.wandParameter({ geschoss: 'EG', punkte: [[0, 0], [5000, 0], [5000, 4000], [0, 4000], [0.2, -0.3]], dicke: 250, hoehe: 2800, bezugslinie: 'links', material: 'beton' });
  assert.equal(p.geschlossen, true);
  assert.equal(p.punkte.length, 4);
  assert.equal(p.hoehe, 2800);
  assert.equal(p.bezugslinie, 'links');
  assert.equal(p.material, 'beton');
});

test('Wand: doppelte Punkte entfallen, Pflicht Dicke, zu wenig Punkte', () => {
  const p = P.wandParameter({ geschoss: 'EG', punkte: [[0, 0], [0.4, 0.2], [3000, 0]], dicke: 200 });
  assert.equal(p.punkte.length, 2);
  assert.throws(() => P.wandParameter({ geschoss: 'EG', punkte: [[0, 0], [100, 0]], dicke: null }), (e) => e instanceof P.WerkzeugFehler && /dicke/i.test(e.message) && !!e.vorschlag);
  assert.throws(() => P.wandParameter({ geschoss: 'EG', punkte: [[0, 0]], dicke: 200 }), P.WerkzeugFehler);
  assert.throws(() => P.wandParameter({ geschoss: null, punkte: [[0, 0], [1000, 0]], dicke: 200 }), P.WerkzeugFehler);
});

test('Tür und Fenster → oeffnung_setzen', () => {
  const t = P.oeffnungParameter({ klasse: 'Tuer', wand: 'W-1', punkt: [1200.4, 3.2], breite: 900, hoehe: 2100, seite: 'rechts', band: 'ende' });
  assert.deepEqual(t, { klasse: 'Tuer', wand: 'W-1', punkt: [1200.4, 3.2], breite: 900, hoehe: 2100, seite: 'rechts', band: 'ende' });
  const f = P.oeffnungParameter({ klasse: 'Fenster', wand: 'W-2', punkt: [0, 0], breite: 1200, hoehe: 1350, bruestung: 900, seite: 'links', band: 'anfang' });
  assert.equal(f.bruestung, 900);
  assert.equal('seite' in f, false);
  assert.throws(() => P.oeffnungParameter({ klasse: 'Fenster', wand: 'W-2', punkt: [0, 0], breite: 1200, hoehe: 1350 }), /Brüstung/);
  assert.equal(P.oeffnungParameter({ klasse: 'Fenster', wand: 'W-2', punkt: [0, 0], breite: 1200, hoehe: 1350, bruestung: 0 }).bruestung, 0);
  assert.throws(() => P.oeffnungParameter({ klasse: 'Tuer', wand: null, punkt: [0, 0], breite: 900, hoehe: 2100 }), /Wand/);
  assert.throws(() => P.oeffnungParameter({ klasse: 'Tuer', wand: 'W-1', punkt: [0, 0], breite: null, hoehe: 2100 }), /Breite/);
});

test('Raum → raum_erkennen', () => {
  assert.deepEqual(P.raumParameter({ geschoss: 'EG', punkt: [2500.123456, 1800] }), { geschoss: 'EG', punkt: [2500.123, 1800] });
  const p = P.raumParameter({ geschoss: 'EG', punkt: [1, 2], nummer: '1.02', nutzung: 'Bad' });
  assert.equal(p.nummer, '1.02');
  assert.equal(p.nutzung, 'Bad');
});

test('Achse: Bezeichnungen zählen weiter', () => {
  assert.equal(P.naechsteBezeichnung('A'), 'B');
  assert.equal(P.naechsteBezeichnung('Z'), 'AA');
  assert.equal(P.naechsteBezeichnung('AZ'), 'BA');
  assert.equal(P.naechsteBezeichnung('9'), '10');
  assert.equal(P.naechsteBezeichnung('A1'), 'A2');
  assert.equal(P.naechsteBezeichnung(''), null);
  assert.equal(P.bezeichnungVorschlag([], 'buchstaben'), 'A');
  assert.equal(P.bezeichnungVorschlag(['A', 'B', 'C', '1'], 'buchstaben'), 'D');
  assert.equal(P.bezeichnungVorschlag(['A', 'B', '1', '2'], 'zahlen'), '3');
  assert.equal(P.bezeichnungVorschlag([], 'zahlen'), '1');
});

test('Achse → element_anlegen', () => {
  assert.deepEqual(P.achseParameter({ a: [0, 0], b: [0, 9000], bezeichnung: 'A' }),
    { klasse: 'Achse', eigenschaften: { bezeichnung: 'A' }, geometrie: { art: 'linie', punkte: [[0, 0], [0, 9000]] } });
  assert.throws(() => P.achseParameter({ a: [0, 0], b: [0, 9000], bezeichnung: ' ' }), /bezeichnung/i);
  assert.throws(() => P.achseParameter({ a: [0, 0], b: [0.3, 0], bezeichnung: 'A' }), /Länge/);
});

test('Bemassung: versatz aus dem Abstand des Klicks', () => {
  // ausgerichtet, Richtung +x: links = +y
  nah(P.bemassungVersatz([[0, 0], [3000, 0]], 'ausgerichtet', [1500, 500]), 500);
  nah(P.bemassungVersatz([[0, 0], [3000, 0]], 'ausgerichtet', [1500, -800]), -800);
  // Richtung −x: links = −y
  nah(P.bemassungVersatz([[3000, 0], [0, 0]], 'ausgerichtet', [1500, 500]), -500);
  // horizontal bei schräger Kette: Messrichtung +x, links = +y
  nah(P.bemassungVersatz([[0, 0], [1000, 1000]], 'horizontal', [0, 300]), 300);
  // vertikal: Messrichtung +y, links = −x
  nah(P.bemassungVersatz([[0, 0], [1000, 1000]], 'vertikal', [-250, 0]), 250);
  // Masskette: der erste Messpunkt bestimmt die Linie
  nah(P.bemassungVersatz([[100, 50], [2000, 50], [4000, 50]], 'ausgerichtet', [3000, 650]), 600);
});

test('Bemassung → element_anlegen mit bezuege.ansicht', () => {
  const p = P.bemassungParameter({ ansicht: 'AN-1', punkte: [[0, 0], [3000, 0]], richtung: 'ausgerichtet', lage: [1500, 400] });
  assert.deepEqual(p, { klasse: 'Bemassung', eigenschaften: { richtung: 'ausgerichtet', versatz: 400 }, geometrie: { art: 'polylinie', punkte: [[0, 0], [3000, 0]] }, bezuege: { ansicht: 'AN-1' } });
  assert.throws(() => P.bemassungParameter({ ansicht: 'AN-1', punkte: [[0, 0]], lage: [0, 0] }), /zwei Messpunkte/);
  assert.throws(() => P.bemassungParameter({ ansicht: null, punkte: [[0, 0], [1, 1000]], lage: [0, 0] }), /Ansicht/);
  assert.equal(P.bemassungParameter({ ansicht: 'AN-1', punkte: [[0, 0], [3000, 0]], lage: [0, 0], einheit: 'cm' }).eigenschaften.einheit, 'cm');
});

test('Text, Detaillinie, Füllbereich, Kote, Nordpfeil', () => {
  const t = P.textParameter({ ansicht: 'AN-1', punkt: [100, 200], inhalt: 'Bad\nWC  ', groesse: 3.5, ausrichtung: 'mitte', winkel: 90 });
  assert.equal(t.klasse, 'Textnotiz');
  assert.equal(t.eigenschaften.inhalt, 'Bad\nWC');
  assert.equal(t.eigenschaften.winkel, 90);
  assert.throws(() => P.textParameter({ ansicht: 'AN-1', punkt: [0, 0], inhalt: '  ' }), /leer/);
  const l = P.linieParameter({ ansicht: 'AN-1', punkte: [[0, 0], [1000, 0], [1000, 500]], stift: 'dick', linienart: 'strich' });
  assert.deepEqual(l.eigenschaften, { stift: 'dick', linienart: 'strich' });
  assert.equal(l.geometrie.art, 'polylinie');
  assert.throws(() => P.linieParameter({ ansicht: 'AN-1', punkte: [[0, 0]] }), P.WerkzeugFehler);
  const fb = P.fuellParameter({ ansicht: 'AN-1', punkte: [[0, 0], [1000, 0], [1000, 500], [0, 0]], muster: 'voll', farbe: '#cccccc', rand: true });
  assert.equal(fb.geometrie.art, 'polygon');
  assert.equal(fb.geometrie.punkte.length, 3);
  assert.deepEqual(fb.eigenschaften, { muster: 'voll', farbe: '#cccccc', rand: true });
  assert.throws(() => P.fuellParameter({ ansicht: 'AN-1', punkte: [[0, 0], [1, 1000], [1000, 0]], farbe: 'rot' }), /Farbe/);
  const k = P.koteParameter({ ansicht: 'AN-1', punkt: [10, 20], z: 2900, zusatz: 'OK FB' });
  assert.deepEqual(k.geometrie.punkte, [[10, 20, 2900]]);
  assert.equal(k.eigenschaften.zusatz, 'OK FB');
  assert.deepEqual(P.koteParameter({ ansicht: 'AN-1', punkt: [10, 20] }).geometrie.punkte, [[10, 20]]);   // Schnitt: v = y
  const n = P.nordpfeilParameter({ ansicht: 'AN-1', punkt: [0, 0], winkel: 15, groesse: 20 });
  assert.deepEqual(n.eigenschaften, { art: 'nordpfeil', winkel: 15, groesse: 20 });
});

test('Schnitt: blick aus der Seite des Mauszeigers', () => {
  const a = [0, 0], b = [5000, 0];
  assert.equal(P.seiteDerLinie(a, b, [2500, 1000]), 'links');
  assert.equal(P.seiteDerLinie(a, b, [2500, -1000]), 'rechts');
  assert.equal(P.seiteDerLinie(b, a, [2500, 1000]), 'rechts');   // Linie umgekehrt: Seiten tauschen
  const p = P.schnittParameter({ geschoss: 'EG', a, b, seitenpunkt: [2500, -800], tiefe: 10000, massstab: 50, name: 'Schnitt 1' });
  assert.deepEqual(p, {
    klasse: 'Ansicht', eigenschaften: { art: 'schnitt', blick: 'rechts', tiefe: 10000, massstab: 50 },
    geometrie: { art: 'linie', punkte: [[0, 0], [5000, 0]] }, geschoss: 'EG', name: 'Schnitt 1',
  });
  assert.throws(() => P.schnittParameter({ a, b, seitenpunkt: [0, 1], tiefe: null }), /tiefe/i);
  assert.throws(() => P.schnittParameter({ a, b: [0.2, 0], seitenpunkt: [0, 1], tiefe: 1000 }), /Länge/);
});

test('Ändern: Verschieben, Kopieren, Drehen, Spiegeln', () => {
  assert.deepEqual(P.verschiebenParameter(['W-1', 'T-1'], [100, 100], [350.5, -50]), { elemente: ['W-1', 'T-1'], dx: 250.5, dy: -150 });
  assert.deepEqual(P.kopierenParameter(['W-1'], [0, 0], [1000, 0], 3), { elemente: ['W-1'], dx: 1000, dy: 0, anzahl: 3 });
  assert.equal(P.kopierenParameter(['W-1'], [0, 0], [1000, 0], 0).anzahl, 1);
  assert.throws(() => P.verschiebenParameter([], [0, 0], [1, 1]), /Nichts gewählt/);
  nah(P.drehwinkel([0, 0], [1, 0], [0, 1]), 90);
  nah(P.drehwinkel([0, 0], [0, 1], [1, 0]), -90);
  nah(P.drehwinkel([10, 10], [20, 10], [10, 20]), 90);
  nah(P.drehwinkel([0, 0], [1, 0], [-1, -0.0001]), -179.9943, 1e-3);
  assert.deepEqual(P.drehenParameter(['W-1'], [10, 20], 45), { elemente: ['W-1'], mitte: [10, 20], winkel: 45 });
  assert.throws(() => P.drehenParameter(['W-1'], [10, 20], null), /winkel/i);
  assert.deepEqual(P.spiegelnParameter(['W-1'], [0, 0], [0, 1000]), { elemente: ['W-1'], achse: [[0, 0], [0, 1000]] });
  assert.throws(() => P.spiegelnParameter(['W-1'], [0, 0], [0.1, 0]), /Länge/);
});

test('Griffe: neue Geometrie behält Art, Dicke, Höhe und z', () => {
  const g = { art: 'linie', punkte: [[0, 0, 0], [5000, 0, 0]], dicke: 300, hoehe: 2900 };
  const n = P.griffGeometrie(g, 1, [5200.00041, 100]);
  assert.deepEqual(n, { art: 'linie', punkte: [[0, 0, 0], [5200, 100, 0]], dicke: 300, hoehe: 2900 });
  assert.deepEqual(g.punkte[1], [5000, 0, 0], 'Original bleibt unverändert');
  const l = P.griffGeometrie({ art: 'polylinie', punkte: [[0, 0], [1, 1], [2, 0]] }, 0, [-5, 5]);
  assert.deepEqual(l.punkte[0], [-5, 5]);
  assert.throws(() => P.griffGeometrie(g, 2, [0, 0]), P.WerkzeugFehler);
  assert.equal(P.griffPunkte(g).length, 2);
  assert.deepEqual(P.griffPunkte({ art: 'punkt', punkte: [[0, 0]] }), []);
});

test('Griffe: Tür umdrehen (seite, band)', () => {
  const t = { kennzeichen: 'T-1', werte: { seite: 'links', band: 'anfang' } };
  assert.deepEqual(P.tuerUmdrehen(t, 'seite'), { element: 'T-1', eigenschaften: { seite: 'rechts' } });
  assert.deepEqual(P.tuerUmdrehen(t, 'band'), { element: 'T-1', eigenschaften: { band: 'ende' } });
  assert.deepEqual(P.tuerUmdrehen({ kennzeichen: 'T-2', werte: {} }, 'seite'), { element: 'T-2', eigenschaften: { seite: 'rechts' } });   // Vorgabe links
  assert.throws(() => P.tuerUmdrehen(t, 'farbe'), P.WerkzeugFehler);
});

// ============================================================================================== Schicht (Markup)

test('Schicht: Fangsymbole je Art, Text gegengespiegelt', () => {
  const s = 0.5;
  for (const art of ['endpunkt', 'schnittpunkt', 'mitte', 'lot', 'naechster', 'raster']) {
    const m = S.fangsymbol({ punkt: [100, 200], art }, s);
    assert.match(m, /class="wz-fang/, art);
  }
  assert.match(S.fangsymbol({ punkt: [1, 2], art: 'endpunkt' }, s), /<rect/);
  assert.match(S.fangsymbol({ punkt: [1, 2], art: 'endpunkt' }, s), />Endpunkt</);
  assert.match(S.fangsymbol({ punkt: [1, 2], art: 'mitte' }, s), /<polygon/);
  assert.equal(S.fangsymbol({ punkt: [1, 2], art: 'frei' }, s), '');
  assert.equal(S.fangsymbol(null, s), '');
  assert.match(S.text([0, 0], 'x', 1), /scale\(1 -1\)/);
});

test('Schicht: temporäre Bemassung zeigt Länge in mm und Winkel', () => {
  const m = S.tempMass([0, 0], [3000, 0], 1);
  assert.match(m, /3000 mm/);
  assert.match(m, /0\.0°/);
  const d = S.tempMass([0, 0], [1000, 1000], 1);
  assert.match(d, /1414 mm/);
  assert.match(d, /45\.0°/);
  assert.equal(S.tempMass([5, 5], [5, 5], 1), '');
  assert.doesNotMatch(S.tempMass([0, 0], [3000, 0], 1, { mitWinkel: false }), /°/);
});

test('Schicht: Text wird maskiert, Band und Umrisse', () => {
  assert.match(S.text([0, 0], '<b>&', 1), /&lt;b&gt;&amp;/);
  assert.match(S.band([0, 0], [1000, 0], 300), /points="0,150 1000,150 1000,-150 0,-150"/);
  const wand = { klasse: 'Wand', geometrie: { art: 'linie', punkte: [[0, 0, 0], [1000, 0, 0]], dicke: 200 } };
  const u = S.umriss(wand);
  assert.equal(u.art, 'wand');
  const svg = S.umrisseSvg([wand, { klasse: 'Raum', geometrie: { art: 'polygon', punkte: [[0, 0], [1, 0], [1, 1]] } }, { klasse: 'WC', geometrie: { art: 'punkt', punkte: [[5, 5]] } }], (p) => [p[0] + 10, p[1]], 1);
  assert.match(svg, /points="10,100 1010,100 1010,-100 10,-100"/);
  assert.match(svg, /<polygon/);
  assert.equal(S.umriss({ klasse: 'X' }), null);
});

test('Schicht: lesbarer Textwinkel', () => {
  nah(S.lesbarerWinkel([0, 0], [1, 0]), 0);
  nah(S.lesbarerWinkel([0, 0], [-1, 0]), 0);
  nah(S.lesbarerWinkel([0, 0], [0, 1]), 90);
  nah(S.lesbarerWinkel([0, 0], [0, -1]), 90);
});

// ============================================================================================== Optionen

test('Optionen: Pflichtfelder sind leer, Vorgaben gelten, Werte werden gemerkt', () => {
  assert.equal(O.wert('wand', 'dicke'), null, 'keine Vorgabedicke');
  assert.equal(O.wert('wand', 'bezugslinie'), 'mitte');
  assert.equal(O.wert('wand', 'kette'), true);
  assert.equal(O.wert('wand', 'raster'), 100);
  assert.equal(O.rasterWert(), 100);
  O.setze('wand', 'dicke', 300);
  assert.equal(O.wert('wand', 'dicke'), 300);
  assert.equal(O.werte('wand').dicke, 300);
  O.setze('fangen', 'raster', 50);
  assert.equal(O.rasterWert(), 50);
  assert.equal(O.wert('tuer', 'raster'), 50, 'Raster gilt für alle Werkzeuge');
  O.setze('wand', 'dicke', null);
  assert.equal(O.wert('wand', 'dicke'), null);
  assert.equal(O.wert('schnitt', 'tiefe'), 10000);
  assert.equal(O.wert('tuer', 'breite'), null);
  assert.equal(O.wert('fenster', 'bruestung'), null);
  assert.equal(O.ausText(O.feldDef('wand', 'dicke'), '30,5'), 30.5);
  assert.equal(O.ausText(O.feldDef('wand', 'dicke'), 'abc'), null);
  assert.equal(O.ausText(O.feldDef('raum', 'nutzung'), ' Bad '), 'Bad');
  O.setze('fangen', 'raster', 100);
});

test('Optionen: jedes Werkzeug hat Felder mit eindeutigen Ids', () => {
  for (const [id, def] of Object.entries(O.WERKZEUGE)) {
    assert.ok(def.titel, id);
    const ids = def.felder.map((f) => f.id);
    assert.equal(new Set(ids).size, ids.length, `${id}: doppelte Felder`);
    for (const f of def.felder) assert.ok(['zahl', 'text', 'auswahl', 'bool', 'hinweis', 'knopf'].includes(f.art), `${id}.${f.id}`);   // hinweis, knopf: Leitungen (Modul 3, C1)
  }
});

// ============================================================================================== Mehrfachauswahl

test('Mehrfachauswahl: Strg+Klick ergänzt, Klick ersetzt, leer hebt auf', async () => {
  const Z = await import('../zustand.js');
  const A = await import('./auswahl.js');
  for (const [id, kz] of [['a', 'W-1'], ['b', 'W-2'], ['c', 'T-1']]) {
    Z.zustand.elemente.set(id, { id, kennzeichen: kz, klasse: 'Wand' });
    Z.zustand.nachKennzeichen.set(kz.toLowerCase(), id);
  }
  Z.auswaehlen('a');
  assert.deepEqual(A.auswahlKennzeichen(), ['W-1']);
  A.strgMerken(); Z.auswaehlen('b');
  assert.deepEqual(A.auswahlKennzeichen(), ['W-1', 'W-2']);
  assert.equal(A.istMehrfach(), true);
  A.strgMerken(); Z.auswaehlen('c');
  assert.deepEqual(A.auswahlKennzeichen(), ['W-1', 'W-2', 'T-1']);
  A.strgMerken(); Z.auswaehlen('a');
  assert.deepEqual(A.auswahlKennzeichen(), ['W-2', 'T-1'], 'Strg+Klick auf ein Mitglied nimmt es heraus');
  Z.auswaehlen('b');
  assert.deepEqual(A.auswahlKennzeichen(), ['W-2'], 'Klick ohne Strg ersetzt');
  Z.auswaehlen(null);
  assert.deepEqual(A.auswahlKennzeichen(), []);
  assert.equal(A.hatAuswahl(), false);
  Z.melden('modell', { geloescht: ['b'], ids: [] });
});

// ============================================================================================== Gegen den echten Kern (optional)

const SERVER = process.env.HLKS_TEST_SERVER;
test('Kern (Trockenlauf): gebildete Parameter werden angenommen', { skip: !SERVER }, async () => {
  const befehl = async (name, p) => (await fetch(`${SERVER}/api/befehl/${name}?trockenlauf=true`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(p) })).json();
  const modell = await (await fetch(`${SERVER}/api/modell`)).json();
  const geschoss = modell.elemente.find((e) => e.klasse === 'Geschoss');
  const ansicht = modell.elemente.find((e) => e.klasse === 'Ansicht' && e.werte?.art === 'grundriss' && !e.werte?.vorlage);
  assert.ok(geschoss && ansicht);

  let r = await befehl('wand_zeichnen', P.wandParameter({ geschoss: geschoss.kennzeichen, punkte: [[30000, 30000], [35000, 30000], [35000, 34000], [30000, 34000], [30000, 30000]], dicke: 300, bezugslinie: 'links' }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.achseParameter({ a: [30000, 29000], b: [30000, 35000], bezeichnung: 'Z' }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.bemassungParameter({ ansicht: ansicht.kennzeichen, punkte: [[30000, 30000], [35000, 30000]], lage: [32500, 28000] }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.textParameter({ ansicht: ansicht.kennzeichen, punkt: [31000, 31000], inhalt: 'Test' }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.linieParameter({ ansicht: ansicht.kennzeichen, punkte: [[0, 0], [1000, 1000]] }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.fuellParameter({ ansicht: ansicht.kennzeichen, punkte: [[0, 0], [1000, 0], [1000, 1000]], muster: 'voll' }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.koteParameter({ ansicht: ansicht.kennzeichen, punkt: [500, 500], z: geschoss.werte?.kote ?? 0 }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.nordpfeilParameter({ ansicht: ansicht.kennzeichen, punkt: [500, 500] }));
  assert.equal(r.angenommen, true, r.fehler);
  r = await befehl('element_anlegen', P.schnittParameter({ geschoss: geschoss.kennzeichen, a: [0, 4000], b: [10000, 4000], seitenpunkt: [5000, 6000], tiefe: 10000, massstab: 50, name: 'Schnitt Test' }));
  assert.equal(r.angenommen, true, r.fehler);
  const wand = modell.elemente.find((e) => e.klasse === 'Wand' && e.geschoss === geschoss.kennzeichen);
  if (wand) {
    const m = wand.geometrie.punkte;
    r = await befehl('elemente_verschieben', P.verschiebenParameter([wand.kennzeichen], [0, 0], [100, 50]));
    assert.equal(r.angenommen, true, r.fehler);
    r = await befehl('elemente_drehen', P.drehenParameter([wand.kennzeichen], m[0], 90));
    assert.equal(r.angenommen, true, r.fehler);
    r = await befehl('elemente_spiegeln', P.spiegelnParameter([wand.kennzeichen], [0, 0], [0, 1000]));
    assert.equal(r.angenommen, true, r.fehler);
    r = await befehl('elemente_kopieren', P.kopierenParameter([wand.kennzeichen], [0, 0], [0, 1000], 2));
    assert.equal(r.angenommen, true, r.fehler);
    r = await befehl('element_aendern', { element: wand.kennzeichen, geometrie: P.griffGeometrie(wand.geometrie, 1, [m[1][0] + 100, m[1][1]]) });
    assert.equal(r.angenommen, true, r.fehler);
  }
});

// ============================================================================================== Kalibrieren (Modul 1b, C1)

const K = await import('./kalibrierung.js');
const T1 = { faktor: 25.4, drehung: 30, dx: 1000, dy: -2000 };

test('Kalibrieren: Unterlagen-Koordinaten → Modell → zurück (DWG, Drehung und Verschiebung)', () => {
  for (const uv of [[0, 0], [10, 0], [3.5, -7.25], [1234.5, 99.9]]) {
    const m = K.unterlageNachModell(T1, 'dwg', uv);
    punktGleich(K.modellNachUnterlage(T1, 'dwg', m), uv, 1e-9);
  }
  // Ursprung der Unterlage liegt bei [dx, dy]; ein Punkt auf der u-Achse in Richtung der Drehung
  punktGleich(K.unterlageNachModell(T1, 'dwg', [0, 0]), [1000, -2000]);
  const m = K.unterlageNachModell(T1, 'dwg', [10, 0]);
  nah(m[0], 1000 + 254 * Math.cos(Math.PI / 6), 1e-9);
  nah(m[1], -2000 + 254 * Math.sin(Math.PI / 6), 1e-9);
});

test('Kalibrieren: PDF und Bild spiegeln y zuerst (Pixel, y nach unten)', () => {
  const t = { faktor: 2, drehung: 0, dx: 100, dy: 200 };
  punktGleich(K.unterlageNachModell(t, 'pdf', [10, 30]), [120, 200 - 60]);
  punktGleich(K.unterlageNachModell(t, 'dwg', [10, 30]), [120, 260]);
  for (const art of ['pdf', 'bild']) {
    const u = [412.5, 86.25];
    punktGleich(K.modellNachUnterlage({ ...T1, faktor: 0.5 }, art, K.unterlageNachModell({ ...T1, faktor: 0.5 }, art, u)), u, 1e-9);
  }
});

test('Kalibrieren: transformationVon liest die Eigenschaft und verwirft Ungültiges', () => {
  assert.deepEqual(K.transformationVon({ werte: { transformation: { faktor: 2, drehung: 90, dx: 1, dy: 2 } } }), { faktor: 2, drehung: 90, dx: 1, dy: 2 });
  assert.deepEqual(K.transformationVon({ werte: { transformation: { faktor: 1 } } }), { faktor: 1, drehung: 0, dx: 0, dy: 0 });
  assert.equal(K.transformationVon({ werte: {} }), null);
  assert.equal(K.transformationVon({ werte: { transformation: { faktor: 0 } } }), null);
  assert.equal(K.transformationVon({ werte: { transformation: { faktor: 'x' } } }), null);
  assert.equal(K.transformationVon(null), null);
});

test('Kalibrieren: Parameter für unterlage_kalibrieren (zwei Punktpaare, nur verschieben)', () => {
  const p = K.kalibrierParameter({ unterlage: 'UL-1', von: [[0, 0], [10.1234567891, 0]], nach: [[1000.12345, 2000], [6000, 2000.5]] });
  assert.deepEqual(p, { unterlage: 'UL-1', von: [[0, 0], [10.123457, 0]], nach: [[1000.123, 2000], [6000, 2000.5]] });
  assert.equal(p.nurVerschieben, undefined);
  const v = K.kalibrierParameter({ unterlage: 'UL-1', von: [[5, 5]], nach: [[100, 200]], nurVerschieben: true });
  assert.deepEqual(v, { unterlage: 'UL-1', von: [[5, 5]], nach: [[100, 200]], nurVerschieben: true });
});

test('Kalibrieren: Fehler mit Vorschlag bei fehlender Unterlage, falscher Punktzahl und gleichen Punkten', () => {
  const fehler = (f) => { try { f(); } catch (e) { return e; } return null; };
  let e = fehler(() => K.kalibrierParameter({ unterlage: '', von: [[0, 0], [1, 0]], nach: [[0, 0], [1, 0]] }));
  assert.ok(e instanceof P.WerkzeugFehler && e.vorschlag);
  e = fehler(() => K.kalibrierParameter({ unterlage: 'UL-1', von: [[0, 0]], nach: [[0, 0]] }));
  assert.ok(e instanceof P.WerkzeugFehler && /zwei Punkte/.test(e.message));
  e = fehler(() => K.kalibrierParameter({ unterlage: 'UL-1', von: [[1, 1], [1, 1]], nach: [[0, 0], [5, 0]] }));
  assert.ok(e && /derselbe Punkt/.test(e.message));
  e = fehler(() => K.kalibrierParameter({ unterlage: 'UL-1', von: [[0, 0], [1, 0]], nach: [[7, 7], [7, 7.2]] }));
  assert.ok(e && /Zielpunkte/.test(e.message));
  e = fehler(() => K.kalibrierParameter({ unterlage: 'UL-1', von: [[0, 0], [NaN, 0]], nach: [[0, 0], [5, 0]] }));
  assert.ok(e && /gültigen Koordinaten/.test(e.message));
});

test('Kalibrieren: Optionen — Unterlagenliste kommt frisch aus dem Zustand, nur kalibrierbare Arten', async () => {
  const def = O.WERKZEUGE.kalibrieren;
  assert.ok(def);
  const feld = def.felder.find((f) => f.id === 'unterlage');
  assert.deepEqual(feld.werte, []);                                  // ohne Projekt keine Unterlagen
  const Z = await import('../zustand.js');
  Z.zustand.elemente.set('a', { id: 'a', kennzeichen: 'UL-1', klasse: 'Unterlage', name: 'Grundriss EG', geschoss: 'EG', werte: { art: 'pdf' } });
  Z.zustand.elemente.set('b', { id: 'b', kennzeichen: 'UL-2', klasse: 'Unterlage', name: null, geschoss: null, werte: { art: 'ifc' } });
  Z.zustand.elemente.set('c', { id: 'c', kennzeichen: 'UL-3', klasse: 'Unterlage', name: null, geschoss: '1.OG', werte: { art: 'dwg' } });
  assert.deepEqual(feld.werte, [['UL-1', 'Grundriss EG (PDF · EG)'], ['UL-3', 'UL-3 (DWG · 1.OG)']]);
  Z.zustand.elemente.clear();
  assert.equal(O.wert('kalibrieren', 'nurVerschieben'), false);
  assert.equal(O.wert('kalibrieren', 'unterlage'), '');
});
