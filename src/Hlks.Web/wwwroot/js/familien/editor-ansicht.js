// Familieneditor – Ansichtsbereich (Modul 2, C1): die Familie allein, vier Fenster (vorne, oben, links, 3D) oder ein Fenster.
// Die Grafik kommt vom Kern (Abfrage familie_ansicht): 2D mit dem Plangrafik-Zeichner (plangrafik.js), 3D mit koerper3d.js.
// Zusätzlich zeigt die Oberfläche Ursprung (Einfügepunkt) und die Achsen x, y, z, die Anschlüsse mit Pfeil und Namen (2D vom Kern, 3D hier)
// und den gewählten Körper hervorgehoben (2D Rahmen, 3D Farbe) – Zuordnung über die Reihenfolge der Definition (familien-ansicht.js).
// Keine Fachlogik: gerechnet wird im Kern.

import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';
import { abfrage } from '../api.js';
import { planSvg, begrenzungVon } from '../ansichten/plangrafik.js';
import { baueKoerper, hervorheben, freigeben } from '../ansichten/koerper3d.js';
import { cssVar } from '../ansichten/hilfen.js';
import { ORBIT_TASTEN, mitteDoppelklick, mitteVerschieben } from '../ansichten/maus.js';
import { ANSICHTEN, VIER_FENSTER, teilBox, boxProjektion, teilIndexVon, achsenLaenge, ueberlagerungSvg, naechsterPunkt, pickPunkteSvg } from './familien-ansicht.js';
import { esc, kernFehler } from './familien-text.js';

const SVGNS = 'http://www.w3.org/2000/svg';
const MINPX = 1;

// ------------------------------------------------------------------------------------------------ 2D

class Fenster2D {
  constructor(behaelter, ansicht) {
    this.ansicht = ansicht;
    this.plan = null;
    this.vb = null;
    this.benutzt = false;
    this.pick = null;           // Modul 2, C2: Rückruf beim Anklicken eines Punkts (Passpunkte des Kontrollbilds)
    this.pickPunkte = [];
    behaelter.innerHTML = `<svg class="gr-svg fe-svg" xmlns="${SVGNS}" preserveAspectRatio="xMidYMid meet" tabindex="-1" data-ansicht="${esc(ansicht)}"><g class="gr-welt" transform="scale(1 -1)"><g class="fe-plan"></g><g class="fe-ueber"></g></g></svg>`;
    this.svg = behaelter.querySelector('svg');
    this.planG = this.svg.querySelector('.fe-plan');
    this.ueberG = this.svg.querySelector('.fe-ueber');
    this.bind();
    this.beobachter = typeof ResizeObserver === 'function' ? new ResizeObserver(() => this.groesse()) : null;
    this.beobachter?.observe(this.svg);
  }

  bind() {
    const svg = this.svg;
    let druck = null;
    // Mittlere Taste (N10): verschieben, Doppelklick = alles zeigen, kein Autoscroll – auch während die Punktwahl aktiv ist
    mitteVerschieben(svg, {
      beiBeginn: () => svg.classList.add('schiebt'),
      beiBewegung: (dx, dy) => { if (!this.vb) return; const r = svg.getBoundingClientRect(); this.vb.x -= (dx / r.width) * this.vb.w; this.vb.y -= (dy / r.height) * this.vb.h; this.benutzt = true; this.anwenden(); },
      beiEnde: () => svg.classList.remove('schiebt'),
      beiDoppelklick: () => { this.benutzt = false; this.einpassen(); },
    });
    svg.addEventListener('pointerdown', (e) => { if (e.button !== 0) return; druck = { x: e.clientX, y: e.clientY }; this.start = { x: e.clientX, y: e.clientY }; try { svg.setPointerCapture(e.pointerId); } catch { /* egal */ } svg.classList.add('schiebt'); });
    svg.addEventListener('pointermove', (e) => {
      if (!druck || !this.vb) return;
      const r = svg.getBoundingClientRect();
      this.vb.x -= ((e.clientX - druck.x) / r.width) * this.vb.w; this.vb.y -= ((e.clientY - druck.y) / r.height) * this.vb.h;
      druck = { x: e.clientX, y: e.clientY }; this.benutzt = true; this.anwenden();
    });
    const los = (e) => {
      druck = null; svg.classList.remove('schiebt'); try { svg.releasePointerCapture(e.pointerId); } catch { /* egal */ }
      if (e.type === 'pointerup' && this.pick && this.start && Math.hypot(e.clientX - this.start.x, e.clientY - this.start.y) < 4) this.klickPick(e);
      this.start = null;
    };
    svg.addEventListener('pointerup', los); svg.addEventListener('pointercancel', los);
    svg.addEventListener('dblclick', () => { this.benutzt = false; this.einpassen(); });
    svg.addEventListener('wheel', (e) => {
      if (!this.vb) return;
      e.preventDefault();
      const r = svg.getBoundingClientRect();
      const px = this.vb.x + ((e.clientX - r.left) / r.width) * this.vb.w, py = this.vb.y + ((e.clientY - r.top) / r.height) * this.vb.h;
      const f = Math.exp(-Math.max(-300, Math.min(300, e.deltaY)) * 0.0016);
      const w = this.vb.w / f, h = this.vb.h / f;
      this.vb = { x: px - ((e.clientX - r.left) / r.width) * w, y: py - ((e.clientY - r.top) / r.height) * h, w, h };
      this.benutzt = true; this.anwenden();
    }, { passive: false });
  }

  groesse() { if (!this.benutzt) this.einpassen(); else this.anwenden(); }

  /** Rahmen der Anzeige in Ansichtskoordinaten (Plan, Ursprung, Achsen). */
  rahmen() {
    const b = begrenzungVon(this.plan) ?? { minx: -100, miny: -100, maxx: 100, maxy: 100 };
    const l = achsenLaenge(this.plan?.begrenzung);
    return { minx: Math.min(b.minx, -l * 0.3), miny: Math.min(b.miny, -l * 0.3), maxx: Math.max(b.maxx, l * 1.4), maxy: Math.max(b.maxy, l * 1.4) };
  }

  einpassen() {
    const W = this.svg.clientWidth, H = this.svg.clientHeight;
    if (W < 2 || H < 2) return;
    const b = this.rahmen();
    const rand = Math.max(b.maxx - b.minx, b.maxy - b.miny, 100) * 0.08;
    let w = b.maxx - b.minx + 2 * rand, h = b.maxy - b.miny + 2 * rand;
    const cx = (b.minx + b.maxx) / 2, cy = -(b.miny + b.maxy) / 2;
    if (w / h < W / H) w = h * (W / H); else h = w * (H / W);
    this.vb = { x: cx - w / 2, y: cy - h / 2, w, h };
    this.anwenden();
  }

  anwenden() {
    if (!this.vb) return;
    this.svg.setAttribute('viewBox', `${this.vb.x} ${this.vb.y} ${this.vb.w} ${this.vb.h}`);
    const W = this.svg.clientWidth;
    if (W > 1) this.svg.style.setProperty('--minpx', `${(MINPX * this.vb.w) / W}px`);
    this.s = W > 1 ? this.vb.w / W : 1;
    if (this.ueber) this.ueberZeichnen();
  }

  /** daten: Antwort familie_ansicht (plangrafik), rahmen: Rahmen des gewählten Körpers (Ansichtskoordinaten) oder null. */
  setzen(daten, rahmen, neuEinpassen) {
    this.plan = daten?.plangrafik ?? null;
    this.svg.style.setProperty('--m', String(this.plan?.massstab ?? 10));
    // Ids entfernen: die Familie allein ist nicht anklickbar (kein Hover-Hervorheben des ganzen Teils)
    const sauber = this.plan ? { ...this.plan, teile: (this.plan.teile ?? []).map((t) => ({ ...t, id: null })) } : null;
    this.planG.innerHTML = sauber ? planSvg(sauber) : '';
    this.ueber = { rahmen };
    if (neuEinpassen || !this.vb || !this.benutzt) { this.benutzt = false; this.einpassen(); } else this.anwenden();
  }

  ueberZeichnen() {
    this.ueberG.innerHTML = ueberlagerungSvg(this.ansicht, { laenge: achsenLaenge(this.plan?.begrenzung), rahmen: this.ueber?.rahmen ?? null, s: this.s ?? 1 }) + pickPunkteSvg(this.pickPunkte, this.s ?? 1);
  }

  /** Klick im Pick-Modus (Modul 2, C2): Punkt in Ansichtskoordinaten (u nach rechts, v nach oben, mm), auf den nächsten Eckpunkt gerastet (12 Pixel). */
  klickPick(e) {
    const ctm = this.svg.getScreenCTM();
    if (!ctm || !this.pick) return;
    const p = new DOMPoint(e.clientX, e.clientY).matrixTransform(ctm.inverse());
    let u = p.x, v = -p.y;
    const ecke = naechsterPunkt(this.plan, u, v, 12 * (this.s ?? 1));
    if (ecke) [u, v] = ecke;
    this.pick({ u, v, gerastet: !!ecke, ansicht: this.ansicht });
  }

  setzePunkte(punkte) { this.pickPunkte = punkte ?? []; if (this.ueber) this.ueberZeichnen(); }

  zerstoeren() { this.beobachter?.disconnect(); }
}

// ------------------------------------------------------------------------------------------------ 3D

const M = 1 / 1000;
const vek3 = (p) => new THREE.Vector3(p[0] * M, p[2] * M, -p[1] * M);

class Fenster3D {
  constructor(behaelter) {
    this.behaelter = behaelter;
    behaelter.innerHTML = '<div class="d3-wurzel fe-3d"><div class="fe-labels"></div></div>';
    this.wurzel = behaelter.querySelector('.d3-wurzel');
    this.labels = behaelter.querySelector('.fe-labels');
    this.tot = false;
    this.benutzt = false;
    try {
      this.renderer = new THREE.WebGLRenderer({ antialias: true });
    } catch (e) {
      console.error('Familieneditor 3D: WebGL nicht verfügbar', e);
      this.wurzel.innerHTML = '<div class="d3-hinweis">3D nicht verfügbar:<br>WebGL kann in diesem Browser nicht gestartet werden.</div>';
      return;
    }
    this.renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 3));
    this.renderer.domElement.className = 'd3-leinwand';
    this.wurzel.prepend(this.renderer.domElement);
    this.szene = new THREE.Scene();
    this.szene.background = new THREE.Color(cssVar('--d3-hintergrund', '#eef1f5'));
    this.beiDesign = () => {   // Designwechsel (hell/dunkel): Hintergrund und Farben der Körper neu
      this.szene.background = new THREE.Color(cssVar('--d3-hintergrund', '#eef1f5'));
      if (this.letzte) this.setzen(this.letzte.daten, { ...this.letzte.opt, neuEinpassen: false });
    };
    window.addEventListener('hlks-design', this.beiDesign);
    this.szene.add(new THREE.AmbientLight(0xffffff, 1.55));
    const sonne = new THREE.DirectionalLight(0xffffff, 2.2); sonne.position.set(6, 12, 8); this.szene.add(sonne);
    const gegen = new THREE.DirectionalLight(0xffffff, 0.8); gegen.position.set(-8, 5, -6); this.szene.add(gegen);
    this.modell = new THREE.Group(); this.szene.add(this.modell);
    this.zusatz = new THREE.Group(); this.szene.add(this.zusatz);
    this.kamera = new THREE.PerspectiveCamera(40, 1, 0.001, 100);
    this.steuerung = new OrbitControls(this.kamera, this.renderer.domElement);
    this.steuerung.enableDamping = false;
    // Maustasten wie in der 3D-Ansicht (N10): links drehen, Mitte verschieben, Umschalt + Mitte drehen, Rad zoomt zum Mauszeiger, Doppelklick Mitte = alles zeigen
    this.steuerung.mouseButtons = { LEFT: THREE.MOUSE[ORBIT_TASTEN.LEFT], MIDDLE: THREE.MOUSE[ORBIT_TASTEN.MIDDLE], RIGHT: THREE.MOUSE[ORBIT_TASTEN.RIGHT] };
    this.steuerung.zoomToCursor = true;
    const dom = this.renderer.domElement;
    const mitteDk = mitteDoppelklick();
    dom.addEventListener('pointerdown', (e) => {
      if (e.button !== 1) return;
      e.preventDefault();
      if (mitteDk.unten(e)) { e.stopPropagation(); this.benutzt = false; this.einpassen(); }
    }, true);
    dom.addEventListener('pointermove', (e) => mitteDk.bewegt(e), true);
    dom.addEventListener('pointerup', (e) => mitteDk.oben(e), true);
    dom.addEventListener('pointercancel', (e) => mitteDk.oben(e), true);
    dom.addEventListener('mousedown', (e) => { if (e.button === 1) e.preventDefault(); });
    dom.addEventListener('auxclick', (e) => e.preventDefault());
    this.steuerung.addEventListener('change', () => this.anfordern());
    this.steuerung.addEventListener('start', () => { this.benutzt = true; });
    this.beobachter = typeof ResizeObserver === 'function' ? new ResizeObserver(() => this.groesse()) : null;
    this.beobachter?.observe(this.wurzel);
    this.punkte = [];   // Beschriftungen: { text, position (three), klasse }
    this.groesse();
  }

  groesse() {
    if (this.tot || !this.renderer) return;
    const W = this.wurzel.clientWidth, H = this.wurzel.clientHeight;
    if (W < 2 || H < 2) return;
    this.renderer.setSize(W, H);
    this.kamera.aspect = W / H; this.kamera.updateProjectionMatrix();
    if (!this.benutzt && this.hatInhalt) this.einpassen(); else this.anfordern();
  }

  anfordern() { if (!this.geplant && !this.tot) this.geplant = requestAnimationFrame(() => { this.geplant = 0; this.zeichnen(); }); }

  zeichnen() {
    if (this.tot || !this.renderer) return;
    this.renderer.render(this.szene, this.kamera);
    this.labelsLegen();
  }

  labelsLegen() {
    const W = this.wurzel.clientWidth, H = this.wurzel.clientHeight;
    const v = new THREE.Vector3();
    const kinder = this.labels.children;
    this.punkte.forEach((p, i) => {
      const el = kinder[i];
      if (!el) return;
      v.copy(p.position).project(this.kamera);
      const sichtbar = v.z > -1 && v.z < 1;
      el.style.display = sichtbar ? '' : 'none';
      el.style.left = `${(v.x * 0.5 + 0.5) * W}px`;
      el.style.top = `${(-v.y * 0.5 + 0.5) * H}px`;
    });
  }

  einpassen() {
    if (!this.modell || !this.hatInhalt) return;
    const box = new THREE.Box3().setFromObject(this.zusatzBox ?? this.modell);
    if (box.isEmpty()) return;
    const mitte = box.getCenter(new THREE.Vector3());
    const radius = Math.max(box.getSize(new THREE.Vector3()).length() / 2, 0.02);
    const abstand = radius / Math.sin((this.kamera.fov * Math.PI) / 360) * 1.05;
    // Blick von vorne (Vorderseite der Familie = −y = +z in three), rechts und von oben
    const r = new THREE.Vector3(0.55, 0.45, 1).normalize();
    this.kamera.position.copy(mitte).addScaledVector(r, abstand);
    this.kamera.near = abstand / 200; this.kamera.far = abstand * 40; this.kamera.updateProjectionMatrix();
    this.steuerung.target.copy(mitte);
    this.steuerung.update();
    this.anfordern();
  }

  /** daten: Antwort familie_ansicht (3d); auswahlIndex: Index des hervorzuhebenden Teils (oder -1). */
  setzen(daten, { auswahlIndex = -1, laenge = 100, neuEinpassen = false } = {}) {
    if (!this.renderer || this.tot) return;
    this.letzte = { daten, opt: { auswahlIndex, laenge } };
    for (const g of [...this.modell.children]) { this.modell.remove(g); freigeben(g); }
    for (const g of [...this.zusatz.children]) { this.zusatz.remove(g); g.traverse?.((o) => { o.geometry?.dispose?.(); o.material?.dispose?.(); }); }
    this.punkte = [];
    const teile = daten?.koerper?.teile ?? [];
    const farbeAuswahl = new THREE.Color(cssVar('--auswahl', '#1e90ff'));
    teile.forEach((t, i) => {
      const g = baueKoerper({ id: `teil${i}`, ebene: daten.koerper.ebene ?? 'sonst', teile: [t] });
      if (!g) return;
      if (i === auswahlIndex) hervorheben(g, 'auswahl', farbeAuswahl);
      this.modell.add(g);
    });
    this.hatInhalt = teile.length > 0;
    this.zusatzBox = null;
    // Achsen
    const L = laenge * M;
    const achse = (ende, farbeName, text) => {
      const geo = new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, 0, 0), ende]);
      const linie = new THREE.Line(geo, new THREE.LineBasicMaterial({ color: cssVar(farbeName, '#888888') }));
      this.zusatz.add(linie);
      this.punkte.push({ text, position: ende.clone().multiplyScalar(1.12), klasse: 'fe-label-achse' });
    };
    achse(vek3([laenge, 0, 0]), '--fe-achse-x', 'X');
    achse(vek3([0, laenge, 0]), '--fe-achse-y', 'Y');
    achse(vek3([0, 0, laenge]), '--fe-achse-z', 'Z');
    // Ursprung
    const kugel = new THREE.Mesh(new THREE.SphereGeometry(Math.max(L * 0.06, 0.002), 12, 8), new THREE.MeshBasicMaterial({ color: cssVar('--text', '#ffffff') }));
    this.zusatz.add(kugel);
    this.punkte.push({ text: 'Ursprung', position: new THREE.Vector3(0, -L * 0.15, 0), klasse: 'fe-label-ursprung' });
    // Anschlüsse mit Pfeil
    for (const a of daten?.anschluesse ?? []) {
      if (!Array.isArray(a.lage) || !Array.isArray(a.richtung)) continue;
      const o = vek3(a.lage);
      const dir = new THREE.Vector3(a.richtung[0], a.richtung[2], -a.richtung[1]);
      if (dir.lengthSq() < 1e-12) continue;
      const pfeil = new THREE.ArrowHelper(dir.normalize(), o, Math.max(L * 0.6, 0.03), cssVar('--fe-anschluss', '#e0b04a'), Math.max(L * 0.18, 0.01), Math.max(L * 0.1, 0.006));
      this.zusatz.add(pfeil);
      this.punkte.push({ text: a.name, position: o.clone().add(new THREE.Vector3(0, L * 0.12, 0)), klasse: 'fe-label-anschluss' });
    }
    this.labels.innerHTML = this.punkte.map((p) => `<span class="${p.klasse}">${esc(p.text)}</span>`).join('');
    this.zusatzBox = this.modell;
    if (neuEinpassen || !this.benutzt) { this.benutzt = false; this.einpassen(); } else this.anfordern();
  }

  zerstoeren() {
    this.tot = true;
    window.removeEventListener('hlks-design', this.beiDesign ?? (() => {}));
    if (this.geplant) cancelAnimationFrame(this.geplant);
    this.beobachter?.disconnect();
    if (this.modell) for (const g of [...this.modell.children]) freigeben(g);
    this.steuerung?.dispose();
    try { this.renderer?.dispose(); this.renderer?.forceContextLoss(); } catch { /* egal */ }
  }
}

// ------------------------------------------------------------------------------------------------ Bereich

/** Der Ansichtsbereich: Leiste (Typ, Detailgrad, Fenster) und die Fenster. wirt: { kennzeichen(), def(), fam(), auswahlKoerper() }. */
export function ansichtsBereich(behaelter, wirt) {
  const zustand = { typ: null, detailgrad: 'fein', modus: 'vier', einzeln: 'vorne' };
  behaelter.innerHTML = `<div class="fe-avleiste"><label>Typ <select class="fe-av-typ"></select></label>
      <label>Detailgrad <select class="fe-av-detail">${['grob', 'mittel', 'fein'].map((g) => `<option${g === 'fein' ? ' selected' : ''}>${g}</option>`).join('')}</select></label>
      <label>Fenster <select class="fe-av-modus"><option value="vier">vier Fenster</option><option value="eins">ein Fenster</option></select></label>
      <label class="fe-av-einzeln-l" hidden>Ansicht <select class="fe-av-einzeln">${ANSICHTEN.map(([w, t]) => `<option value="${w}">${t}</option>`).join('')}</select></label>
      <span class="fe-av-info leise"></span></div>
    <div class="fe-avflaeche"></div>`;
  const q = (s) => behaelter.querySelector(s);
  const flaeche = q('.fe-avflaeche');
  const info = q('.fe-av-info');
  let fenster = new Map();   // Ansicht → { kopf, inhalt, objekt }
  let nr = 0;
  let zuletzt = null;        // letzter Aufbau der Fenster (Modus/Ansichten)
  let tot = false;

  const ansichten = () => (zustand.modus === 'vier' ? VIER_FENSTER : [zustand.einzeln]);

  function aufbauen() {
    const sig = ansichten().join(',');
    if (zuletzt === sig) return;
    zuletzt = sig;
    for (const f of fenster.values()) f.objekt?.zerstoeren?.();
    fenster = new Map();
    flaeche.className = `fe-avflaeche ${zustand.modus === 'vier' ? 'vier' : 'eins'}`;
    flaeche.innerHTML = ansichten().map((a) => `<div class="fe-fenster" data-ansicht="${a}"><div class="fe-fenster-kopf">${esc(ANSICHTEN.find(([w]) => w === a)?.[1] ?? a)}</div><div class="fe-fenster-inhalt"></div></div>`).join('');
    for (const el of flaeche.querySelectorAll('.fe-fenster')) {
      const a = el.dataset.ansicht;
      const inhalt = el.querySelector('.fe-fenster-inhalt');
      fenster.set(a, { el, inhalt, objekt: a === '3d' ? new Fenster3D(inhalt) : new Fenster2D(inhalt, a) });
    }
  }

  function typeFuellen() {
    const typen = (wirt.fam()?.typen ?? []).map((t) => t.name);
    const sel = q('.fe-av-typ');
    if (!typen.includes(zustand.typ)) zustand.typ = typen[0] ?? null;
    const sig = typen.join('|') + '#' + zustand.typ;
    if (sel.dataset.sig === sig) return;
    sel.dataset.sig = sig;
    sel.innerHTML = typen.map((t) => `<option${t === zustand.typ ? ' selected' : ''}>${esc(t)}</option>`).join('');
  }

  function fehlerAnzeigen(text) {
    for (const f of fenster.values()) {
      f.objekt?.planG && (f.objekt.planG.innerHTML = '');
      f.inhalt.querySelector('.fe-fehlertext')?.remove();
      f.inhalt.insertAdjacentHTML('beforeend', `<div class="fe-fehlertext">${esc(text)}</div>`);
    }
  }

  let cache = null;   // letzte Antworten des Kerns: { d3, plaene: [[ansicht, antwort]] } – Hervorheben braucht keine neue Abfrage

  /** Zeigt die Antworten in den Fenstern; der gewählte Körper wird hervorgehoben (Zuordnung über die Reihenfolge, siehe familien-ansicht.js). */
  function anwenden(neuEinpassen) {
    if (!cache || tot) return;
    const { d3, plaene } = cache;
    const def = wirt.def();
    const teile = d3.koerper?.teile ?? [];
    const sel = wirt.auswahlKoerper();
    const idx = sel && def ? teilIndexVon(def, zustand.detailgrad, teile.length, sel) : -1;
    const box = idx >= 0 ? teilBox(teile[idx]) : null;
    let mass = 0;
    for (const t of teile) { const b = teilBox(t); if (b) mass = Math.max(mass, ...b.max.map((c, i) => c - b.min[i])); }
    const L = Math.max(50, Math.round((mass || 500) / 10));
    info.textContent = `${teile.length} Körper${sel && idx < 0 ? ' · Hervorheben nicht eindeutig' : ''}`;
    for (const [a, d] of plaene) {
      const f = fenster.get(a);
      f?.inhalt.querySelector('.fe-fehlertext')?.remove();
      f?.objekt.setzen(d, box ? boxProjektion(a, box) : null, neuEinpassen);
    }
    const f3 = fenster.get('3d');
    if (f3) { f3.inhalt.querySelector('.fe-fehlertext')?.remove(); f3.objekt.setzen(d3, { auswahlIndex: idx, laenge: L, neuEinpassen }); }
    behaelter.dataset.stand = JSON.stringify({ typ: zustand.typ, detailgrad: zustand.detailgrad, teile: teile.length, ansichten: ansichten(), auswahl: sel ?? null, hervorgehoben: idx });
  }

  async function laden(opt = {}) {
    if (tot) return;
    const mein = ++nr;
    aufbauen();
    typeFuellen();
    const kz = wirt.kennzeichen();
    if (!zustand.typ) { cache = null; info.textContent = 'Kein Typ'; fehlerAnzeigen('Die Familie hat keinen Typ.'); return; }
    const p = { familie: kz, typ: zustand.typ, detailgrad: zustand.detailgrad };
    try {
      // 3D zuerst: seine Körperteile liefern auch die Rahmen für die Hervorhebung in 2D
      const d3 = await abfrage('familie_ansicht', { ...p, ansicht: '3d' });
      const plaene = await Promise.all(ansichten().filter((a) => a !== '3d').map((a) => abfrage('familie_ansicht', { ...p, ansicht: a }).then((d) => [a, d])));
      if (mein !== nr || tot) return;
      cache = { d3, plaene };
      anwenden(!!opt.neuEinpassen);
    } catch (e) {
      if (mein !== nr || tot) return;
      cache = null;
      const k = kernFehler({ fehler: e.message, vorschlag: e.vorschlag });
      info.textContent = 'Ansicht nicht möglich';
      fehlerAnzeigen(`${k.text}${e.vorschlag ? ' – ' + e.vorschlag : ''}`);
    }
  }

  q('.fe-av-typ').onchange = (e) => { zustand.typ = e.target.value; laden({ neuEinpassen: true }); };
  q('.fe-av-detail').onchange = (e) => { zustand.detailgrad = e.target.value; laden({ neuEinpassen: true }); };
  q('.fe-av-modus').onchange = (e) => { zustand.modus = e.target.value; q('.fe-av-einzeln-l').hidden = zustand.modus !== 'eins'; laden({ neuEinpassen: true }); };
  q('.fe-av-einzeln').onchange = (e) => { zustand.einzeln = e.target.value; laden({ neuEinpassen: true }); };

  return {
    zustand,
    laden,
    /** Modul 2, C2: Gibt es ein 2D-Fenster dieser Ansicht? Sonst auf ein Fenster mit dieser Ansicht umstellen (wartet, bis die Ansichten da sind). */
    async fensterFuer(a) { if (!ansichten().includes(a)) { zustand.modus = 'eins'; zustand.einzeln = a; q('.fu-av-modus').value = 'eins'; q('.fu-av-einzeln').value = a; q('.fu-av-einzeln-l').hidden = false; await laden({ neuEinpassen: true }); } },
    /** Modul 2, C2: Klicks im 2D-Fenster dieser Ansicht melden fn({ u, v, gerastet }); null/null beendet das Anklicken in allen Fenstern (die Marken bleiben, bis markieren() sie ersetzt). */
    picken(a, fn) { for (const [name, f] of fenster) { if (f.objekt instanceof Fenster2D) { f.objekt.pick = a && name === a ? fn : null; f.objekt.svg.classList.toggle('picken', !!(a && name === a)); } } },
    /** Modul 2, C2: angeklickte Passpunkte [{ u, v, label }] im Fenster dieser Ansicht zeigen. */
    markieren(a, punkte) { for (const [name, f] of fenster) if (f.objekt instanceof Fenster2D) f.objekt.setzePunkte(name === a ? punkte : []); },
    /** Auswahl des Körpers geändert: nur neu hervorheben. */
    hervorheben() { anwenden(false); },
    /** Von aussen setzen (Ribbon): Ansicht/Modus/Detailgrad/Typ. */
    einstellen(opt = {}) {
      if (opt.detailgrad) { zustand.detailgrad = opt.detailgrad; q('.fe-av-detail').value = opt.detailgrad; }
      if (opt.typ) zustand.typ = opt.typ;
      if (opt.modus) { zustand.modus = opt.modus; q('.fe-av-modus').value = opt.modus; q('.fe-av-einzeln-l').hidden = opt.modus !== 'eins'; }
      if (opt.ansicht) { zustand.einzeln = opt.ansicht; q('.fe-av-einzeln').value = opt.ansicht; }
      return laden({ neuEinpassen: true });
    },
    zoomAlles() { for (const f of fenster.values()) { if (f.objekt instanceof Fenster2D) { f.objekt.benutzt = false; f.objekt.einpassen(); } else if (f.objekt) { f.objekt.benutzt = false; f.objekt.einpassen(); } } },
    zerstoeren() { tot = true; nr++; for (const f of fenster.values()) f.objekt?.zerstoeren?.(); fenster = new Map(); },
  };
}

