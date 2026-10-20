// Modul 7 Teil B1 (Plandarstellung wie H+K) in der Oberfläche, ohne Browser und ohne Server: die beiden Schalter der Ansicht
// («Koordination», «Aussparungsboxen») setzen nur Ansichtseigenschaften über element_aendern – keine Logik in der Oberfläche –, und der
// Katalog kennt die Werte (Quelle der Eigenschaftenpalette). Gelesen wird der Quelltext bzw. der Katalog.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/plandarstellung-b1.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const quelle = readFileSync(path.join(hier, 'aktionen-ansicht.js'), 'utf8');
const katalogOrdner = process.env.HLKS_KATALOG || path.join(hier, '../../../../../kataloge');
const klassen = JSON.parse(readFileSync(path.join(katalogOrdner, 'klassen.json'), 'utf8'));
const gewerke = JSON.parse(readFileSync(path.join(katalogOrdner, 'gewerke.json'), 'utf8'));
const ansicht = klassen.find((k) => k.name === 'Ansicht');
const eig = (n) => ansicht.eigenschaften.find((e) => e.name === n);

/** Der Quelltext einer Aktion: von ihrer id bis zum nächsten «registrieren(». */
const aktion = (id) => {
  const i = quelle.indexOf(`id: '${id}'`);
  assert.ok(i > 0, `${id} ist registriert`);
  const j = quelle.indexOf('registrieren({', i);
  return quelle.slice(i, j > 0 ? j : undefined);
};

test('Katalog: darstellung kennt «koordination», aussparungsboxen ist ein Schalter', () => {
  assert.ok(eig('darstellung').auswahl.includes('koordination'));
  assert.equal(eig('aussparungsboxen').typ, 'bool');
});

test('Gewerke: die Koordinationsfarben stehen im Katalog (nicht in der Oberfläche)', () => {
  const f = (id) => gewerke.gewerke.find((g) => g.id === id).koordination;
  assert.equal(f('heizung'), '#FF0000');
  assert.equal(f('lueftung'), '#0000FF');
  assert.equal(f('sanitaer'), '#00B000');
  assert.equal(f('architektur'), '#808080');
  assert.ok(!/#FF0000|#0000FF|#00B000/i.test(quelle), 'keine Gewerkefarben im Quelltext der Aktionen');
});

test('Schalter «Koordination»: Register Ansicht, Gruppe Darstellung, setzt darstellung über element_aendern', () => {
  const a = aktion('ansicht.koordinationsplan');
  assert.match(a, /register: 'ansicht', gruppe: 'Darstellung'/);
  assert.match(a, /gedrueckt: \(\) => ansichtVon\(\)\?\.werte\?\.darstellung === 'koordination'/);
  assert.match(a, /sende\('element_aendern', \{ element: a\.kennzeichen, eigenschaften: \{ darstellung: an \? 'koordination' : null \} \}\)/);
  assert.match(a, /aktiv: grundrissMoeglich/);
});

test('Schalter «Aussparungsboxen»: setzt aussparungsboxen (an = true, aus = null) über element_aendern', () => {
  const a = aktion('ansicht.aussparungsboxen');
  assert.match(a, /register: 'ansicht', gruppe: 'Darstellung'/);
  assert.match(a, /gedrueckt: \(\) => ansichtVon\(\)\?\.werte\?\.aussparungsboxen === true/);
  assert.match(a, /eigenschaften: \{ aussparungsboxen: an \? true : null \}/);
});

test('Grundrissansicht ohne Vorlage: grundrissMoeglich', () => {
  assert.match(quelle, /const grundrissMoeglich = \(p\) => \{ const a = ansichtVon\(p\); return !!a && a\.werte\?\.art === 'grundriss' && a\.werte\?\.vorlage !== true; \};/);
});
