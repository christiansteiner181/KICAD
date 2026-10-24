// Quest App V1a (Christian 24.10.2026: «nutze die gleiche symbolsprache wie im cad»): die Texturen der App-Symbole (src/Hlks.Quest/Assets/symbole, gebaut mit
// werkzeuge/quest_symbole.mjs) passen zur Quelle in symbole.js, werkzeuge/symbole.js und symbole-revit.js. Ändert sich ein Symbol in KICAD, schlägt dieser Test fehl,
// bis die Texturen neu gebaut sind. Ohne Browser: verglichen werden die Prüfsummen der Quelle und die Kennzahlen der PNG-Dateien.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest-symbole.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import path from 'node:path';
import { quelle, NAMEN, SAETZE, ZIEL, PX } from '../../../../../werkzeuge/quest_symbole.mjs';
import * as strich from './symbole.js';
import * as rv from './symbole-revit.js';
import * as wz from '../werkzeuge/symbole.js';

const manifest = JSON.parse(readFileSync(path.join(ZIEL, 'symbole.json'), 'utf8'));

test('Jede Textur der App passt zur Quelle in KICAD (Prüfsumme je Symbol und Satz); sonst: node werkzeuge/quest_symbole.mjs', async () => {
  const q = await quelle();
  const veraltet = [];
  for (const n of NAMEN) for (const s of SAETZE) if ((manifest.symbole[n]?.[s] ?? null) !== (q[n][s]?.pruefsumme ?? null)) veraltet.push(`${n} (${s})`);
  assert.deepEqual(veraltet, [], 'Texturen veraltet – node werkzeuge/quest_symbole.mjs ausführen');
  assert.deepEqual(Object.keys(manifest.symbole).sort(), [...NAMEN].sort());
  assert.deepEqual(manifest.saetze, SAETZE);
  assert.equal(manifest.groesse, PX);
});

test('Jede Textur ist ein PNG mit Deckkraft (RGBA) in der Grösse PX × PX', () => {
  for (const s of SAETZE) for (const n of NAMEN) {
    const f = path.join(ZIEL, s, `${n}.png`);
    assert.ok(existsSync(f), `${s}/${n}.png fehlt`);
    const b = readFileSync(f);
    assert.equal(b.subarray(0, 8).toString('hex'), '89504e470d0a1a0a', `${s}/${n}.png ist kein PNG`);
    assert.equal(b.subarray(12, 16).toString('latin1'), 'IHDR');
    assert.equal(b.readUInt32BE(16), PX, `${s}/${n}.png Breite`);
    assert.equal(b.readUInt32BE(20), PX, `${s}/${n}.png Höhe`);
    assert.equal(b[25], 6, `${s}/${n}.png ohne Deckkraft (Farbtyp ${b[25]}, erwartet 6 = RGBA)`);
  }
});

test('Die App nimmt bestehende Schlüssel von KICAD; die neuen (sitzungstisch, gesten, haken) stehen in symbole.js und haben eine Revit-Fassung', () => {
  for (const n of NAMEN) assert.ok(strich.SYMBOLNAMEN_STRICH.includes(n) || wz.SYMBOLNAMEN.includes(n), `${n} fehlt in symbole.js bzw. werkzeuge/symbole.js`);
  for (const n of ['sitzungstisch', 'gesten', 'haken']) assert.ok(strich.SYMBOLNAMEN_STRICH.includes(n), `${n} fehlt in symbole.js`);
  for (const n of ['info', 'geschoss', 'bemassung', 'sichtbarkeit', 'schliessen', 'loeschen', 'problemMarker', 'raumBegehen']) assert.ok(NAMEN.includes(n));
  for (const n of ['sitzungstisch', 'gesten', 'haken', 'problemMarker', 'raumBegehen']) assert.ok(rv.hatRevitFassung(n), `${n} ohne Revit-Fassung`);
});
