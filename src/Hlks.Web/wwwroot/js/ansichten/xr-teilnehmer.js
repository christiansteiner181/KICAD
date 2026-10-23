// «PC sieht die Brille» (Modul XR, Quest App P7): zeigt jede mit KICAD verbundene Brille der Quest App in der 3D-Ansicht als einfache Figur – Kopf (Kugel mit Blickkegel und
// Blicklinie), Hände (Punkte am Handgelenk und an der Zeigefingerspitze, bei Pinch grösser) und ihr Name als Etikett. Die Lage kommt aus GET /api/xr/teilnehmer (Server: XrKanal.cs,
// KICAD-mm, z oben); die Regeln (Pose → Figur, Takt der Abfrage) stehen in xr-teilnehmer-logik.js. Hier nur three.js und der Takt.
//
// Abfrage: ein gemeinsamer Takt für alle offenen 3D-Ansichten (abonnieren). Mit Brille alle 200 ms, ohne Brille nur alle 4 s ein winziger Blick auf die Liste; ausgeschaltet
// (Einstellung ansicht3d.brillen_zeigen, Register Ansicht › Fenster › «Brille zeigen») oder bei verstecktem Fenster gar keine Anfrage. Ohne offene 3D-Ansicht läuft nichts.
// Die Figuren hängen an der Szene, nicht am Modell: sie zählen nicht zum Einpassen, werden nicht angeklickt und nicht vom Schnittbereich beschnitten. In VR (Browser-Prototyp) bleiben sie aus.

import * as THREE from 'three';
import { holen } from '../api.js';
import { benutzerLaden, benutzerGeladen } from '../oberflaeche/benutzer.js';
import { EINSTELLUNG, EREIGNIS, brilleZeigenAn, brilleZeigenSetzen } from './xr-brille-einstellung.js';
import { figurenAus, abfragePlan, figurText } from './xr-teilnehmer-logik.js';

// Einstellung und Schalter stehen in xr-brille-einstellung.js (ohne three.js)
export { EINSTELLUNG, EREIGNIS, brilleZeigenAn, brilleZeigenSetzen };

// ------------------------------------------------------------------------------------------ Abfrage im Takt (gemeinsam)

const abonnenten = new Set();
let timer = 0;
let anzahl = 0;       // Brillen in der letzten Antwort (bestimmt den Takt)
let nr = 0;           // gegen veraltete Antworten

async function takt() {
  timer = 0;
  if (abonnenten.size === 0) return;
  const mein = ++nr;
  if (!benutzerGeladen()) { try { await benutzerLaden(); } catch { /* Vorgabe gilt */ } }
  const an = brilleZeigenAn();
  let figuren = [];
  if (abfragePlan({ an, versteckt: document.hidden, anzahl }).frage) {
    try {
      const antwort = await holen('/api/xr/teilnehmer');
      anzahl = Array.isArray(antwort?.teilnehmer) ? antwort.teilnehmer.length : 0;
      figuren = figurenAus(antwort, Date.now());
    } catch { anzahl = 0; /* Server kurz weg: keine Figuren, der nächste Takt versucht es wieder */ }
  } else anzahl = 0;
  if (mein !== nr || abonnenten.size === 0) return;
  for (const fn of [...abonnenten]) { try { fn(figuren); } catch (e) { console.error('Brillen-Anzeige', e); } }
  timer = setTimeout(takt, abfragePlan({ an: brilleZeigenAn(), versteckt: document.hidden, anzahl }).nach);
}

function neuPlanen() {
  clearTimeout(timer);
  timer = 0;
  if (abonnenten.size > 0) takt();
}

/** Meldet fn(figuren) bei jeder Antwort an; der erste Abonnent startet den Takt, der letzte beendet ihn. Gibt die Abmeldung zurück. */
export function abonnieren(fn) {
  abonnenten.add(fn);
  if (abonnenten.size === 1) {
    window.addEventListener(EREIGNIS, neuPlanen);
    document.addEventListener('visibilitychange', neuPlanen);
    neuPlanen();
  }
  return () => {
    abonnenten.delete(fn);
    if (abonnenten.size === 0) {
      clearTimeout(timer);
      timer = 0;
      nr++;
      anzahl = 0;
      window.removeEventListener(EREIGNIS, neuPlanen);
      document.removeEventListener('visibilitychange', neuPlanen);
    }
  };
}

/** Anzahl der Abonnenten (Kontrolle für Tests: ohne offene 3D-Ansicht fragt niemand). */
export const abonnentenAnzahl = () => abonnenten.size;

// ------------------------------------------------------------------------------------------ Darstellung (three.js)

const KOPF_R = 0.11;      // m
const HAND_R = 0.04;
const SPITZE_R = 0.02;
const BLICK_LAENGE = 3.0;
const ETIKETT_HOEHE = 0.032;   // Anteil der Bildhöhe (sizeAttenuation false)
const GEO = {
  kopf: new THREE.SphereGeometry(KOPF_R, 16, 12),
  kegel: new THREE.ConeGeometry(0.07, 0.22, 16),
  hand: new THREE.SphereGeometry(HAND_R, 10, 8),
  spitze: new THREE.SphereGeometry(SPITZE_R, 8, 6),
};
const OBEN = new THREE.Vector3(0, 1, 0);

function material(farbe, deckkraft = 1) {
  return new THREE.MeshBasicMaterial({ color: farbe, transparent: true, opacity: deckkraft, depthTest: false });
}
function punktMaterial(farbe, px, deckkraft = 1) {
  return new THREE.PointsMaterial({ color: farbe, size: px, sizeAttenuation: false, transparent: true, opacity: deckkraft, depthTest: false });
}
function punkt(farbe, px) {
  const g = new THREE.BufferGeometry();
  g.setAttribute('position', new THREE.Float32BufferAttribute([0, 0, 0], 3));
  const p = new THREE.Points(g, punktMaterial(farbe, px));
  p.renderOrder = 1000;
  return p;
}

/** Etikett: Name auf farbigem Grund als Sprite mit gleichbleibender Grösse auf dem Bildschirm. */
function etikettZeichnen(text, farbe) {
  const c = document.createElement('canvas');
  c.width = 320; c.height = 64;
  const g = c.getContext('2d');
  g.font = '600 30px sans-serif';
  const breite = Math.min(312, Math.ceil(g.measureText(text).width) + 24);
  g.fillStyle = farbe;
  g.globalAlpha = 0.92;
  g.beginPath();
  g.roundRect((c.width - breite) / 2, 6, breite, 52, 12);
  g.fill();
  g.globalAlpha = 1;
  g.fillStyle = '#ffffff';
  g.textAlign = 'center';
  g.textBaseline = 'middle';
  g.fillText(text, c.width / 2, 33, 300);
  const t = new THREE.CanvasTexture(c);
  t.colorSpace = THREE.SRGBColorSpace;
  return t;
}

function baueFigur(f) {
  const gruppe = new THREE.Group();
  gruppe.userData.xrBrille = f.id;
  const m = material(f.farbe);
  const kopf = new THREE.Mesh(GEO.kopf, m); kopf.renderOrder = 998;
  const kegel = new THREE.Mesh(GEO.kegel, m); kegel.renderOrder = 998;
  const linieGeo = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(), new THREE.Vector3(0, 0, -BLICK_LAENGE)]);
  const linie = new THREE.Line(linieGeo, new THREE.LineBasicMaterial({ color: f.farbe, transparent: true, depthTest: false }));
  linie.renderOrder = 999;
  const marke = punkt(f.farbe, 14);
  const etikettMat = new THREE.SpriteMaterial({ map: etikettZeichnen(figurText(f), f.farbe), transparent: true, depthTest: false, sizeAttenuation: false });
  const etikett = new THREE.Sprite(etikettMat);
  etikett.center.set(0.5, 0);
  etikett.scale.set(ETIKETT_HOEHE * 5, ETIKETT_HOEHE, 1);
  etikett.renderOrder = 1001;
  gruppe.add(kopf, kegel, linie, marke, etikett);
  const e = { gruppe, kopf, kegel, linie, marke, etikett, haende: new Map(), farbe: f.farbe, text: figurText(f), material: m };
  return e;
}

function handHinzu(e, seite) {
  const mesh = new THREE.Mesh(GEO.hand, material(e.farbe)); mesh.renderOrder = 998;
  const spitze = new THREE.Mesh(GEO.spitze, material(e.farbe)); spitze.renderOrder = 998;
  const marke = punkt(e.farbe, 8);
  e.gruppe.add(mesh, spitze, marke);
  const h = { mesh, spitze, marke };
  e.haende.set(seite, h);
  return h;
}

function handEntfernen(e, seite) {
  const h = e.haende.get(seite);
  if (!h) return;
  e.gruppe.remove(h.mesh, h.spitze, h.marke);
  h.mesh.material.dispose(); h.spitze.material.dispose(); h.marke.geometry.dispose(); h.marke.material.dispose();
  e.haende.delete(seite);
}

function figurFreigeben(e) {
  e.material.dispose();
  e.linie.geometry.dispose(); e.linie.material.dispose();
  e.marke.geometry.dispose(); e.marke.material.dispose();
  e.etikett.material.map?.dispose(); e.etikett.material.dispose();
  for (const s of [...e.haende.keys()]) handEntfernen(e, s);
}

function lageSetzen(e, f) {
  const [x, y, z] = f.kopf.pos;
  const blick = new THREE.Vector3(...f.kopf.blick);
  e.kopf.position.set(x, y, z);
  e.marke.position.set(x, y, z);
  e.kegel.position.set(x + blick.x * 0.17, y + blick.y * 0.17, z + blick.z * 0.17);
  e.kegel.quaternion.setFromUnitVectors(OBEN, blick);
  e.linie.position.set(x, y, z);
  e.linie.quaternion.setFromUnitVectors(new THREE.Vector3(0, 0, -1), blick);
  e.etikett.position.set(...f.etikett.pos);
  const deckkraft = f.veraltet ? 0.3 : 1;
  e.material.opacity = deckkraft;
  e.linie.material.opacity = f.veraltet ? 0.2 : 0.8;
  e.marke.material.opacity = deckkraft;
  e.etikett.material.opacity = deckkraft;
  const text = figurText(f);
  if (text !== e.text) {
    e.etikett.material.map?.dispose();
    e.etikett.material.map = etikettZeichnen(text, f.farbe);
    e.etikett.material.needsUpdate = true;
    e.text = text;
  }
  for (const seite of [...e.haende.keys()]) if (!f.haende.some((h) => h.seite === seite)) handEntfernen(e, seite);
  for (const hf of f.haende) {
    const h = e.haende.get(hf.seite) ?? handHinzu(e, hf.seite);
    const s = hf.pinch ? 1.7 : 1;
    h.mesh.position.set(...hf.gelenk); h.mesh.scale.setScalar(s);
    h.marke.position.set(...hf.gelenk);
    if (hf.spitze) { h.spitze.visible = true; h.spitze.position.set(...hf.spitze); h.spitze.scale.setScalar(s); } else h.spitze.visible = false;
    for (const mm of [h.mesh.material, h.spitze.material, h.marke.material]) mm.opacity = deckkraft;
  }
}

/**
 * Hängt die Brillen-Figuren an die Szene einer 3D-Ansicht. optionen: { szene, anfordern, sichtbar?: () => bool (Standard immer; in VR false), beiAenderung?: (figuren) => void }.
 * Gibt { zerstoeren, figuren() } zurück. Eine ausgeschaltete Anzeige (Einstellung) zeigt nichts; die Abfrage ruht dann.
 */
export function erzeugeBrillen({ szene, anfordern, sichtbar = () => true, beiAenderung = () => {} }) {
  const wurzel = new THREE.Group();
  wurzel.name = 'xr-brillen';
  szene.add(wurzel);
  const figuren = new Map();   // id → Eintrag
  let letzte = [];
  let zerstoert = false;

  function setzen(alle) {
    const liste = alle.filter((f) => !f.amTisch);   // im Sitzungsraum steht die Brille am Tisch, nicht im Gebäude: keine Figur im Modell
    if (zerstoert) return;
    const hatte = figuren.size > 0;
    const ids = new Set(liste.map((f) => f.id));
    for (const [id, e] of [...figuren]) if (!ids.has(id)) { wurzel.remove(e.gruppe); figurFreigeben(e); figuren.delete(id); }
    for (const f of liste) {
      let e = figuren.get(f.id);
      if (!e) { e = baueFigur(f); figuren.set(f.id, e); wurzel.add(e.gruppe); }
      lageSetzen(e, f);
    }
    wurzel.visible = sichtbar();
    letzte = liste;
    if (hatte || liste.length > 0) { beiAenderung(liste); anfordern(); }
  }

  const abmelden = abonnieren(setzen);
  return {
    figuren: () => letzte,
    zerstoeren() {
      zerstoert = true;
      abmelden();
      for (const e of figuren.values()) { wurzel.remove(e.gruppe); figurFreigeben(e); }
      figuren.clear();
      szene.remove(wurzel);
    },
  };
}
