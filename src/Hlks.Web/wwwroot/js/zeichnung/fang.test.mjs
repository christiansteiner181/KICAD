// Tests zu js/zeichnung/logik.js: Tangenten- und Polarfang (Rechenwerte).
import test from 'node:test';
import assert from 'node:assert/strict';
import { tangente, polar, fangSuchen } from './logik.js';

test('Tangente: Berührpunkt vom Punkt (0, 0) an den Kreis um (10, 0) mit Radius 6 – Abstand zum Punkt 8, rechter Winkel zum Radius', () => {
  // Berührpunkte: (6.4, ±4.8); Zeiger nahe dem oberen
  const t = tangente([[10, 0, 6]], 6.5, 4.7, 1, [0, 0]);
  assert.ok(Math.abs(t.x - 6.4) < 1e-9 && Math.abs(t.y - 4.8) < 1e-9);
  assert.equal(t.art, 5);
  assert.equal(tangente([[10, 0, 6]], 6.5, 4.7, 1, [9, 0]), null);    // von innen keine Tangente
  assert.equal(tangente([[10, 0, 6]], 16, 0, 1, [0, 0]), null);       // Zeiger am Kreis, aber weit weg vom Berührpunkt
  assert.equal(fangSuchen(null, null, 6.5, 4.7, 1, [0, 0], [[10, 0, 6]]).art, 5);
});

test('Polarfang: rastet innerhalb von 3° auf Vielfache des Schritts und legt den Punkt auf den Strahl', () => {
  const t = polar([100, 3], [0, 0], 45);            // 1.7° über der Waagrechten
  assert.equal(t.winkel, 0);
  assert.ok(Math.abs(t.p[0] - 100) < 1e-9 && Math.abs(t.p[1]) < 1e-9);
  const d = polar([71, 70], [0, 0], 45);            // fast 45°
  assert.equal(d.winkel, 45);
  assert.ok(Math.abs(d.p[0] - d.p[1]) < 1e-9);
  assert.equal(polar([100, 20], [0, 0], 45), null); // 11.3°: kein Raster
  assert.equal(polar([0, -50], [0, 0], 90).winkel, 270);
});
