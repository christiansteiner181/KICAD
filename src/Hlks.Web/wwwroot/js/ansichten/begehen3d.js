// «Raum begehen» in der 3D-Ansicht (Nachbearbeitung N15, Ich-Perspektive): perspektivische Kamera (FOV 75°) im Raum auf Augenhöhe, OrbitControls aus,
// der 3D-Schnittbereich (N13) ist für die Dauer abgeschaltet (die gespeicherte Einstellung bleibt unberührt), Tasten und Maus wie in begehen-logik.js
// beschrieben. Esc, der Haus-Knopf des Navigationswürfels oder ein Klick auf eine Würfelfläche beenden das Begehen und stellen Kamera und Schnittbereich
// wieder her. Der Navigationswürfel bleibt sichtbar und dreht mit (er liest Kameraposition und Ziel der Steuerung).
// Hier steht nur, was three und den Browser braucht; die Rechenregeln stehen in begehen-logik.js (Node-getestet). Angeschlossen wird in ansicht3d.js
// (erzeugeBegehen) und wuerfel.js (Option `begehen`).

import { esc } from './hilfen.js';
import { erzeugeDurchgang } from './durchgang3d.js';
import {
  FOV_GRAD, NAH_M, FERN_M, TASTENHILFE, tastenAktion, tastenVerlauf, zustandAusStandort, kameraWerte, projektWerte, tippen, fliessend, umsehen, verschieben,
  einblendung,
} from './begehen-logik.js';

const jetzt = () => (typeof performance !== 'undefined' ? performance.now() : Date.now());
const eingabeFeld = (t) => t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName));

/**
 * o: { wurzel, dom (Leinwand), kamera, steuerung, anfordern(), schnitt() → Schnittbereich | null, benutzt: { lesen(), setzen(b) }, beiBeendet?(),
 *      bauteile?() → Iterable<Körpergruppe> (N22: Wände, Decken usw. sind beim Begehen undurchsichtig, durchscheinend nur im Durchgang) }
 * Rückgabe { starten(standort, augenhoeheMm?), beenden(), neueObjekte(), aktiv, zustand, zerstoeren() }
 */
export function erzeugeBegehen(o) {
  const { wurzel, dom, kamera, steuerung } = o;
  // N22: Durchgang – Bauteile undurchsichtig, durchscheinend nur näher als 500 mm (durchgang3d.js); Kontrolle für Browser-Tests in data-durchgang
  const durchgang = o.bauteile ? erzeugeDurchgang({
    bauteile: o.bauteile, kamera, anfordern: () => o.anfordern(),
    beiAenderung: (z) => { if (z) wurzel.dataset.durchgang = JSON.stringify(z); else delete wurzel.dataset.durchgang; },
  }) : null;
  let aktiv = false;
  let zustand = null;
  let standort = null;
  let gespeichert = null;          // Kamera und Zustand vor dem Begehen
  let umschalt = false;
  let schleife = 0;
  let letzte = 0;
  let zeiger = null;               // { id, taste, x, y }
  const tasten = tastenVerlauf();
  let hud = null;

  const sichtbar = () => wurzel.isConnected && wurzel.getClientRects().length > 0 && wurzel.clientWidth > 0;

  // ---------------------------------------------------------------------------------------------- Einblendung
  function hudBauen() {
    hud = document.createElement('div');
    hud.className = 'd3-begehen';
    hud.setAttribute('role', 'status');
    hud.innerHTML = '<div class="d3-begehen-titel"></div><div class="d3-begehen-zeile d3-begehen-geschoss"></div><div class="d3-begehen-zeile d3-begehen-augen"></div>'
      + `<table class="d3-begehen-tasten">${TASTENHILFE.map(([t, w]) => `<tr><th>${esc(t)}</th><td>${esc(w)}</td></tr>`).join('')}</table>`;
    wurzel.appendChild(hud);
    wurzel.classList.add('d3-begehen-an');
  }
  function hudAktualisieren() {
    if (!hud || !zustand) return;
    const e = einblendung(standort, zustand);
    hud.querySelector('.d3-begehen-titel').textContent = `Begehen · ${e.titel}`;
    hud.querySelector('.d3-begehen-geschoss').textContent = e.geschoss;
    hud.querySelector('.d3-begehen-augen').textContent = e.augenhoehe;
  }

  // ---------------------------------------------------------------------------------------------- Kamera
  function anwenden() {
    const k = kameraWerte(zustand);
    kamera.position.set(k.position.x, k.position.y, k.position.z);
    steuerung.target.set(k.ziel.x, k.ziel.y, k.ziel.z);
    kamera.up.set(0, 1, 0);
    kamera.lookAt(k.ziel.x, k.ziel.y, k.ziel.z);
    kamera.updateMatrixWorld(true);
    hudAktualisieren();
    durchgang?.pruefen();   // N22: nach jeder Bewegung Abstand zu den Bauteilen neu bewerten
    // Kontrolle für Browser-Tests und Fehlersuche: Standort im Projekt (mm) und Blick
    wurzel.dataset.begehen = JSON.stringify({ raum: standort?.raum ?? null, ...projektWerte(zustand), fov: kamera.fov });
    o.anfordern();
  }

  // ---------------------------------------------------------------------------------------------- Tasten
  function schritt() {
    schleife = 0;
    if (!aktiv) return;
    const t = jetzt();
    const dt = Math.min((t - letzte) / 1000, 0.1);
    letzte = t;
    const lauf = tasten.fliessend(t);
    if (lauf.size > 0 && dt > 0) { zustand = fliessend(zustand, lauf, dt, umschalt); anwenden(); }
    if (tasten.haelt) schleife = requestAnimationFrame(schritt);
  }
  function schleifeStarten() {
    if (schleife || !aktiv) return;
    letzte = jetzt();
    schleife = requestAnimationFrame(schritt);
  }
  function schleifeStoppen() { if (schleife) { cancelAnimationFrame(schleife); schleife = 0; } }

  function beiTasteUnten(e) {
    if (!aktiv || !sichtbar() || eingabeFeld(e.target)) return;
    umschalt = e.shiftKey;
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    const a = tastenAktion(e.code);
    if (!a) return;
    if (a === 'ende') {
      if (document.querySelector('.kmenu')) return;   // ein offenes Menü schliesst zuerst
      e.preventDefault(); e.stopImmediatePropagation();
      if (!e.repeat) beenden();
      return;
    }
    e.preventDefault(); e.stopImmediatePropagation();
    const s = tasten.unten(e.code, jetzt(), e.repeat);
    if (s) { zustand = tippen(zustand, s, e.shiftKey); anwenden(); }
    if (tasten.haelt) schleifeStarten();
  }
  function beiTasteOben(e) {
    umschalt = e.shiftKey;
    if (!aktiv) return;
    tasten.oben(e.code);
    if (!tasten.haelt) schleifeStoppen();
  }
  function fensterWeg() { tasten.alleLos(); schleifeStoppen(); }

  // ---------------------------------------------------------------------------------------------- Maus
  function beiMausUnten(e) {
    if (!aktiv || (e.button !== 0 && e.button !== 1) || zeiger) return;
    if (e.button === 1) e.preventDefault();
    zeiger = { id: e.pointerId, taste: e.button, x: e.clientX, y: e.clientY };
    try { dom.setPointerCapture(e.pointerId); } catch { /* egal */ }
    dom.style.cursor = e.button === 1 ? 'move' : 'grabbing';
  }
  function beiMausBewegt(e) {
    if (!aktiv || !zeiger || e.pointerId !== zeiger.id) return;
    if (e.buttons === 0) { beiMausOben(e); return; }   // Loslassen verpasst (ausserhalb des Fensters): nicht an der Maus hängen bleiben
    const dx = e.clientX - zeiger.x, dy = e.clientY - zeiger.y;
    if (dx === 0 && dy === 0) return;
    zeiger.x = e.clientX; zeiger.y = e.clientY;
    zustand = zeiger.taste === 1 ? verschieben(zustand, dx, dy) : umsehen(zustand, dx, dy);
    anwenden();
  }
  function beiMausOben(e) {
    if (!zeiger || e.pointerId !== zeiger.id) return;
    zeiger = null;
    try { dom.releasePointerCapture(e.pointerId); } catch { /* egal */ }
    dom.style.cursor = '';
  }
  const verschlucken = (e) => { if (aktiv) e.preventDefault(); };

  // ---------------------------------------------------------------------------------------------- Beginnen und Beenden
  function anhaengen() {
    window.addEventListener('keydown', beiTasteUnten, true);
    window.addEventListener('keyup', beiTasteOben, true);
    window.addEventListener('blur', fensterWeg);
    dom.addEventListener('pointerdown', beiMausUnten);
    dom.addEventListener('pointermove', beiMausBewegt);
    dom.addEventListener('pointerup', beiMausOben);
    dom.addEventListener('pointercancel', beiMausOben);
    dom.addEventListener('auxclick', verschlucken);
  }
  function loesen() {
    window.removeEventListener('keydown', beiTasteUnten, true);
    window.removeEventListener('keyup', beiTasteOben, true);
    window.removeEventListener('blur', fensterWeg);
    dom.removeEventListener('pointerdown', beiMausUnten);
    dom.removeEventListener('pointermove', beiMausBewegt);
    dom.removeEventListener('pointerup', beiMausOben);
    dom.removeEventListener('pointercancel', beiMausOben);
    dom.removeEventListener('auxclick', verschlucken);
  }

  /** Beginnt das Begehen mit dem Standort der Abfrage raum_begehen; läuft es schon, wird nur umgesetzt (die ursprüngliche Kamera bleibt gemerkt). */
  function starten(neuerStandort, augenhoeheMm = null) {
    const z = zustandAusStandort(neuerStandort, augenhoeheMm);
    if (!z) throw new Error('Der Standort zum Begehen ist unvollständig.');
    if (!aktiv) {
      gespeichert = {
        position: kamera.position.clone(), ziel: steuerung.target.clone(), fov: kamera.fov, nah: kamera.near, fern: kamera.far,
        freigegeben: steuerung.enabled, benutzt: o.benutzt.lesen(),
      };
      aktiv = true;
      anhaengen();
      hudBauen();
      steuerung.enabled = false;
      o.schnitt()?.pausieren(true);
    }
    standort = neuerStandort;
    zustand = z;
    tasten.alleLos();
    schleifeStoppen();
    kamera.fov = FOV_GRAD;
    kamera.near = NAH_M;
    kamera.far = FERN_M;
    kamera.updateProjectionMatrix();
    o.benutzt.setzen(true);   // weder Grössenänderung noch Neuladen stellen die Kamera neu ein
    anwenden();
    durchgang?.starten();     // N22: Bauteile undurchsichtig (beim zweiten Aufruf nur neu bewerten)
  }

  /** Beendet das Begehen: Kamera, Steuerung und Schnittbereich wie vorher. */
  function beenden() {
    if (!aktiv) return;
    aktiv = false;
    schleifeStoppen();
    tasten.alleLos();
    loesen();
    zeiger = null;
    dom.style.cursor = '';
    hud?.remove(); hud = null;
    wurzel.classList.remove('d3-begehen-an');
    delete wurzel.dataset.begehen;
    const g = gespeichert;
    gespeichert = null;
    if (g) {
      kamera.fov = g.fov; kamera.near = g.nah; kamera.far = g.fern;
      kamera.updateProjectionMatrix();
      kamera.position.copy(g.position);
      steuerung.target.copy(g.ziel);
      steuerung.enabled = g.freigegeben;
      steuerung.update();
      o.benutzt.setzen(g.benutzt);
    }
    durchgang?.beenden();   // N22: Deckkraft der Bauteile wie vorher (der Schnittbereich bekommt seine Ebenen unten zurück)
    o.schnitt()?.pausieren(false);
    o.anfordern();
    o.beiBeendet?.();
  }

  return {
    starten, beenden,
    /** Es sind Körper dazugekommen oder weggefallen (N22). */
    neueObjekte: () => durchgang?.neueObjekte(),
    get aktiv() { return aktiv; },
    get zustand() { return zustand; },
    zerstoeren() {
      if (aktiv) { aktiv = false; schleifeStoppen(); loesen(); hud?.remove(); hud = null; durchgang?.beenden(); }
    },
  };
}
