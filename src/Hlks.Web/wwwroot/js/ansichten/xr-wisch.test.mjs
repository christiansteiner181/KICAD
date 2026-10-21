// Tests der Wischgeste (Modul XR, Teil XR-C): Masslinien wegwischen. Nur die Rechenregeln in xr-greifen-logik.js, ohne Browser und ohne Brille.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/xr-wisch.test.mjs
import { test } from 'node:test';
import assert from 'node:assert/strict';

const G = await import('./xr-greifen-logik.js');
const v = (x, y, z) => ({ x, y, z });
const BILD = 1 / 72;

/** Verlauf einer Hand, die in `dauer` s geradlinig von a nach b geht (Bilder zu 1/72 s ab t0). */
function strich(a, b, dauer, t0 = 0) {
  const n = Math.max(2, Math.round(dauer / BILD));
  return Array.from({ length: n + 1 }, (_, i) => ({ t: t0 + (i / n) * dauer, p: v(a.x + (b.x - a.x) * i / n, a.y + (b.y - a.y) * i / n, a.z + (b.z - a.z) * i / n) }));
}
/** Spielt einen Verlauf durch wischSchritt; Rückgabe alle Ergebnisse (null oder Wisch). */
function abspielen(z, verlauf, erlaubt = () => true) { return verlauf.map((s) => G.wischSchritt(z, s.t, s.p, erlaubt(s))); }

test('Wisch erkannt: 30 cm waagrecht in 0.25 s (1.2 m/s)', () => {
  const w = G.wischErkennen(strich(v(0, 1.2, -0.5), v(0.3, 1.2, -0.5), 0.25));
  assert.ok(w);
  assert.ok(w.strecke >= G.WISCH_WEG_M && w.v >= G.WISCH_V_MS && w.dauer <= G.WISCH_ZEIT_S);
  assert.ok(w.richtung.x > 0.99, 'Richtung +x');
  assert.ok(w.weg.length > 5);
});

test('Wisch mit schwach geneigter Bahn (20° gegen die Waagrechte) zählt, 60° nicht', () => {
  const a = v(0, 1.2, -0.5);
  assert.ok(G.wischErkennen(strich(a, v(0.3, 1.2 + 0.3 * Math.tan(20 * Math.PI / 180), -0.5), 0.25)));
  assert.equal(G.wischErkennen(strich(a, v(0.3, 1.2 + 0.3 * Math.tan(60 * Math.PI / 180), -0.5), 0.25)), null);
});

test('nicht erkannt: langsam, kurz, senkrecht, Ausreisser in einem Bild', () => {
  const a = v(0, 1.2, -0.5);
  assert.equal(G.wischErkennen(strich(a, v(0.3, 1.2, -0.5), 0.8)), null, 'langsam (0.4 m/s)');
  assert.equal(G.wischErkennen(strich(a, v(0.3, 1.2, -0.5), 0.45)), null, 'zu lange (0.67 m/s)');
  assert.equal(G.wischErkennen(strich(a, v(0.1, 1.2, -0.5), 0.1)), null, 'zu kurz (10 cm)');
  assert.equal(G.wischErkennen(strich(a, v(0, 1.5, -0.5), 0.2)), null, 'senkrecht');
  assert.equal(G.wischErkennen([{ t: 0, p: a }, { t: BILD, p: v(0.4, 1.2, -0.5) }]), null, 'Sprung in einem Bild');
  assert.equal(G.wischErkennen([]), null);
  assert.equal(G.wischErkennen([{ t: 0, p: a }]), null);
});

test('wischSchritt: Wisch erkannt, nicht erkannt bei Faust oder Pinch (erlaubt = false)', () => {
  const verlauf = strich(v(0, 1.2, -0.5), v(0.4, 1.2, -0.5), 0.3);
  const z = G.neuerWischer();
  const erg = abspielen(z, verlauf);
  assert.ok(erg.some((e) => e?.neu), 'Wisch erkannt');
  assert.equal(erg.filter((e) => e?.neu).length, 1, 'genau ein erstes Bild');
  // Faust / Pinch: die Hand ist nicht «erlaubt»
  const z2 = G.neuerWischer();
  assert.ok(abspielen(z2, verlauf, () => false).every((e) => e === null), 'Faust/Pinch/Tablet: nichts');
});

test('Sperre: nach einem Wisch 0.5 s lang kein zweiter', () => {
  const z = G.neuerWischer();
  const v1 = strich(v(0, 1.2, -0.5), v(0.4, 1.2, -0.5), 0.3, 0);
  abspielen(z, v1);
  // Hand steht (Wisch endet) und wischt sofort zurück: gesperrt
  const halt = Array.from({ length: 36 }, (_, i) => ({ t: 0.3 + (i + 1) * BILD, p: v(0.4, 1.2, -0.5) }));   // der Strich verlässt das Fenster nach 0.4 s
  abspielen(z, halt);
  assert.ok(z.sperreBis > 0.3, `Sperre bis ${z.sperreBis}`);
  const t1 = halt[halt.length - 1].t, zurueck = strich(v(0.4, 1.2, -0.5), v(0, 1.2, -0.5), 0.1, t1 + BILD);   // 4 m/s, ganz innerhalb der Sperre
  assert.ok(t1 + BILD + 0.1 < z.sperreBis, `Sperre bis ${z.sperreBis}`);
  assert.ok(abspielen(z, zurueck).every((e) => e === null), 'innerhalb der Sperre nichts');
  // nach der Sperre geht es wieder
  const spaeter = strich(v(0, 1.2, -0.5), v(0.4, 1.2, -0.5), 0.3, z.sperreBis + 0.1);
  assert.ok(abspielen(z, spaeter).some((e) => e?.neu), 'nach der Sperre erkannt');
});

test('Anlauf aus der Ruhe: der Verlauf wird gesammelt, bis die Schwellen erfüllt sind', () => {
  const z = G.neuerWischer();
  const ruhe = Array.from({ length: 10 }, (_, i) => ({ t: i * BILD, p: v(0, 1.2, -0.5) }));
  abspielen(z, ruhe);
  assert.ok(abspielen(z, strich(v(0, 1.2, -0.5), v(0.4, 1.2, -0.5), 0.3, 10 * BILD)).some((e) => e?.neu));
});

test('Strecken-Abstand: gekreuzt, parallel, windschief, Punkt', () => {
  const nah = (a, b) => assert.ok(Math.abs(a - b) < 1e-9, `${a} ≠ ${b}`);
  nah(G.streckenAbstand(v(-1, 0, 0), v(1, 0, 0), v(0, 0.3, -1), v(0, 0.3, 1)), 0.3);       // windschief, 30 cm darüber
  nah(G.streckenAbstand(v(0, 0, 0), v(1, 0, 0), v(0, 0.2, 0), v(1, 0.2, 0)), 0.2);         // parallel
  nah(G.streckenAbstand(v(0, 0, 0), v(1, 0, 0), v(2, 0, 0), v(3, 0, 0)), 1);               // in einer Linie, getrennt
  nah(G.streckenAbstand(v(0, 0, 0), v(0, 0, 0), v(1, 0, 0), v(1, 1, 0)), 1);               // Punkt gegen Strecke
  nah(G.streckenAbstand(v(-1, 0, 0), v(1, 0, 0), v(0, 0, -1), v(0, 0, 1)), 0);             // schneiden sich
});

test('Treffer: Linie im Abstand unter 10 cm → getroffen, darüber nicht; mehrere zugleich; ins Leere nichts', () => {
  // Handweg: 40 cm in x-Richtung auf Höhe 1.2 m, z = -0.5
  const w = G.wischErkennen(strich(v(0, 1.2, -0.5), v(0.4, 1.2, -0.5), 0.3));
  assert.ok(w);
  const quer = (x, dz, y = 1.2) => ({ a: v(x, y - 0.03, -0.5 + dz), b: v(x, y + 0.03, -0.5 + dz) });   // kurze senkrechte Masslinie bei x, dz vor/hinter dem Weg, auf Höhe y
  const linien = [
    quer(0.2, 0.0),         // 0: mitten im Weg → getroffen
    quer(0.2, 0.07),        // 1: 7 cm daneben (in z) → getroffen
    quer(0.2, 0.0, 1.4),    // 2: 17 cm darüber → nicht
    quer(0.2, 0.0, 1.0),    // 3: 17 cm darunter → nicht
    quer(0.8, 0.0),         // 4: jenseits des Wegs (40 cm hinter dem Ende) → nicht
  ];
  assert.deepEqual(G.wischTreffer(w.weg, linien), [0, 1]);
  assert.deepEqual(G.wischTreffer(w.weg, [linien[2], linien[3], linien[4]]), [], 'Wisch ins Leere: nichts');
  assert.deepEqual(G.wischTreffer([], linien), []);
  // Grenzwert: knapp unter bzw. über WISCH_TREFFER_M
  assert.deepEqual(G.wischTreffer(w.weg, [quer(0.2, G.WISCH_TREFFER_M - 0.01)]), [0]);
  assert.deepEqual(G.wischTreffer(w.weg, [quer(0.2, G.WISCH_TREFFER_M + 0.01)]), []);
});

test('Durchlauf: Wisch von weit vor der Linie bis hinter sie löscht die Linie (Treffer während des Strichs)', () => {
  const z = G.neuerWischer();
  const linie = { a: v(0.5, 1.1, -0.5), b: v(0.5, 1.3, -0.5) };
  let geloescht = false;
  for (const s of strich(v(0, 1.2, -0.5), v(0.8, 1.2, -0.5), 0.5)) {
    const w = G.wischSchritt(z, s.t, s.p, true);
    if (w && G.wischTreffer(w.weg, [linie]).length) geloescht = true;
  }
  assert.ok(geloescht, 'die Linie liegt am Weg und wird getroffen');
  // Ein Wisch weit weg (1 m) trifft nicht
  const z2 = G.neuerWischer();
  const fern = { a: v(0.5, 1.1, -1.6), b: v(0.5, 1.3, -1.6) };
  let treffer = false;
  for (const s of strich(v(0, 1.2, -0.5), v(0.8, 1.2, -0.5), 0.5)) { const w = G.wischSchritt(z2, s.t, s.p, true); if (w && G.wischTreffer(w.weg, [fern]).length) treffer = true; }
  assert.equal(treffer, false);
});

test('Sprung des Trackings (Hand springt 1 m in einem Bild) ist kein Wisch', () => {
  const z = G.neuerWischer();
  const ruhe = Array.from({ length: 12 }, (_, i) => ({ t: i * BILD, p: v(0, 1.2, -0.5) }));
  const danach = Array.from({ length: 12 }, (_, i) => ({ t: (12 + i) * BILD, p: v(1.0, 1.2, -0.5) }));
  assert.ok([...abspielen(z, ruhe), ...abspielen(z, danach)].every((e) => e === null));
});
