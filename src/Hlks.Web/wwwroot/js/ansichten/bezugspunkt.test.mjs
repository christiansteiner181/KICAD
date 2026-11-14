// Frage 32 (Christian 13.11.2026): Bezugspunkt der 3D-Ansicht wie der XR-Export – Regel und Umrechnung Modell ↔ three an den Schnittstellen.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { B, bezugAus, bezugSetzen, begrenzungVon } from './bezugspunkt.js';
import { ebenen, enthaelt, weltPunkt, modellPunkt } from './schnittbereich-logik.js';
import { zustandAusStandort, projektWerte } from './begehen-logik.js';

test('Bezugspunkt: Projekt-Nullpunkt bis ±5 km, sonst Mitte der Achse auf ganze Meter (wie XrKoordinaten)', () => {
  assert.deepEqual(bezugAus([[0, 0, 0, 40000, 20000, 9000]]), [0, 0, 0]);
  // LV95: E 2 600 000 m, N 1 200 000 m, Höhe 500 m – x und y verschoben, z bleibt
  assert.deepEqual(bezugAus([begrenzungVon([[2_600_000_000, 1_200_000_000, 500_000], [2_600_040_400, 1_200_020_600, 509_000]])]), [2_600_020_000, 1_200_010_000, 0]);
});

test('Schnittbereich und Begehen: Projekt-mm bleiben, three relativ zum Bezugspunkt', () => {
  try {
    bezugSetzen([2_600_000_000, 1_200_000_000, 0]);
    const sb = { aktiv: true, min: [2_600_000_000, 1_200_000_000, 0], max: [2_600_010_000, 1_200_005_000, 3000], winkel: 0 };
    const w = weltPunkt(sb, 2_600_005_000, 1_200_002_000, 1500);   // Quadermitte: in three nahe am Ursprung
    assert.ok(Math.abs(w.x - 5) < 1e-9 && Math.abs(w.y - 1.5) < 1e-9 && Math.abs(w.z + 2) < 1e-9);
    assert.ok(enthaelt(sb, w));
    assert.ok(!enthaelt(sb, { x: 11, y: 1, z: -2 }));
    assert.deepEqual(modellPunkt(w), [2_600_005_000, 1_200_002_000, 1500]);
    assert.equal(ebenen(sb).length, 6);
    const z = zustandAusStandort({ standpunkt: { x: 2_600_001_000, y: 1_200_002_000 }, bodenOk: 0, blick: { azimutGrad: 90 } }, 1600);
    assert.ok(Math.abs(z.x - 1) < 1e-9 && Math.abs(z.z + 2) < 1e-9);
    const p = projektWerte(z);
    assert.deepEqual([p.x, p.y, p.bodenOk], [2_600_001_000, 1_200_002_000, 0]);
  } finally { bezugSetzen([0, 0, 0]); }
  assert.deepEqual(B, [0, 0, 0]);
});
