// Tests zu js/zeichnung/logik.js: Messwerte (Rechenwerte) und Objektfang.
import test from 'node:test';
import assert from 'node:assert/strict';
import { fangpunkte, Fangraster, messwerte, laengeText, flaecheText } from './logik.js';

test('messwerte: Strecke, Winkel, Fläche und Umfang eines Dreiecks 3-4-5', () => {
  const w = messwerte([[0, 0], [3, 0], [3, 4]]);
  assert.equal(w.strecke, 4);
  assert.equal(w.laenge, 7);
  assert.equal(w.umfang, 12);
  assert.equal(w.flaeche, 6);
  assert.ok(Math.abs(w.winkel - 90) < 1e-9);
  assert.ok(Math.abs(w.richtung - 90) < 1e-9);
  assert.equal(messwerte([[0, 0]]), null);
});

test('Einheiten: Meter-Zeichnung in m und m², unbekannte Einheit ohne Umrechnung', () => {
  assert.equal(laengeText(3.865, 1000, true), '3.865 m');
  assert.equal(laengeText(0.24, 1000, true), '240.0 mm');
  assert.equal(flaecheText(8.98, 1000, true), '8.980 m²');
  assert.equal(flaecheText(2_000_000, 1, true), '2.000 m²');
  assert.equal(laengeText(12.5, 1, false), '12.500 E');
});

test('Fang: Endpunkt gewinnt vor Mittelpunkt, Zentrum eines Kreises, ausserhalb des Radius nichts, Ansichtsfenster begrenzt', () => {
  const prim = [['l', 0, [0, 0, 10, 0]], ['k', 0, 50, 50, 5]];
  const raster = new Fangraster(fangpunkte(prim), 1);
  assert.deepEqual(raster.suche(0.3, 0.2, 1), { x: 0, y: 0, art: 0 });
  assert.deepEqual(raster.suche(5.2, 0.1, 1), { x: 5, y: 0, art: 1 });
  assert.deepEqual(raster.suche(50.4, 49.8, 1), { x: 50, y: 50, art: 2 });
  assert.equal(raster.suche(20, 20, 1), null);
  assert.equal(fangpunkte(prim, [-1, -1, 6, 6]).length / 3, 2);   // nur Anfang und Mitte der Linie liegen im Fenster
});
