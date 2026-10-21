// Tests von Modul XR, Teil XR-C: das Tablet beruhigen (Rückmeldung nach dem ersten Brillentest, 21.10.2026: «tablet ist etwas nervös, sollte etwas stabilisiert werden»).
// Reine Logik aus xr-greifen-logik.js: Glättung der Handflächen-Normale, One-Euro-Filter, Totzone, Nachführen.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/xr-tablet-ruhe.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';

const G = await import('./xr-greifen-logik.js');
const M = await import('./xr-mess-logik.js');
const v = (x, y, z) => ({ x, y, z });

/** Deterministischer Zufall (mulberry32). */
function zufall(seed) { let a = seed >>> 0; return () => { a = (a + 0x6D2B79F5) >>> 0; let t = a; t = Math.imul(t ^ (t >>> 15), t | 1); t ^= t + Math.imul(t ^ (t >>> 7), t | 61); return ((t ^ (t >>> 14)) >>> 0) / 4294967296; }; }
/** Lässt die Kette Palm-Glättung → tabletLage → tabletNachfuehren mit 72 Hz laufen. eingabe(t) → { ort, normale }; liefert die gezeigten Lagen je Bild. */
function ruheLauf(sekunden, eingabe, hz = 72) {
  const kopf = v(0, 1.6, 0.8), dt = 1 / hz, z = G.neuerTabletZustand(), aus = [];
  let gezeigt = null;
  for (let i = 0; i <= Math.round(sekunden * hz); i++) {
    const e = eingabe(i * dt), n = G.tabletPalmGlaetten(z, e.normale, dt);
    gezeigt = G.tabletNachfuehren(gezeigt, G.tabletLage(e.ort, n, kopf), dt, z);
    aus.push(gezeigt);
  }
  return aus;
}
/** Normale (0, 1, 0), um ax/az (Grad) gekippt. */
const kipp = (ax, az) => M.einheit(v(Math.tan(az * Math.PI / 180), 1, Math.tan(ax * Math.PI / 180)));

test('Zittern (±5 mm, ±2°, 2 s bei 72 Hz) bewegt das Tablet höchstens 2 mm bzw. 1°', () => {
  for (const seed of [1, 2, 3, 4, 5]) {
    const r = zufall(seed), s = (a) => (r() * 2 - 1) * a;
    const lagen = ruheLauf(2, () => ({ ort: v(0.1 + s(0.005), 1.1 + s(0.005), -0.3 + s(0.005)), normale: kipp(s(2), s(2)) }));
    const l0 = lagen[0];
    let dm = 0, dn = 0;
    for (const l of lagen) { dm = Math.max(dm, M.abstand3(l.mitte, l0.mitte)); dn = Math.max(dn, G.winkelGrad(l.normale, l0.normale)); }
    assert.ok(dm <= 0.002, `Seed ${seed}: Mitte bewegt sich um ${(dm * 1000).toFixed(2)} mm`);
    assert.ok(dn <= 1, `Seed ${seed}: Normale bewegt sich um ${dn.toFixed(2)}°`);
  }
});

test('ein Sprung um 10 cm ist nach 0,3 s zu mindestens 90 % eingeholt und kommt zur Ruhe', () => {
  const lagen = ruheLauf(2, (t) => ({ ort: v(t < 0.5 ? 0 : 0.1, 1.1, -0.3), normale: v(0, 1, 0) }));
  const vorher = lagen[Math.round(0.4 * 72)].mitte, ende = lagen[lagen.length - 1].mitte;
  const nach = lagen[Math.round(0.5 * 72) + Math.round(0.3 * 72)].mitte;
  const weg = M.abstand3(vorher, ende);
  assert.ok(weg > 0.09 && weg < 0.11, `Sprungweite ${weg}`);
  assert.ok(M.abstand3(nach, ende) <= 0.1 * weg, `nach 0,3 s fehlen noch ${(M.abstand3(nach, ende) * 1000).toFixed(1)} mm`);
  for (const l of lagen.slice(-20)) assert.ok(M.abstand3(l.mitte, ende) < 1e-9, 'steht am Ende still');
});

test('eine Drehung der Hand um 20° wird gefolgt', () => {
  const lagen = ruheLauf(2, (t) => ({ ort: v(0, 1.1, -0.3), normale: kipp(t < 0.5 ? 0 : 20, 0) }));
  const ziel = G.tabletLage(v(0, 1.1, -0.3), kipp(20, 0), v(0, 1.6, 0.8));
  const ende = lagen[lagen.length - 1];
  assert.ok(M.abstand3(ende.mitte, ziel.mitte) < 0.015, `Mitte weicht ${(M.abstand3(ende.mitte, ziel.mitte) * 1000).toFixed(1)} mm ab`);
  assert.ok(M.abstand3(ende.mitte, lagen[0].mitte) > 0.03, 'Tablet hat sich mit der Hand bewegt');
});

test('Öffnen übernimmt die erste Lage sofort; langsames Wandern wird nachgeführt', () => {
  const kopf = v(0, 1.6, 0.8), z = G.neuerTabletZustand();
  const soll = G.tabletLage(v(0.3, 1.2, -0.2), v(0, 1, 0), kopf);
  const erste = G.tabletNachfuehren(null, soll, 1 / 72, z);
  assert.deepEqual(erste, soll, 'erste Lage unverändert');
  const lagen = ruheLauf(4, (t) => ({ ort: v(0.05 * t, 1.1, -0.3), normale: v(0, 1, 0) }));
  const ende = lagen[lagen.length - 1];
  assert.ok(Math.abs(ende.mitte.x - lagen[0].mitte.x - 0.2) < G.TABLET_TOTZONE_M + 0.01, `Rückstand: Weg ${ende.mitte.x - lagen[0].mitte.x}`);
});

test('Controller-Fall (feste Normale, wackelnde Hand) bleibt in der Totzone ruhig', () => {
  const r = zufall(9), s = (a) => (r() * 2 - 1) * a;
  const lagen = ruheLauf(2, () => ({ ort: v(s(0.004), 1.0 + s(0.004), -0.2 + s(0.004)), normale: v(0, 1, 0) }));
  let dm = 0; for (const l of lagen) dm = Math.max(dm, M.abstand3(l.mitte, lagen[0].mitte));
  assert.ok(dm <= 0.002, `${dm}`);
});

test('Totzone und Filterwerte sind benannte Konstanten', () => {
  assert.ok(G.TABLET_TOTZONE_M > 0 && G.TABLET_TOTZONE_GRAD > 0 && G.TABLET_RUHE_ANTEIL < 1);
  assert.ok(G.TABLET_FILTER_MITTE.minCut > 0 && G.TABLET_FILTER_PALM.minCut > 0);
});
