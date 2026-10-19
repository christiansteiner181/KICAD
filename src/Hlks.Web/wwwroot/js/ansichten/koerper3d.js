// Baut aus den Körpern des Kerns (/api/koerper, Grafiktypen.cs: quader, prisma, zylinder, kugel, koerper) three.js-Objekte für die
// 3D-Ansicht. Ohne Renderer, ohne DOM (in Node testbar). Die Oberfläche rechnet nichts Fachliches: Masse, Lagen und
// Drehungen liefert der Kern, hier werden sie nur in three-Koordinaten umgesetzt.
//
// Koordinaten: Modell mm (x Ost, y Nord, z oben) → three Meter: x = x, y = z (oben), z = −y.
// Jeder Körper ist eine Gruppe mit userData { id, ebene, vorrang, materialien, original }; Hervorheben und Freigeben
// arbeiten darauf. Farben: Teil.farbe, sonst die Materialfarbe aus den Design-Variablen (--d3-<material>), sonst eine
// Vorgabe je Design. Deckkraft: Teil.deckkraft, sonst die Vorgabe des Materials (wand: --d3-wand-deckung).
//
// Art «netz» (Hintergrund einer IFC-Unterlage, Modul 1b): der Körper bekommt nur einen leeren Halter (userData.netz); das Netz
// lädt ansicht3d.js einmal je Unterlage (netzdatei.js) und hängt es als EIN Mesh (eine Geometrie, nicht auswählbar) an.

import * as THREE from 'three';
import { cssVar } from './hilfen.js';

const M = 1 / 1000;
/** Segmente je Umfang für Zylinder und Kugeln (unter ~11° je Segment wirkt es rund). */
export const RUND = 32;
/** Modellpunkt (mm) → three-Vektor (m). */
export const vek = (p) => new THREE.Vector3(p[0] * M, (p[2] ?? 0) * M, -p[1] * M);

/** Wie gut lässt sich ein Körper anklicken, wenn mehrere hinter dem Mauszeiger liegen? Grösser = bevorzugt. */
export const VORRANG = { raum: 0, decke: 1, wand: 1, oeffnung: 1, sonst: 1, leitung: 2, formteil: 2, armatur: 2, apparat: 2 };
export const vorrangVon = (ebene) => VORRANG[ebene] ?? 1;

/** Ebenen bzw. Klassen, deren Änderung auch die Körper der Nachbarn ändert (Wandanschlüsse, Öffnungen in Wänden).
 *  Dach: «Decke läuft durch» kürzt die Wände auch unter einem Dach (decke_erzeugen art dach, Modul 1c).
 *  Geschoss: Kote oder Höhe ändern Wandhöhen, Decken und Dächer (Nachführung des Kerns) – dann alle Körper neu holen.
 *  Familie (Modul 2): eine neue Definition ändert die Körper aller platzierten Elemente, auch wenn deren Daten gleich bleiben. */
export const WIRKT_AUF_NACHBARN_EBENEN = new Set(['wand', 'oeffnung', 'decke']);
export const WIRKT_AUF_NACHBARN_KLASSEN = new Set(['Wand', 'Tuer', 'Fenster', 'Decke', 'Dach', 'Geschoss', 'Familie']);
/** Mehr Ids als hier holt die Ansicht nicht einzeln (URL-Länge), sondern alle Körper. */
export const MAX_IDS = 100;

/** Muss ein Teil der Änderung alle Körper neu holen lassen? ids = geänderte Ids, klasseVon(id) → Klasse oder null,
 *  ebeneVon(id) → bisherige Körperebene oder null (für gelöschte Elemente). */
export function brauchtAlleKoerper(ids, klasseVon, ebeneVon = () => null) {
  for (const id of ids ?? []) {
    if (WIRKT_AUF_NACHBARN_KLASSEN.has(klasseVon(id)) || WIRKT_AUF_NACHBARN_EBENEN.has(ebeneVon(id))) return true;
  }
  return false;
}

/** URL für /api/koerper: alle oder nur die genannten Ids. */
export function koerperUrl(ids) {
  if (!ids || ids.length === 0 || ids.length > MAX_IDS) return '/api/koerper';
  return '/api/koerper?ids=' + ids.map(encodeURIComponent).join(',');
}

// ---------------------------------------------------------------------------------------------- Farben

const VORGABEN = {
  hell: { wand: '#cfd3d8', decke: '#b9bec6', raum: '#9db8d4', glas: '#a9d4ee', tuer: '#b08d62', rohr: '#777777', apparat: '#f4f5f7', geraet: '#7b8a99', armatur: '#666666', sonst: '#888888' },
  dunkel: { wand: '#aab1bb', decke: '#7c8697', raum: '#6d8aa8', glas: '#8bbbd8', tuer: '#a9825a', rohr: '#9aa3ae', apparat: '#aeb6c2', geraet: '#7b8a99', armatur: '#8a929e', sonst: '#8a929e' },
};
const VORGABE_DECKUNG = { hell: { wand: 0.28 }, dunkel: { wand: 0.22 } };
/** Materialnamen des Kerns, die kein eigenes Material haben. */
const ALIAS = { formteil: 'rohr', leitung: 'rohr' };

function designName() {
  try { return document.documentElement.getAttribute('data-design') === 'hell' ? 'hell' : 'dunkel'; }   // Vorgabe der Oberfläche: dunkel
  catch { return 'hell'; }   // ohne Browser (Tests)
}

const hexOderNull = (f) => (typeof f === 'string' && /^#[0-9a-fA-F]{3,8}$/.test(f) ? f : null);
const KANTE_AB_DECKKRAFT = 0.2;   // Kanten nur bei halbwegs deckenden Körpern (durchscheinende Räume bleiben ohne Gitter)

/** Farbe eines Teils: Teil.farbe, sonst --d3-<material> aus dem Design, sonst Vorgabe. */
export function teilFarbe(teil) {
  const eigene = hexOderNull(teil.farbe);
  if (eigene) return eigene;
  const mat = ALIAS[teil.material] ?? teil.material ?? 'sonst';
  const vorgabe = VORGABEN[designName()][mat] ?? VORGABEN[designName()].sonst;
  return hexOderNull(cssVar(`--d3-${mat}`, vorgabe)) ?? vorgabe;
}

/** Deckkraft eines Teils: Teil.deckkraft, sonst Materialvorgabe (wand: --d3-wand-deckung), sonst 1. */
export function teilDeckkraft(teil) {
  if (Number.isFinite(teil.deckkraft)) return Math.min(Math.max(teil.deckkraft, 0), 1);
  const mat = ALIAS[teil.material] ?? teil.material;
  const v = VORGABE_DECKUNG[designName()][mat];
  if (v === undefined) return 1;
  const n = Number(cssVar(`--d3-${mat}-deckung`, String(v)));
  return Number.isFinite(n) && n > 0 ? n : v;
}

// ---------------------------------------------------------------------------------------------- Geometrie

const zahl = (v) => Number.isFinite(v);
const punkt3 = (p) => Array.isArray(p) && p.length >= 2 && zahl(p[0]) && zahl(p[1]) && (p[2] === undefined || zahl(p[2]));

/** Quader: Mittelpunkt [x, y, z], Kantenlängen [lx, ly, lz] im eigenen System, Drehung um z (Grad, Gegenuhrzeigersinn). */
export function quaderGeometrie(t) {
  if (!punkt3(t.mitte) || !Array.isArray(t.masse) || !t.masse.slice(0, 3).every((x) => zahl(x) && x > 0)) return null;
  const [lx, ly, lz] = t.masse;
  const g = new THREE.BoxGeometry(lx * M, lz * M, ly * M);   // three: x, y (oben = z), z (= −y)
  if (zahl(t.drehung) && t.drehung !== 0) g.rotateY(t.drehung * Math.PI / 180);   // Drehung um z (Modell) = um y (three), gleicher Drehsinn
  const m = vek(t.mitte);
  g.translate(m.x, m.y, m.z);
  return g;
}

/** Prisma: Grundriss (Aussenring) mit Löchern, senkrecht von «unten» bis «oben» (mm) extrudiert. */
export function prismaGeometrie(t) {
  const ring = (t.grundriss ?? []).filter(punkt3);
  if (ring.length < 3 || !zahl(t.unten) || !zahl(t.oben) || t.oben <= t.unten) return null;
  const form = new THREE.Shape(ring.map((p) => new THREE.Vector2(p[0] * M, p[1] * M)));
  for (const l of t.loecher ?? []) {
    const r = (l ?? []).filter(punkt3);
    if (r.length >= 3) form.holes.push(new THREE.Path(r.map((p) => new THREE.Vector2(p[0] * M, p[1] * M))));
  }
  const g = new THREE.ExtrudeGeometry(form, { depth: (t.oben - t.unten) * M, bevelEnabled: false, curveSegments: RUND });
  g.rotateX(-Math.PI / 2);        // (x, y, z) → (x, z, −y): Extrusion nach oben, Grundriss in die Bodenebene
  g.translate(0, t.unten * M, 0);
  return g;
}

/** Zylinder von «von» nach «nach» mit Radius (mm), RUND Segmente. */
export function zylinderGeometrie(t) {
  if (!punkt3(t.von) || !punkt3(t.nach) || !(t.radius > 0)) return null;
  const a = vek(t.von), b = vek(t.nach);
  const richtung = new THREE.Vector3().subVectors(b, a);
  const l = richtung.length();
  if (l < 1e-9) return null;
  const g = new THREE.CylinderGeometry(t.radius * M, t.radius * M, l, RUND);
  g.applyQuaternion(new THREE.Quaternion().setFromUnitVectors(new THREE.Vector3(0, 1, 0), richtung.normalize()));
  const mitte = a.add(b).multiplyScalar(0.5);
  g.translate(mitte.x, mitte.y, mitte.z);
  return g;
}

/** Kugel um «mitte» mit Radius (mm). */
export function kugelGeometrie(t) {
  if (!punkt3(t.mitte) || !(t.radius > 0)) return null;
  const g = new THREE.SphereGeometry(t.radius * M, RUND, RUND / 2);
  const m = vek(t.mitte);
  g.translate(m.x, m.y, m.z);
  return g;
}

// ---------------------------------------------------------------------------------------------- Netz eines Familienkörpers

/** Knickwinkel in Grad (wie Schnittzerlegung.Knickwinkel im Kern): darüber getrennte Normalen (scharfe Kante), darunter
 *  gemittelt (runde Flächen wirken glatt). */
export const KNICK = 20;

/** Art «koerper» (Modul 2): allgemeines Netz eines Familienkörpers – Ecken [[x, y, z] …] in mm, Dreiecke [[i, j, k] …] im
 *  Gegenuhrzeigersinn von aussen. Ergebnis: BufferGeometry ohne Index (je Dreieck drei Ecken) mit Normalen je Ecke, über
 *  Knicke ab KNICK getrennt. Die Umrechnung x = x, y = z, z = −y ist eine Drehung: die Normalen zeigen weiter nach aussen.
 *  null bei ungültigen Angaben (Ecke ohne z, Index ausserhalb, kein gültiges Dreieck). */
export function netzKoerperGeometrie(t) {
  const ecken = t?.ecken, dreiecke = t?.dreiecke;
  if (!Array.isArray(ecken) || !Array.isArray(dreiecke) || ecken.length < 3) return null;
  if (!ecken.every((p) => Array.isArray(p) && p.length >= 3 && zahl(p[0]) && zahl(p[1]) && zahl(p[2]))) return null;
  const n = ecken.length;
  const d = dreiecke.filter((x) => Array.isArray(x) && x.length === 3 && x.every((i) => Number.isInteger(i) && i >= 0 && i < n) && x[0] !== x[1] && x[1] !== x[2] && x[0] !== x[2]);
  if (d.length === 0) return null;
  const p = ecken.map(vek);
  const ab = new THREE.Vector3(), ac = new THREE.Vector3();
  const fn = d.map(([a, b, c]) => new THREE.Vector3().crossVectors(ab.subVectors(p[b], p[a]), ac.subVectors(p[c], p[a])).normalize());
  // Winkel jedes Dreiecks an seinen Ecken: Gewicht der Flächennormale (unabhängig davon, wie eine Fläche zerlegt ist)
  const winkel = d.map((x) => x.map((i, k) => ab.subVectors(p[x[(k + 1) % 3]], p[i]).angleTo(ac.subVectors(p[x[(k + 2) % 3]], p[i]))));
  const anEcke = Array.from({ length: n }, () => []);
  d.forEach((x, f) => x.forEach((i, k) => anEcke[i].push([f, k])));
  const grenze = Math.cos(KNICK * Math.PI / 180);
  const pos = new Float32Array(d.length * 9), nor = new Float32Array(d.length * 9);
  const summe = new THREE.Vector3();
  d.forEach((x, f) => {
    for (let k = 0; k < 3; k++) {
      const i = x[k];
      summe.set(0, 0, 0);
      for (const [g, kg] of anEcke[i]) if (fn[g].dot(fn[f]) >= grenze) summe.addScaledVector(fn[g], winkel[g][kg] || 0);
      if (summe.lengthSq() < 1e-20) summe.copy(fn[f]);
      summe.normalize();
      const o = f * 9 + k * 3;
      pos[o] = p[i].x; pos[o + 1] = p[i].y; pos[o + 2] = p[i].z;
      nor[o] = summe.x; nor[o + 1] = summe.y; nor[o + 2] = summe.z;
    }
  });
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(pos, 3));
  g.setAttribute('normal', new THREE.BufferAttribute(nor, 3));
  g.computeBoundingBox();
  g.computeBoundingSphere();
  return g;
}

// ---------------------------------------------------------------------------------------------- Netz (Hintergrund)

/** Schlüssel, an dem sich die Netzdatei einer Unterlage erkennen lässt (Name der Datei in der Eigenschaft «netz»; sonst die Id). */
export const netzSchluessel = (unterlage) => String(unterlage?.werte?.netz ?? unterlage?.netz ?? unterlage?.id ?? '');

/** URL der Netzdatei einer Unterlage; der Schlüssel verhindert, dass der Browser eine alte Datei wiederverwendet. */
export const netzUrl = (quelle, schluessel = '') => `/api/unterlagen/${encodeURIComponent(quelle)}/netz${schluessel ? '?v=' + encodeURIComponent(schluessel) : ''}`;

/** BufferGeometry aus dem Ergebnis von leseNetz (positionen in three-Metern, indizes); null, wenn nichts zu zeigen ist.
 *  Die Geometrie gehört dem Zwischenspeicher der Ansicht (userData.geteilt): freigeben() lässt sie stehen. */
export function netzGeometrie(netz) {
  if (!netz || !(netz.indizes?.length >= 3) || !(netz.positionen?.length >= 9)) return null;
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.BufferAttribute(netz.positionen, 3));
  g.setIndex(new THREE.BufferAttribute(netz.indizes, 1));
  g.computeBoundingBox();
  g.computeBoundingSphere();
  g.userData.geteilt = true;
  return g;
}

/** Mesh des Hintergrunds: durchscheinend, doppelseitig, ohne Normalen (flache Schattierung aus Bildschirmableitungen),
 *  nie ein Treffer (Raycast aus – bei Millionen Dreiecken auch aus Leistungsgründen). */
export function netzMesh(geometrie, farbe, deckkraft) {
  const durchscheinend = deckkraft < 1;
  const mat = new THREE.MeshLambertMaterial({ color: farbe, side: THREE.DoubleSide, flatShading: true, transparent: durchscheinend, opacity: deckkraft, depthWrite: !durchscheinend });
  const mesh = new THREE.Mesh(geometrie, mat);
  mesh.raycast = () => {};
  mesh.renderOrder = -1;   // zuerst zeichnen; die Körper des Modells liegen davor
  mesh.userData.netz = true;
  return mesh;
}

const GEOMETRIEN = { quader: quaderGeometrie, prisma: prismaGeometrie, zylinder: zylinderGeometrie, kugel: kugelGeometrie, koerper: netzKoerperGeometrie };
const MIT_KANTEN = new Set(['quader', 'prisma', 'koerper']);

/** Geometrie eines Körperteils in three-Koordinaten (Meter); null bei ungültigen Angaben. */
export const teilGeometrie = (teil) => GEOMETRIEN[teil?.art]?.(teil) ?? null;

// ---------------------------------------------------------------------------------------------- Körper

function material(farbe, deckkraft) {
  const durchscheinend = deckkraft < 1;
  return new THREE.MeshLambertMaterial({ color: farbe, side: THREE.DoubleSide, transparent: durchscheinend, opacity: deckkraft, depthWrite: !durchscheinend });
}

/** Darstellung «Hülle»: Architekturkörper werden blasser (Faktor auf die Deckkraft je Ebene, Annahme), damit die Hüllflächen
 *  sichtbar bleiben. Decken sind sonst voll deckend und würden die Flächen darunter verdecken. */
export const BLASS_HUELLE = Object.freeze({ wand: 0.5, oeffnung: 0.5, decke: 0.22, raum: 0.4 });

/** Baut einen Körper des Kerns; null, wenn nichts darstellbar ist. Fehler werden gemeldet, nicht geworfen.
 *  opt.blasser: Faktoren je Ebene auf die Deckkraft (BLASS_HUELLE); die Kanten richten sich nach der unverminderten Deckkraft. */
export function baueKoerper(k, opt = {}) {
  try {
    const g = new THREE.Group();
    g.userData = { id: k.id, ebene: k.ebene ?? 'sonst', vorrang: vorrangVon(k.ebene), materialien: [] };
    const kantenfarbe = cssVar('--d3-kante', '#6b7280');
    const materialien = new Map();   // Farbe|Deckkraft → gemeinsames Material des Körpers
    for (const t of k.teile ?? []) {
      if (t.art === 'netz') {   // Halter; das Netz kommt asynchron (ansicht3d.js)
        if (!t.quelle) continue;
        const halter = new THREE.Group();
        halter.userData.netz = { quelle: t.quelle, farbe: teilFarbe({ ...t, material: 'sonst' }), deckkraft: Number.isFinite(t.deckkraft) ? Math.min(Math.max(t.deckkraft, 0), 1) : 0.5 };
        g.add(halter);
        continue;
      }
      const geom = teilGeometrie(t);
      if (!geom) continue;
      const farbe = teilFarbe(t), deckVoll = teilDeckkraft(t);
      const deck = deckVoll * (opt.blasser?.[k.ebene] ?? 1);
      const schluessel = `${farbe}|${deck}`;
      let mat = materialien.get(schluessel);
      if (!mat) { mat = material(farbe, deck); materialien.set(schluessel, mat); g.userData.materialien.push(mat); }
      g.add(new THREE.Mesh(geom, mat));
      if (MIT_KANTEN.has(t.art) && deckVoll >= KANTE_AB_DECKKRAFT) {
        const kante = new THREE.LineSegments(new THREE.EdgesGeometry(geom, 20), new THREE.LineBasicMaterial({ color: kantenfarbe }));
        kante.raycast = () => {};   // Kanten werden nie angeklickt
        kante.userData.kante = true;
        g.add(kante);
      }
    }
    if (g.children.length === 0) return null;
    g.traverse((o) => { o.userData.id = k.id; });   // (der Halter des Netzes trägt die Id der Unterlage; das Mesh darunter ist nie ein Treffer)
    g.userData.original = g.userData.materialien.map((m) => ({ m, farbe: m.color.clone(), emissiv: m.emissive.clone() }));
    return g;
  } catch (e) {
    console.error('3D: Körper nicht darstellbar', k?.id, e);
    return null;
  }
}

/** Hervorhebung setzen: modus null | 'auswahl' | 'fremd'; farbe = THREE.Color der Hervorhebung. */
export function hervorheben(g, modus, farbe) {
  for (const o of g.userData.original ?? []) {
    if (!modus) { o.m.color.copy(o.farbe); o.m.emissive.copy(o.emissiv); continue; }
    o.m.color.copy(o.farbe).lerp(farbe, modus === 'auswahl' ? 0.55 : 0.5);
    o.m.emissive.copy(farbe).multiplyScalar(0.35);
  }
}

/** Gibt Geometrien und Materialien eines Körpers frei. */
export function freigeben(g) {
  g.traverse((o) => {
    if (!o.geometry?.userData?.geteilt) o.geometry?.dispose?.();   // geteilte Netze gehören dem Zwischenspeicher der Ansicht
    const m = o.material;
    if (Array.isArray(m)) m.forEach((x) => x.dispose()); else m?.dispose?.();
  });
}
