// Tests zu js/gewerk-alias.js: «Kälte ist Teil der Heizung» (Entscheid Christian 20.10.2026) – alter Gewerk-Wert «kaelte» wird als «heizung» gelesen.
import test from 'node:test';
import assert from 'node:assert/strict';
import { GEWERK_ALIAS, gewerkAlias, gewerkeAlias } from './gewerk-alias.js';
import { seitenText } from './oberflaeche/koordination-text.js';
import { gewerkName } from './familien/bibliothek-text.js';
import { GEWERKE } from './oberflaeche/werkzeug.js';

test('gewerkAlias: kaelte (auch Kaelte, Kälte) heisst heizung, alles andere bleibt', () => {
  assert.equal(gewerkAlias('kaelte'), 'heizung');
  assert.equal(gewerkAlias('Kaelte'), 'heizung');
  assert.equal(gewerkAlias(' kälte '), 'heizung');
  assert.equal(gewerkAlias('heizung'), 'heizung');
  assert.equal(gewerkAlias('sanitaer'), 'sanitaer');
  assert.equal(gewerkAlias(undefined), undefined);
  assert.equal(gewerkAlias(null), null);
  assert.deepEqual(Object.values(GEWERK_ALIAS), ['heizung', 'heizung']);
});

test('gewerkeAlias: Liste mit Alias gelesen, ohne Doppelte, Reihenfolge bleibt', () => {
  assert.deepEqual(gewerkeAlias(['kaelte', 'architektur']), ['heizung', 'architektur']);
  assert.deepEqual(gewerkeAlias(['heizung', 'kaelte', 'sanitaer']), ['heizung', 'sanitaer']);
  assert.deepEqual(gewerkeAlias([]), []);
  assert.deepEqual(gewerkeAlias(null), []);
});

test('Texte: kein Gewerk Kälte in den Listen der Oberfläche; ein alter Wert heisst «Heizung»', () => {
  assert.equal(GEWERKE.some(([id]) => id === 'kaelte'), false);
  assert.equal(seitenText('kaelte'), 'Heizung');
  assert.equal(seitenText('heizung'), 'Heizung');
  assert.equal(seitenText('hlks'), 'alle HLKS-Gewerke');
  assert.equal(gewerkName('kaelte'), 'Heizung');
  assert.equal(gewerkName('lueftung'), 'Lüftung');
});
