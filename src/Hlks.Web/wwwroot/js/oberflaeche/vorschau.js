// 3D-Vorschaubild des offenen Projekts für die Kacheln des Startbildschirms (S1, entwurf/startbildschirm.md).
// Erzeugt im Browser aus den Körpern des Kerns (/api/koerper, ganzes Projekt, ohne die 3D-Ansicht): feste Isometrie von Südwesten,
// Architektur grau, Gebäudetechnik in Systemfarben, durchsichtiger Hintergrund, 640 × 400. Gespeichert wird über
// POST /api/projekt/vorschau als vorschau.png im Projektordner.
// Wann: nach dem Laden eines Projekts und nach Änderungen (entprellt), danach höchstens alle 10 Minuten und nur bei geändertem
// Stand (Prüfsumme). Fehler stören nie: sie landen in der Konsole, der nächste Versuch kommt frühestens nach dem Mindestabstand.
// Die Entscheidungen und Farben stehen in vorschau-logik.js (rein, in Node getestet); three wird erst beim Erzeugen geladen.

import { zustand, abonnieren, merken } from '../zustand.js';
import { holen } from '../api.js';
import { BILD_BREITE, BILD_HOEHE, BLICK_VON, vorschauEntscheid, teilStil, teilZeigen, vorschauSpeicherUrl } from './vorschau-logik.js';

const NACH_LADEN_MS = 5000;     // dem Projekt Zeit zum Zeichnen lassen, bevor das Bild entsteht
const NACH_AENDERUNG_MS = 20000;
let timer = null;
let laeuft = false;
let eingerichtet = false;

const gemerktAlle = () => merken.lesen('vorschau', {});
function gemerkt(ort) { return gemerktAlle()[ort] ?? null; }
function merkeVersuch(ort, stand, ok) {
  const alle = gemerktAlle();
  alle[ort] = { stand: ok ? stand : null, zeit: Date.now() };   // bei Misserfolg ohne Stand: nach dem Mindestabstand erneut versuchen
  const behalten = Object.entries(alle).sort((a, b) => (b[1].zeit ?? 0) - (a[1].zeit ?? 0)).slice(0, 40);
  merken.schreiben('vorschau', Object.fromEntries(behalten));
}

export function einrichten() {
  if (eingerichtet) return;
  eingerichtet = true;
  abonnieren('modell', (d) => { if (!d?.alles) planen(NACH_AENDERUNG_MS); });
}

/** Nach dem Laden eines Projekts aufrufen. */
export function nachLaden() { einrichten(); planen(NACH_LADEN_MS); }

function planen(ms) {
  clearTimeout(timer);
  timer = setTimeout(() => pruefen().catch((e) => console.error('Vorschaubild', e)), ms);
}

/** Entscheidet nach Stand und Abstand, ob jetzt ein Bild entsteht, später oder nichts. */
export async function pruefen() {
  const ort = zustand.status?.ort;
  if (!zustand.status?.offen || !ort || zustand.geladen !== ort) return 'kein Projekt';
  const e = vorschauEntscheid({ gemerkt: gemerkt(ort), stand: zustand.stand?.pruefsumme, jetzt: Date.now() });
  if (e.aktion === 'warten') { planen(e.wartenMs + 1000); return 'warten'; }
  if (e.aktion === 'nichts') return 'unveraendert';
  return erzeugen();
}

/**
 * Erzeugt das Bild des geöffneten Projekts und speichert es. Ergebnis: 'gespeichert' | 'leer' | 'fehler' | 'laeuft'.
 * Wird auch von Tests aufgerufen (ohne Abstand).
 */
export async function erzeugen() {
  if (laeuft) return 'laeuft';
  const ort = zustand.status?.ort;
  const stand = zustand.stand?.pruefsumme ?? null;
  if (!ort) return 'fehler';
  laeuft = true;
  try {
    const png = await bildErzeugen();
    if (!png) { merkeVersuch(ort, stand, true); return 'leer'; }
    const r = await fetch(vorschauSpeicherUrl(stand, ort), { method: 'POST', headers: { 'Content-Type': 'image/png' }, body: png });
    if (!r.ok) throw new Error(`Speichern fehlgeschlagen (${r.status}): ${(await r.text()).slice(0, 200)}`);
    merkeVersuch(ort, stand, true);
    return 'gespeichert';
  } catch (e) {
    console.error('Vorschaubild nicht erzeugt', e);
    merkeVersuch(ort, stand, false);
    return 'fehler';
  } finally { laeuft = false; }
}

/** Rendert das ganze Projekt; null, wenn nichts zu zeigen ist. */
async function bildErzeugen() {
  const THREE = await import('three');
  const { teilGeometrie } = await import('../ansichten/koerper3d.js');
  const koerper = await holen('/api/koerper');
  const szene = new THREE.Scene();
  const modell = new THREE.Group();
  szene.add(modell);
  szene.add(new THREE.AmbientLight(0xffffff, 1.5));
  const sonne = new THREE.DirectionalLight(0xffffff, 2.2);
  sonne.position.set(-6, 12, 8);   // aus Blickrichtung (Südwesten) beleuchtet
  szene.add(sonne);
  const gegen = new THREE.DirectionalLight(0xffffff, 0.7);
  gegen.position.set(8, 5, -6);
  szene.add(gegen);

  const freizugeben = [];
  let teile = 0;
  for (const k of Array.isArray(koerper) ? koerper : []) {
    for (const t of k.teile ?? []) {
      if (!teilZeigen(k.ebene, t)) continue;
      const geom = teilGeometrie(t);
      if (!geom) continue;
      const stil = teilStil(k.ebene, t);
      const mat = new THREE.MeshLambertMaterial({ color: stil.farbe, side: THREE.DoubleSide, transparent: stil.deckkraft < 1, opacity: stil.deckkraft, depthWrite: stil.deckkraft >= 1 });
      modell.add(new THREE.Mesh(geom, mat));
      freizugeben.push(geom, mat);
      if (stil.kanten && ['quader', 'prisma', 'koerper'].includes(t.art)) {
        const kg = new THREE.EdgesGeometry(geom, 20);
        const km = new THREE.LineBasicMaterial({ color: '#6b7280', transparent: true, opacity: 0.55 });
        modell.add(new THREE.LineSegments(kg, km));
        freizugeben.push(kg, km);
      }
      teile++;
    }
  }
  if (teile === 0) return null;

  const box = new THREE.Box3().setFromObject(modell);
  if (box.isEmpty()) return null;
  const mitte = box.getCenter(new THREE.Vector3());
  const abstand = Math.max(box.getSize(new THREE.Vector3()).length() * 2, 10);
  const kamera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, abstand * 4);
  kamera.position.copy(mitte).addScaledVector(new THREE.Vector3(...BLICK_VON).normalize(), abstand);
  kamera.lookAt(mitte);
  kamera.updateMatrixWorld();
  const inv = kamera.matrixWorld.clone().invert();
  // Ausschnitt aus den wirklichen Ecken der Körper (nicht aus dem umschliessenden Quader, der in der Schrägansicht viel zu viel Rand ergibt)
  let x0 = Infinity, x1 = -Infinity, y0 = Infinity, y1 = -Infinity;
  const p = new THREE.Vector3();
  for (const g of freizugeben) {
    const pos = g.attributes?.position;
    if (!pos || !g.isBufferGeometry) continue;
    for (let i = 0; i < pos.count; i++) {
      p.fromBufferAttribute(pos, i).applyMatrix4(inv);
      if (p.x < x0) x0 = p.x; if (p.x > x1) x1 = p.x; if (p.y < y0) y0 = p.y; if (p.y > y1) y1 = p.y;
    }
  }
  const verhaeltnis = BILD_BREITE / BILD_HOEHE, rand = 1.06;
  let halbB = (x1 - x0) / 2 * rand, halbH = (y1 - y0) / 2 * rand;
  if (halbB / halbH > verhaeltnis) halbH = halbB / verhaeltnis; else halbB = halbH * verhaeltnis;
  const cxm = (x0 + x1) / 2, cym = (y0 + y1) / 2;
  Object.assign(kamera, { left: cxm - halbB, right: cxm + halbB, top: cym + halbH, bottom: cym - halbH });
  kamera.updateProjectionMatrix();

  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, preserveDrawingBuffer: true });
  try {
    renderer.setPixelRatio(1);
    renderer.setSize(BILD_BREITE, BILD_HOEHE, false);
    renderer.setClearColor(0x000000, 0);
    renderer.render(szene, kamera);
    return await new Promise((fertig, fehler) => renderer.domElement.toBlob((b) => (b ? fertig(b) : fehler(new Error('PNG nicht erzeugt'))), 'image/png'));
  } finally {
    for (const o of freizugeben) o.dispose?.();
    renderer.dispose();
    try { renderer.forceContextLoss(); } catch { /* egal */ }
  }
}
