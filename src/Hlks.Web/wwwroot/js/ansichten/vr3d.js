// «In VR begehen» (Nachbearbeitung N23): WebXR-Sitzung auf der Meta Quest – entweder über Quest Link/Air Link mit KICAD in Chrome am PC (OpenXR-Schnittstelle
// der Meta-Link-App) oder im Browser der Quest selbst (adb reverse, Dialog «Quest verbinden»; der Weg für PCs, die Link ablehnen).
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
  SNAP_GRAD, VR_TEXT, knopfZustand, sitzungsOptionen, gierenVonRichtung, kopfWelt, kopfAusWelt, blickGierenWelt, rigStart, rigDrehen, rigAufZiel, rigVersetzen,
  stickBewegung, snapPruefen, teleportZiel, ifcAnfangsSichtbar, bildzeit,
} from './vr-logik.js';
import { erzeugeXr } from './xr-greifen.js';
import { etikett } from './xr-holo.js';   // kurzer Hinweis in VR (hinweisen)
import { erzeugeSitzung } from './xr-sitzung.js';   // XR-H: Sitzungsraum mit dem Modell auf dem Tisch

const jetzt = () => (typeof performance !== 'undefined' ? performance.now() : Date.now());
const rund = (x, n = 3) => { const f = 10 ** n; return Math.round(x * f) / f; };
const SCHREIB_ABSTAND_MS = 250;   // so oft schreibt das Bild die Prüfhilfe data-vr
/** Modul XR: öffnet den Dialog «Quest verbinden» (oberflaeche/quest-dialog.js); dynamischer Import, damit vr3d.js ohne die Oberfläche ladbar bleibt. */
const questDialogOeffnen = () => import('../oberflaeche/quest-dialog.js').then((m) => m.questDialog()).catch((e) => console.error('Quest verbinden', e));

/**
 * o: { wurzel, renderer, kamera, szene, modell (Gruppe der Körper), begehen (begehen3d.js), standortAusKamera() → Standort, anfordern(), meldung?(text, art),
 *      vorRender?() (Schnittbereich vor jedem Bild), schnittNeu?() (Schnittbereich neu bewerten lassen), beiBeendet?(), xr?() → navigator.xr (Vorgabe: navigator.xr), anbinden?(sitzung) → Promise }
 * Rückgabe { knopf(), pruefen(), starten(standort?), beenden(), aktiv, kopf(), netzeAnwenden(), zustand(), api, zerstoeren() }
 */
export function erzeugeVr(o) {
  const { wurzel, renderer, kamera, szene, modell, begehen } = o;
  const xrQuelle = o.xr ?? (() => (typeof navigator !== 'undefined' ? navigator.xr : undefined));
  const api = {
    anbinden: o.anbinden ?? ((s) => renderer.xr.setSession(s)),
    kopfPose: null,   // () → { x, y, z, gieren } im Referenzraum | null (Vorgabe: Kamera des Renderers)
    eingabe: null,    // () → { links: { x, y, schnell }, rechts: { x }, ifc: bool } (Vorgabe: Controller)
    beiEnde: null,    // (ende) → Rückruf nach dem Ende einer gelaufenen Sitzung, ende = { x, z, boden, gieren }: letzter Kopfstand (Welt, m / rad); die VR-Startseite /vr merkt ihn
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
  const netzOriginal = new Map();       // Material eines Netzes → { opacity, transparent, depthWrite } vor der Sitzung (VR zeigt die Unterlage undurchsichtig)
  const quellen = [];                   // XRInputSource je Controller-Index
  const zielende = new Set();           // Controller, die gerade zielen
  let marker = null;
  let xrWerk = null;                    // XR-B Greifen, XR-C Tablet und Messen (xr-greifen.js)
  let raum = null;                      // XR-H Sitzungsraum (xr-sitzung.js); solange er aktiv ist, pausiert das Begehen (keine Sticks, kein Teleport, kein Durchgang)
  let ziel = null;                      // letztes gültiges Teleportziel
  let letzterKopf = null;               // letzter Kopfstand der Sitzung { x, z, boden, gieren } für api.beiEnde

  // ---------------------------------------------------------------------------------------------- Knopf
  function knopfZeigen(k) {
    const z = knopfZustand({ xrVorhanden: !!xrQuelle(), unterstuetzt, aktiv: !!sitzung, startet });
    const b = k.querySelector('.d3-vr-knopf');
    b.textContent = z.text;
    b.setAttribute('aria-disabled', z.deaktiviert ? 'true' : 'false');
    b.classList.toggle('d3-vr-aus', z.deaktiviert);
    b.title = z.hinweis || (sitzung ? 'Die VR-Sitzung beenden' : 'Den Raum mit der VR-Brille begehen');
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
      + '<button type="button" class="d3-vr-quest">Quest im Browser der Brille nutzen …</button>'
      + '<button type="button" class="d3-vr-brille" title="Das Begehen (sonst die 3D-Gesamtansicht) als Auftrag an die Brille senden; in der Quest KICAD VR öffnen">An die Brille senden</button>';   // VR-Auftrag (VrAuftrag.cs)
    k.querySelector('.d3-vr-brille').addEventListener('click', (e) => { e.stopPropagation(); import('../oberflaeche/vr-auftrag.js').then((m) => m.anBrilleSenden()).catch((x) => console.error('An die Brille senden', x)); });   // Modul XR: nur bei grauem Knopf sichtbar (CSS), öffnet «Quest verbinden»
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
  // Kopf im Referenzraum des Rigs. Vorrang hat die Pose des Betrachters aus dem XRFrame (genau der Referenzraum, ohne Verzug). Sonst die Kamera des Renderers:
  // three r160 liefert ihre Lage mit zwei Augen in Weltkoordinaten (die Kamera hängt im Rig) – darum zurück in den Referenzraum (Befund Brillentest 21.10.2026:
  // Kopf, Sitzungsraum, Durchgang und Gehrichtung lagen doppelt um das Rig versetzt, sobald das Rig weit vom Nullpunkt stand bzw. gedreht war).
  const tmpQ = new THREE.Quaternion();
  function kopfPoseVorgabe() {
    const c = renderer.xr.getCamera();
    if (!c.cameras || c.cameras.length === 0) return null;
    try {
      const frame = renderer.xr.getFrame?.(), ref = renderer.xr.getReferenceSpace?.();
      const pose = frame && ref ? frame.getViewerPose(ref) : null;
      if (pose) {
        const p = pose.transform.position, o = pose.transform.orientation;
        tmp.set(0, 0, -1).applyQuaternion(tmpQ.set(o.x, o.y, o.z, o.w));
        return { x: p.x, y: p.y, z: p.z, gieren: gierenVonRichtung({ x: tmp.x, z: tmp.z }) };
      }
    } catch { /* ausserhalb des Bildaufrufs: weiter mit der Kamera */ }
    tmp.set(0, 0, -1).applyQuaternion(c.quaternion);
    const welt = { x: c.position.x, y: c.position.y, z: c.position.z, gieren: gierenVonRichtung({ x: tmp.x, z: tmp.z }) };
    return rig && c.cameras.length > 1 && kamera.parent === rigObj ? kopfAusWelt(rig, welt) : welt;
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
      const start = () => { if (xrWerk?.selectStart(c)) return; zielende.add(c); };   // Tablet, Messen, Antippen und Heranholen gehen vor dem Teleport   // Abzug gedrückt bzw. Pinch der Hand: Zielstrahl
      const ende = () => { xrWerk?.selectEnd(c); if (zielende.delete(c)) teleportieren(c); };   // loslassen: springen
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
    if (!rig || raum?.aktiv) return;
    const z = zielen(c);
    if (!z) return;
    const k = kopfLokal() ?? { x: 0, y: 0, z: 0 };
    rigSetzen(rigAufZiel(rig, k, { x: z.x, z: z.z }, rig.y));
    zustandSchreiben(true);
    o.anfordern?.();
  }

  // ---------------------------------------------------------------------------------------------- IFC-Unterlage
  /** Macht das Netz der IFC-Unterlage für die Dauer der Sitzung undurchsichtig (Deckkraft 1, nicht transparent, schreibt Tiefe); die bisherigen Werte merkt sich netzOriginal. */
  function netzUndurchsichtig(m) {
    for (const mat of [].concat(m.material ?? [])) {
      if (!mat) continue;
      if (!netzOriginal.has(mat)) netzOriginal.set(mat, { opacity: mat.opacity, transparent: mat.transparent, depthWrite: mat.depthWrite });
      if (mat.transparent) mat.needsUpdate = true;   // «transparent» steckt im Shaderschlüssel
      mat.opacity = 1; mat.transparent = false; mat.depthWrite = true;
    }
  }
  /** Stellt die Netze der IFC-Unterlage exakt so her, wie sie vor der Sitzung waren. */
  function netzeZurueck() {
    for (const [mat, w] of netzOriginal) {
      if (mat.transparent !== w.transparent) mat.needsUpdate = true;
      mat.opacity = w.opacity; mat.transparent = w.transparent; mat.depthWrite = w.depthWrite;
    }
    netzOriginal.clear();
  }
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
      netzUndurchsichtig(m);   // Christian 21.10.2026: «alle wände und boden sollten in vr solide sein»
      if (ifcAn) { if (versteckt.delete(m)) m.visible = true; }
      else { if (m.visible) versteckt.add(m); m.visible = false; }
    }
  }
  /** XR-B/XR-C: Greifen, Tablet und Messwerkzeug an die Sitzung hängen (Hände, Controller und Tabletflächen hängen im Rig). */
  function xrBauen() {
    raum = erzeugeSitzung({
      szene, modell, renderer, wurzel, rig: () => rigObj, kopf,
      blick: () => { if (!rig) return null; const k = kopfLokal(); return k ? blickGierenWelt(rig, k.gieren) : rig.gieren; },
      boden: () => rig?.y ?? 0,
      ifc: { an: () => ifcAn, setzen: (an) => ifcSetzen(an) },
      anfordern: o.anfordern, meldung: o.meldung,
      // nach dem Verlassen des Sitzungsraums: der Schnittbereich (N13) hängt seine Ebenen neu an bzw. nimmt sie weg (im Begehen pausiert er), der Durchgang (N22) bewertet neu
      uebergeben: () => { o.schnittNeu?.(); begehen.neueObjekte(); },
    });
    api.sitzung = raum;   // Test-Naht
    xrWerk = erzeugeXr({
      sitzung: raum,
      wurzel, renderer, szene, rig: () => rigObj, controller, quellen: () => quellen, kopf,
      koerperDaten: o.koerperDaten ?? (() => new Map()), objekte: o.objekte ?? (() => new Map()), element: o.element ?? (() => null), gewaehlt: o.gewaehlt ?? (() => null),
      auswaehlen: o.auswaehlen ?? (() => {}), befehl: o.befehl, abfrage: o.abfrage ?? (async () => ({})), status: o.status ?? (async () => ({})), verbindungen: o.verbindungen ?? (() => []),
      meldung: o.meldung, anfordern: o.anfordern,
      aktionen: {
        hoehe: (d) => { if (rig) { rigSetzen({ ...rig, y: rig.y + d }); zustandSchreiben(true); } },
        schnitt: () => o.schnittSchalter?.(), schnittAn: () => !!o.schnittAn?.(), schnittVorhanden: () => o.schnittVorhanden?.() ?? true,
        ifc: () => ifcUmschalten(), ifcAn: () => ifcAn, ende: () => { beenden(); },
      },
    });
    api.xr = xrWerk;
    xrWerk.anbinden();
  }
  function ifcUmschalten() { ifcAn = !ifcAn; netzeAnwenden(); zustandSchreiben(true); o.anfordern?.(); }
  /** VR-Startseite /vr (neuer Auftrag vom PC, ohne neuen Tipp): in der laufenden Sitzung an den Standort einer Abfrage raum_begehen wechseln; ein offener Sitzungsraum wird dabei verlassen. */
  function versetzen(standort) {
    if (!sitzung || !rig || !standort) return false;
    try {
      if (raum?.aktiv) raum.verlassen();
      begehen.starten(standort);   // läuft das Begehen schon, wird nur umgesetzt
    } catch (e) { console.warn('VR: Standort nicht gewechselt', e?.message ?? e); return false; }
    startZustand = { ...begehen.zustand };
    ausrichten = true;             // beim nächsten Bild: Kopf an den neuen Standpunkt
    zustandSchreiben(true);
    return true;
  }
  /** Kurzer Hinweis in VR (Tablet-Zeile des Greifens/Messens); ohne XR-Werk bleibt es bei der Meldung. */
  let hinweisEtikett = null;   // { e, bis } – das Hologramm-Etikett des Hinweises
  function hinweisWeg() { if (!hinweisEtikett) return; szene.remove(hinweisEtikett.e.sprite); hinweisEtikett.e.freigeben(); hinweisEtikett = null; }
  /** Kurzer Hinweis in VR: ein Hologramm-Etikett etwa 1 m vor dem Kopf, 2 s lang. */
  function hinweisen(text) {
    if (!sitzung || !rig) { o.meldung?.(text, 'info'); return; }
    hinweisWeg();
    const e = etikett(text, { breite: 0.6 });
    const k = kopf() ?? { x: rig.x, y: rig.y + 1.6, z: rig.z }, kp = kopfLokal();
    const g = kp ? blickGierenWelt(rig, kp.gieren) : rig.gieren;
    e.sprite.position.set(k.x + Math.cos(g), k.y - 0.1, k.z - Math.sin(g));
    szene.add(e.sprite);
    hinweisEtikett = { e, bis: jetzt() + 2000 };
  }
  function ifcSetzen(an) { if (ifcAn !== !!an) ifcUmschalten(); }   // XR-H: der Sitzungsraum blendet die IFC-Unterlage aus und stellt sie danach wieder her

  // ---------------------------------------------------------------------------------------------- Bild
  /** Merkt den Kopfstand für api.beiEnde (jedes Bild, nicht gedrosselt: der Standpunkt am Ende soll der letzte sein). */
  function kopfMerken() {
    if (!rig) return;
    const k = kopf(), kp = kopfLokal();
    if (k) letzterKopf = { x: k.x, z: k.z, boden: rig.y, gieren: kp ? blickGierenWelt(rig, kp.gieren) : rig.gieren };
  }
  function zustandSchreiben(erzwingen = false) {
    if (!sitzung) return;
    kopfMerken();
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
    if (hinweisEtikett && jetzt() > hinweisEtikett.bis) hinweisWeg();
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
      const b = raum?.aktiv ? { dx: 0, dz: 0 } : stickBewegung(e.links, blickGierenWelt(rig, k.gieren), dt, !!e.links?.schnell);   // im Sitzungsraum steht man am Tisch
      if (b.dx !== 0 || b.dz !== 0) rigSetzen(rigVersetzen(rig, b.dx, b.dz));
      // rechter Stick: Snap-Turn um den Kopf
      snap = snapPruefen(snap, e.rechts?.x);
      if (snap.schritt !== 0 && !raum?.aktiv) { rigSetzen(rigDrehen(rig, k, snap.schritt * SNAP_GRAD)); zustandSchreiben(true); }
      // Taste A rechts: IFC-Unterlage ein/aus
      if (e.ifc && !aVorher) ifcUmschalten();
      aVorher = !!e.ifc;
    }
    if (xrWerk) xrWerk.bild(dt);   // XR-B/XR-C: Greifen, Tablet, Messwerkzeug
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
    if (!raum?.aktiv) begehen.pruefenDurchgang();   // N22 mit der Kopfposition (im Sitzungsraum pausiert das Begehen)
    o.vorRender?.();              // Schnittbereich (N13): hängt die Clipping-Ebenen an die Materialien bzw. nimmt sie weg (pausiert im Begehen) – sonst nur in zeichnen()
    raum?.vorRender();            // XR-H: danach die Etagenebene an den Materialien kontrollieren (die Ansicht setzt sie bei neuen Körpern zurück)
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
      snap = { bereit: true }; aVorher = false; letzteZeit = 0; letztesSchreiben = 0; ifcDreiecke = 0; ifcAn = true; letzterKopf = null;
      zeit.leeren();
      steuerungBauen();
      xrBauen();
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
    const ende = wurzel.classList.contains('d3-vr-an') ? (letzterKopf ?? (rig ? { x: rig.x, z: rig.z, boden: rig.y, gieren: rig.gieren } : null)) : null;   // nur nach einer wirklich gelaufenen Sitzung
    letzterKopf = null;
    sitzung = null;
    try { s?.removeEventListener?.('end', beiEnde); } catch { /* egal */ }
    try { renderer.setAnimationLoop(null); } catch { /* egal */ }
    for (const m of versteckt) m.visible = true;
    versteckt.clear();
    try { raum?.abbauen(); } catch { /* egal */ }   // XR-H: Modell, Hintergrund, Lichter, Clipping zurückstellen (vor dem Abbau des Rigs)
    raum = null; api.sitzung = null;
    netzeZurueck();
    try { xrWerk?.abbauen(); } catch { /* egal */ }
    xrWerk = null; api.xr = null;
    steuerungAbbauen();
    hinweisWeg();
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
    if (ende) { try { api.beiEnde?.(ende); } catch (e) { console.error('VR: Rückruf am Sitzungsende', e); } }
  }

  return {
    knopf, pruefen, starten, beenden, kopf, netzeAnwenden, api, versetzen, hinweisen,
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
