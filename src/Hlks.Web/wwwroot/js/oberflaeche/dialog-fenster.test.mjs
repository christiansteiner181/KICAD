// Tests der Dialogfenster (Auftrag W1) ohne Browser: Begrenzung im sichtbaren Bereich, Verschieben, Grösse am Rand und an der Ecke, Mindestgrösse,
// gemerkte Lage und Grösse je Dialogart (Lesen ungültiger Werte, Begrenzung der Anzahl), Verdrahtung in dialoge.js und im CSS.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/dialog-fenster.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { begrenzen, verschieben, groesseAendern, zentrieren, mindestGroesse, istSichtbar, enthaelt } from './fenstergeometrie.js';
import { dialogArt, bereinigen, setzen, positionVergessen, MAX_EINTRAEGE, SCHLUESSEL } from './dialog-merken.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');
const SICHT = { x: 0, y: 0, b: 1600, h: 900 };
const R = (x, y, b, h) => ({ x, y, b, h });

// ------------------------------------------------------------------------------------------------ Begrenzung im sichtbaren Bereich

test('begrenzen: ein Rechteck im sichtbaren Bereich bleibt unverändert', () => {
  assert.deepEqual(begrenzen(R(100, 80, 560, 400), SICHT), R(100, 80, 560, 400));
});

test('begrenzen: ragt es rechts oder unten hinaus, wird es zurückgeholt (Grösse bleibt)', () => {
  assert.deepEqual(begrenzen(R(1400, 700, 560, 400), SICHT), R(1040, 500, 560, 400));
});

test('begrenzen: ragt es links oder oben hinaus, wird es an den Rand gesetzt', () => {
  assert.deepEqual(begrenzen(R(-300, -50, 560, 400), SICHT), R(0, 0, 560, 400));
});

test('begrenzen: ist es grösser als der sichtbare Bereich, wird es verkleinert und liegt dann ganz drin', () => {
  const r = begrenzen(R(-20, -20, 3000, 2000), SICHT);
  assert.deepEqual(r, R(0, 0, 1600, 900));
  assert.ok(istSichtbar(r, SICHT));
});

test('begrenzen: die Mindestgrösse gilt, solange sie hineinpasst; passt sie nicht, gilt der sichtbare Bereich', () => {
  assert.deepEqual(begrenzen(R(10, 10, 50, 30), SICHT, { b: 300, h: 120 }), R(10, 10, 300, 120));
  assert.deepEqual(begrenzen(R(10, 10, 50, 30), R(0, 0, 200, 100), { b: 300, h: 120 }), R(0, 0, 200, 100));
});

test('begrenzen: verkleinert man das Browserfenster, wird ein verschobener Dialog zurückgeholt', () => {
  const dialog = R(1000, 600, 560, 400);
  const klein = R(0, 0, 1024, 700);
  const r = begrenzen(dialog, klein);
  assert.ok(istSichtbar(r, klein), JSON.stringify(r));
  assert.deepEqual(r, R(464, 300, 560, 400));
});

test('begrenzen: ungültige Zahlen (NaN) ergeben nie ein Rechteck ausserhalb', () => {
  const r = begrenzen({ x: NaN, y: undefined, b: NaN, h: NaN }, SICHT, { b: 100, h: 50 });
  assert.ok(istSichtbar(r, SICHT), JSON.stringify(r));
  assert.ok(Object.values(r).every(Number.isFinite));
});

test('verschieben: bleibt beim Ziehen über den Rand ganz sichtbar', () => {
  assert.deepEqual(verschieben(R(100, 100, 400, 300), 50, -20, SICHT), R(150, 80, 400, 300));
  assert.deepEqual(verschieben(R(100, 100, 400, 300), 5000, 5000, SICHT), R(1200, 600, 400, 300));
  assert.deepEqual(verschieben(R(100, 100, 400, 300), -5000, -5000, SICHT), R(0, 0, 400, 300));
});

test('zentrieren: Mitte des sichtbaren Bereichs, bei zu grossem Rechteck begrenzt', () => {
  assert.deepEqual(zentrieren(560, 400, SICHT), R(520, 250, 560, 400));
  assert.deepEqual(zentrieren(5000, 400, SICHT), R(0, 250, 1600, 400));
});

test('enthaelt und istSichtbar: Randfälle', () => {
  assert.ok(enthaelt(R(10, 10, 100, 50), 10, 10));
  assert.ok(!enthaelt(R(10, 10, 100, 50), 110, 30));
  assert.ok(istSichtbar(R(0, 0, 1600, 900), SICHT));
  assert.ok(!istSichtbar(R(1, 0, 1600, 900), SICHT));
});

// ------------------------------------------------------------------------------------------------ Grösse am Rand und an der Ecke

test('groesseAendern: rechter und unterer Rand wachsen, die Gegenseite bleibt stehen', () => {
  assert.deepEqual(groesseAendern(R(200, 100, 500, 400), 'e', 80, 999, { b: 300, h: 200 }, SICHT), R(200, 100, 580, 400));
  assert.deepEqual(groesseAendern(R(200, 100, 500, 400), 's', 999, 60, { b: 300, h: 200 }, SICHT), R(200, 100, 500, 460));
  assert.deepEqual(groesseAendern(R(200, 100, 500, 400), 'se', 40, 30, { b: 300, h: 200 }, SICHT), R(200, 100, 540, 430));
});

test('groesseAendern: linker und oberer Rand verschieben die Ecke, die Gegenseite bleibt stehen', () => {
  const r = groesseAendern(R(200, 100, 500, 400), 'nw', -50, -40, { b: 300, h: 200 }, SICHT);
  assert.deepEqual(r, R(150, 60, 550, 440));
  assert.equal(r.x + r.b, 700);
  assert.equal(r.y + r.h, 500);
});

test('groesseAendern: unter die Mindestgrösse lässt es sich nicht ziehen, die Gegenseite bleibt stehen', () => {
  const r = groesseAendern(R(200, 100, 500, 400), 'w', 900, 0, { b: 300, h: 200 }, SICHT);
  assert.deepEqual(r, R(400, 100, 300, 400));
  const o = groesseAendern(R(200, 100, 500, 400), 'n', 0, 900, { b: 300, h: 200 }, SICHT);
  assert.deepEqual(o, R(200, 300, 500, 200));
});

test('groesseAendern: über den sichtbaren Bereich hinaus lässt es sich nicht ziehen', () => {
  assert.deepEqual(groesseAendern(R(200, 100, 500, 400), 'e', 9000, 0, { b: 300, h: 200 }, SICHT), R(200, 100, 1400, 400));
  assert.deepEqual(groesseAendern(R(200, 100, 500, 400), 'nw', -9000, -9000, { b: 300, h: 200 }, SICHT), R(0, 0, 700, 500));
});

test('mindestGroesse: Titel mit Knöpfen, Knopfleiste, Fehlerzeile und eine lesbare Zeile Inhalt', () => {
  assert.deepEqual(mindestGroesse({ kopfB: 200, fussB: 320, kopfH: 26, fussH: 34, fehlerH: 0 }), { b: 320, h: 120 });
  assert.deepEqual(mindestGroesse({ kopfB: 400, fussB: 100, kopfH: 26, fussH: 34, fehlerH: 20 }), { b: 400, h: 140 });
  assert.deepEqual(mindestGroesse({}), { b: 260, h: 60 });
});

// ------------------------------------------------------------------------------------------------ Dialogart und gemerkte Werte

test('dialogArt: ausdrücklich angegebene Art geht vor, sonst der Titel ohne Namen in «…» und ohne Ziffern', () => {
  assert.equal(dialogArt({ art: 'Typenliste', titel: 'egal' }), 'typenliste');
  assert.equal(dialogArt({ titel: 'Familie «Heizkörper 22» umbenennen' }), 'familie umbenennen');
  assert.equal(dialogArt({ titel: '3 Elemente löschen?' }), 'elemente löschen');
  assert.equal(dialogArt({ titel: 'Projekt öffnen' }), 'projekt öffnen');
  assert.equal(dialogArt({ titel: 'Projekt öffnen' }), dialogArt({ titel: 'Projekt   Öffnen' }));
});

test('dialogArt: ohne Titel gilt die Klasse, sonst «dialog»; lange Titel werden gekürzt', () => {
  assert.equal(dialogArt({ titel: '', klasse: 'dlg-hoch huelle' }), 'klasse:huelle');
  assert.equal(dialogArt({}), 'dialog');
  assert.ok(dialogArt({ titel: 'x'.repeat(500) }).length <= 60);
});

test('bereinigen: ungültige gemerkte Werte werden verworfen, gültige gerundet', () => {
  assert.deepEqual(bereinigen(null), {});
  assert.deepEqual(bereinigen('kaputt'), {});
  assert.deepEqual(bereinigen([1, 2]), {});
  assert.deepEqual(bereinigen({ a: 5, b: null, c: 'x', d: [], e: { x: 'a', y: 2 }, f: { x: NaN, y: 1, b: 2, h: 3 } }), {});
  assert.deepEqual(bereinigen({ ok: { x: 10.4, y: 20.6, b: 500.2, h: 300.7 } }), { ok: { x: 10, y: 21, b: 500, h: 301 } });
  assert.deepEqual(bereinigen({ nurLage: { x: 5, y: 6 } }), { nurLage: { x: 5, y: 6 } });
  assert.deepEqual(bereinigen({ nurGroesse: { b: 400, h: 300 } }), { nurGroesse: { b: 400, h: 300 } });
  assert.deepEqual(bereinigen({ winzig: { b: 10, h: 10 } }), {}, 'zu kleine Grösse');
  assert.deepEqual(bereinigen({ riesig: { x: 1e9, y: 0 } }), {}, 'unvernünftige Lage');
  assert.deepEqual(bereinigen({ halb: { x: 5, y: 6, b: 400 } }), { halb: { x: 5, y: 6 } }, 'Breite ohne Höhe zählt nicht');
});

test('setzen: neue Werte ergänzen die alten, die zuletzt benutzte Art steht zuletzt', () => {
  let k = setzen({}, 'a', { x: 10, y: 20 });
  k = setzen(k, 'b', { x: 1, y: 2 });
  k = setzen(k, 'a', { b: 500, h: 400 });
  assert.deepEqual(k.a, { x: 10, y: 20, b: 500, h: 400 });
  assert.deepEqual(Object.keys(k), ['b', 'a']);
});

test('setzen: höchstens MAX_EINTRAEGE Arten, die ältesten fallen weg', () => {
  let k = {};
  for (let i = 0; i < MAX_EINTRAEGE + 15; i++) k = setzen(k, 'art ' + String.fromCharCode(97 + (i % 26)) + String.fromCharCode(97 + Math.floor(i / 26)), { x: i, y: i });
  assert.equal(Object.keys(k).length, MAX_EINTRAEGE);
  assert.ok(!('art aa' in k), 'der älteste ist weg');
  assert.ok(Object.keys(k).at(-1).startsWith('art'), 'der neueste bleibt');
});

test('positionVergessen: die Lage fällt weg (Mitte), die Grösse bleibt', () => {
  const k = { a: { x: 10, y: 20, b: 500, h: 400 }, b: { x: 1, y: 2 } };
  assert.deepEqual(positionVergessen(k, 'a'), { a: { b: 500, h: 400 }, b: { x: 1, y: 2 } });
  assert.deepEqual(positionVergessen(k, 'b'), { a: { x: 10, y: 20, b: 500, h: 400 } });
  assert.deepEqual(positionVergessen(k, 'nichtda'), k);
});

test('Speicherschlüssel der Dialoge', () => {
  assert.equal(SCHLUESSEL, 'dialoge');
});

// ------------------------------------------------------------------------------------------------ Verdrahtung

test('dialoge.js: jeder Dialog läuft über zeigen und bekommt das Fensterverhalten; die Titelleiste trägt Einklappen und Schliessen', () => {
  const q = lies('./dialoge.js');
  assert.match(q, /fensterAnbinden\(fenster, \{ art: dialogArt\(/);
  assert.match(q, /kopfKnoepfeHtml\(schliessbar\)/);
  const f = lies('./dialoge-fenster.js');
  assert.match(f, /class="dlg-ein"/);
  assert.match(f, /class="dlg-x"/);
  assert.match(f, /dblclick/);
  assert.match(f, /eingeklappt/);
});

test('Ein Weg: kein anderes Modul baut ein eigenes Dialogfenster (class dlg) ausser zeigen in dialoge.js', () => {
  const wurzel = fileURLToPath(new URL('../', import.meta.url));
  const dateien = readdirSync(wurzel, { recursive: true }).map(String).filter((n) => n.endsWith('.js'));
  assert.ok(dateien.length > 100, 'die Module wurden gefunden');
  const verdaechtig = dateien.filter((n) => !/(^|[\\/])oberflaeche[\\/]dialoge\.js$/.test(n)
    && /className\s*=\s*['"`]dlg[\s'"`]|class="dlg[\s"]/.test(readFileSync(path.join(wurzel, n), 'utf8')));
  assert.deepEqual(verdaechtig, []);
});

test('CSS: Dialoge ohne Abdunkeln, aber modal; Einklappen blendet Inhalt, Fehler und Knopfleiste aus', () => {
  const hlks = lies('../../css/hlks.css');
  assert.ok(!/#dialogschicht\s*\{[^}]*rgba\(0,\s*0,\s*0/.test(hlks), 'die Schicht ist nicht mehr abgedunkelt');
  assert.match(hlks, /#dialogschicht\s*\{[^}]*position:\s*fixed;\s*inset:\s*0/, 'sie deckt weiter alles ab (Klicks dahinter gesperrt)');
  const css = lies('../../css/fensterrahmen.css');
  assert.match(css, /\.dlg\.verschoben\s*\{[^}]*position:\s*absolute/);
  assert.match(css, /\.dlg\.eingeklappt\s*>\s*\.dlg-inhalt/);
  assert.match(css, /\.dlg-griff-se/);
  assert.match(lies('../../index.html'), /css\/fensterrahmen\.css/);
});
