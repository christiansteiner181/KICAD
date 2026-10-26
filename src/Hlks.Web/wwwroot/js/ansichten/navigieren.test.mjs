// Tests von navigieren() (Modul G, Gesten): die Rechnung in navigieren-logik.js und – mit den echten three.js und OrbitControls, ohne Browser – die Zerlegung in die
// aufrechte Lage der Steuerung plus Rolle um die Blickachse, wie ansicht3d.js sie anwendet.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/navigieren.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const N = await import('./navigieren-logik.js');
const THREE = await import(threeUrl);
const { OrbitControls } = await import(pathToFileURL(path.resolve(hier, '../../lib/three/addons/controls/OrbitControls.js')).href);

const GRAD = Math.PI / 180;
const nahe = (a, b, eps = 1e-9) => Math.abs(a - b) <= eps;
const naheV = (a, b, eps = 1e-9) => a.length === b.length && a.every((x, i) => nahe(x, b[i], eps));
/** gleiche Drehung (q und −q sind dieselbe). */
const gleicheDrehung = (a, b, eps = 1e-6) => naheV(a, b, eps) || naheV(a, b.map((x) => -x), eps);
/** Punkt P im Bild der Kamera: x rechts, y oben, z zum Betrachter (Kamerakoordinaten). */
const imBild = (lage, P) => N.qDrehe(N.qKonj(lage.q), N.minus(P, lage.position));
/** Kamera bei position mit Blick auf ziel, aufrecht. */
const lage = (position, ziel) => ({ position, ziel, q: N.aufrechtQ(position, ziel) });

test('Quaternionen: Achsdrehung, Produkt, Drehen eines Vektors wie three.js', () => {
  assert.ok(naheV(N.qDrehe(N.qAchse([0, 1, 0], 90 * GRAD), [1, 0, 0]), [0, 0, -1], 1e-12));
  const a = N.qAchse([1, 2, 3], 0.7), b = N.qAchse([-2, 0.5, 1], -1.3), v = [0.3, -1.2, 2.5];
  const ta = new THREE.Quaternion().fromArray(a), tb = new THREE.Quaternion().fromArray(b);
  assert.ok(naheV(N.qMal(a, b), new THREE.Quaternion().multiplyQuaternions(ta, tb).toArray(), 1e-12));
  assert.ok(naheV(N.qDrehe(a, v), new THREE.Vector3(...v).applyQuaternion(ta).toArray(), 1e-12));
  assert.ok(naheV(N.qDrehe(N.qMal(a, b), v), N.qDrehe(a, N.qDrehe(b, v)), 1e-12), 'erst b, dann a');
});

test('aufrechtQ entspricht camera.lookAt von three.js, auch senkrecht von oben', () => {
  for (const [p, z] of [[[18, 14, 24], [0, 0, 0]], [[0, 0, 10], [0, 0, 0]], [[-3, 5, 2], [1, -1, 4]], [[2, 30, 2], [2, 0, 2]], [[0, -8, 0], [0, 0, 0]]]) {
    const k = new THREE.PerspectiveCamera();
    k.position.set(...p);
    k.lookAt(...z);
    assert.ok(gleicheDrehung(N.aufrechtQ(p, z), k.quaternion.toArray(), 1e-9), JSON.stringify(p));
  }
});

test('Rolle: rolleZwischen kehrt mitRolle um und entspricht kamera.rotateZ', () => {
  const basis = N.aufrechtQ([18, 14, 24], [0, 0, 0]);
  for (const r of [0.3, -1.2, 2.9, -3.1, 0]) {
    assert.ok(nahe(N.rolleZwischen(basis, N.mitRolle(basis, r)), r, 1e-12), String(r));
    const k = new THREE.PerspectiveCamera();
    k.quaternion.fromArray(basis);
    k.rotateZ(r);
    assert.ok(gleicheDrehung(k.quaternion.toArray(), N.mitRolle(basis, r), 1e-12));
  }
});

test('drehen: dx > 0 dreht die Vorderseite des Modells nach rechts, dy > 0 nach unten; der Drehpunkt bleibt im Bild stehen', () => {
  const l = lage([0, 0, 10], [0, 0, 0]);
  const d = [0, 0, 0];
  const vorne = [0, 0, 1];   // Punkt auf der Vorderseite (zur Kamera hin)
  const r = N.navigieren3d(l, { drehen: [20 * GRAD, 0] }, { drehpunkt: d });
  assert.ok(imBild(r, vorne)[0] > 0.3, 'nach rechts');
  assert.ok(nahe(imBild(r, vorne)[1], 0, 1e-9));
  const u = N.navigieren3d(l, { drehen: [0, 20 * GRAD] }, { drehpunkt: d });
  assert.ok(imBild(u, vorne)[1] < -0.3, 'nach unten');
  // Drehpunkt neben der Blicklinie: er bleibt, wo er im Bild war
  const p = [2, -1, 0.5];
  const l2 = lage([5, 6, 12], [0, 0, 0]);
  const r2 = N.navigieren3d(l2, { drehen: [0.4, -0.3], rollen: 0.2 }, { drehpunkt: p });
  assert.ok(naheV(imBild(r2, p), imBild(l2, p), 1e-9));
  // das Ziel der Steuerung bleibt auf der Blicklinie, der Abstand Kamera–Ziel bleibt
  const blick = N.qDrehe(r2.q, [0, 0, -1]);
  const zuZiel = N.minus(r2.ziel, r2.position);
  assert.ok(nahe(N.laenge(N.kreuz(blick, N.einheit(zuZiel))), 0, 1e-9));
  assert.ok(nahe(N.laenge(zuZiel), N.laenge(N.minus(l2.ziel, l2.position)), 1e-9));
});

test('rollen: positiv dreht das Bild gegen den Uhrzeiger (ein Punkt rechts wandert nach oben), um die Blickachse durch den Drehpunkt', () => {
  const l = lage([18, 14, 24], [0, 0, 0]);
  const rechts = N.plus([0, 0, 0], N.qDrehe(l.q, [1, 0, 0]));
  const r = N.navigieren3d(l, { rollen: 30 * GRAD }, { drehpunkt: [0, 0, 0] });
  const a = imBild(l, rechts), b = imBild(r, rechts);
  assert.ok(b[1] > a[1] + 0.4, 'nach oben');
  assert.ok(nahe(Math.atan2(b[1], b[0]) - Math.atan2(a[1], a[0]), 30 * GRAD, 1e-9), 'genau 30°');
  assert.ok(naheV(N.qDrehe(r.q, [0, 0, -1]), N.qDrehe(l.q, [0, 0, -1]), 1e-12), 'die Blickrichtung bleibt');
  assert.ok(nahe(N.rolleZwischen(N.aufrechtQ(r.position, r.ziel), r.q), -30 * GRAD, 1e-9), 'die Kamera rollt gegenläufig');
});

test('verschieben: Anteil der Ansicht, der Inhalt folgt (rechts, unten); zoom zur Mitte, durch das Modell, wenn zu nah', () => {
  const l = lage([0, 0, 10], [0, 0, 0]);
  const fov = 45, asp = 2;
  const r = N.navigieren3d(l, { verschieben: [0.25, 0.1] }, { fovGrad: fov, seitenverhaeltnis: asp });
  const h = 2 * 10 * Math.tan(fov / 2 * GRAD), b = h * asp;
  const p = imBild(r, [0, 0, 0]);
  assert.ok(nahe(p[0], 0.25 * b, 1e-9) && nahe(p[1], -0.1 * h, 1e-9), JSON.stringify(p));
  assert.ok(gleicheDrehung(r.q, l.q, 1e-12));
  const z = N.navigieren3d(l, { zoom: 2 }, { minAbstand: 0.3 });
  assert.ok(naheV(z.position, [0, 0, 5]) && naheV(z.ziel, [0, 0, 0]));
  const weit = N.navigieren3d(l, { zoom: 0.5 }, { maxAbstand: 15 });
  assert.ok(naheV(weit.position, [0, 0, 15]), 'maxAbstand');
  const durch = N.navigieren3d(lage([0, 0, 1], [0, 0, 0]), { zoom: 4 }, { minAbstand: 0.5 });
  assert.ok(naheV(durch.position, [0, 0, 0.25]) && naheV(durch.ziel, [0, 0, -0.25]), 'das Ziel wandert mit');
});

test('Eingabe: nur endliche Zahlen, je Aufruf begrenzt; ohne Wirkung bleibt alles, wie es ist', () => {
  assert.equal(N.eingabeNormal(null), null);
  assert.equal(N.eingabeNormal({}), null);
  assert.equal(N.eingabeNormal({ drehen: [NaN, 1] }), null);
  assert.equal(N.eingabeNormal({ zoom: 1, rollen: 0, drehen: [0, 0] }), null);
  assert.deepEqual(N.eingabeNormal({ drehen: [10, -10], zoom: 100, rollen: Infinity, verschieben: [5, 0] }), { drehen: [Math.PI, -Math.PI], verschieben: [2, 0], zoom: 5 });
  assert.equal(N.eingabeNormal({ zoom: -2 }), null);
  const l = lage([1, 2, 3], [0, 0, 0]);
  assert.deepEqual(N.navigieren3d(l, { drehen: 'x' }), l);
});

test('2D: verschieben und zoomen am Ausschnitt (SVG, y nach unten), Grenzen der Breite', () => {
  const vb = { x: 0, y: 0, w: 1000, h: 500 };
  assert.deepEqual(N.navigieren2d(vb, { verschieben: [0.1, 0.2] }), { x: -100, y: -100, w: 1000, h: 500 }, 'Inhalt nach rechts unten = Ausschnitt nach links oben');
  assert.deepEqual(N.navigieren2d(vb, { zoom: 2 }), { x: 250, y: 125, w: 500, h: 250 }, 'Mitte bleibt');
  assert.deepEqual(N.navigieren2d(vb, { zoom: 4 }, { minBreite: 400 }), { x: 300, y: 150, w: 400, h: 200 });
  assert.equal(N.navigieren2d(vb, { drehen: [1, 0], rollen: 1 }), null, 'Plan und Schema drehen nicht');
  assert.equal(N.navigieren2d(null, { zoom: 2 }), null);
});

// ------------------------------------------------------------------ mit den echten OrbitControls (wie ansicht3d.js)

function steuerungMitRolle(position, ziel) {
  const dom = { style: {}, addEventListener() {}, removeEventListener() {}, ownerDocument: { addEventListener() {}, removeEventListener() {} }, getRootNode() { return this; }, clientHeight: 600, clientWidth: 900 };
  const kamera = new THREE.PerspectiveCamera(45, 1.5, 0.1, 1000);
  kamera.position.set(...position);
  const s = new OrbitControls(kamera, dom);
  s.target.set(...ziel);
  s.screenSpacePanning = true;
  const z = { rolle: 0, kamera, s };
  const orbitUpdate = s.update;
  s.update = function (...a) { const r = orbitUpdate.apply(this, a); if (z.rolle !== 0) kamera.rotateZ(z.rolle); return r; };
  s.update();
  /** wie navigieren() in ansicht3d.js */
  z.navigieren = (e, drehpunkt) => {
    kamera.updateMatrixWorld();
    const neu = N.navigieren3d({ position: kamera.position.toArray(), ziel: s.target.toArray(), q: kamera.quaternion.toArray() }, e,
      { drehpunkt, fovGrad: kamera.fov, seitenverhaeltnis: kamera.aspect, minAbstand: 0.3, maxAbstand: Infinity });
    kamera.position.fromArray(neu.position);
    s.target.fromArray(neu.ziel);
    z.rolle = 0;
    s.update();
    z.rolle = N.rolleZwischen(kamera.quaternion.toArray(), neu.q);
    kamera.rotateZ(z.rolle);
    return neu;
  };
  return z;
}

test('ansicht3d: nach navigieren steht die Kamera genau in der frei gedrehten Lage, auch über den Pol hinweg; die Steuerung behält die Rolle', () => {
  const z = steuerungMitRolle([18, 14, 24], [0, 0, 0]);
  const d = [1, 0.5, -2];
  let erwartet = null;
  // viele kleine Schritte wie aus der Faust: drehen, rollen, über den Pol (Blick senkrecht und darüber hinaus)
  for (let i = 0; i < 60; i++) {
    erwartet = z.navigieren({ drehen: [0.03, 0.06], rollen: 0.02 }, d);
    assert.ok(gleicheDrehung(z.kamera.quaternion.toArray(), erwartet.q, 1e-6), `Lage im Schritt ${i}`);
    assert.ok(naheV(z.kamera.position.toArray(), erwartet.position, 1e-6), `Ort im Schritt ${i}`);
  }
  // eine weitere Aktualisierung der Steuerung (z. B. Mausrad, Grössenänderung) ändert die Lage nicht
  const q = z.kamera.quaternion.toArray();
  z.s.update();
  assert.ok(gleicheDrehung(z.kamera.quaternion.toArray(), q, 1e-9));
  assert.ok(Math.abs(z.rolle) > 0.1, 'die Ansicht ist gerollt');
  // aufrichten: Rolle 0 → wieder aufrecht (Hochrichtung der Kamera in der senkrechten Ebene durch die Blickrichtung, oben)
  z.rolle = 0;
  z.s.update();
  const hoch = new THREE.Vector3(0, 1, 0).applyQuaternion(z.kamera.quaternion);
  const rechts = new THREE.Vector3(1, 0, 0).applyQuaternion(z.kamera.quaternion);
  assert.ok(nahe(rechts.y, 0, 1e-9) && hoch.y > 0, 'aufrecht');
});

test('ansicht3d: Maus nach dem Rollen – die Steuerung dreht weiter um die Hochachse des Modells, die Rolle bleibt erhalten', () => {
  const z = steuerungMitRolle([10, 5, 10], [0, 0, 0]);
  z.navigieren({ rollen: 0.5 }, [0, 0, 0]);
  const rolle = z.rolle;
  // die Steuerung bewegt die Kamera um die Hochachse (wie Ziehen mit der linken Taste): Ort um y gedreht, dann update
  const p = z.kamera.position.clone().applyAxisAngle(new THREE.Vector3(0, 1, 0), 0.4);
  z.kamera.position.copy(p);
  z.s.update();
  const basis = new THREE.PerspectiveCamera();
  basis.position.copy(z.kamera.position);
  basis.lookAt(z.s.target);
  assert.ok(nahe(N.rolleZwischen(basis.quaternion.toArray(), z.kamera.quaternion.toArray()), rolle, 1e-9));
});
