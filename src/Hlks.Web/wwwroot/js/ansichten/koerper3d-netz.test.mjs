// Tests der Körperart «koerper» (Modul 2, B1: allgemeines Netz eines Familienkörpers) in koerper3d.js – ohne Browser.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/ansichten/koerper3d-netz.test.mjs   (oder node --test im Ordner ansichten)

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

// «three» ist im Browser über die Importmap erreichbar; in Node per Auflöser auf die lokale Datei umlenken
const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const k3 = await import('./koerper3d.js');
const THREE = await import(threeUrl);

/** Quader 100 × 200 × 300 mm als Netz wie der Kern (NetzBau.Quader): Dreiecke im Gegenuhrzeigersinn von aussen. */
function quader() {
  const e = [[0, 0, 0], [100, 0, 0], [100, 200, 0], [0, 200, 0], [0, 0, 300], [100, 0, 300], [100, 200, 300], [0, 200, 300]];
  const v = (a, b, c, d) => [[a, b, c], [a, c, d]];
  const dr = [...v(0, 3, 2, 1), ...v(4, 5, 6, 7), ...v(0, 1, 5, 4), ...v(1, 2, 6, 5), ...v(2, 3, 7, 6), ...v(3, 0, 4, 7)];
  return { art: 'koerper', ecken: e, dreiecke: dr, material: 'geraet' };
}

/** Zylinder r 50, h 100 um z mit 48 Facetten (Mantel glatt, Kanten oben und unten scharf). */
function zylinder() {
  const n = 48, e = [], dr = [];
  for (let k = 0; k < n; k++) { const w = 2 * Math.PI * k / n; e.push([50 * Math.cos(w), 50 * Math.sin(w), 0], [50 * Math.cos(w), 50 * Math.sin(w), 100]); }
  const u = e.length; e.push([0, 0, 0]); const o = e.length; e.push([0, 0, 100]);
  for (let k = 0; k < n; k++) {
    const a = 2 * k, b = 2 * ((k + 1) % n);
    dr.push([a, b, b + 1], [a, b + 1, a + 1], [u, b, a], [o, a + 1, b + 1]);
  }
  return { art: 'koerper', ecken: e, dreiecke: dr };
}

test('3D Netz: Ecken in three-Koordinaten (x, z, −y in m), ein Dreieck = drei Ecken, Normalen zeigen nach aussen', () => {
  const g = k3.netzKoerperGeometrie(quader());
  assert.ok(g instanceof THREE.BufferGeometry);
  assert.equal(g.index, null);
  assert.equal(g.getAttribute('position').count, 36);
  const b = g.boundingBox;
  for (const [ist, soll] of [[b.min.x, 0], [b.max.x, 0.1], [b.min.y, 0], [b.max.y, 0.3], [b.min.z, -0.2], [b.max.z, 0]]) assert.ok(Math.abs(ist - soll) < 1e-6, `${ist} statt ${soll}`);   // Float32
  // jede Normale zeigt vom Mittelpunkt weg (aussen) und ist achsparallel (Kanten des Quaders sind Knicke)
  const pos = g.getAttribute('position'), nor = g.getAttribute('normal');
  const mitte = new THREE.Vector3(0.05, 0.15, -0.1);
  for (let i = 0; i < pos.count; i++) {
    const p = new THREE.Vector3().fromBufferAttribute(pos, i), n = new THREE.Vector3().fromBufferAttribute(nor, i);
    assert.ok(n.dot(p.clone().sub(mitte)) > 0, `Normale ${i} zeigt nach innen`);
    assert.ok(Math.abs(Math.max(Math.abs(n.x), Math.abs(n.y), Math.abs(n.z)) - 1) < 1e-6, `Normale ${i} nicht achsparallel`);
  }
  // Volumen über die Dreiecke (Divergenzsatz) = 0.1 · 0.2 · 0.3 m³ und positiv
  let v = 0;
  for (let i = 0; i < pos.count; i += 3) {
    const a = new THREE.Vector3().fromBufferAttribute(pos, i), c = new THREE.Vector3().fromBufferAttribute(pos, i + 1), d = new THREE.Vector3().fromBufferAttribute(pos, i + 2);
    v += a.dot(c.clone().cross(d)) / 6;
  }
  assert.ok(Math.abs(v - 0.006) < 1e-8, `Volumen ${v}`);
});

test('3D Netz: runde Flächen glatt (gemittelte Normalen), Knicke ab 20° scharf', () => {
  assert.equal(k3.KNICK, 20);
  const g = k3.netzKoerperGeometrie(zylinder());
  const pos = g.getAttribute('position'), nor = g.getAttribute('normal');
  let glatt = 0, deckel = 0;
  for (let i = 0; i < pos.count; i++) {
    const n = new THREE.Vector3().fromBufferAttribute(nor, i), p = new THREE.Vector3().fromBufferAttribute(pos, i);
    if (Math.abs(Math.abs(n.y) - 1) < 1e-6) { deckel++; continue; }                        // Deckel: senkrecht, nicht mit dem Mantel gemittelt
    assert.ok(Math.abs(n.y) < 1e-6, 'Mantelnormale hat einen senkrechten Anteil (Knick oben/unten nicht getrennt)');
    const radial = new THREE.Vector3(p.x, 0, p.z).normalize();
    if (n.dot(radial) > 1 - 1e-6) glatt++;                                                 // Mantel: Normale radial wie am Kreis
  }
  assert.equal(deckel, 48 * 2 * 3);
  assert.equal(glatt, 48 * 2 * 3);
});

test('3D Netz: ungültige Angaben ergeben null, teilGeometrie kennt die Art', () => {
  assert.equal(k3.netzKoerperGeometrie({ art: 'koerper' }), null);
  assert.equal(k3.netzKoerperGeometrie({ art: 'koerper', ecken: [[0, 0], [1, 0], [0, 1]], dreiecke: [[0, 1, 2]] }), null);          // ohne z
  assert.equal(k3.netzKoerperGeometrie({ art: 'koerper', ecken: [[0, 0, 0], [1, 0, 0], [0, 1, 0]], dreiecke: [[0, 1, 3]] }), null); // Index ausserhalb
  assert.equal(k3.netzKoerperGeometrie({ art: 'koerper', ecken: [[0, 0, 0], [1, 0, 0], [0, 1, 0]], dreiecke: [[0, 1, 1]] }), null); // entartet
  const ok = k3.netzKoerperGeometrie({ art: 'koerper', ecken: [[0, 0, 0], [1000, 0, 0], [0, 1000, 0]], dreiecke: [[0, 1, 2], [9, 9, 9]] });
  assert.equal(ok.getAttribute('position').count, 3);                                                                            // ungültiges Dreieck übergangen
  assert.ok(k3.teilGeometrie(quader()) instanceof THREE.BufferGeometry);
});

test('3D Nachladen: eine geänderte Familie lädt alle Körper neu (ihre Elemente ändern die Gestalt ohne eigene Änderung)', () => {
  assert.ok(k3.WIRKT_AUF_NACHBARN_KLASSEN.has('Familie'));
  assert.equal(k3.brauchtAlleKoerper(['f'], (id) => (id === 'f' ? 'Familie' : null)), true);
  assert.equal(k3.brauchtAlleKoerper(['h'], (id) => (id === 'h' ? 'Heizkoerper' : null)), false);
});

test('3D Netz: Körper mit Mesh und Kanten, Material je Farbe, freigeben', () => {
  const g = k3.baueKoerper({ id: 'hk-1', ebene: 'apparat', teile: [quader(), { ...zylinder(), farbe: '#d0312d' }] });
  assert.ok(g);
  assert.equal(g.userData.id, 'hk-1');
  assert.equal(g.userData.vorrang, k3.VORRANG.apparat);
  const meshes = g.children.filter((c) => c.isMesh), kanten = g.children.filter((c) => c.userData.kante);
  assert.equal(meshes.length, 2);
  assert.equal(kanten.length, 2);
  assert.equal(g.userData.materialien.length, 2);
  assert.equal('#' + meshes[1].material.color.getHexString(), '#d0312d');
  // Kanten: beim Quader die 12 Würfelkanten, beim Zylinder nur die beiden Kreise (Mantelfacetten unter 20°)
  assert.equal(kanten[0].geometry.getAttribute('position').count, 24);
  assert.equal(kanten[1].geometry.getAttribute('position').count, 2 * 48 * 2);
  assert.doesNotThrow(() => k3.freigeben(g));
});
