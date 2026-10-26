// Tests der Gestenlogik (Modul G «Gesten am Arbeitsplatz»): Merkmale, Faust, Pistole, Wisch, Spielfilter, Händigkeit, Aufnahmen – und alle Punktfolgen in testfolgen/*.json
// (synthetische aus testfolgen/erzeugen.mjs und später echte Aufnahmen von Christian im Format von aufnahmeBeenden(), je mit «erwartet»). Ohne Browser und Kamera.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/gesten/gesten-logik.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import * as L from './gesten-logik.js';
import { handBild } from './testfolgen/handmodell.mjs';
import { alleFolgen, dateiName, dateiText, folge, k, P0, plus } from './testfolgen/erzeugen.mjs';

const hier = path.dirname(fileURLToPath(import.meta.url));
const ORDNER = path.join(hier, 'testfolgen');
const GRAD = Math.PI / 180;
const W = L.stellschraubenWerte();
// Die Erwartungen der Testfolgen sind für die ursprüngliche Übersetzung bemessen. Seit der Abnahme 26.10.2026 sind die Vorgaben gedämpft
// («alle operationen abdämpfen»); die Folgen laufen deshalb mit dieser Bemessung, die Dämpfung prüft ein eigener Test.
const BEMESSUNG = Object.freeze({ drehen_grad_je_cm: 6, verschieben_je_cm: 0.04, rollen_faktor: 1, zoom_faktor: 2.5, glaettung_ms: 80, zoom_glaettung_ms: 150 });
const abspielen = (a, opt = {}) => L.aufnahmeAbspielen(a, { ...opt, stellschrauben: { ...BEMESSUNG, ...(opt.stellschrauben ?? {}) } });
const m = (haltung, extra = {}) => L.merkmale(handBild({ haltung, lage: P0, ...extra }), 0.75);

// ---------------------------------------------------------------------------------------------- Stellschrauben

test('Stellschrauben: je Schlüssel Vorgabe im Bereich, Schritt, Einheit, Titel, Erklärung, Herkunft; Namen wie in der Quest App', () => {
  const s = L.STELLSCHRAUBEN_VORGABE;
  for (const name of ['anstoss_weg', 'anstoss_tempo_ms', 'randwinkel_grad', 'handflaeche_grad', 'totbereich', 'rollen_min_grad', 'zoom_totbereich', 'zoom_faktor', 'sperre_ms']) assert.ok(s[name], name);
  for (const [name, d] of Object.entries(s)) {
    assert.match(name, /^[a-z][a-z0-9_]*$/);
    for (const f of ['vorgabe', 'min', 'max', 'schritt']) assert.ok(Number.isFinite(d[f]), `${name}.${f}`);
    assert.ok(d.min <= d.vorgabe && d.vorgabe <= d.max && d.schritt > 0, name);
    for (const f of ['einheit', 'titel', 'erklaerung', 'herkunft', 'gruppe']) assert.equal(typeof d[f], 'string', `${name}.${f}`);
    assert.ok(d.titel.length > 3 && d.erklaerung.length > 20 && d.herkunft.length > 5, name);
    assert.ok(!/ß/.test(d.titel + d.erklaerung + d.herkunft), `${name}: «ss» statt «ß»`);
  }
  // Werte der Spezifikation (entwurf/modul-gesten.md) und der Quest App (V1h)
  assert.equal(s.totbereich.vorgabe, 0.015);
  assert.equal(s.rollen_min_grad.vorgabe, 8);
  assert.equal(s.sperre_ms.vorgabe, 300);
  assert.equal(s.anstoss_weg.vorgabe, 0.045);
  assert.equal(s.anstoss_tempo_ms.vorgabe, 0.35);
  assert.equal(s.randwinkel_grad.vorgabe, 25);
  assert.ok(Object.isFrozen(s) && Object.isFrozen(s.totbereich));
});

test('Stellschrauben: Werte begrenzt, unbekannte und ungültige fallen weg', () => {
  const w = L.stellschraubenWerte({ totbereich: 5, sperre_ms: -3, anstoss_weg: 'x', fremd: 1, rollen_min_grad: 12, zoom_faktor: null });
  assert.equal(w.totbereich, L.STELLSCHRAUBEN_VORGABE.totbereich.max);
  assert.equal(w.sperre_ms, 0);
  assert.equal(w.anstoss_weg, 0.045);
  assert.equal(w.rollen_min_grad, 12);
  assert.equal(w.zoom_faktor, L.STELLSCHRAUBEN_VORGABE.zoom_faktor.vorgabe);
  assert.ok(!('fremd' in w));
  assert.deepEqual(L.stellschraubenWerte(), L.stellschraubenWerte(null));
});

// ---------------------------------------------------------------------------------------------- Merkmale

test('Merkmale: Faustverhältnis offen ≈ 1.9, locker zwischen «offen_ab» und offen, Faust ≈ 1 und darunter; Handgrösse aus den Weltpunkten', () => {
  const offen = m('offen'), locker = m('locker'), faust = m('faust');
  assert.ok(offen.faust > 1.75 && offen.faust < 2.0, offen.faust);
  assert.ok(locker.faust >= W.offen_ab && locker.faust < L.OFFEN_GANZ, locker.faust);
  assert.ok(faust.faust < 1.1, faust.faust);
  assert.ok(Math.abs(offen.groesse - 0.092) < 0.001, 'Handgelenk bis Grundgelenk Mittelfinger');
  assert.ok(offen.kruemmung.every((x) => x < 0.1) && faust.kruemmung.slice(1).every((x) => x > 0.5), 'Krümmung je Finger');
  assert.equal(L.merkmale({ punkte: [[0, 0]], welt: [] }), null);
  assert.equal(L.merkmale(null), null);
});

test('Merkmale: Faust, Pistole (Daumen oben), Zeigen (Daumen anliegend), lockere Hand', () => {
  assert.ok(L.istFaust(m('faust'), false, W) && !L.istFaust(m('offen'), false, W) && !L.istFaust(m('pistole'), false, W));
  assert.ok(L.istFaust(m('griff'), false, W), 'ein Griff (Tasse) sieht wie eine Faust aus – darum zählt nur die im Bild geschlossene Faust');
  assert.ok(L.istPistole(m('pistole')) && !L.istPistole(m('zeigen')) && !L.istPistole(m('faust')) && !L.istPistole(m('offen')));
  assert.ok(L.pistolenHaltung(m('zeigen')), 'Zeigen ist die Haltung der Pistole ohne Daumen oben');
  assert.equal(L.lockereHand(m('offen'), W), null);
  assert.equal(L.lockereHand(m('locker'), W), null);
  assert.equal(L.lockereHand(m('faust'), W), 'Faust');
  assert.equal(L.lockereHand(m('pistole'), W), 'Pistolengeste');
  assert.match(L.lockereHand(m('locker'), { ...W, offen_ab: 1.8 }), /zu geschlossen/);
  // Hysterese der Faust: eine halb geöffnete Hand bleibt Faust, wenn sie es war
  const halb = L.merkmale(handBild({ haltung: { finger: [[42, 55, 25], [42, 55, 25], [42, 55, 25], [42, 55, 25]], daumen: [[0.035, -0.040, -0.022], [0.030, -0.058, -0.040], [0.010, -0.062, -0.045]] }, lage: P0 }), 0.75);
  assert.ok(halb.faust > W.faust_an && halb.faust < W.faust_an + L.FAUST_LOS_PLUS, halb.faust);
  assert.ok(!L.istFaust(halb, false, W) && L.istFaust(halb, true, W));
});

test('Merkmale: Rollwinkel der Knöchellinie (gegen den Uhrzeiger positiv), scheinbare Grösse und Lage in m unabhängig vom Abstand', () => {
  const a = m('faust'), b = m('faust', { roll: 30 * GRAD });
  assert.ok(Math.abs(L.winkelDiff(a.roll, b.roll) - 30 * GRAD) < 0.01, 'Rollen um 30°');
  const nah = L.merkmale(handBild({ haltung: 'faust', lage: [-0.12, 0.05, 0.4] }), 0.75), fern = L.merkmale(handBild({ haltung: 'faust', lage: [-0.12, 0.05, 0.6] }), 0.75);
  assert.ok(Math.abs(nah.groesseLn - fern.groesseLn - Math.log(0.6 / 0.4)) < 0.01, 'ln der scheinbaren Grösse');
  const ln = L.lageMeter(nah.mitte, nah.groesseLn, 0.75), lf = L.lageMeter(fern.mitte, fern.groesseLn, 0.75);
  assert.ok(Math.hypot(ln[0] - lf[0], ln[1] - lf[1]) < 0.002, 'gleiche Lage in m, ob 40 oder 60 cm vor der Kamera');
  assert.ok(Math.abs(ln[0] - (-0.12)) < 0.01, 'Lage seitlich in m');
  assert.ok(Math.abs(nah.meterJeBild / fern.meterJeBild - 0.4 / 0.6) < 0.01);
});

test('Handfläche zur Wischrichtung: zur Kamera passt immer, flach nach unten nur hoch/runter, seitlich nur links/rechts', () => {
  const kamera = m('offen').normale, unten = m('offen', { nick: 90 * GRAD }).normale, seitlich = m('offen', { gier: 90 * GRAD }).normale;
  const quer = [1, 0], senk = [0, 1];
  assert.ok(L.handflaecheWinkel(kamera, quer) < 1 && L.handflaecheWinkel(kamera, senk) < 1);
  assert.ok(L.handflaecheWinkel(unten, quer) > 85 && L.handflaecheWinkel(unten, senk) < 5);
  assert.ok(L.handflaecheWinkel(seitlich, quer) < 5 && L.handflaecheWinkel(seitlich, senk) > 85);
  assert.equal(L.seitenWinkel(0.5, 60), 0);
  assert.ok(Math.abs(L.seitenWinkel(0, 60) + 30) < 1e-9 && Math.abs(L.seitenWinkel(1, 60) - 30) < 1e-9);
});

// ---------------------------------------------------------------------------------------------- Bausteine

test('Spielfilter: unter der Schwelle nichts, danach linear mit kleinem Spiel; Umkehr erst nach dem Spiel', () => {
  const s = { aktiv: false, anker: 0, ref: null };
  assert.equal(L.spielSkalar(s, 0.9, 1, 0.25), 0);
  assert.equal(L.spielSkalar(s, 1.2, 1, 0.25), 0, 'Schwelle überschritten: noch kein Sprung');
  assert.ok(Math.abs(L.spielSkalar(s, 2.2, 1, 0.25) - 1) < 1e-12, 'danach 1:1');
  assert.equal(L.spielSkalar(s, 1.8, 1, 0.25), 0, 'Umkehr innerhalb des doppelten Spiels');
  assert.ok(Math.abs(L.spielSkalar(s, 1.4, 1, 0.25) + 0.3) < 1e-12, 'zurück: 1.4 + Spiel gegen die Referenz 1.95');
  const l = { aktiv: false, anker: [0, 0], ref: null };
  assert.deepEqual(L.spielLage(l, [0.5, 0], 1, 0.3), [0, 0]);
  L.spielLage(l, [1.5, 0], 1, 0.3);
  const d = L.spielLage(l, [1.5, 1], 1, 0.3);
  assert.ok(d[1] > 0.6 && Math.abs(d[0]) < 0.25, JSON.stringify(d));
});

test('Glättung, Winkelunterschied', () => {
  assert.equal(L.glaettAnteil(33, 0), 1);
  assert.ok(Math.abs(L.glaettAnteil(80, 80) - (1 - Math.exp(-1))) < 1e-12);
  assert.equal(L.glaettAnteil(-5, 80), 0);
  assert.ok(Math.abs(L.winkelDiff(170 * GRAD, -170 * GRAD) - 20 * GRAD) < 1e-12);
  assert.ok(Math.abs(L.winkelDiff(-170 * GRAD, 170 * GRAD) + 20 * GRAD) < 1e-12);
});

// ---------------------------------------------------------------------------------------------- Händigkeit, MediaPipe, Aufnahmen

test('Händigkeit: im ungespiegelten Kamerabild nennt MediaPipe die Hand des Benutzers (Abnahme 26.10.2026); Punkte wie im Spiegel', () => {
  assert.equal(L.MEDIAPIPE_ERWARTET_SPIEGELBILD, false);
  const roh = { punkte: Array.from({ length: 21 }, (_, i) => [0.2 + i * 0.01, 0.5, 0.01]), welt: Array.from({ length: 21 }, () => [0.03, 0.01, -0.02]), haendigkeitRoh: 'Left', score: 0.9 };
  const [h] = L.haendeImSpiegel([roh]);
  assert.equal(h.haendigkeit, 'links');
  assert.ok(Math.abs(h.punkte[0][0] - 0.8) < 1e-12 && h.punkte[0][1] === 0.5 && h.punkte[0][2] === 0.01);
  assert.equal(h.welt[0][0], -0.03);
  assert.equal(L.haendeImSpiegel([{ ...roh, haendigkeitRoh: 'Right' }])[0].haendigkeit, 'rechts');
  const g = L.haendeImSpiegel([roh], { bildGespiegelt: true })[0];
  assert.equal(g.haendigkeit, 'rechts', 'schon gespiegeltes Bild: getauscht');
  assert.equal(g.punkte[0][0], 0.2);
  assert.equal(L.haendeImSpiegel([{ ...roh, haendigkeitRoh: null }])[0].haendigkeit, null);
});

test('Gestenhand: nur die eingestellte Hand; bei zweien die sicherere, bei Gleichstand die auf ihrer Seite', () => {
  const hand = (x, h, score = 0.9) => ({ punkte: Array.from({ length: 21 }, () => [x, 0.5, 0]), welt: Array.from({ length: 21 }, () => [0, 0, 0]), haendigkeit: h, score });
  assert.equal(L.gestenhand([hand(0.3, 'rechts')], 'links'), null);
  assert.equal(L.gestenhand([hand(0.3, 'rechts'), hand(0.7, 'links')], 'links').punkte[0][0], 0.7);
  assert.equal(L.gestenhand([hand(0.3, 'links', 0.6), hand(0.7, 'links', 0.9)], 'links').punkte[0][0], 0.7);
  assert.equal(L.gestenhand([hand(0.7, 'links'), hand(0.3, 'links')], 'links').punkte[0][0], 0.3);
  assert.equal(L.gestenhand([hand(0.3, 'rechts'), hand(0.7, 'rechts')], 'rechts').punkte[0][0], 0.7);
  assert.equal(L.gestenhand(null, 'links'), null);
});

test('Ergebnis von MediaPipe → nur Zahlen: Bilder, Masken und fremde Felder fallen weg, unvollständige Hände auch', () => {
  const p = (i) => ({ x: i / 40, y: 0.5, z: -0.01, visibility: 0.9, presence: 0.8 });
  const r = {
    landmarks: [Array.from({ length: 21 }, (_, i) => p(i)), Array.from({ length: 5 }, (_, i) => p(i))],
    worldLandmarks: [Array.from({ length: 21 }, (_, i) => ({ x: i / 1000, y: 0, z: 0 })), []],
    handedness: [[{ categoryName: 'Left', score: 0.97, index: 0, displayName: '' }], [{ categoryName: 'Right', score: 0.5 }]],
    bild: { daten: new Uint8Array(10) }, segmentationMasks: ['MASKE'],
  };
  const e = L.ergebnisAusMediapipe(r);
  assert.equal(e.haende.length, 1);
  assert.deepEqual(Object.keys(e), ['haende']);
  assert.deepEqual(Object.keys(e.haende[0]), ['punkte', 'welt', 'haendigkeitRoh', 'score']);
  assert.equal(e.haende[0].haendigkeitRoh, 'Left');
  assert.deepEqual(e.haende[0].punkte[2], [0.05, 0.5, -0.01]);
  const text = JSON.stringify(e);
  assert.ok(!/MASKE|daten|visibility|displayName/.test(text), text.slice(0, 200));
  assert.deepEqual(L.ergebnisAusMediapipe(null), { haende: [] });
  assert.equal(L.ergebnisAusMediapipe({ ...r, handedness: [[{ categoryName: '<script>', score: 1 }]] }).haende[0].haendigkeitRoh, null);
  assert.equal(L.ergebnisAusMediapipe({ landmarks: [Array.from({ length: 21 }, () => ({ x: NaN, y: 0, z: 0 }))], worldLandmarks: r.worldLandmarks }).haende.length, 0);
});

test('Netzsperre des Workers: nur /lib/mediapipe/ desselben Ursprungs', () => {
  const h = 'http://localhost:5130';
  assert.ok(L.netzErlaubt('http://localhost:5130/lib/mediapipe/hand_landmarker.task', h));
  assert.ok(L.netzErlaubt('/lib/mediapipe/wasm/vision_wasm_module_internal.wasm', h));
  assert.ok(L.netzErlaubt(new URL('http://localhost:5130/lib/mediapipe/vision_bundle.mjs'), h));
  for (const u of ['https://odml.pa.googleapis.com/v1/log', 'http://localhost:5131/lib/mediapipe/x', '/api/status', '/lib/mediapipe/../../api/befehl', 'data:text/plain,abc', 'blob:http://localhost:5130/x', '//cdn.jsdelivr.net/npm/@mediapipe/tasks-vision/wasm', '']) {
    assert.equal(L.netzErlaubt(u, h), false, u);
  }
  assert.equal(L.netzErlaubt('/lib/mediapipe/x', 'kein ursprung'), false);
});

test('Aufnahme: Bild hin und zurück (zwei Hände, gerundet), leeres Bild', () => {
  const a = handBild({ haltung: 'offen', lage: P0 }), b = handBild({ seite: 'rechts', haltung: 'faust', lage: [0.1, 0, 0.5] });
  const bild = L.bildFuerAufnahme(33.33333, [{ ...a, haendigkeit: 'links', score: 0.951234 }, { ...b, haendigkeit: 'rechts', score: 0.8 }]);
  assert.equal(bild.t, 33.3);
  assert.equal(bild.score, 0.951);
  assert.equal(bild.weitere.length, 1);
  assert.deepEqual(Object.keys(bild), ['t', 'punkte', 'welt', 'haendigkeit', 'score', 'weitere']);
  const zurueck = L.bildAusAufnahme(bild);
  assert.equal(zurueck.haende.length, 2);
  assert.deepEqual(zurueck.haende.map((h) => h.haendigkeit), ['links', 'rechts']);
  assert.ok(Math.abs(zurueck.haende[0].punkte[8][1] - a.punkte[8][1]) < 1e-4);
  assert.deepEqual(L.bildFuerAufnahme(5, []), { t: 5, punkte: null, welt: null, haendigkeit: null });
  assert.deepEqual(L.bildAusAufnahme({ t: 5, punkte: null, welt: null }).haende, []);
});

// ---------------------------------------------------------------------------------------------- Punktfolgen (synthetisch und echt)

/** Prüft eine Folge gegen ihr «erwartet» (siehe testfolgen/erzeugen.mjs). */
function pruefen(a, erwartet, opt = {}) {
  const r = abspielen(a, opt);
  const name = a.name ?? '?';
  if (!erwartet) return r;
  assert.deepEqual(r.ereignisse.map((e) => ({ geste: e.geste, richtung: e.richtung })), erwartet.schritte ?? [], `${name}: Schritt-Gesten (Gründe: ${r.gruende.join(' | ')})`);
  const s = r.summe;
  if (erwartet.navigation === 'keine') assert.equal(s.bilderMitNavigation, 0, `${name}: keine Bewegung der Ansicht`);
  else if (erwartet.navigation) {
    const n = erwartet.navigation;
    const imBereich = (x, [a1, b1], was) => assert.ok(x >= a1 && x <= b1, `${name}: ${was} ${x.toFixed(3)} nicht in [${a1}, ${b1}]`);
    if (n.drehen) { imBereich(s.drehen[0], n.drehen[0], 'drehen x'); imBereich(s.drehen[1], n.drehen[1], 'drehen y'); }
    if (n.verschieben) { imBereich(s.verschieben[0], n.verschieben[0], 'verschieben x'); imBereich(s.verschieben[1], n.verschieben[1], 'verschieben y'); }
    if (n.rollen) imBereich(s.rollen, n.rollen, 'rollen');
    if (n.zoom) imBereich(s.zoom, n.zoom, 'zoom');
  }
  if (erwartet.navigationEndetBis) assert.ok(s.letzteNavigation !== null && s.letzteNavigation <= erwartet.navigationEndetBis, `${name}: letzte Bewegung ${s.letzteNavigation}`);
  for (const g of erwartet.gesten ?? []) assert.ok(r.gesten.includes(g), `${name}: Geste ${g} (${r.gesten})`);
  return r;
}

const dateien = readdirSync(ORDNER).filter((d) => d.endsWith('.json')).sort();

test('Testfolgen: vorhanden, gültiges Format, die synthetischen sind aktuell (erzeugen.mjs)', () => {
  assert.ok(dateien.length >= 20, `${dateien.length} Dateien`);
  for (const d of dateien) {
    const a = JSON.parse(readFileSync(path.join(ORDNER, d), 'utf8'));
    assert.equal(a.version, L.AUFNAHME_VERSION, d);
    assert.ok(a.hand === 'links' || a.hand === 'rechts', d);
    assert.ok(Array.isArray(a.bilder) && a.bilder.length > 10, d);
    for (const b of a.bilder) assert.ok(Number.isFinite(b.t) && (b.punkte === null || b.punkte.length === 21), d);
  }
  const neu = new Map(alleFolgen().map((f) => [dateiName(f), dateiText(f)]));
  for (const [d, text] of neu) assert.equal(readFileSync(path.join(ORDNER, d), 'utf8'), text, `${d} ist veraltet: node testfolgen/erzeugen.mjs`);
  for (const d of dateien.filter((x) => x.startsWith('synth-'))) assert.ok(neu.has(d), `${d} gehört zu keiner Folge mehr`);
});

for (const d of dateien) {
  test(`Folge ${d}`, () => {
    const a = JSON.parse(readFileSync(path.join(ORDNER, d), 'utf8'));
    const r = pruefen(a, a.erwartet);
    assert.ok(r.zustaende.length > 0);
    if (a.erwartet?.zweiD) {
      const r2 = abspielen(a, { ansicht3d: false });
      const v = a.erwartet.zweiD.verschieben;
      assert.ok(r2.summe.verschieben[0] >= v[0][0] && r2.summe.verschieben[0] <= v[0][1], `2D verschieben x ${r2.summe.verschieben[0]}`);
      assert.ok(r2.summe.verschieben[1] >= v[1][0] && r2.summe.verschieben[1] <= v[1][1], `2D verschieben y ${r2.summe.verschieben[1]}`);
      assert.deepEqual(r2.summe.drehen, [0, 0], 'Plan dreht nicht');
      assert.ok(r2.gesten.includes('verschieben') && !r2.gesten.includes('drehen'));
    }
  });
}

const lies = (name) => JSON.parse(readFileSync(path.join(ORDNER, `${name}.json`), 'utf8'));

test('Aufnahme als Datei (Dialog «Gesten», gesten-text.js aufnahmeDatei) läuft genau wie live – auch Sperre, Zoom und falsche Hand', async () => {
  const { aufnahmeDatei } = await import('./gesten-text.js');
  for (const name of ['synth-faust-zur-kamera', 'synth-stoer-maus-sperre', 'synth-wisch-links', 'synth-stoer-tippen-rand', 'synth-faust-rollen']) {
    const a = lies(name);
    const d = JSON.parse(aufnahmeDatei({ ...a, bild: 'DATA' }).text);
    assert.ok(!JSON.stringify(d).includes('DATA'));
    const live = abspielen(a), datei = abspielen(d);
    assert.deepEqual(datei.ereignisse, live.ereignisse, name);
    assert.deepEqual(datei.summe, live.summe, name);
  }
});

// ---------------------------------------------------------------------------------------------- einzelne Regeln (die Folge löst aus, sobald die Regel fehlt)

test('Zustand: «bereit» ohne Hand, «hand» mit Hand, «aktiv» mit Namen nur bei wirkender Faust', () => {
  const r = abspielen(lies('synth-faust-drehen-rechts'));
  assert.deepEqual(r.zustaende.sort(), ['aktiv', 'hand']);
  assert.deepEqual(r.gesten, ['drehen']);
  const z = L.neuerZustand();
  assert.equal(L.schritt(z, { t: 0, haende: [] }).zustand, 'bereit');
  assert.equal(L.schritt(z, { t: NaN, haende: [] }).zustand, 'bereit');
  const w = abspielen(lies('synth-wisch-links'));
  assert.deepEqual(w.zustaende, ['hand'], 'ein Wisch macht nicht «aktiv»');
});

test('Faust: Rollen nur in 3D; im Plan rollt nichts', () => {
  const f = lies('synth-faust-rollen');
  const plan = abspielen(f, { ansicht3d: false });
  assert.equal(plan.summe.rollen, 0);
  assert.ok(!plan.gesten.includes('rollen'));
});

test('Faust: Totbereich und Mindestdrehung sind Stellschrauben (grösser = weniger Weg)', () => {
  const f = lies('synth-faust-drehen-rechts');
  const normal = abspielen(f).summe.drehen[0];
  const gross = abspielen(f, { stellschrauben: { totbereich: 0.05 } }).summe.drehen[0];
  assert.ok(gross < normal - 0.15, `${gross} < ${normal}`);
  const rollen = abspielen(lies('synth-faust-rollen'), { stellschrauben: { rollen_min_grad: 30 } }).summe.rollen;
  assert.ok(rollen < 0.25, rollen);
  const doppelt = abspielen(f, { stellschrauben: { drehen_grad_je_cm: 12 } }).summe.drehen[0];
  assert.ok(Math.abs(doppelt - 2 * normal) < 0.05, 'Übersetzung linear');
});

test('Faust: die schon geschlossen ins Bild kommende Hand navigiert nicht (Tasse); im Bild geschlossen schon', () => {
  const t = abspielen(lies('synth-stoer-tasse'));
  assert.ok(t.gruende.includes('Faust nicht im Bild geschlossen'), t.gruende.join(' | '));
});

test('Wisch: die Handfläche flach nach unten wischt nicht seitlich; ohne die Regel schon', () => {
  const a = folge({
    name: 'flach', dauer: 1300, schluessel: [k(0, 'locker', P0, { nick: 90 * GRAD }), k(600, 'locker', P0, { nick: 90 * GRAD }), k(750, 'locker', plus(P0, [-0.09, 0, 0]), { nick: 90 * GRAD }), k(1300, 'locker', plus(P0, [-0.09, 0, 0]), { nick: 90 * GRAD })],
  });
  assert.equal(abspielen(a).ereignisse.length, 0);
  assert.deepEqual(abspielen(a, { stellschrauben: { handflaeche_grad: 90 } }).ereignisse.map((e) => e.richtung), ['links']);
});

test('Wisch: nach aussen am eigenen Bildrand zählt nicht (linke Hand nach links); nach innen schon', () => {
  const rand = [-0.17, 0.05, 0.5];   // Handflächenmitte rund 0.2 Bildbreiten vom linken Rand (≈ 16° neben der Mitte)
  const mach = (d) => folge({ name: 'rand', dauer: 1300, schluessel: [k(0, 'locker', rand), k(600, 'locker', rand), k(750, 'locker', plus(rand, d)), k(1300, 'locker', plus(rand, d))] });
  assert.equal(abspielen(mach([-0.05, 0, 0]), { stellschrauben: { randwinkel_grad: 12 } }).ereignisse.length, 0, 'nach aussen');
  assert.deepEqual(abspielen(mach([-0.05, 0, 0]), { stellschrauben: { randwinkel_grad: 90 } }).ereignisse.map((e) => e.richtung), ['links']);
  assert.deepEqual(abspielen(mach([0.08, 0, 0]), { stellschrauben: { randwinkel_grad: 12 } }).ereignisse.map((e) => e.richtung), ['rechts'], 'nach innen');
});

test('Wisch: Sperre neben der Maus – ohne Sperre löst dieselbe Folge aus; nach der Sperre wieder', () => {
  const a = lies('synth-stoer-maus-sperre');
  assert.equal(abspielen(a).ereignisse.length, 0);
  assert.equal(abspielen(a, { stellschrauben: { sperre_ms: 0 } }).ereignisse.length, 1);
  assert.equal(abspielen({ ...a, eingaben: [200] }).ereignisse.length, 1, 'Klick 400 ms vor dem Anstoss stört nicht');
});

test('Wisch: zu kurzer Anstoss (Stellschraube anstoss_weg) und zu langsames Tempo lösen nichts aus', () => {
  const a = lies('synth-wisch-links');
  assert.equal(abspielen(a, { stellschrauben: { anstoss_weg: 0.12 } }).ereignisse.length, 0);
  assert.equal(abspielen(a, { stellschrauben: { anstoss_tempo_ms: 2.5 } }).ereignisse.length, 0);
});

test('Falsche Hand: mit Gestenhand rechts lösen dieselben Bewegungen aus', () => {
  const a = lies('synth-stoer-falsche-hand');
  const r = abspielen(a, { hand: 'rechts' });
  assert.deepEqual(r.ereignisse.map((e) => e.richtung), ['rechts']);
  assert.ok(r.summe.drehen[0] < -0.5, 'die rechte Faust dreht nach links');
});

test('Pistole: länger gezeigt als «pistole_max_ms» löst nichts aus, mit grösserer Grenze schon', () => {
  const a = lies('synth-stoer-zeigen-lange');
  assert.ok(abspielen(a).gruende.includes('Pistole: zu lange gezeigt'));
  assert.deepEqual(abspielen(a, { stellschrauben: { pistole_max_ms: 4000 } }).ereignisse.map((e) => e.geste), ['G6']);
  const p = lies('synth-pistole-kurz');
  assert.equal(abspielen({ ...p, eingaben: [700] }).ereignisse.length, 0, 'Maus während der Pistole');
});

test('Einstellen: neue Gestenhand beginnt neu, Stellschrauben gelten sofort', () => {
  const z = L.neuerZustand({ hand: 'links', stellschrauben: { totbereich: 0.02 } });
  z.faust = { x: 1 };
  L.einstellen(z, { stellschrauben: { sperre_ms: 500 } });
  assert.equal(z.werte.totbereich, 0.02);
  assert.equal(z.werte.sperre_ms, 500);
  assert.ok(z.faust);
  L.einstellen(z, { hand: 'rechts' });
  assert.equal(z.hand, 'rechts');
  assert.equal(z.faust, null);
  assert.equal(z.werte.sperre_ms, 500);
});

test('Vorgaben gedämpft (Abnahme 26.10.2026): halbe Übersetzung, stärkere Glättung', () => {
  for (const [k, v] of Object.entries({ drehen_grad_je_cm: 3, verschieben_je_cm: 0.02, rollen_faktor: 0.5, zoom_faktor: 1.25, glaettung_ms: 160, zoom_glaettung_ms: 250 })) assert.equal(W[k], v, k);
  const f = lies('synth-faust-drehen-rechts');
  const voll = abspielen(f).summe.drehen[0], vorgabe = L.aufnahmeAbspielen(f).summe.drehen[0];
  assert.ok(vorgabe > 0 && vorgabe < 0.6 * voll, `${vorgabe} gegen ${voll}`);
});

test('Abbruchprotokoll: das Ende einer scharfen Faust meldet Grund und Masse (nur Zahlen); Wisch ohne Faust nicht', () => {
  const geloest = abspielen(lies('synth-faust-loesen-im-zug'));
  assert.ok(geloest.abbrueche.length >= 1, 'Faust im Zug gelöst');
  const a = geloest.abbrueche[0];
  assert.equal(a.art, 'abbruch');
  assert.ok(['faust gelöst', 'zeigefinger gestreckt', 'hand öffnet sich'].includes(a.grund), a.grund);
  assert.ok(a.dauerMs > 0 && Number.isFinite(a.werte.minFaust));
  for (const v of Object.values(a.werte)) assert.ok(v === null || typeof v === 'number', 'nur Zahlen');
  assert.equal(abspielen(lies('synth-wisch-links')).abbrueche.length, 0, 'Wisch ohne Faust');
  // Faust, dann ist die Hand länger weg: «hand verloren»
  const f = lies('synth-faust-drehen-rechts');
  const ende = f.bilder.at(-1).t * 0.7;
  const weg = { ...f, bilder: [...f.bilder.filter((b) => b.t < ende), { t: ende + 1000, punkte: null, welt: null, haendigkeit: null }] };
  const v = abspielen(weg).abbrueche;
  assert.deepEqual(v.map((x) => x.grund), ['hand verloren']);
  assert.ok(v[0].werte.luecke > L.LUECKE_MS);
});
