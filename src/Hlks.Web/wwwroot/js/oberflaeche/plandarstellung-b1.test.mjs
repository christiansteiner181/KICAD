// Modul 7 Teil B1 (Plandarstellung wie H+K) in der Oberfläche, ohne Browser und ohne Server: die beiden Einstellungen der Ansicht
// (darstellung = koordination, aussparungsboxen) stehen im Katalog und erscheinen damit in der Eigenschaftenpalette der Ansicht – die
// Oberfläche rechnet und färbt nichts selbst (keine Gewerkefarben im Quelltext der Oberfläche). Gelesen wird der Katalog.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/plandarstellung-b1.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const katalogOrdner = process.env.HLKS_KATALOG || path.join(hier, '../../../../../kataloge');
const klassen = JSON.parse(readFileSync(path.join(katalogOrdner, 'klassen.json'), 'utf8'));
const gewerke = JSON.parse(readFileSync(path.join(katalogOrdner, 'gewerke.json'), 'utf8'));
const ansicht = klassen.find((k) => k.name === 'Ansicht');
const eig = (n) => ansicht.eigenschaften.find((e) => e.name === n);

test('Katalog: darstellung kennt «koordination», aussparungsboxen ist ein Schalter (Eigenschaftenpalette der Ansicht)', () => {
  assert.ok(eig('darstellung').auswahl.includes('koordination'));
  assert.ok(eig('darstellung').auswahl.includes('aussparungen'), 'bisherige Werte bleiben');
  assert.equal(eig('aussparungsboxen').typ, 'bool');
  assert.match(eig('aussparungsboxen').beschreibung, /Gewerk/);
});

test('Gewerke: die Koordinationsfarben stehen im Katalog, nicht in der Oberfläche', () => {
  const f = (id) => gewerke.gewerke.find((g) => g.id === id).koordination;
  assert.equal(f('heizung'), '#FF0000');
  assert.equal(f('lueftung'), '#0000FF');
  assert.equal(f('sanitaer'), '#00B000');
  assert.equal(f('architektur'), '#808080');
  const text = readFileSync(path.join(hier, 'aktionen-ansicht.js'), 'utf8');
  assert.ok(!/#FF0000|#0000FF|#00B000/i.test(text), 'keine Gewerkefarben im Quelltext der Aktionen');
  assert.ok(readdirSync(hier).includes('aktionen-ansicht.js'));
});
