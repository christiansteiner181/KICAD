// Navigationswürfel der 3D-Ansicht (Nachbearbeitung N11, analog Revit ViewCube): oben rechts ein Würfel mit eigener kleiner Szene und Kamera, die die
// Hauptkamera mitdreht, darunter ein Kompassring (N, O, S, W) und daneben ein Haus-Knopf (Startansicht). Die Rechenregeln stehen in wuerfel-logik.js
// (rein, in Node getestet); hier wird gezeichnet und bedient.
//
// Der Würfel wird mit dem Renderer der 3D-Ansicht in einen Ausschnitt (Viewport/Scissor) der Leinwand gezeichnet – kein zweiter WebGL-Kontext.
// Darüber liegt ein DOM-Bereich, der die Zeigerereignisse annimmt (Treffer über einen Strahl in der kleinen Szene) und den Kompassring (SVG).
// Fläche (OBEN, UNTEN, VORNE, HINTEN, LINKS, RECHTS), 12 Kanten und 8 Ecken sind anklickbar (Teilflächen, siehe teilflaechen()).
// Ziehen am Würfel dreht die Hauptkamera, Ziehen am Ring dreht um die Hochachse. Kontextmenü: Startansicht festlegen.

import * as THREE from 'three';
import { cssVar } from './hilfen.js';
import {
  teilflaechen, FLAECHEN, bereichsSchluessel, bereichsName, bereichsArt, zielWinkel, kugelwinkel, richtungAusWinkeln, weltZuLokal, zwischenstand,
  ziehenZuWinkel, ringMarken, ringWinkel, ringZug, startansichtZiel, startansichtAusRichtung, startansichtLesen, weich,
} from './wuerfel-logik.js';
import { startansichtWert } from './schnittbereich-logik.js';

const KLICK_TOLERANZ = 4;      // px, darunter ist es ein Klick
const FAHRT_MS = 380;          // weicher Kameraübergang
const GROESSE = 128;           // px, Kantenlänge des Würfelbereichs
const ABSTAND = 7.2;           // Abstand der kleinen Kamera (Würfel hat Kantenlänge 2)
const RING_B = 128, RING_H = 40;

const HAUS = '<svg viewBox="0 0 16 16" width="16" height="16" aria-hidden="true"><path d="M2 8.2 8 2.6l6 5.6M3.8 7.2V13.4h3V9.6h2.4v3.8h3V7.2" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linejoin="round" stroke-linecap="round"/></svg>';

/**
 * Baut den Würfel in `wurzel` (Behälter der 3D-Ansicht).
 *  optionen: renderer, kamera (Hauptkamera), steuerung (OrbitControls), anfordern() (neu zeichnen), beiBenutzt() (Kamera vom Benutzer bewegt),
 *            begehen { aktiv(), beenden() } (N15 «Raum begehen»: Haus-Knopf und Klick auf eine Fläche beenden es, Ziehen ist dann aus),
 *            ausrichtungGrad() (Gebäudeausrichtung, wuerfel-logik gebaeudeAusrichtungGrad), nordwinkelGrad(), einpassung() → { ziel: Vector3, abstand },
 *            startLesen() / startSchreiben(wert | null) (Startansicht merken), menue(x, y, eintraege),
 *            rolle { lesen(), setzen(w) } (Modul G, Gesten: Rolle der Hauptkamera um die Blickachse; der Würfel steht wie die Ansicht, jede Bewegung am Würfel
 *            richtet sie auf – Klick und Haus weich mit der Fahrt, Ziehen sofort)
 */
export function erzeugeWuerfel(o) {
  const { wurzel, renderer, kamera, steuerung } = o;
  const gruppe = document.createElement('div');
  gruppe.className = 'd3-wuerfel';
  gruppe.innerHTML = `<button type="button" class="d3-haus" title="Startansicht (Home) – Rechtsklick: Startansicht festlegen" aria-label="Startansicht">${HAUS}</button>
    <div class="d3-wuerfel-flaeche" title="Navigationswürfel: Fläche, Kante oder Ecke anklicken · ziehen = drehen · Rechtsklick: Startansicht festlegen" tabindex="-1"></div>
    <svg class="d3-kompass" viewBox="0 0 ${RING_B} ${RING_H}" width="${RING_B}" height="${RING_H}" role="img" aria-label="Kompass: ziehen dreht um die Hochachse"><title>Kompass: ziehen dreht um die Hochachse (N = geografisch Nord)</title>
      <ellipse class="d3-ring" cx="${RING_B / 2}" cy="${RING_H / 2}" rx="${RING_B / 2 - 6}" ry="${RING_H / 2 - 5}"/><g class="d3-marken"></g></svg>`;
  wurzel.appendChild(gruppe);
  const flaeche = gruppe.querySelector('.d3-wuerfel-flaeche');
  const haus = gruppe.querySelector('.d3-haus');
  const kompass = gruppe.querySelector('.d3-kompass');
  const markenG = kompass.querySelector('.d3-marken');
  for (const t of ['N', 'O', 'S', 'W']) {
    const el = document.createElementNS('http://www.w3.org/2000/svg', 'text');
    el.setAttribute('class', `d3-marke d3-marke-${t}`);
    el.setAttribute('text-anchor', 'middle');
    el.setAttribute('dominant-baseline', 'central');
    el.textContent = t;
    markenG.appendChild(el);
  }

  // ---------- Szene des Würfels ----------
  const szene = new THREE.Scene();
  const wKamera = new THREE.PerspectiveCamera(28, 1, 0.5, 40);
  const kern = new THREE.Mesh(new THREE.BoxGeometry(1.99, 1.99, 1.99), new THREE.MeshBasicMaterial({ color: 0x59616c }));
  szene.add(kern);
  const umriss = new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.BoxGeometry(2, 2, 2)), new THREE.LineBasicMaterial({ color: 0x59616c }));
  szene.add(umriss);
  const teile = [];          // { mesh, schluessel, mitte }
  const etiketten = [];      // { mesh, name }
  const v3 = (p) => new THREE.Vector3(p.x, p.y, p.z);
  for (const t of teilflaechen()) {
    const g = new THREE.BufferGeometry();
    const e = t.ecken;
    g.setAttribute('position', new THREE.Float32BufferAttribute([e[0], e[1], e[2], e[0], e[2], e[3]].flatMap((p) => [p.x, p.y, p.z]), 3));
    const m = new THREE.Mesh(g, new THREE.MeshBasicMaterial({ color: 0xffffff }));
    m.userData = { schluessel: bereichsSchluessel(t.bereich), bereich: t.bereich, mitte: t.mitte };
    szene.add(m);
    teile.push(m);
  }
  for (const f of FLAECHEN) {
    const m = new THREE.Mesh(new THREE.PlaneGeometry(1.24, 1.24), new THREE.MeshBasicMaterial({ transparent: true, depthWrite: false }));
    const n = v3({ x: f.normale[0], y: f.normale[1], z: f.normale[2] });
    const rechts = v3({ x: f.rechts[0], y: f.rechts[1], z: f.rechts[2] }), hoch = v3({ x: f.hoch[0], y: f.hoch[1], z: f.hoch[2] });
    m.matrixAutoUpdate = false;
    m.matrix.makeBasis(rechts, hoch, n).setPosition(n.clone().multiplyScalar(1.006));
    szene.add(m);
    etiketten.push({ mesh: m, name: f.name });
  }

  // ---------- Farben (Design hell/dunkel) ----------
  let farben = null;
  function farbenLesen() {
    farben = {
      flaeche: new THREE.Color(cssVar('--wuerfel-flaeche', '#e8ebef')),
      teil: new THREE.Color(cssVar('--wuerfel-teil', '#d3d8df')),
      kante: new THREE.Color(cssVar('--wuerfel-kante', '#59616c')),
      text: cssVar('--wuerfel-text', '#2b313a'),
      hover: new THREE.Color(cssVar('--wuerfel-hover', '#8fc3ff')),
    };
    kern.material.color.copy(farben.kante);
    umriss.material.color.copy(farben.kante);
    for (const e of etiketten) {
      e.mesh.material.map?.dispose();
      e.mesh.material.map = etikett(e.name, farben.text);
      e.mesh.material.needsUpdate = true;
    }
    teileFaerben();
  }
  function etikett(text, farbe) {
    const c = document.createElement('canvas');
    c.width = c.height = 256;
    const g = c.getContext('2d');
    g.fillStyle = farbe;
    g.font = '600 62px "Segoe UI", Arial, sans-serif';
    g.textAlign = 'center';
    g.textBaseline = 'middle';
    g.fillText(text, 128, 132);
    const t = new THREE.CanvasTexture(c);
    t.anisotropy = 4;
    t.colorSpace = THREE.SRGBColorSpace;
    return t;
  }
  let hover = null;   // Schlüssel des Bereichs unter dem Zeiger
  function teileFaerben() {
    for (const m of teile) m.material.color.copy(m.userData.schluessel === hover ? farben.hover : (m.userData.mitte ? farben.flaeche : farben.teil));
  }
  farbenLesen();

  // ---------- Zeichnen (in einen Ausschnitt der Leinwand) ----------
  const ausrichtungRad = () => (o.ausrichtungGrad?.() ?? 0) * Math.PI / 180;
  const sicht = new THREE.Vector3();
  const groesse = new THREE.Vector2();
  function zeichnen() {
    const r = flaeche.getBoundingClientRect(), c = renderer.domElement.getBoundingClientRect();
    if (r.width < 8 || r.height < 8 || c.width < 8) return;
    sicht.copy(kamera.position).sub(steuerung.target);
    if (sicht.lengthSq() < 1e-12) return;
    const lokal = weltZuLokal({ x: sicht.x, y: sicht.y, z: sicht.z }, ausrichtungRad());
    const l = Math.hypot(lokal.x, lokal.y, lokal.z) || 1;
    wKamera.position.set((lokal.x / l) * ABSTAND, (lokal.y / l) * ABSTAND, (lokal.z / l) * ABSTAND);
    wKamera.up.set(0, 1, 0);
    wKamera.lookAt(0, 0, 0);
    const rolle = o.rolle?.lesen?.() ?? 0;   // Modul G: der Würfel steht wie die Ansicht
    if (rolle) wKamera.rotateZ(rolle);
    wKamera.aspect = r.width / r.height;
    wKamera.updateProjectionMatrix();
    const x = r.left - c.left, y = c.bottom - r.bottom;
    const auto = renderer.autoClear;
    renderer.autoClear = false;
    renderer.setViewport(x, y, r.width, r.height);
    renderer.setScissor(x, y, r.width, r.height);
    renderer.setScissorTest(true);
    renderer.clearDepth();
    renderer.render(szene, wKamera);
    renderer.setScissorTest(false);
    renderer.getSize(groesse);
    renderer.setViewport(0, 0, groesse.x, groesse.y);
    renderer.autoClear = auto;
    ringZeichnen();
  }

  // ---------- Kompassring ----------
  function ringZeichnen() {
    const az = kugelwinkel({ x: sicht.x, y: sicht.y, z: sicht.z }, 0).azimut;
    const m = ringMarken(az, o.nordwinkelGrad?.() ?? 0);
    const rx = RING_B / 2 - 6, ry = RING_H / 2 - 5, cx = RING_B / 2, cy = RING_H / 2;
    for (const [name, w] of Object.entries(m)) {
      const t = markenG.querySelector(`.d3-marke-${name}`);
      if (!t) continue;
      const x = cx + rx * Math.sin(w), y = cy - ry * Math.cos(w);
      t.setAttribute('x', x.toFixed(1));
      t.setAttribute('y', y.toFixed(1));
      const hinten = Math.cos(w) > 0.2;   // auf der abgewandten Seite des Rings: kleiner und blasser
      t.classList.toggle('hinten', hinten);
    }
    kompass.dataset.nord = String(Math.round(((m.N * 180 / Math.PI) % 360 + 360) % 360));
  }

  // ---------- Kamera bewegen ----------
  let fahrt = 0;
  function aktuell() {
    const d = new THREE.Vector3().copy(kamera.position).sub(steuerung.target);
    const w = kugelwinkel({ x: d.x, y: d.y, z: d.z }, 0);
    return { azimut: w.azimut, polar: w.polar, abstand: d.length(), ziel: { x: steuerung.target.x, y: steuerung.target.y, z: steuerung.target.z } };
  }
  function setzen(s) {
    const r = richtungAusWinkeln(s.azimut, Math.max(1e-5, Math.min(Math.PI - 1e-5, s.polar)));
    steuerung.target.set(s.ziel.x, s.ziel.y, s.ziel.z);
    kamera.position.set(s.ziel.x + r.x * s.abstand, s.ziel.y + r.y * s.abstand, s.ziel.z + r.z * s.abstand);
    steuerung.update();
    o.anfordern();
  }
  function abbrechen() { if (fahrt) { cancelAnimationFrame(fahrt); fahrt = 0; } }
  /** Weicher Übergang zu { azimut, polar, abstand?, ziel? }; was fehlt, bleibt wie es ist. */
  function fahreZu(nach, dauer = FAHRT_MS) {
    abbrechen();
    o.beiBenutzt?.();
    const von = aktuell();
    const rolleVon = o.rolle?.lesen?.() ?? 0;   // Modul G: die Fahrt richtet die Ansicht zugleich auf
    const ziel = { azimut: nach.azimut ?? von.azimut, polar: nach.polar ?? von.polar, abstand: nach.abstand ?? von.abstand, ziel: nach.ziel ?? von.ziel };
    const klein = typeof matchMedia === 'function' && matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (dauer <= 0 || klein) { o.rolle?.setzen?.(0); setzen(ziel); return; }
    const t0 = performance.now();
    const schritt = (jetzt) => {
      const t = (jetzt - t0) / dauer;
      o.rolle?.setzen?.(t >= 1 ? 0 : rolleVon * (1 - weich(t)));
      setzen(t >= 1 ? ziel : zwischenstand(von, ziel, t));
      fahrt = t >= 1 ? 0 : requestAnimationFrame(schritt);
    };
    fahrt = requestAnimationFrame(schritt);
  }
  steuerung.addEventListener('start', abbrechen);

  /** Dreht die Hauptkamera um Winkel (Ziehen am Würfel bzw. Ring). */
  function drehen(dAzimut, dPolar) {
    if (o.begehen?.aktiv()) return;   // N15: beim Begehen dreht der Würfel mit, wird aber nicht gezogen
    abbrechen();
    o.beiBenutzt?.();
    o.rolle?.setzen?.(0);   // Modul G: Ziehen am Würfel bzw. Ring richtet auf
    const a = aktuell();
    setzen({ ...a, azimut: a.azimut + dAzimut, polar: Math.max(1e-4, Math.min(Math.PI - 1e-4, a.polar + dPolar)) });
  }

  // ---------- Bedienung am Würfel ----------
  const strahl = new THREE.Raycaster();
  const ndc = new THREE.Vector2();
  function bereichAn(e) {
    const r = flaeche.getBoundingClientRect();
    if (r.width < 1) return null;
    ndc.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    strahl.setFromCamera(ndc, wKamera);
    const t = strahl.intersectObjects(teile, false)[0];
    return t ? t.object.userData.bereich : null;
  }
  function hoverSetzen(bereich) {
    const s = bereich ? bereichsSchluessel(bereich) : null;
    if (s === hover) return;
    hover = s;
    teileFaerben();
    flaeche.style.cursor = s ? 'pointer' : '';
    flaeche.dataset.bereich = bereich ? bereichsName(bereich) : '';
    flaeche.title = bereich ? `${bereichsName(bereich)} (${{ flaeche: 'Fläche', kante: 'Kante', ecke: 'Ecke' }[bereichsArt(bereich)]})` : 'Navigationswürfel';
    o.anfordern();
  }
  function klickBereich(bereich) {
    if (o.begehen?.aktiv()) o.begehen.beenden();   // N15: ein Klick auf eine Fläche beendet das Begehen (vorherige Kamera), dann fährt die Kamera dorthin
    const z = zielWinkel(bereich, ausrichtungRad());
    fahreZu({ azimut: z.azimut, polar: z.polar });
  }

  let druck = null;
  flaeche.addEventListener('pointerdown', (e) => {
    if (e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation();
    abbrechen();
    druck = { x: e.clientX, y: e.clientY, sx: e.clientX, sy: e.clientY, bewegt: false, bereich: bereichAn(e) };
    try { flaeche.setPointerCapture(e.pointerId); } catch { /* egal */ }
  });
  flaeche.addEventListener('pointermove', (e) => {
    if (!druck) { hoverSetzen(bereichAn(e)); return; }
    if (!druck.bewegt && Math.hypot(e.clientX - druck.sx, e.clientY - druck.sy) > KLICK_TOLERANZ) { druck.bewegt = true; hoverSetzen(null); }
    if (druck.bewegt) {
      const w = ziehenZuWinkel(e.clientX - druck.x, e.clientY - druck.y);
      drehen(w.dAzimut, w.dPolar);
      druck.x = e.clientX; druck.y = e.clientY;
    }
  });
  const loslassen = (e) => {
    const d = druck;
    druck = null;
    try { flaeche.releasePointerCapture(e.pointerId); } catch { /* egal */ }
    if (!d || e.type === 'pointercancel') return;
    if (!d.bewegt && d.bereich) klickBereich(d.bereich);
  };
  flaeche.addEventListener('pointerup', loslassen);
  flaeche.addEventListener('pointercancel', loslassen);
  flaeche.addEventListener('pointerleave', () => { if (!druck) hoverSetzen(null); });
  // Rad über dem Würfel zoomt die Ansicht wie sonst auch
  const weiterreichen = (e) => { e.preventDefault(); renderer.domElement.dispatchEvent(new WheelEvent('wheel', e)); };
  gruppe.addEventListener('wheel', weiterreichen, { passive: false });

  // ---------- Bedienung am Kompassring ----------
  let ringDruck = null;
  const ringMitte = () => { const r = kompass.getBoundingClientRect(); return { x: r.left + r.width / 2, y: r.top + r.height / 2, v: (RING_H / 2 - 5) / (RING_B / 2 - 6) }; };
  kompass.addEventListener('pointerdown', (e) => {
    if (e.button !== 0) return;
    e.preventDefault();
    e.stopPropagation();
    abbrechen();
    const m = ringMitte();
    ringDruck = { winkel: ringWinkel({ x: e.clientX, y: e.clientY }, m, m.v) };
    try { kompass.setPointerCapture(e.pointerId); } catch { /* egal */ }
    kompass.classList.add('zieht');
  });
  kompass.addEventListener('pointermove', (e) => {
    if (!ringDruck) return;
    const m = ringMitte();
    const w = ringWinkel({ x: e.clientX, y: e.clientY }, m, m.v);
    const d = ringZug(ringDruck.winkel, w);
    ringDruck.winkel = w;
    if (d) drehen(d, 0);
  });
  const ringLos = (e) => { ringDruck = null; kompass.classList.remove('zieht'); try { kompass.releasePointerCapture(e.pointerId); } catch { /* egal */ } };
  kompass.addEventListener('pointerup', ringLos);
  kompass.addEventListener('pointercancel', ringLos);

  // ---------- Startansicht ----------
  function startZiel() { return startansichtZiel(startansichtLesen(o.startLesen?.()), ausrichtungRad()); }
  /** Zur Startansicht (Home): gemerkte oder Isometrie, alles im Bild. */
  function heim(dauer = FAHRT_MS) {
    if (o.begehen?.aktiv()) { o.begehen.beenden(); return; }   // N15: Haus-Knopf beim Begehen = zurück zur vorherigen Kamera
    const z = startZiel();
    const e = o.einpassung();
    fahreZu({ azimut: z.azimut, polar: z.polar, abstand: e.abstand, ziel: e.ziel }, dauer);
  }
  function startFestlegen() {
    if (o.begehen?.aktiv()) return null;   // N15: die Kamera im Raum ist keine Startansicht
    const d = new THREE.Vector3().copy(kamera.position).sub(steuerung.target);
    // im Modell (Eigenschaft startansicht der Ansicht): Blickrichtung relativ zur Gebäudeausrichtung, dazu Position und Ziel der Kamera in Modell-mm
    const wert = startansichtWert(startansichtAusRichtung({ x: d.x, y: d.y, z: d.z }, ausrichtungRad()), kamera.position, steuerung.target);
    o.startSchreiben?.(wert);
    return wert;
  }
  function startZuruecksetzen() { o.startSchreiben?.(null); }
  haus.addEventListener('click', (e) => { e.stopPropagation(); heim(); });
  haus.addEventListener('pointerdown', (e) => e.stopPropagation());
  gruppe.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    e.stopPropagation();
    o.menue?.(e.clientX, e.clientY, [
      { titel: 'Zur Startansicht', aktion: () => heim() },
      { titel: 'Aktuelle Ansicht als Startansicht festlegen', aktion: () => startFestlegen() },
      { titel: 'Startansicht zurücksetzen (Isometrie)', aktion: () => startZuruecksetzen() },
    ]);
  });

  function designGeaendert() { farbenLesen(); o.anfordern(); }

  return {
    zeichnen, heim, fahreZu, drehen, abbrechen, startFestlegen, startZuruecksetzen, designGeaendert, klickBereich, startZiel,
    element: gruppe,
    zerstoeren() {
      abbrechen();
      steuerung.removeEventListener('start', abbrechen);
      for (const m of teile) { m.geometry.dispose(); m.material.dispose(); }
      for (const e of etiketten) { e.mesh.geometry.dispose(); e.mesh.material.map?.dispose(); e.mesh.material.dispose(); }
      kern.geometry.dispose(); kern.material.dispose(); umriss.geometry.dispose(); umriss.material.dispose();
      gruppe.remove();
    },
  };
}
