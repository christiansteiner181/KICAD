// Darstellung «Hülle» im 3D (Modul 1c, Auftrag C1b): macht aus den Zeilen der Abfrage raumgrenzen {geometrie: true} farbige,
// durchscheinende Flächen. Eine Geometrie je Angrenzung (aussen, erdreich, unbeheizt, beheizt = vier Meshes), damit auch rund
// 10 000 Raumgrenzen (BKW) flüssig bleiben. Ohne Renderer, ohne DOM (in Node testbar). Der Kern liefert die Geometrie, hier wird
// nur in three-Koordinaten umgesetzt und trianguliert.
//
// Seiten (wand, fenster, tuer): senkrechtes Viereck Von/Bis × unten/oben auf der Raumkante, um ABSTAND_MM in den Raum versetzt
// (der Raum liegt links der Laufrichtung Von → Bis, so rechnet der Kern: Raumgrenzen.Kante). Fenster und Türen liegen
// nochmals davor (doppelter Versatz), damit sie nicht in der Wandfläche liegen; sie decken doppelt und erscheinen dunkler.
// Boden/Decke/Dach: Ring (Löcher über Stege der Breite 0) auf der Höhe z, um ABSTAND_MM in den Raum versetzt (Boden nach oben,
// Decke und Dach nach unten), trianguliert mit ShapeUtils (earcut; Stege sind für earcut gebaut).
// Koordinaten: Modell mm (x Ost, y Nord, z oben) → three Meter: x = x, y = z, z = −y.

import * as THREE from 'three';
import { ANGRENZUNGEN, HUELLE_FARBEN, HUELLE_DECKKRAFT_3D } from './huelle-darstellung.js';

const M = 1 / 1000;
/** Versatz der Flächen in den Raum in mm (Annahme: gegen Z-Fighting mit Wand-, Decken- und Raumkörpern). */
export const ABSTAND_MM = 12;
/** renderOrder der Hüllflächen: nach den Körpern des Modells (durchscheinende Objekte zeichnet three nach renderOrder). */
export const RENDER_ORDER = 5;

const endlich = (...v) => v.every(Number.isFinite);

/** Eckpunkte (mm, Modellsystem) einer Seitenfläche: Viereck. null bei ungültigen Angaben. */
export function seitenEcken(z) {
  const p = z?.punkte;
  if (!Array.isArray(p) || p.length < 2 || !endlich(p[0][0], p[0][1], p[1][0], p[1][1], z.unten, z.oben)) return null;
  const dx = p[1][0] - p[0][0], dy = p[1][1] - p[0][1], l = Math.hypot(dx, dy);
  if (l < 1 || z.oben - z.unten < 1) return null;
  // Raum links der Laufrichtung → Normale nach links (−dy, dx); Öffnungen doppelt so weit
  const v = ABSTAND_MM * (z.art === 'fenster' || z.art === 'tuer' ? 2 : 1);
  const nx = -dy / l * v, ny = dx / l * v;
  return [
    [p[0][0] + nx, p[0][1] + ny, z.unten], [p[1][0] + nx, p[1][1] + ny, z.unten],
    [p[1][0] + nx, p[1][1] + ny, z.oben], [p[0][0] + nx, p[0][1] + ny, z.oben],
  ];
}

/** Höhe (mm) der waagrechten Fläche im Raum: Boden nach oben, Decke und Dach nach unten versetzt. */
export const ebenenHoehe = (z) => z.z + (z.art === 'boden' ? ABSTAND_MM : -ABSTAND_MM);

/** Umrisspunkte (mm, Modellsystem) einer Zeile: Viereck der Seite oder Ring der waagrechten Fläche; [] wenn ungültig. */
export function umrissEcken(z) {
  if (z?.polygon) {
    if (!Array.isArray(z.polygon) || z.polygon.length < 3 || !Number.isFinite(z.z)) return [];
    const h = ebenenHoehe(z);
    return z.polygon.filter((q) => endlich(q[0], q[1])).map((q) => [q[0], q[1], h]);
  }
  return seitenEcken(z) ?? [];
}

const three = (q) => [q[0] * M, q[2] * M, -q[1] * M];

/** Dreiecke einer Zeile als flache Liste von Eckpunkten (three-Meter, je drei Zahlen) an «aus» angehängt; Rückgabe: Zahl der Dreiecke. */
export function zeileDreiecke(z, aus) {
  if (z?.polygon) {
    const ecken = umrissEcken(z);
    if (ecken.length < 3) return 0;
    const kontur = ecken.map((q) => new THREE.Vector2(q[0], q[1]));
    const flaechen = THREE.ShapeUtils.triangulateShape(kontur, []);
    for (const f of flaechen) for (const i of f) aus.push(...three(ecken[i]));
    return flaechen.length;
  }
  const e = seitenEcken(z);
  if (!e) return 0;
  for (const i of [0, 1, 2, 0, 2, 3]) aus.push(...three(e[i]));
  return 2;
}

/**
 * Geometrien aus den Zeilen der Abfrage raumgrenzen (geometrie: true), je Angrenzung eine.
 * Ergebnis: Map angrenzung → { geometrie: BufferGeometry | null, zeile: Uint32Array (Dreieck → Index in zeilen), dreiecke }.
 * Zeilen ohne Geometrie oder mit unbekannter Angrenzung werden übergangen.
 */
export function huelleGeometrien(zeilen) {
  const jeFarbe = new Map(ANGRENZUNGEN.map((a) => [a, { pos: [], zeile: [] }]));
  (zeilen ?? []).forEach((z, i) => {
    const ziel = jeFarbe.get(z?.angrenzung);
    if (!ziel) return;
    const n = zeileDreiecke(z, ziel.pos);
    for (let k = 0; k < n; k++) ziel.zeile.push(i);
  });
  const aus = new Map();
  for (const [a, d] of jeFarbe) {
    let geometrie = null;
    if (d.pos.length >= 9) {
      geometrie = new THREE.BufferGeometry();
      geometrie.setAttribute('position', new THREE.BufferAttribute(Float32Array.from(d.pos), 3));
      geometrie.computeBoundingBox();
      geometrie.computeBoundingSphere();
    }
    aus.set(a, { geometrie, zeile: Uint32Array.from(d.zeile), dreiecke: d.zeile.length });
  }
  return aus;
}

/** Gruppe mit einem Mesh je Angrenzung. userData: huelle (true), zeilen (Zeilen der Abfrage), Mesh.userData.zeile (Dreieck → Zeile). */
export function baueHuelle(zeilen) {
  const gruppe = new THREE.Group();
  gruppe.userData.huelle = true;
  gruppe.userData.zeilen = zeilen ?? [];
  for (const [a, d] of huelleGeometrien(zeilen)) {
    if (!d.geometrie) continue;
    const mat = new THREE.MeshBasicMaterial({ color: HUELLE_FARBEN[a], side: THREE.DoubleSide, transparent: true, opacity: HUELLE_DECKKRAFT_3D[a], depthWrite: false });
    const mesh = new THREE.Mesh(d.geometrie, mat);
    mesh.renderOrder = RENDER_ORDER;
    mesh.userData = { huelle: true, angrenzung: a, zeile: d.zeile, dreiecke: d.dreiecke };
    gruppe.add(mesh);
  }
  return gruppe;
}

/** Raumgrenze (Zeile der Abfrage) an einem Strahltreffer (intersectObjects) oder null. */
export function zeileAmTreffer(gruppe, treffer) {
  const t = treffer?.find((x) => x.object?.userData?.huelle && Number.isInteger(x.faceIndex));
  if (!t) return null;
  const idx = t.object.userData.zeile[t.faceIndex];
  return idx === undefined ? null : { zeile: gruppe.userData.zeilen[idx], index: idx, distanz: t.distance };
}

/** Linienzug (LineLoop) um die Fläche einer Zeile, für die Hervorhebung der angeklickten Raumgrenze. */
export function umrissLinie(z, farbe) {
  const e = umrissEcken(z);
  if (e.length < 3) return null;
  const pos = Float32Array.from(e.flatMap(three));
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  const linie = new THREE.LineLoop(g, new THREE.LineBasicMaterial({ color: farbe, depthTest: false, transparent: true }));
  linie.renderOrder = RENDER_ORDER + 1;
  linie.raycast = () => {};
  return linie;
}

/** Gibt Geometrien und Materialien der Gruppe frei. */
export function huelleFreigeben(gruppe) {
  gruppe?.traverse((o) => { o.geometry?.dispose?.(); o.material?.dispose?.(); });
}
