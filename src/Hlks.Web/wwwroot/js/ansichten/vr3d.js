// «In VR begehen» (Nachbearbeitung N23): WebXR-Sitzung auf der Meta Quest über Air Link, KICAD läuft in Chrome am PC (OpenXR-Schnittstelle der Meta-Quest-Link-App).
// Diese Datei hängt die Sitzung an die vorhandene 3D-Ansicht (three, r160, WebXRManager des Renderers) und ans Begehen am Bildschirm (begehen3d.js):
//   Knopf «In VR begehen» (3D-Ansicht und Einblendung des Begehens) → immersive-vr mit Referenzraum local-floor → Begehen startet (Schnittbereich aus, Bauteile
//   undurchsichtig, N22 mit der Kopfposition) → die Kamera hängt in einem Rig (Gruppe), das auf OK Boden an den Standpunkt gesetzt wird → Controller bzw. Hände bewegen das Rig
//   → Ende der Sitzung (Quest-Menü, Esc am PC, Knopf «VR beenden») stellt Kamera, Darstellung und Begehen wieder her.
// Die Rechenregeln (Rig, Sticks, Snap-Turn, Teleport, Knopfzustand) stehen in vr-logik.js und sind in Node getestet.
//
// Prüfhilfen: .d3-wurzel[data-vr] (JSON: Rig, Kopf, Hände, IFC, Bildzeit), Klasse d3-vr-an, data-vr-status am Knopf.
// Test-Nähte (Browser-Reihe mit nachgebildetem navigator.xr): api.anbinden(sitzung), api.kopfPose(), api.eingabe() sind austauschbar.

import * as THREE from 'three';
import {
  SNAP_GRAD, VR_TEXT, knopfZustand, sitzungsOptionen, gierenVonRichtung, kopfWelt, blickGierenWelt, rigStart, rigDrehen, rigAufZiel, rigVersetzen,
  stickBewegung, snapPruefen, teleportZiel, ifcAnfangsSichtbar, bildzeit,
} from './vr-logik.js';

const jetzt = () => (typeof performance !== 'undefined' ? performance.now() : Date.now());
const rund = (x, n = 3) => { const f = 10 ** n; return Math.round(x * f) / f; };
const SCHREIB_ABSTAND_MS = 250;   // so oft schreibt das Bild die Prüfhilfe data-vr
/** Modul XR: öffnet den Dialog «Quest verbinden» (oberflaeche/quest-dialog.js); dynamischer Import, damit vr3d.js ohne die Oberfläche ladbar bleibt. */
const questDialogOeffnen = () => import('../oberflaeche/quest-dialog.js').then((m) => m.questDialog()).catch((e) => console.error('Quest verbinden', e));

/**
 * o: { wurzel, renderer, kamera, szene, modell (Gruppe der Körper), begehen (begehen3d.js), standortAusKamera() → Standort, anfordern(), meldung?(text, art),
 *      vorRender?() (Schnittbereich vor jedem Bild), beiBeendet?(), xr?() → navigator.xr (Vorgabe: navigator.xr), anbinden?(sitzung) → Promise }
 * Rückgabe { knopf(), pruefen(), starten(standort?), beenden(), aktiv, kopf(), netzeAnwenden(), zustand(), api, zerstoeren() }
 */
export function erzeugeVr(o) {
  const { wurzel, renderer, kamera, szene, modell, begehen } = o;
  const xrQuelle = o.xr ?? (() => (typeof navigator !== 'undefined' ? navigator.xr : undefined));
  const api = {
    anbinden: o.anbinden ?? ((s) => renderer.xr.setSession(s)),
    kopfPose: null,   // () → { x, y, z, gieren } im Referenzraum | null (Vorgabe: Kamera des Renderers)
    eingabe: null,    // () → { links: { x, y, schnell }, rechts: { x }, ifc: bool } (Vorgabe: Controller)
  };
  const knoepfe = new Map();   // Element → war schon eingehängt
  const zeit = bildzeit();
  const tmp = new THREE.Vector3();
  let unterstuetzt = null;
  let startet = false;
  let sitzung = null;
  let zerstoert = false;

  // Zustand der laufenden Sitzung
  let rigObj = null, rig = null;        // three-Gruppe und ihr Zustand { x, y, z, gieren }
  let ausrichten = false;               // beim ersten gültigen Kopf einmal auf den Standpunkt ausrichten
  let startZustand = null;              // Standpunkt des Begehens beim Start
  let gespeichert = null;               // Kamera und Renderer vor VR
  let warBegehen = false;               // das Begehen lief schon vor VR (dann bleibt es danach)
  let snap = { bereit: true };
  let letzteZeit = 0, letztesSchreiben = 0, aVorher = false;
  let ifcAn = true, ifcDreiecke = 0;
  const versteckt = new Set();          // Netze der IFC-Unterlage, die VR ausgeblendet hat
  const quellen = [];                   // XRInputSource je Controller-Index
  const zielende = new Set();           // Controller, die gerade zielen
  let marker = null;
  let ziel = null;                      // letztes gültiges Teleportziel

  // ---------------------------------------------------------------------------------------------- Knopf
  function knopfZeigen(k) {
    const z = knopfZustand({ xrVorhanden: !!xrQuelle(), unterstuetzt, aktiv: !!sitzung, startet });
    const b = k.querySelector('.d3-vr-knopf');
    b.textContent = z.text;
    b.setAttribute('aria-disabled', z.deaktiviert ? 'true' : 'false');
    b.classList.toggle('d3-vr-aus', z.deaktiviert);
    b.title = z.hinweis || (sitzung ? 'Die VR-Sitzung beenden' : 'Den Raum mit der VR-Brille begehen (Meta Quest über Air Link)');
    k.querySelector('.d3-vr-hinweis').textContent = z.hinweis;
    k.dataset.vrStatus = sitzung ? 'aktiv' : startet ? 'startet' : z.deaktiviert ? 'aus' : 'bereit';
  }
  function knoepfeAktualisieren() {
    for (const [k, eingehaengt] of [...knoepfe]) {
      if (k.isConnected) knoepfe.set(k, true);
      else if (eingehaengt) { knoepfe.delete(k); continue; }   // war da und ist wieder weg (Einblendung des Begehens abgebaut)
      knopfZeigen(k);
    }
  }
  /** Ein neuer Knopf «In VR begehen» (Einblendung des Begehens oder 3D-Ansicht); alle Knöpfe zeigen denselben Zustand. */
  function knopf() {
    const k = document.createElement('div');
    k.className = 'd3-vr';
    k.innerHTML = '<button type="button" class="d3-vr-knopf"></button><span class="d3-vr-hinweis"></span>'
      + '<button type="button" class="d3-vr-quest">Quest im Browser der Brille nutzen …</button>';   // Modul XR: nur bei grauem Knopf sichtbar (CSS), öffnet «Quest verbinden»
    k.querySelector('.d3-vr-quest').addEventListener('click', (e) => { e.stopPropagation(); questDialogOeffnen(); });
    k.querySelector('.d3-vr-knopf').addEventListener('click', (e) => {
      e.stopPropagation();
      if (sitzung) { beenden(); return; }
      if (k.dataset.vrStatus === 'aus') { pruefen().then((ok) => { if (ok) starten().catch(() => {}); else questDialogOeffnen(); }); return; }   // Brille inzwischen verbunden? sonst Hinweis und «Quest verbinden»
      starten().catch(() => {});
    });
    knoepfe.set(k, false);
    knopfZeigen(k);
    if (unterstuetzt === null) pruefen();
    return k;
  }

  /** Fragt den Browser, ob eine VR-Sitzung möglich ist (Quest Link/Air Link läuft, OpenXR-Runtime «Meta Quest Link» aktiv). */
  async function pruefen() {
    const xr = xrQuelle();
    if (!xr || typeof xr.isSessionSupported !== 'function') unterstuetzt = false;
    else { try { unterstuetzt = (await xr.isSessionSupported('immersive-vr')) === true; } catch { unterstuetzt = false; } }
    knoepfeAktualisieren();
    return unterstuetzt;
  }
  const geraeteWechsel = () => { pruefen(); };
  try { xrQuelle()?.addEventListener?.('devicechange', geraeteWechsel); } catch { /* egal */ }

  // ---------------------------------------------------------------------------------------------- Kopf
  /** Kopfpose im Referenzraum (vor der Rig-Drehung) aus dem XR-Kameraobjekt des Renderers; null, solange die Brille keine Pose liefert. */
  function kopfPoseVorgabe() {
    const c = renderer.xr.getCamera();
    if (!c.cameras || c.cameras.length === 0) return null;
    tmp.set(0, 0, -1).applyQuaternion(c.quaternion);
    return { x: c.position.x, y: c.position.y, z: c.position.z, gieren: gierenVonRichtung({ x: tmp.x, z: tmp.z }) };
  }
  const kopfLokal = () => (api.kopfPose ?? kopfPoseVorgabe)();
  /** Kopfposition in der Welt (m) oder null, wenn keine VR-Sitzung läuft (begehen3d.js: Durchgang N22). */
  function kopf() {
    if (!sitzung || !rig) return null;
    const k = kopfLokal();
    return k ? kopfWelt(rig, k) : { x: rig.x, y: rig.y + 1.6, z: rig.z };
  }

  // ---------------------------------------------------------------------------------------------- Rig
  function rigSetzen(neu) {
    rig = neu;
    rigObj.position.set(rig.x, rig.y, rig.z);
    rigObj.rotation.set(0, rig.gieren, 0);
    rigObj.updateMatrixWorld(true);
  }

  // ---------------------------------------------------------------------------------------------- Eingabe
  function achsen(q) {
    const a = q?.gamepad?.axes;
    if (!a) return null;
    return a.length >= 4 ? { x: a[2], y: a[3] } : { x: a[0] ?? 0, y: a[1] ?? 0 };
  }
  function eingabeVorgabe() {
    const e = { links: { x: 0, y: 0, schnell: false }, rechts: { x: 0 }, ifc: false };
    for (const q of quellen) {
      const a = achsen(q);
      if (!a) continue;
      if (q.handedness === 'left') { e.links.x = a.x; e.links.y = a.y; e.links.schnell = !!q.gamepad.buttons?.[3]?.pressed; }
      else if (q.handedness === 'right') { e.rechts.x = a.x; e.ifc = !!q.gamepad.buttons?.[4]?.pressed; }
    }
    return e;
  }

  const controller = () => [renderer.xr.getController(0), renderer.xr.getController(1)];
  api.controller = controller;   // Test-Naht: die beiden Controller-Objekte (Ereignisse selectstart/selectend einspeisen, Lage setzen)
  api.modell = modell;           // Test-Naht: die Körpergruppe (Netze der IFC-Unterlage nachbilden)
  function steuerungBauen() {
    controller().forEach((c, i) => {
      const strahl = new THREE.Line(
        new THREE.BufferGeometry().setFromPoints([new THREE.Vector3(0, 0, 0), new THREE.Vector3(0, 0, -1)]),
        new THREE.LineBasicMaterial({ color: 0x1e90ff, transparent: true, opacity: 0.8, depthTest: false }));
      strahl.name = 'vr-strahl';
      strahl.scale.z = 0.4;
      strahl.renderOrder = 998;
      strahl.visible = false;
      c.add(strahl);
      const verbunden = (e) => { quellen[i] = e.data; strahl.visible = true; zustandSchreiben(true); };
      const getrennt = () => { quellen[i] = null; zielende.delete(c); strahl.visible = false; zustandSchreiben(true); };
      const start = () => { zielende.add(c); };   // Abzug gedrückt bzw. Pinch der Hand: Zielstrahl
      const ende = () => { if (zielende.delete(c)) teleportieren(c); };   // loslassen: springen
      c.addEventListener('connected', verbunden);
      c.addEventListener('disconnected', getrennt);
      c.addEventListener('selectstart', start);
      c.addEventListener('selectend', ende);
      c.userData.vr = {
        strahl,
        lose() {
          c.removeEventListener('connected', verbunden); c.removeEventListener('disconnected', getrennt);
          c.removeEventListener('selectstart', start); c.removeEventListener('selectend', ende);
          c.remove(strahl); strahl.geometry.dispose(); strahl.material.dispose();
        },
      };
      rigObj.add(c);
    });
    marker = new THREE.Mesh(new THREE.RingGeometry(0.14, 0.2, 40).rotateX(-Math.PI / 2), new THREE.MeshBasicMaterial({ color: 0x1e90ff, transparent: true, opacity: 0.9, depthTest: false }));
    marker.renderOrder = 999;
    marker.visible = false;
    szene.add(marker);
  }
  function steuerungAbbauen() {
    for (const c of controller()) {
      c.userData.vr?.lose();
      delete c.userData.vr;
      c.removeFromParent();
    }
    if (marker) { szene.remove(marker); marker.geometry.dispose(); marker.material.dispose(); marker = null; }
    quellen.length = 0; zielende.clear(); ziel = null;
  }

  /** Zielstrahl eines Controllers (Hand: Pinch-Strahl) auf den Boden des Geschosses. */
  function zielen(c) {
    c.updateMatrixWorld(true);
    const u = new THREE.Vector3().setFromMatrixPosition(c.matrixWorld);
    const d = new THREE.Vector3(0, 0, -1).transformDirection(c.matrixWorld);
    return teleportZiel(u, d, rig.y);
  }
  function teleportieren(c) {
    if (!rig) return;
    const z = zielen(c);
    if (!z) return;
    const k = kopfLokal() ?? { x: 0, y: 0, z: 0 };
    rigSetzen(rigAufZiel(rig, k, { x: z.x, z: z.z }, rig.y));
    zustandSchreiben(true);
    o.anfordern?.();
  }

  // ---------------------------------------------------------------------------------------------- IFC-Unterlage
  /** Blendet das Hintergrund-Netz der IFC-Unterlage in VR aus oder ein (Bildrate); neu geladene Netze folgen der Wahl. */
  function netzeAnwenden() {
    if (!sitzung) return;
    let dreiecke = 0;
    const netze = [];
    modell.traverse((m) => {
      if (m.userData?.netz === true && m.geometry) { netze.push(m); dreiecke += (m.geometry.index?.count ?? m.geometry.attributes.position?.count ?? 0) / 3; }
    });
    if (ifcDreiecke === 0 && dreiecke > 0) ifcAn = ifcAnfangsSichtbar(dreiecke);   // die erste Messung entscheidet die Vorgabe
    ifcDreiecke = dreiecke;
    for (const m of netze) {
      if (ifcAn) { if (versteckt.delete(m)) m.visible = true; }
      else { if (m.visible) versteckt.add(m); m.visible = false; }
    }
  }
  function ifcUmschalten() { ifcAn = !ifcAn; netzeAnwenden(); zustandSchreiben(true); o.anfordern?.(); }

  // ---------------------------------------------------------------------------------------------- Bild
  function zustandSchreiben(erzwingen = false) {
    if (!sitzung) return;
    const t = jetzt();
    if (!erzwingen && t - letztesSchreiben < SCHREIB_ABSTAND_MS) return;
    letztesSchreiben = t;
    const k = kopf(), kp = rig ? kopfLokal() : null;
    wurzel.dataset.vr = JSON.stringify({
      aktiv: true, rig: rig ? { x: rund(rig.x), y: rund(rig.y), z: rund(rig.z), gieren: rund(rig.gieren) } : null,
      kopf: k ? { x: rund(k.x), y: rund(k.y), z: rund(k.z) } : null,
      blickGrad: rig && kp ? rund(blickGierenWelt(rig, kp.gieren) / (Math.PI / 180), 2) : null,
      kamera: (() => { const p = tmp.setFromMatrixPosition(kamera.matrixWorld); return { x: rund(p.x), y: rund(p.y), z: rund(p.z) }; })(),
      haende: quellen.some((q) => !!q?.hand), steuerung: quellen.filter((q) => q && !q.hand).length,
      ifc: { sichtbar: ifcAn, dreiecke: Math.round(ifcDreiecke) }, bildMs: rund(zeit.mittel, 2), bilder: zeit.anzahl,
      aufrufe: renderer.info?.render?.calls ?? 0, dreieckeBild: renderer.info?.render?.triangles ?? 0,
    });
  }

  function bild(zeitstempel) {
    if (!sitzung || !rig) return;
    const t0 = jetzt();
    const dt = letzteZeit ? Math.min(Math.max(((zeitstempel ?? t0) - letzteZeit) / 1000, 0), 0.1) : 0;
    letzteZeit = zeitstempel ?? t0;
    const k = kopfLokal();
    if (k) {
      if (api.kopfPose) { kamera.position.set(k.x, k.y, k.z); kamera.rotation.set(0, k.gieren - Math.PI / 2, 0); }   // Attrappe (Tests): sonst setzt die Brille die Kamera selbst
      if (ausrichten) {   // erster gültiger Kopf: an den Standpunkt und in die Blickrichtung des Begehens stellen
        ausrichten = false;
        rigSetzen(rigStart(startZustand, k));
      }
      const e = (api.eingabe ?? eingabeVorgabe)();
      // linker Stick: gehen in Blickrichtung des Kopfes, ohne Höhe (keine Kollision)
      const b = stickBewegung(e.links, blickGierenWelt(rig, k.gieren), dt, !!e.links?.schnell);
      if (b.dx !== 0 || b.dz !== 0) rigSetzen(rigVersetzen(rig, b.dx, b.dz));
      // rechter Stick: Snap-Turn um den Kopf
      snap = snapPruefen(snap, e.rechts?.x);
      if (snap.schritt !== 0) { rigSetzen(rigDrehen(rig, k, snap.schritt * SNAP_GRAD)); zustandSchreiben(true); }
      // Taste A rechts: IFC-Unterlage ein/aus
      if (e.ifc && !aVorher) ifcUmschalten();
      aVorher = !!e.ifc;
    }
    // Zielstrahl und Marker
    ziel = null;
    for (const c of controller()) {
      const v = c.userData.vr;
      if (!v) continue;
      if (zielende.has(c)) {
        const z = zielen(c);
        if (z) ziel = z;
        v.strahl.scale.z = z ? z.distanz : 5;
        v.strahl.material.color.setHex(z ? 0x1e90ff : 0x999999);
      } else v.strahl.scale.z = 0.4;
    }
    if (marker) { marker.visible = !!ziel; if (ziel) marker.position.set(ziel.x, ziel.y + 0.01, ziel.z); }
    begehen.pruefenDurchgang();   // N22 mit der Kopfposition
    o.vorRender?.();              // Schnittbereich (N13): hängt die Clipping-Ebenen an die Materialien bzw. nimmt sie weg (pausiert im Begehen) – sonst nur in zeichnen()
    renderer.render(szene, kamera);
    zeit.neu(jetzt() - t0);
    zustandSchreiben();
  }

  // ---------------------------------------------------------------------------------------------- Beginnen und Beenden
  function kameraSichern() {
    gespeichert = {
      position: kamera.position.clone(), quaternion: kamera.quaternion.clone(), scale: kamera.scale.clone(), parent: kamera.parent,
      fov: kamera.fov, zoom: kamera.zoom, aspect: kamera.aspect, nah: kamera.near, fern: kamera.far,
      schatten: renderer.shadowMap.enabled, xr: renderer.xr.enabled,
    };
  }
  function kameraZurueck() {
    const g = gespeichert;
    if (!g) return;
    kamera.removeFromParent();
    if (g.parent) g.parent.add(kamera);
    kamera.position.copy(g.position); kamera.quaternion.copy(g.quaternion); kamera.scale.copy(g.scale);
    kamera.fov = g.fov; kamera.zoom = g.zoom; kamera.aspect = g.aspect; kamera.near = g.nah; kamera.far = g.fern;
    kamera.updateProjectionMatrix();
    kamera.updateMatrixWorld(true);
    renderer.shadowMap.enabled = g.schatten;
    renderer.xr.enabled = g.xr;
    gespeichert = null;
  }

  /**
   * Startet die VR-Sitzung. standort: Antwort der Abfrage raum_begehen (sonst der laufende Standort des Begehens, sonst der aktuelle Kamerastandpunkt).
   * Muss aus einem Klick heraus aufgerufen werden (WebXR verlangt eine Benutzeraktion): requestSession kommt vor jedem await.
   */
  async function starten(standort = null) {
    if (sitzung || startet || zerstoert) return;
    const xr = xrQuelle();
    if (!xr) { o.meldung?.(VR_TEXT.keinBrowser, 'warnung'); return; }
    startet = true;
    knoepfeAktualisieren();
    let s = null;
    try {
      warBegehen = begehen.aktiv;
      const st = standort ?? (warBegehen ? begehen.standort : o.standortAusKamera());
      if (!st) throw new Error('Der Standort für VR ist unvollständig.');
      s = await xr.requestSession('immersive-vr', sitzungsOptionen());   // zuerst: braucht die Benutzeraktion
      sitzung = s;
      s.addEventListener('end', beiEnde);
      if (!warBegehen || standort) begehen.starten(st);   // Schnittbereich aus, Bauteile undurchsichtig, Kamera im Raum (Begehen am Bildschirm)
      startZustand = { ...begehen.zustand };
      kameraSichern();
      renderer.shadowMap.enabled = false;          // Schatten aus (Bildrate)
      renderer.xr.enabled = true;
      rigObj = new THREE.Group();
      rigObj.name = 'vr-rig';
      szene.add(rigObj);
      rigObj.add(kamera);
      rigSetzen(rigStart(startZustand));           // bis zum ersten Kopfwert: Rig auf dem Standpunkt, Kopf im Nullpunkt
      ausrichten = true;
      snap = { bereit: true }; aVorher = false; letzteZeit = 0; letztesSchreiben = 0; ifcDreiecke = 0; ifcAn = true;
      zeit.leeren();
      steuerungBauen();
      renderer.xr.setReferenceSpaceType('local-floor');
      await api.anbinden(s);
      if (!sitzung) return;                        // inzwischen beendet
      wurzel.classList.add('d3-vr-an');
      netzeAnwenden();
      startet = false;
      renderer.setAnimationLoop(bild);
      zustandSchreiben(true);
      knoepfeAktualisieren();
    } catch (e) {
      console.error('VR: Sitzung konnte nicht gestartet werden', e);
      if (s) { try { await s.end(); } catch { /* egal */ } }
      aufraeumen();
      o.meldung?.(`VR konnte nicht gestartet werden: ${e?.message ?? e}`, 'warnung');
    }
  }

  // Das Quest-Menü beendet die Sitzung von selbst: erst three (onSessionEnd, Render-Ziel), dann wir – deshalb einen Takt später
  function beiEnde() { setTimeout(aufraeumen, 0); }

  /** Beendet die Sitzung (Knopf, Esc am PC) und wartet, bis alles wieder steht. */
  async function beenden() {
    const s = sitzung;
    if (!s) return;
    try { await s.end(); } catch { /* war schon zu */ }
    aufraeumen();
  }

  /** Stellt Kamera, Darstellung und Begehen wieder her (idempotent). */
  function aufraeumen() {
    startet = false;
    if (!sitzung && !rigObj) { knoepfeAktualisieren(); return; }
    const s = sitzung;
    sitzung = null;
    try { s?.removeEventListener?.('end', beiEnde); } catch { /* egal */ }
    try { renderer.setAnimationLoop(null); } catch { /* egal */ }
    for (const m of versteckt) m.visible = true;
    versteckt.clear();
    steuerungAbbauen();
    kameraZurueck();
    if (rigObj) { rigObj.removeFromParent(); rigObj = null; }
    rig = null; startZustand = null; ausrichten = false;
    wurzel.classList.remove('d3-vr-an');
    delete wurzel.dataset.vr;
    // lief das Begehen nur für VR, endet es jetzt (Kamera, Schnittbereich, Darstellung wie vorher); sonst bleibt der Raum am Bildschirm
    if (!warBegehen && begehen.aktiv) begehen.beenden();
    else begehen.pruefenDurchgang();   // zurück zur Kamera als Messpunkt
    knoepfeAktualisieren();
    o.beiBeendet?.();
    o.anfordern?.();
  }

  return {
    knopf, pruefen, starten, beenden, kopf, netzeAnwenden, api,
    get aktiv() { return !!sitzung; },
    get startet() { return startet; },
    get unterstuetzt() { return unterstuetzt; },
    /** Kontrolle für Tests und Fehlersuche. */
    zustand: () => ({ aktiv: !!sitzung, rig: rig ? { ...rig } : null, kopf: kopf(), ifcAn, ifcDreiecke, bildMs: zeit.mittel, bilder: zeit.anzahl, ziel }),
    /** Ein Bild von Hand rechnen (Tests ohne XR-Schleife). */
    bild: (zeitstempel) => bild(zeitstempel),
    zerstoeren() {
      zerstoert = true;
      try { xrQuelle()?.removeEventListener?.('devicechange', geraeteWechsel); } catch { /* egal */ }
      if (sitzung) { try { sitzung.end?.(); } catch { /* egal */ } aufraeumen(); }
      knoepfe.clear();
    },
  };
}
