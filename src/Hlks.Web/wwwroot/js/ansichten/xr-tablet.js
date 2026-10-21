// Modul XR-C: das Tablet an der linken Hand – eine durchscheinende Hologramm-Fläche im XR-Raum (Canvas-Textur; HTMLMesh/InteractiveGroup liegen nicht in lib/three/addons,
// und es gibt keine CDN-Abhängigkeit). Die Geste, die Lage und die Treffer rechnet xr-greifen-logik.js; hier stehen die Darstellung (Stil xr-holo.js) und die Tasten.
// Zwei Flächen: der Grund (dunkel, halbtransparent, Raster, Scanlinien, Cyan-Linien und -Schrift) und ein weicher, additiv gemischter Leuchtrand.
// Beim Öffnen baut sich das Tablet von der Hand aus auf (Skalieren und Einblenden), beim Antippen leuchtet die Taste kurz auf.

import * as THREE from 'three';
import { HOLO, holoGrund, holoTaste, holoZeile, glowTextur, rundRechteck } from './xr-holo.js';
import { TABLET_PX, TABLET_B, TABLET_H, TABLET_KOPF_H, tabletLayout, tasteBei, aufbauAnteil, aufleuchten, AUFLEUCHT_DAUER } from './xr-greifen-logik.js';

const ZEICHEN_ABSTAND = 1 / 12;   // s: das Band läuft mit etwa 12 Bildern je Sekunde
const SCHLIESS_DAUER = 0.15;

/**
 * o: { szene, eigenschaften() → Zeilen, messZeilen() → Zeilen, tastenZustand() → { id: bool }, aktion(id), zeit?() → s, layout?() → Tasten | null (XR-H: andere Seite im Sitzungsraum) }
 * Rückgabe { offen, anteil, oeffnen(), schliessen(), bild(dt, lage), tippen(u, v), taste(id), zustand(), freigeben(), gruppe, layout }
 */
export function erzeugeTablet(o) {
  const layoutStandard = tabletLayout();
  const aktuellesLayout = () => o.layout?.() ?? layoutStandard;   // XR-H: im Sitzungsraum zeigt das Tablet die Etagen
  const SK = 2;   // Textur in doppelter Auflösung (scharfe Schrift)
  const canvas = typeof document !== 'undefined' ? document.createElement('canvas') : null;
  if (canvas) { canvas.width = TABLET_PX.b * SK; canvas.height = TABLET_PX.h * SK; }
  const ctx = canvas?.getContext('2d') ?? null;
  const tex = canvas ? new THREE.CanvasTexture(canvas) : null;
  if (tex) { tex.colorSpace = THREE.SRGBColorSpace; tex.anisotropy = 4; }
  const grundMat = new THREE.MeshBasicMaterial({ map: tex, transparent: true, depthWrite: false, depthTest: false, side: THREE.DoubleSide });
  const grund = new THREE.Mesh(new THREE.PlaneGeometry(TABLET_B, TABLET_H), grundMat);
  grund.renderOrder = 990;
  const glowTex = glowTextur(TABLET_PX.b * 0.4, TABLET_PX.h * 0.4);   // Hochformat, wie das Tablet
  const glowMat = new THREE.MeshBasicMaterial({ map: glowTex, transparent: true, depthWrite: false, depthTest: false, blending: THREE.AdditiveBlending, side: THREE.DoubleSide });
  const glow = new THREE.Mesh(new THREE.PlaneGeometry(TABLET_B * 1.22, TABLET_H * 1.26), glowMat);
  glow.position.z = -0.002; glow.renderOrder = 989;
  const gruppe = new THREE.Group();
  gruppe.name = 'vr-tablet';
  gruppe.add(glow, grund);
  gruppe.visible = false;
  o.szene.add(gruppe);

  let offen = false, schliessend = false;
  let t = 0, schliessT = 0, anteil = 0;
  let uhr = 0, letztesZeichnen = -1, sig = '';
  const leuchten = new Map();   // Taste → Zeit des Antippens
  const tmpM = new THREE.Matrix4(), tmpP = new THREE.Vector3(), tmpQ = new THREE.Quaternion(), eins = new THREE.Vector3(1, 1, 1);
  let letzteLage = null, startMitte = null;
  const now = () => o.zeit?.() ?? uhr;

  function zeichnen(zwang = false) {
    if (!ctx) return;
    const tz = o.tastenZustand?.() ?? {};
    const eig = o.eigenschaften?.() ?? [];
    const mz = o.messZeilen?.() ?? [];
    const layout = aktuellesLayout();
    const neu = JSON.stringify([tz, eig, mz, layout.map((k) => k.id + k.text)]);
    if (!zwang && neu === sig && now() - letztesZeichnen < ZEICHEN_ABSTAND) return;   // sonst höchstens 12 Bilder je Sekunde (Band, Aufleuchten)
    sig = neu; letztesZeichnen = now();
    ctx.setTransform(SK, 0, 0, SK, 0, 0);
    holoGrund(ctx, TABLET_PX.b, TABLET_PX.h, now());
    // Kopf: Titel und Eigenschaften des gewählten bzw. gegriffenen Elements (lesend)
    holoZeile(ctx, 'KICAD · VR', 20, 36, { gross: true, farbe: HOLO.cyan });
    const zeilen = mz.length ? mz : eig;
    zeilen.slice(0, 3).forEach((z, i) => holoZeile(ctx, z, 20, 62 + i * 21, { max: TABLET_PX.b - 40, farbe: mz.length ? '#ffffff' : HOLO.hell }));
    ctx.strokeStyle = 'rgba(56, 232, 255, 0.45)'; ctx.lineWidth = 1;
    ctx.beginPath(); ctx.moveTo(16, TABLET_KOPF_H - 8); ctx.lineTo(TABLET_PX.b - 16, TABLET_KOPF_H - 8); ctx.stroke();
    for (const k of layout) {
      const a = aufleuchten(now() - (leuchten.get(k.id) ?? -10));
      holoTaste(ctx, k, { aufleuchten: a, an: !!tz[k.id], platzhalter: !!k.platzhalter, zusatz: k.platzhalter ? 'folgt' : '' });
    }
    tex.needsUpdate = true;
  }

  function lageAnwenden(lage) {
    if (!lage) return;
    letzteLage = lage;
    const ziel = new THREE.Vector3(lage.mitte.x, lage.mitte.y, lage.mitte.z);
    const von = startMitte ?? ziel;
    const a = Math.min(1, anteil);
    tmpP.copy(von).lerp(ziel, Math.min(1, a * 1.25));
    tmpM.makeBasis(new THREE.Vector3(lage.rechts.x, lage.rechts.y, lage.rechts.z), new THREE.Vector3(lage.oben.x, lage.oben.y, lage.oben.z), new THREE.Vector3(lage.normale.x, lage.normale.y, lage.normale.z));
    tmpQ.setFromRotationMatrix(tmpM);
    gruppe.position.copy(tmpP);
    gruppe.quaternion.copy(tmpQ);
    const s = Math.max(0.001, anteil);
    gruppe.scale.set(s, s, s);
    gruppe.updateMatrixWorld(true);
  }
  function deckkraft() {
    const a = Math.min(1, Math.max(0, anteil));
    grundMat.opacity = a; glowMat.opacity = a * (0.85 + 0.15 * Math.sin(now() * 3));
  }

  return {
    get offen() { return offen && !schliessend; },
    get anteil() { return anteil; },
    gruppe, get layout() { return aktuellesLayout(); },
    /** Öffnen: von der Hand aus aufbauen. handMitte = Punkt, von dem aus es wächst. */
    oeffnen(handMitte = null) {
      if (offen && !schliessend) return;
      offen = true; schliessend = false; t = 0;
      startMitte = handMitte ? new THREE.Vector3(handMitte.x, handMitte.y, handMitte.z) : null;
      gruppe.visible = true; zeichnen(true);
    },
    schliessen() { if (offen && !schliessend) { schliessend = true; schliessT = 0; } },
    /** Je Bild: dt in s, lage = aktuelle Lage über der Hand (oder null: unverändert). */
    bild(dt, lage) {
      uhr += dt;
      if (!offen) return;
      if (schliessend) {
        schliessT += dt; anteil = Math.max(0, anteil - dt / SCHLIESS_DAUER);
        if (schliessT >= SCHLIESS_DAUER || anteil <= 0) { offen = false; schliessend = false; anteil = 0; gruppe.visible = false; return; }
      } else { t += dt; anteil = aufbauAnteil(t); }
      lageAnwenden(lage ?? letzteLage);
      deckkraft();
      zeichnen();
    },
    /** Tippen an der Stelle (u, v): die Taste leuchtet auf und die Aktion läuft; Platzhalter tun nichts. Rückgabe die Taste oder null. */
    tippen(u, v) {
      if (!offen || schliessend) return null;
      const k = tasteBei(u, v, aktuellesLayout());
      if (!k) return null;
      leuchten.set(k.id, now());
      if (!k.platzhalter) o.aktion?.(k.id);
      zeichnen(true);
      return k;
    },
    taste: (id) => aktuellesLayout().find((k) => k.id === id) ?? null,
    zustand: () => ({ offen: offen && !schliessend, anteil: Math.round(anteil * 1000) / 1000, sichtbar: gruppe.visible, mitte: gruppe.visible ? { x: gruppe.position.x, y: gruppe.position.y, z: gruppe.position.z } : null,
      leuchten: Object.fromEntries([...leuchten].map(([k, ts]) => [k, Math.round(aufleuchten(now() - ts) * 100) / 100])) }),
    freigeben() {
      gruppe.removeFromParent();
      grund.geometry.dispose(); glow.geometry.dispose(); grundMat.dispose(); glowMat.dispose(); tex?.dispose(); glowTex?.dispose();
    },
  };
}
