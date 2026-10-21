// Modul XR, Teil XR-H «Sitzungsraum mit Modell auf dem Tisch»: ein eigener, dunkler virtueller Raum («Black Mirror»), in dem das Modell der 3D-Ansicht verkleinert auf einem
// Glastisch liegt. Wechsel über das Tablet (Taste «Sitzungsraum» ↔ «Begehen»); die Etagen wählt das Tablet, das Modell dreht und zoomt die Hand.
// Die Rechenregeln (Einpassen, Gesten, Schnitthöhe, Tablet-Seite) stehen in xr-sitzung-logik.js (Node-getestet: xr-sitzung.test.mjs); hier stehen three, die Szene und die Anschlüsse.
//
// Keine zweite Geometrie: die Körpergruppe der 3D-Ansicht (`modell`) wird skaliert in einen Halter auf dem Tisch umgehängt und beim Verlassen exakt zurückgestellt
// (Elternknoten, Lage, Drehung, Grösse, Hintergrund, Nebel, Lichter, ausgeblendete Objekte). Die Clipping-Ebenen der Materialien gibt er nicht zurück, sondern an den
// Schnittbereich der Ansicht (N13) ab (`uebergeben`): der bewertet neu, ob er gerade gilt (im Begehen pausiert) – Befund Brillentest 21.10.2026, siehe verlassen(). Der Raum steht um den Kopf beim Betreten
// (Boden = OK Boden des Rigs, Blick nach vorn = Blickrichtung); das Rig bleibt dort, wo es ist, das Begehen pausiert, bis man zurückwechselt.
// Der Etagenschnitt ist eine eigene waagrechte Clipping-Ebene an den Materialien (wie bei N13, aber ohne den gespeicherten Schnittbereich der Ansicht anzufassen).
//
// Griffe: zwei leuchtende Kugeln links und rechts des Modells (Lage und Regeln: xr-sitzung-logik.js); gegriffen wird nur mit Daumen und Zeigefinger (Pinch) bzw. dem Abzug.
//
// Prüfhilfe: .d3-wurzel[data-xr-sitzung] (JSON: Massstab, Lage, Etage, Schnitthöhe, Ebene in der Welt, Modus, Griffe (Welt, Hover, gehalten), Tisch).

import * as THREE from 'three';
import { derKlasse } from '../zustand.js';
import { HOLO, etikett, glowTextur, rundSchein } from './xr-holo.js';
import {
  TISCH, TISCH_MITTE, RAUM, GRIFF_DURCHMESSER_M, einpassen, massstabText, griffPunkte, griffTreffer, gestenNeu, gestenSchritt, geschosseAufbereiten, schnittHoeheMm, schnittEbeneY,
  kopfZeilen, sitzungsLayout, sitzungsTasten, modellMitte,
} from './xr-sitzung-logik.js';

const SCHREIB_MS = 250;
const SYNC_S = 0.5;          // so oft werden die Clipping-Ebenen an die Materialien nachgeführt (neue Körper)
const ETAGEN_S = 1.5;        // so oft wird die Etagenliste aus dem Modell neu gelesen
const rund = (x, n = 4) => { const f = 10 ** n; return Math.round(x * f) / f; };
const ID_HALTER = 'vr-sitzung';
/** Aussehen der Griffe je Stufe: frei, Hand in der Nähe (Hover), gehalten. */
const GRIFF_STUFEN = [
  { farbe: 0x38e8ff, deckkraft: 0.6, schein: 0.45, scheinGroesse: 0.09 },
  { farbe: 0xc8f8ff, deckkraft: 0.95, schein: 0.85, scheinGroesse: 0.14 },
  { farbe: 0xffffff, deckkraft: 1, schein: 1, scheinGroesse: 0.21 },
];
/** Hintergrund des Sitzungsraums (dunkles Blaugrau statt fast schwarz; Christian 21.10.2026: «etwas weniger hart»). */
export const RAUM_HINTERGRUND = 0x0d1219;

/** Geschossdaten aus dem Modell der Oberfläche: dieselbe Quelle wie «Auf Geschoss» (derKlasse('Geschoss'), werte.kote/hoehe) und die Decken (Oberkante aus den Punkten, Dicke). */
function elementeLesen(o) {
  const geschosse = (o.geschosse?.() ?? derKlasse('Geschoss')).map((e) => ({ kennzeichen: e.kennzeichen, name: e.name, kote: e.werte?.kote, hoehe: e.werte?.hoehe, teilVon: e.teilVon }));
  const decken = (o.decken?.() ?? derKlasse('Decke')).map((e) => {
    const z = (e.geometrie?.punkte ?? []).map((p) => (Array.isArray(p) && p.length > 2 ? p[2] : 0));
    return { oberkante: z.length ? Math.max(...z) : NaN, dicke: e.geometrie?.dicke };
  });
  return geschosseAufbereiten(geschosse, decken);
}

/** Box der sichtbaren Teile (ausgeblendete IFC-Netze zählen nicht), in der Welt. */
function sichtbareBox(gruppe) {
  const box = new THREE.Box3(), b = new THREE.Box3();
  gruppe.updateWorldMatrix(true, true);
  const rec = (obj) => {
    if (!obj.visible) return;
    if ((obj.isMesh || obj.isLine || obj.isPoints) && obj.geometry) {
      if (!obj.geometry.boundingBox) obj.geometry.computeBoundingBox();
      b.copy(obj.geometry.boundingBox).applyMatrix4(obj.matrixWorld);
      if (!b.isEmpty()) box.union(b);
    }
    for (const c of obj.children) rec(c);
  };
  rec(gruppe);
  return box;
}

/**
 * o: { szene, modell, renderer, wurzel, rig() → THREE.Group, kopf() → Welt-Kopf | null, blick() → Gieren in der Welt (rad) | null, boden() → OK Boden (m),
 *      ifc: { an(), setzen(bool) }, anfordern(), meldung?(text, art), geschosse?(), decken?(),
 *      uebergeben?() (nach dem Verlassen: Schnittbereich N13 und Durchgang N22 neu bewerten lassen) }
 * Rückgabe { aktiv, betreten(), verlassen(), umschalten(), aktion(id) → bool, layout(), tastenZustand(), kopfZeilen(), bild(dt, seiten), vorRender(), zustand(), abbauen() }
 */
export function erzeugeSitzung(o) {
  const { szene, modell, renderer, wurzel } = o;
  let aktiv = false;
  let lage = einpassen(null), groesse = { b: 1, t: 1, h: 1 }, anker = { x: 0, z: 0 }, unten = 0;
  let wahl = { geschoss: null, deckeZeigen: false };
  let gesten = gestenNeu(lage);
  let etagen = [], etagenSig = '', layoutListe = null, etagenUhr = 0;
  let uhr = 0, syncUhr = 0, letztesSchreiben = -1e9, schmutzig = true, materialSchmutzig = true;
  let ifcVorher = true;
  let gespeichert = null;
  let raum = null, halter = null, bauteile = null, massLabel = null, punktLicht = null;
  let griffe = null;                               // [{ gruppe, kern, ring, schein }] – die beiden Griffe (Anzeige)
  let griffNah = new Set();                        // Griffe, denen sich gerade eine Hand bis GRIFF_HOVER_M nähert
  const ebene = new THREE.Plane(new THREE.Vector3(0, -1, 0), 0);
  const EBENEN = [ebene];
  let fuehrung = null;                             // Material, an dem die Ebenen zuletzt kontrolliert wurden
  const tmp = new THREE.Vector3();

  // ---------------------------------------------------------------------------------------------- Raum bauen
  function raumBauen() {
    const g = new THREE.Group();
    g.name = ID_HALTER;
    const entsorgen = [];
    const mat = (m) => { entsorgen.push(m); return m; };
    // Hülle: Wände und Decke matt anthrazit-blau, Boden dunkel und glänzend (leicht durchscheinend, darunter spiegeln sich die Lichtlinien).
    // Christian 21.10.2026 zum ersten Prüfbild: «ev. etwas weniger hart. aber tron optik ist gut» → nicht mehr fast schwarz, Linien und Kontrast gedämpft.
    const wand = mat(new THREE.MeshStandardMaterial({ color: 0x1b222c, roughness: 0.95, metalness: 0, side: THREE.BackSide }));
    const decke = mat(new THREE.MeshStandardMaterial({ color: 0x151b24, roughness: 0.95, metalness: 0, side: THREE.BackSide }));
    const boden = mat(new THREE.MeshStandardMaterial({ color: 0x111820, roughness: 0.15, metalness: 0.6, transparent: true, opacity: 0.85, side: THREE.BackSide }));
    const huelle = new THREE.Mesh(new THREE.BoxGeometry(RAUM.breite, RAUM.hoehe, RAUM.tiefe), [wand, wand, decke, boden, wand, wand]);   // +x, −x, +y, −y, +z, −z
    huelle.position.set(0, RAUM.hoehe / 2, -(RAUM.tiefe / 2 - RAUM.hinten));
    huelle.name = 'sitzung-huelle';
    g.add(huelle);
    entsorgen.push(huelle.geometry);
    // schmale weisse Lichtlinien in Decke und Wänden (eine Instanz-Gruppe) und ihr Spiegelbild unter dem Boden
    const z0 = -(RAUM.tiefe - RAUM.hinten), z1 = RAUM.hinten, zm = (z0 + z1) / 2, hx = RAUM.breite / 2, hy = RAUM.hoehe;
    const linien = [];   // [mx, my, mz, sx, sy, sz]
    for (const x of [-3, -1.5, 0, 1.5, 3]) linien.push([x, hy - 0.01, zm, 0.04, 0.02, RAUM.tiefe - 0.2]);                     // Decke, längs
    for (const y of [0.04, hy - 0.05]) {                                                                                          // Wände, umlaufend
      linien.push([0, y, z0 + 0.01, RAUM.breite - 0.02, 0.025, 0.02], [0, y, z1 - 0.01, RAUM.breite - 0.02, 0.025, 0.02]);
      linien.push([-hx + 0.01, y, zm, 0.02, 0.025, RAUM.tiefe - 0.02], [hx - 0.01, y, zm, 0.02, 0.025, RAUM.tiefe - 0.02]);
    }
    for (const x of [-2.5, 2.5]) linien.push([x, 1.2, z0 + 0.01, 0.03, 2.2, 0.02]);                                               // zwei senkrechte Schlitze vorn
    const lichtMat = mat(new THREE.MeshBasicMaterial({ color: 0xb8dff2, transparent: true, opacity: 0.8, side: THREE.DoubleSide }));
    const lichtGeo = new THREE.BoxGeometry(1, 1, 1);
    entsorgen.push(lichtGeo);
    const lichtMesh = (spiegel) => {
      const im = new THREE.InstancedMesh(lichtGeo, lichtMat, linien.length);
      const d = new THREE.Object3D();
      linien.forEach(([x, y, z, sx, sy, sz], i) => { d.position.set(x, spiegel ? -y : y, z); d.scale.set(sx, sy, sz); d.updateMatrix(); im.setMatrixAt(i, d.matrix); });
      im.instanceMatrix.needsUpdate = true;
      im.frustumCulled = false;
      return im;
    };
    g.add(lichtMesh(false), lichtMesh(true));
    // Glastisch: getönte Platte, leuchtende Kante in Cyan (Stil xr-holo.js), vier schlanke Beine, weicher Schein auf dem Boden
    const glas = mat(new THREE.MeshStandardMaterial({ color: 0x103042, roughness: 0.05, metalness: 0.25, transparent: true, opacity: 0.35 }));
    const platte = new THREE.Mesh(new THREE.BoxGeometry(TISCH.breite, 0.03, TISCH.tiefe), glas);
    platte.position.set(TISCH_MITTE.x, TISCH.hoehe - 0.015, TISCH_MITTE.z);
    platte.name = 'sitzung-tisch';
    g.add(platte);
    entsorgen.push(platte.geometry);
    const kante = mat(new THREE.MeshBasicMaterial({ color: new THREE.Color(HOLO.cyan) }));
    const kantenGeo = [new THREE.BoxGeometry(TISCH.breite, 0.012, 0.012), new THREE.BoxGeometry(0.012, 0.012, TISCH.tiefe)];
    entsorgen.push(...kantenGeo);
    for (const [geo, dx, dz] of [[kantenGeo[0], 0, TISCH.tiefe / 2], [kantenGeo[0], 0, -TISCH.tiefe / 2], [kantenGeo[1], TISCH.breite / 2, 0], [kantenGeo[1], -TISCH.breite / 2, 0]]) {
      const m = new THREE.Mesh(geo, kante);
      m.position.set(TISCH_MITTE.x + dx, TISCH.hoehe - 0.006, TISCH_MITTE.z + dz);
      g.add(m);
    }
    const beinMat = mat(new THREE.MeshStandardMaterial({ color: 0x111418, roughness: 0.3, metalness: 0.9 }));
    const beinGeo = new THREE.BoxGeometry(0.04, TISCH.hoehe - 0.03, 0.04);
    entsorgen.push(beinGeo);
    for (const sx of [-1, 1]) for (const sz of [-1, 1]) {
      const b = new THREE.Mesh(beinGeo, beinMat);
      b.position.set(TISCH_MITTE.x + sx * (TISCH.breite / 2 - 0.1), (TISCH.hoehe - 0.03) / 2, TISCH_MITTE.z + sz * (TISCH.tiefe / 2 - 0.1));
      g.add(b);
    }
    const scheinTex = glowTextur();
    const schein = new THREE.Mesh(new THREE.PlaneGeometry(TISCH.breite * 1.5, TISCH.tiefe * 1.6).rotateX(-Math.PI / 2),
      mat(new THREE.MeshBasicMaterial({ map: scheinTex, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, opacity: 0.3 })));
    schein.position.set(TISCH_MITTE.x, 0.004, TISCH_MITTE.z);
    g.add(schein);
    entsorgen.push(schein.geometry, scheinTex);
    // ein Licht über dem Tisch (Glanz auf dem Boden); keine Schatten
    punktLicht = new THREE.PointLight(0xcfeeff, 10, 0, 2);
    punktLicht.position.set(TISCH_MITTE.x, 2.6, TISCH_MITTE.z);
    g.add(punktLicht);
    // Massstab als Hologramm-Schrift an der Tischkante
    massLabel = etikett('', { breite: 0.3 });
    massLabel.sprite.position.set(TISCH_MITTE.x + TISCH.breite / 2 - 0.2, TISCH.hoehe + 0.07, TISCH_MITTE.z + TISCH.tiefe / 2 + 0.03);
    g.add(massLabel.sprite);
    // Halter des Modells
    halter = new THREE.Group();
    halter.name = 'sitzung-modellhalter';
    g.add(halter);
    // Griffe links und rechts des Modells (Hologramm-Stil: Kugel, Ring und weicher Schein in Cyan; Lage und Farbe setzt griffeZeigen)
    const kugelSchein = rundSchein();
    if (kugelSchein) entsorgen.push(kugelSchein);
    const kernGeo = new THREE.SphereGeometry(GRIFF_DURCHMESSER_M / 2, 20, 14), ringGeo = new THREE.TorusGeometry(GRIFF_DURCHMESSER_M * 0.9, 0.0025, 8, 40).rotateX(Math.PI / 2);
    entsorgen.push(kernGeo, ringGeo);
    griffe = [0, 1].map((i) => {
      const gruppe = new THREE.Group();
      gruppe.name = `sitzung-griff-${i}`;
      const kern = new THREE.Mesh(kernGeo, mat(new THREE.MeshBasicMaterial({ color: new THREE.Color(HOLO.cyan), transparent: true, opacity: 0.6 })));
      const ring = new THREE.Mesh(ringGeo, mat(new THREE.MeshBasicMaterial({ color: new THREE.Color(HOLO.cyan), transparent: true, opacity: 0.6 })));
      const glanz = new THREE.Sprite(mat(new THREE.SpriteMaterial({ map: kugelSchein, color: 0xffffff, transparent: true, depthWrite: false, blending: THREE.AdditiveBlending, opacity: 0.4 })));
      for (const m of [kern, ring, glanz]) m.renderOrder = 5;
      gruppe.add(kern, ring, glanz);
      g.add(gruppe);
      return { gruppe, kern, ring, schein: glanz };
    });
    return { gruppe: g, entsorgen };
  }

  // ---------------------------------------------------------------------------------------------- Etagen und Schnitt
  function etagenLesen(erzwingen = false) {
    if (!erzwingen && uhr - etagenUhr < ETAGEN_S) return;
    etagenUhr = uhr;
    let neu = [];
    try { neu = elementeLesen(o); } catch (e) { console.warn('Sitzungsraum: Geschosse nicht gelesen', e?.message ?? e); }
    const sig = JSON.stringify(neu.map((g) => [g.kennzeichen, g.kote, g.hoehe, g.deckeOK, g.dicke]));
    if (sig === etagenSig) return;
    etagenSig = sig; etagen = neu; layoutListe = sitzungsLayout(etagen); schmutzig = true; materialSchmutzig = true;
    if (wahl.geschoss && !etagen.some((g) => g.kennzeichen === wahl.geschoss)) wahl = { ...wahl, geschoss: null };
  }
  const geschossGewaehlt = () => (wahl.geschoss ? etagen.find((g) => g.kennzeichen === wahl.geschoss) ?? null : null);

  /** Höhe der Clipping-Ebene im Raum (m) bzw. null ohne Schnitt. */
  function ebeneRaumY() {
    const g = geschossGewaehlt();
    if (!g) return null;
    return schnittEbeneY({ schnittMm: schnittHoeheMm(g, wahl.deckeZeigen), deckeZeigen: wahl.deckeZeigen, modellUnten: unten, massstab: lage.massstab });
  }

  /** Hängt die Ebene an alle Materialien des Modells (bzw. nimmt sie weg). Der Stand davor (Ebenen des Schnittbereichs) wird nicht gemerkt: beim Verlassen entscheidet der Schnittbereich neu. */
  function ebeneAnwenden() {
    const soll = wahl.geschoss && geschossGewaehlt() ? EBENEN : null;
    const material = [];
    modell.traverse((m) => {
      const mat = m.material;
      if (!mat) return;
      for (const x of Array.isArray(mat) ? mat : [mat]) material.push(x);
    });
    for (const x of material) {
      if (x.clippingPlanes !== soll) { x.clippingPlanes = soll; x.needsUpdate = true; }
    }
    fuehrung = material[0] ?? null;
  }

  // ---------------------------------------------------------------------------------------------- Betreten und Verlassen
  function betreten() {
    if (aktiv) return true;
    const rig = o.rig();
    if (!rig) return false;
    const kopf = o.kopf() ?? { x: rig.position.x, z: rig.position.z };
    const blick = o.blick?.() ?? rig.rotation.y;
    const boden = o.boden?.() ?? rig.position.y;
    // IFC-Unterlage aus (Bildrate); vor der Messung, damit nur das sichtbare Modell eingepasst wird
    ifcVorher = !!o.ifc?.an();
    o.ifc?.setzen(false);
    const box = sichtbareBox(modell);
    const leer = box.isEmpty();
    groesse = leer ? { b: 1, t: 1, h: 1 } : { b: box.max.x - box.min.x, t: box.max.z - box.min.z, h: box.max.y - box.min.y };
    anker = leer ? { x: 0, z: 0 } : { x: (box.min.x + box.max.x) / 2, z: (box.min.z + box.max.z) / 2 };
    unten = leer ? 0 : box.min.y;
    // Stand sichern
    gespeichert = {
      parent: modell.parent, position: modell.position.clone(), quaternion: modell.quaternion.clone(), scale: modell.scale.clone(), matrixAutoUpdate: modell.matrixAutoUpdate,
      hintergrund: szene.background, nebel: szene.fog, lokalesClipping: renderer.localClippingEnabled,
      lichter: szene.children.filter((c) => c.isLight).map((c) => ({ c, intensitaet: c.intensity })),
      versteckt: [],
    };
    // alles andere aus der Szene ausblenden (Hüllflächen, Schnittgriffe …), ausser Rig, XR-Werkzeug und Lichter
    for (const c of szene.children) {
      if (c.isLight || c === modell || c === rig || c.name === 'vr-rig' || c.name === 'vr-xr' || !c.visible) continue;
      gespeichert.versteckt.push(c); c.visible = false;
    }
    // Raum
    const r = raumBauen();
    raum = r.gruppe; bauteile = r.entsorgen;
    raum.position.set(kopf.x, boden, kopf.z);
    raum.rotation.set(0, blick - Math.PI / 2, 0);
    szene.add(raum);
    szene.background = new THREE.Color(RAUM_HINTERGRUND);
    szene.fog = new THREE.Fog(0x111821, 4.5, 18);
    for (const { c } of gespeichert.lichter) c.intensity *= c.isAmbientLight ? 0.65 : 0.8;
    renderer.localClippingEnabled = true;
    // Modell auf den Tisch
    lage = einpassen(groesse);
    gesten = gestenNeu(lage);
    wahl = { geschoss: null, deckeZeigen: false };
    modell.position.set(-anker.x, -unten, -anker.z);
    modell.quaternion.identity(); modell.scale.set(1, 1, 1);
    halter.add(modell);
    aktiv = true;
    etagenLesen(true);
    schmutzig = true; materialSchmutzig = true;
    anwenden(true);
    if (!lage.passt) o.meldung?.('Das Modell ist grösser als der Tisch bei 1:200 – es ragt über die Platte', 'info');
    o.anfordern?.();
    schreiben(true);
    return true;
  }

  function verlassen(ende = false) {
    if (!aktiv) return false;
    aktiv = false;
    const g = gespeichert; gespeichert = null;
    // Clipping der Materialien: die eigene Ebene weg, danach entscheidet der Schnittbereich (N13). Früher gab der Sitzungsraum die Ebenen zurück, die er beim Betreten vorfand: waren das
    // die des Schnittbereichs und hatte sich dessen Zustand inzwischen geändert (Begehen pausiert ihn, die Taste «Schnittbereich» schaltet ihn), kamen veraltete Ebenen zurück – bei einem
    // Schnittbereich, der den Standpunkt nicht enthält, sieht man dann nur Schwarz (Befund Brillentest 21.10.2026).
    modell.traverse((m) => {
      const mat = m.material;
      if (!mat) return;
      for (const x of Array.isArray(mat) ? mat : [mat]) if (x.clippingPlanes === EBENEN) { x.clippingPlanes = null; x.needsUpdate = true; }
    });
    if (g) {
      modell.removeFromParent();
      (g.parent ?? szene).add(modell);
      modell.position.copy(g.position); modell.quaternion.copy(g.quaternion); modell.scale.copy(g.scale); modell.matrixAutoUpdate = g.matrixAutoUpdate;
      modell.updateMatrixWorld(true);
      szene.background = g.hintergrund; szene.fog = g.nebel; renderer.localClippingEnabled = g.lokalesClipping;
      for (const { c, intensitaet } of g.lichter) c.intensity = intensitaet;
      for (const c of g.versteckt) c.visible = true;
    }
    if (raum) {
      raum.removeFromParent();
      massLabel?.freigeben(); massLabel = null;
      for (const e of bauteile ?? []) e.dispose?.();
      raum = null; halter = null; bauteile = null; punktLicht = null; griffe = null; griffNah = new Set();
    }
    fuehrung = null;
    try { o.uebergeben?.(); } catch (e) { console.warn('Sitzungsraum: Übergabe an den Schnittbereich', e?.message ?? e); }   // Schnittbereich (N13) und Durchgang (N22) bewerten neu
    if (!ende) o.ifc?.setzen(ifcVorher);
    wurzel.dataset.xrSitzung = '';
    delete wurzel.dataset.xrSitzung;
    o.anfordern?.();
    return true;
  }

  // ---------------------------------------------------------------------------------------------- Lage anwenden
  /** Halter, Massstabsschrift und Clipping-Ebene nach einer Änderung von Lage oder Etage. */
  function anwenden(erzwingen = false) {
    if (!aktiv || !halter) return;
    if (!erzwingen && !schmutzig) return;
    schmutzig = false;
    const m = modellMitte(lage);
    halter.position.set(m.x, TISCH.hoehe, m.z);
    halter.rotation.set(0, lage.drehung, 0);
    const s = 1 / lage.massstab;
    halter.scale.set(s, s, s);
    halter.updateMatrixWorld(true);
    massLabel?.setzen(`Massstab ${massstabText(lage.massstab)}\n${geschossGewaehlt() ? `${geschossGewaehlt().kennzeichen}${wahl.deckeZeigen ? ' mit Decke' : ''}` : 'Alle Etagen'}`);
    const y = ebeneRaumY();
    if (y !== null) { raum.updateMatrixWorld(true); tmp.set(0, y, 0); raum.localToWorld(tmp); ebene.constant = tmp.y; }
    if (materialSchmutzig) { materialSchmutzig = false; ebeneAnwenden(); syncUhr = uhr; }
    griffeZeigen();
    o.anfordern?.();
  }

  /** Die beiden Griffe an die Lage des Modells setzen und einfärben: frei (dunkel), Hand nahe (heller), gehalten (leuchtet deutlich). */
  function griffeZeigen() {
    if (!griffe) return;
    const gp = griffPunkte(lage, groesse);
    gp.forEach((p, i) => {
      const v = griffe[i];
      v.gruppe.position.set(p.x, p.y, p.z);
      const gehalten = gesten.besitzt.links === i || gesten.besitzt.rechts === i;
      const stufe = gehalten ? 2 : griffNah.has(i) ? 1 : 0;
      const s = GRIFF_STUFEN[stufe];
      v.kern.material.color.setHex(s.farbe); v.kern.material.opacity = s.deckkraft;
      v.ring.material.color.setHex(s.farbe); v.ring.material.opacity = s.deckkraft;
      v.schein.material.opacity = s.schein; v.schein.scale.set(s.scheinGroesse, s.scheinGroesse, 1);
    });
  }

  // ---------------------------------------------------------------------------------------------- Tablet
  function aktion(id) {
    if (id === 'raum') { if (aktiv) verlassen(); else betreten(); return true; }
    if (!aktiv) return false;
    if (id === 'alle') wahl = { ...wahl, geschoss: null };
    else if (id.startsWith('g:')) {
      const kz = id.slice(2);
      if (!etagen.some((g) => g.kennzeichen === kz)) return true;
      wahl = { ...wahl, geschoss: kz };
    } else if (id === 'decke') wahl = { ...wahl, deckeZeigen: !wahl.deckeZeigen };
    else if (id === 'zuruecksetzen') { lage = einpassen(groesse); gesten = gestenNeu(lage); }
    else return false;   // IFC-Unterlage und «VR beenden» erledigt das Tablet wie sonst
    schmutzig = true; materialSchmutzig = true;
    anwenden();
    schreiben(true);
    return true;
  }

  // ---------------------------------------------------------------------------------------------- Hände
  /** Pinch-Punkt einer Hand in der Welt: Mitte zwischen Daumen- und Zeigefingerspitze; Controller: ihre Lage. */
  function pinchPunkt(S) {
    const d = S.gelenke?.['thumb-tip'], i = S.gelenke?.['index-finger-tip'];
    if (d && i) return { x: (d.x + i.x) / 2, y: (d.y + i.y) / 2, z: (d.z + i.z) / 2 };
    return S.ort ?? null;
  }
  const lokal = (p) => { tmp.set(p.x, p.y, p.z); raum.worldToLocal(tmp); return { x: tmp.x, y: tmp.y, z: tmp.z }; };

  /** Je Bild nach der Eingabe: seiten = { left, right } der Auswertung in xr-greifen.js ({ pinch, griff (Faust), ort, gelenke, strahl, … } in der Welt). */
  function bild(dt, seiten) {
    if (!aktiv || !raum) return;
    uhr += dt;
    raum.updateMatrixWorld(true);
    const gp = griffPunkte(lage, groesse);
    const h = {};
    griffNah = new Set();
    for (const [seite, name] of [['left', 'links'], ['right', 'rechts']]) {
      const S = seiten?.[seite];
      const p = S ? pinchPunkt(S) : null;
      if (!p) { h[name] = null; continue; }
      const pl = lokal(p);
      let strahl = null;   // Controller: der Zielstrahl zählt wie die Nähe (Abzug am Griff)
      if (S.art === 'controller' && S.strahl?.u && S.strahl?.r) {
        const u = lokal(S.strahl.u), e = lokal({ x: S.strahl.u.x + S.strahl.r.x, y: S.strahl.u.y + S.strahl.r.y, z: S.strahl.u.z + S.strahl.r.z });
        strahl = { u, r: { x: e.x - u.x, y: e.y - u.y, z: e.z - u.z } };
      }
      const nah = griffTreffer(pl, strahl, gp);
      if (nah !== null) griffNah.add(nah);
      // nur Daumen und Zeigefinger (bzw. der Abzug) greifen; eine Faust greift nichts
      const pinch = !!S.pinch && !(S.art === 'hand' && S.griff);
      h[name] = { griff: pinch, nah, x: pl.x, z: pl.z };
    }
    const vorher = lage;
    gesten = gestenSchritt(gesten, h);
    lage = gesten.lage;
    if (lage !== vorher) schmutzig = true;
    etagenLesen();
    anwenden();
    griffeZeigen();
    schreiben();
  }

  /** Vor jedem Bild (nach dem Schnittbereich der Ansicht): die Ebenen kontrollieren – die Ansicht setzt sie bei neuen Körpern zurück. */
  function vorRender() {
    if (!aktiv) return;
    if (fuehrung && fuehrung.clippingPlanes !== (wahl.geschoss && geschossGewaehlt() ? EBENEN : null)) materialSchmutzig = true;
    else if (uhr - syncUhr > SYNC_S) materialSchmutzig = true;
    if (materialSchmutzig) anwenden(true);
  }

  // ---------------------------------------------------------------------------------------------- Prüfhilfe
  function zustand() {
    if (!aktiv) return { aktiv: false };
    const g = geschossGewaehlt();
    const y = ebeneRaumY();
    let ebeneWelt = null, tischWelt = null;
    if (raum) {
      raum.updateMatrixWorld(true);
      tischWelt = raum.localToWorld(new THREE.Vector3(TISCH_MITTE.x, TISCH.hoehe, TISCH_MITTE.z));
      if (y !== null) ebeneWelt = raum.localToWorld(new THREE.Vector3(0, y, 0)).y;
    }
    const griffeWelt = raum ? griffPunkte(lage, groesse).map((p) => {
      const w = raum.localToWorld(new THREE.Vector3(p.x, p.y, p.z));
      return { seite: p.seite, x: rund(w.x), y: rund(w.y), z: rund(w.z), hover: griffNah.has(p.index) && gesten.besitzt.links !== p.index && gesten.besitzt.rechts !== p.index, gehalten: gesten.besitzt.links === p.index || gesten.besitzt.rechts === p.index };
    }) : [];
    let geclippt = 0, material = 0;
    modell.traverse((m) => { const mat = m.material; if (!mat) return; for (const x of Array.isArray(mat) ? mat : [mat]) { material++; if (x.clippingPlanes === EBENEN) geclippt++; } });
    return {
      aktiv: true, massstab: rund(lage.massstab, 3), massstabText: massstabText(lage.massstab), drehungGrad: rund(lage.drehung / (Math.PI / 180), 2), lage: { x: rund(lage.x), z: rund(lage.z) },
      passt: lage.passt !== false, modus: gesten.modus, geschoss: wahl.geschoss, deckeZeigen: wahl.deckeZeigen,
      etagen: etagen.map((e) => ({ kz: e.kennzeichen, kote: e.kote, deckeOK: e.deckeOK, dicke: e.dicke, ohneDecke: e.ohneDecke })),
      schnittMm: g ? schnittHoeheMm(g, wahl.deckeZeigen) : null, ebeneRaum: y === null ? null : rund(y, 5), ebeneWelt: ebeneWelt === null ? null : rund(ebeneWelt, 5),
      modellUnten: rund(unten, 4), groesse: { b: rund(groesse.b, 3), t: rund(groesse.t, 3), h: rund(groesse.h, 3) },
      tisch: tischWelt ? { x: rund(tischWelt.x), y: rund(tischWelt.y, 5), z: rund(tischWelt.z) } : null,
      raum: raum ? { x: rund(raum.position.x), y: rund(raum.position.y), z: rund(raum.position.z), gieren: rund(raum.rotation.y) } : null,
      griffe: griffeWelt, material, geclippt, ifc: !!o.ifc?.an(),
    };
  }
  function schreiben(erzwingen = false) {
    if (!aktiv) return;
    if (!erzwingen && uhr - letztesSchreiben < SCHREIB_MS / 1000) return;
    letztesSchreiben = uhr;
    wurzel.dataset.xrSitzung = JSON.stringify(zustand());
  }

  return {
    get aktiv() { return aktiv; },
    betreten, verlassen, umschalten: () => (aktiv ? verlassen() : betreten()), aktion,
    layout: () => (aktiv ? layoutListe : null),
    tastenZustand: () => (aktiv ? sitzungsTasten(wahl, o.ifc?.an()) : {}),
    kopfZeilen: () => (aktiv ? kopfZeilen(lage, wahl, etagen) : null),
    bild, vorRender, zustand, schreiben,
    /** Test-Naht: Etagenwahl und Lage direkt setzen. */
    get lage() { return { ...lage }; },
    /** Sitzung beendet: alles zurückstellen (ohne die IFC-Wahl, die VR selbst zurücksetzt). */
    abbauen() { verlassen(true); },
  };
}
