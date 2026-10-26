// 3D-Ansicht (three.js, lokal in lib/three): baut die Körper aus /api/koerper (Kern: Quader, Prismen, Zylinder, Kugeln).
// koerper3d.js macht daraus three-Objekte; diese Datei hält Szene, Kamera, Bedienung und das Nachladen.
// Koordinaten: x = x, y = Höhe (z des Modells), z = −y, Einheit Meter. Gerendert wird nur bei Bedarf (nach Änderung oder
// Bewegung), nie dauernd. Körper liegen in einer Map id → Gruppe, damit gezielt ersetzt werden kann. Nach einer Änderung
// werden die geänderten Körper neu geholt; ist eine Wand, Tür, ein Fenster oder eine Decke betroffen, alle (die
// Wandanschlüsse wirken auf die Nachbarn).
// Hintergrund-Netze von IFC-Unterlagen (Körperteil «netz», Modul 1b): die Netzdatei wird einmal je Unterlage geladen und gelesen
// (netzdatei.js), alle Hintergrund-Produkte bilden EINE Geometrie (Zwischenspeicher `netze`); sie wird nur neu geladen, wenn sich
// der Dateiname der Unterlage oder die Auswahl der Ansicht ändert (N12, ifcDarstellung: nachbau → Hintergrund, sonst alle Produkte;
// ifc-darstellung.js). Das Mesh ist durchscheinend, doppelseitig und nie ein Treffer.
// Darstellung «Hülle» (Ansichtseigenschaft darstellung = huelle, Modul 1c): zusätzlich werden die Raumgrenzen (Abfrage raumgrenzen
// mit geometrie) als farbige, durchscheinende Flächen gezeigt (huelle3d.js, eine Geometrie je Angrenzung); die Architekturkörper
// werden blasser. Klick auf eine Fläche zeigt Raum, Nachbar, Bauteil, Fläche und U im Infofeld; Legende unten links.
// 3D-Schnittbereich (N13): Clipping, Griffe und Bedienung stehen in schnittbereich3d.js (hier nur die Anschlüsse); die Ansichtseigenschaften
// schnittbereich und startansicht liegen im Modell (Kern, element_aendern), die Startansicht nicht mehr je Rechner im Browser.
// Gesten (Modul G): navigieren({ drehen, rollen, verschieben, zoom }) dreht und rollt um die Mitte der Auswahl bzw. des Modells (navigieren-logik.js). Die
// OrbitControls haben eine feste Hochrichtung (+y); die freie Lage entsteht als ROLLE um die Blickachse, die nach jeder Aktualisierung der Steuerung angewendet wird
// (kamera.rotateZ). Maus (dreht weiter um die Hochachse des Modells, die Rolle bleibt), Treffer, Schnittbereich und Würfel rechnen mit der echten Kamera.
// Navigationswürfel, Startansicht (Haus), «alles zeigen», Begehen, VR und Blickpunkte richten die Ansicht wieder auf (Rolle 0).

import * as THREE from 'three';
import { zustand, element, auswaehlen, derKlasse, merken } from '../zustand.js';
import { holen, abfrage } from '../api.js';
import { sichtbar } from './filter.js';
import { baueKoerper, hervorheben, freigeben, brauchtAlleKoerper, koerperUrl, netzSchluessel, netzUrl, netzGeometrie, netzMesh, BLASS_HUELLE } from './koerper3d.js';
import { istHuelle, legendeDaten, legendeHtml, infoHtml } from './huelle-darstellung.js';
import { baueHuelle, zeileAmTreffer, umrissLinie, huelleFreigeben } from './huelle3d.js';
import { leseNetz } from './netzdatei.js';
import { netzAuswahl, nachbauStil } from './ifc-darstellung.js';
import { cssVar, esc } from './hilfen.js';
import { ORBIT_TASTEN, mitteDoppelklick } from './maus.js';
import { erzeugeWuerfel } from './wuerfel.js';
import { gebaeudeAusrichtungGrad, nordwinkelGrad, startansichtSchluessel, schwenkZiel, startansichtZiel, startansichtLesen, zielWinkel, START_VORGABE } from './wuerfel-logik.js';
import { erzeugeSchnittbereich } from './schnittbereich3d.js';
import { erzeugeBegehen } from './begehen3d.js';
import { erzeugeVr } from './vr3d.js';
import { erzeugeBrillen } from './xr-teilnehmer.js';
import { standortAusKamera } from './vr-logik.js';
import { startansichtUebernahme } from './schnittbereich-logik.js';
import { befehl } from '../api.js';
import { navigieren3d, rolleZwischen } from './navigieren-logik.js';

import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

const FREMD_DAUER = 1500;
const KLICK_TOLERANZ = 4;   // px
const ENTPRELLUNG = 100;    // ms zwischen Ereignis und Neuholen der Körper
const NETZ_WIEDERHOLEN = 10000;   // ms, bis ein fehlgeschlagenes Netz erneut geladen wird
const HUELLE_ENTPRELLUNG = 600;   // ms zwischen Modelländerung und Neuholen der Raumgrenzen (Darstellung Hülle)

function erstellen(behaelter, ansichtElement) {
  let ansicht = ansichtElement;
  const objekte = new Map();     // id → Gruppe (three)
  const daten = new Map();       // id → Körper des Kerns (JSON), für Neuaufbau bei Designwechsel und Filteränderung
  const timer = new Map();       // id → Zeitgeber der «fremd»-Hervorhebung
  const fremdOffen = new Set();  // Ids fremder Änderungen, die nach dem Neuholen aufleuchten
  const anstehend = { alle: false, ids: new Set(), zoom: false };
  let ladeTimer = null;
  let laeuft = false;
  const fremdSet = new Set();
  const netze = new Map();       // Id der Unterlage → { schluessel, zusage → BufferGeometry | null, fehlerZeit } (Hintergrund-Netze)
  let gewaehlt = null;
  let geplant = 0;
  let zerstoert = false;
  let benutzt = false;     // Kamera vom Benutzer bewegt: dann bleibt sie bei Grössenänderung
  let hatGezoomt = false;  // nach dem ersten Laden einmal einpassen
  let huelleAn = istHuelle(ansichtElement);   // Darstellung «Hülle»
  let huelleNr = 0;                           // gegen veraltete Antworten der Abfrage raumgrenzen
  let huelleTimer = null;
  let huelleGruppe = null;                    // three-Gruppe der Hüllflächen (ausserhalb von «modell», zählt nicht zum Einpassen)
  let huelleUmriss = null;                    // Hervorhebung der angeklickten Raumgrenze
  let schnitt = null;                         // 3D-Schnittbereich (schnittbereich3d.js)
  let vr = null;                              // N23: «In VR begehen» (vr3d.js)

  behaelter.innerHTML = '<div class="d3-wurzel"><div class="d3-tip" hidden></div><div class="d3-hinweis" hidden></div><div class="d3-legende hl-feldbox" hidden></div><div class="d3-info hl-feldbox" hidden></div><div class="d3-bedienung" title="Bedienung wie in Revit">Links drehen · Mitte verschieben · Umschalt + Mitte drehen · Rad zoomen · Doppelklick Mitte: alles zeigen</div></div>';
  const wurzel = behaelter.querySelector('.d3-wurzel');
  const tip = wurzel.querySelector('.d3-tip');
  const hinweis = wurzel.querySelector('.d3-hinweis');
  const legende = wurzel.querySelector('.d3-legende');
  const infoFeld = wurzel.querySelector('.d3-info');

  let renderer;
  try {
    renderer = new THREE.WebGLRenderer({ antialias: true });
  } catch (e) {
    console.error('3D: WebGL nicht verfügbar', e);
    hinweis.hidden = false;
    hinweis.innerHTML = `3D-Ansicht nicht verfügbar:<br>WebGL kann in diesem Browser nicht gestartet werden.`;
    return { aktualisieren() {}, ansichtGeaendert() {}, auswahlGeaendert() {}, zoomAlles() {}, zerstoeren() { behaelter.replaceChildren(); } };
  }
  // Volle Bildschirmauflösung (auch hochauflösende Monitore mit Skalierung über 200 %); nachgeführt in groesseAnpassen
  const pixeldichte = () => Math.min(window.devicePixelRatio || 1, 3);
  renderer.setPixelRatio(pixeldichte());
  renderer.domElement.className = 'd3-leinwand';
  wurzel.prepend(renderer.domElement);

  const farbeAuswahl = new THREE.Color(cssVar('--auswahl', '#1e90ff'));
  const farbeFremd = new THREE.Color(cssVar('--fremd', '#d69e2e'));
  const hintergrund = () => cssVar('--d3-hintergrund', '#eef1f5');

  const szene = new THREE.Scene();
  szene.background = new THREE.Color(hintergrund());
  szene.add(new THREE.AmbientLight(0xffffff, 1.55));
  const sonne = new THREE.DirectionalLight(0xffffff, 2.2);
  sonne.position.set(6, 12, 8);
  szene.add(sonne);
  const gegenlicht = new THREE.DirectionalLight(0xffffff, 0.8);
  gegenlicht.position.set(-8, 5, -6);
  szene.add(gegenlicht);
  const modell = new THREE.Group();
  szene.add(modell);

  const kamera = new THREE.PerspectiveCamera(45, 1, 0.1, 1000);
  kamera.position.set(18, 14, 24);
  const steuerung = new OrbitControls(kamera, renderer.domElement);
  steuerung.enableDamping = false;           // keine Dauerschleife nötig
  // Maustasten wie in Revit (N10, maus.js): links drehen, Mitte verschieben, Umschalt + Mitte drehen (die Steuerung vertauscht mit Umschalt),
  // Rad zoomt zum Mauszeiger, rechts verschiebt wie bisher
  steuerung.mouseButtons = { LEFT: THREE.MOUSE[ORBIT_TASTEN.LEFT], MIDDLE: THREE.MOUSE[ORBIT_TASTEN.MIDDLE], RIGHT: THREE.MOUSE[ORBIT_TASTEN.RIGHT] };
  steuerung.zoomToCursor = true;
  steuerung.screenSpacePanning = true;
  // Gesten (Modul G): Rolle um die Blickachse über der aufrechten Lage der Steuerung (Bogenmass; Kamera = aufrecht · Drehung um die lokale z-Achse). Die
  // Steuerung ruft update() auch selbst (scope.update) – darum wird die Methode ersetzt, nicht nur hier aufgerufen.
  let rolle = 0;
  let rolleAus = () => false;   // beim Begehen und in VR gibt es keine Rolle (wird unten gesetzt, sobald Begehen und VR da sind)
  const orbitUpdate = steuerung.update;
  steuerung.update = function (...a) {
    const r = orbitUpdate.apply(this, a);
    if (rolleAus()) rolle = 0;
    else if (rolle !== 0) kamera.rotateZ(rolle);
    return r;
  };
  const strahl = new THREE.Raycaster();

  // ---------- Rendern bei Bedarf ----------
  function zeichnen() {
    geplant = 0;
    if (zerstoert || vr?.aktiv) return;   // N23: in VR zeichnet die Schleife von vr3d.js (Kopfpose je Bild), nicht auf Anforderung
    schnitt?.vorRender();
    renderer.render(szene, kamera);
    wuerfel?.zeichnen();
    kameraMerken();
  }
  /** Kamerazustand zur Kontrolle (Browser-Tests, Fehlersuche): Ziel, Lage, Blickrichtung. */
  function kameraMerken() {
    const f = (v) => [v.x, v.y, v.z].map((x) => Math.round(x * 1000) / 1000);
    const blick = new THREE.Vector3().copy(steuerung.target).sub(kamera.position).normalize();
    const hoch = new THREE.Vector3(0, 1, 0).applyQuaternion(kamera.quaternion);
    wurzel.dataset.kamera = JSON.stringify({ ziel: f(steuerung.target), lage: f(kamera.position), blick: f(blick), fov: kamera.fov, hoch: f(hoch), rolle: Math.round(rolle * 1e4) / 1e4 });
  }
  function anfordern() {
    if (!geplant && !zerstoert) geplant = requestAnimationFrame(zeichnen);
  }
  steuerung.addEventListener('change', anfordern);
  steuerung.addEventListener('start', () => { benutzt = true; });

  // ---------- Navigationswürfel (N11) ----------
  // N9: VORNE folgt dem Planwinkel des Gebäudes (wie der gerade Grundriss). Steht noch keiner am Gebäude (Projekt von vor N9), gilt der
  // Winkel, den der Kern rechnet (Abfrage planwinkel) – derselbe, den der Grundriss dann zeigt.
  const erstesGebaeude = () => derKlasse('Gebaeude').sort((a, b) => String(a.kennzeichen).localeCompare(String(b.kennzeichen)))[0];
  let planwinkelGerechnet = null;
  async function planwinkelHolen() {
    const g = erstesGebaeude();
    if (!g || Number.isFinite(Number(g.werte?.planwinkel))) { planwinkelGerechnet = null; return; }
    try {
      const r = await abfrage('planwinkel', { gebaeude: g.kennzeichen });
      const w = r?.gebaeude?.[0]?.bestimmt?.winkel;
      const neu = Number.isFinite(w) ? w : null;
      if (neu !== planwinkelGerechnet) { planwinkelGerechnet = neu; anfordern(); }
    } catch (e) { console.warn('3D: Planwinkel nicht geholt', e?.message ?? e); }
  }
  const ausrichtungGrad = () => gebaeudeAusrichtungGrad(erstesGebaeude(), planwinkelGerechnet);
  planwinkelHolen();
  const startSchluessel = () => startansichtSchluessel(zustand.status?.ort ?? zustand.status?.projekt, ansicht?.kennzeichen);
  // N15: «Raum begehen» (begehen3d.js) – Ich-Perspektive; Esc, Haus-Knopf und Klick auf den Würfel beenden es
  const begehen = erzeugeBegehen({
    wurzel, dom: renderer.domElement, kamera, steuerung, anfordern,
    schnitt: () => schnitt,
    bauteile: () => objekte.values(),   // N22: Wände, Decken usw. beim Begehen undurchsichtig, durchscheinend nur im Durchgang
    benutzt: { lesen: () => benutzt, setzen: (b) => { benutzt = b; } },
    // war die Kamera vor dem Begehen noch nicht vom Benutzer bewegt (z. B. die Ansicht wurde erst für das Begehen geöffnet), passt sie sich wieder dem Modell an
    beiBeendet: () => { if (!benutzt) zoomAlles(); },
    vr: { aktiv: () => !!vr?.aktiv, beenden: () => vr?.beenden(), kopf: () => vr?.kopf() ?? null, knopf: () => vr?.knopf() },   // N23
  });
  // N23: «In VR begehen» (vr3d.js) – Meta Quest über Air Link, WebXR in Chrome; startet das Begehen und hängt die Kamera in ein Rig
  const begehenEnde = () => { if (vr?.aktiv) vr.beenden(); else begehen.beenden(); };   // in VR beendet jedes Ende des Begehens zuerst die Sitzung
  vr = erzeugeVr({
    wurzel, renderer, kamera, szene, modell, begehen, anfordern,
    // XR-B/XR-C: Greifen, Tablet, Messen – lesender Zugriff auf Körper, Auswahl, Befehle (dieselbe Tür wie am PC)
    koerperDaten: () => daten, objekte: () => objekte, element: (id) => element(id), gewaehlt: () => gewaehlt, auswaehlen: (id) => auswaehlen(id),
    befehl: (name, par) => befehl(name, par), abfrage: (name, par) => abfrage(name, par), status: () => holen('/api/status'), verbindungen: () => zustand.verbindungen,
    schnittSchalter: () => { if (schnitt) schnitt.pausieren(!schnitt.pausiert); }, schnittAn: () => !!schnitt?.aktiv, schnittVorhanden: () => !!schnitt?.quader,
    // ohne Raum: am aktuellen Kamerastandpunkt, auf der tiefsten Stelle des Modells
    standortAusKamera: () => {
      const box = new THREE.Box3().setFromObject(modell);
      return standortAusKamera({ position: kamera.position, ziel: steuerung.target }, box.isEmpty() ? steuerung.target.y : box.min.y);
    },
    meldung: (text, art) => { import('../oberflaeche/dialoge.js').then(({ meldung }) => meldung(text, art)).catch(() => {}); },
    vorRender: () => schnitt?.vorRender(),
    schnittNeu: () => schnitt?.neueObjekte(),   // XR-H: nach dem Sitzungsraum die Ebenen des Schnittbereichs neu bewerten (pausiert im Begehen)
    beiBeendet: () => groesseAnpassen(),
  });
  // Quest App P7 «PC sieht die Brille»: verbundene Brillen als einfache Figuren (xr-teilnehmer.js); in VR (Browser-Prototyp) aus, damit die Figur nicht ins Bild ragt
  const brillen = erzeugeBrillen({
    szene, anfordern, sichtbar: () => !vr?.aktiv,
    beiAenderung: (liste) => { wurzel.dataset.brillen = JSON.stringify(liste.map((f) => ({ id: f.id, name: f.name, kopf: f.kopf, haende: f.haende, veraltet: f.veraltet }))); },
  });
  const vrKnopf = vr.knopf();
  vrKnopf.classList.add('d3-vr-haupt');
  wurzel.appendChild(vrKnopf);
  const wuerfel = erzeugeWuerfel({
    wurzel, renderer, kamera, steuerung,
    begehen: { aktiv: () => begehen.aktiv, beenden: begehenEnde },
    anfordern, beiBenutzt: () => { benutzt = true; },
    ausrichtungGrad,
    nordwinkelGrad: () => nordwinkelGrad(derKlasse('Projektinformation')[0]),
    einpassung: () => einpassWerte(),
    startLesen: () => startRoh(),
    startSchreiben: (wert) => startSchreiben(wert),
    menue: (x, y, eintraege) => import('../oberflaeche/dialoge.js').then(({ kontextmenue }) => kontextmenue(x, y, eintraege)),
    rolle: { lesen: () => rolle, setzen: (w) => { rolle = Number.isFinite(w) ? w : 0; } },   // Modul G: der Würfel zeigt die Rolle und richtet auf
  });
  rolleAus = () => begehen.aktiv || !!vr?.aktiv;

  // ---------- Ansichtseigenschaften im Modell: Startansicht (Rest N11) und Schnittbereich (N13) ----------
  /** Ändert Eigenschaften dieser Ansicht über den Kern (ein Paket, zurücknehmbar). false bei Ablehnung oder Fehler (mit Meldung). */
  async function ansichtSpeichern(eigenschaften) {
    try {
      const r = await befehl('element_aendern', { element: ansicht.kennzeichen, eigenschaften });
      if (r.angenommen) return true;
      const { meldung } = await import('../oberflaeche/dialoge.js');
      meldung(r.fehler || 'Die Änderung wurde abgelehnt.', 'warnung', { vorschlag: r.vorschlag });
    } catch (e) { console.error('3D: Ansichtseigenschaft nicht gespeichert', e); }
    return false;
  }
  // Startansicht: der gerade geschriebene Wert gilt sofort (startLokal), bis die Antwort des Kerns die Ansicht erreicht hat; sonst der Wert der Ansicht.
  let startLokal;            // undefined = Wert aus dem Modell; sonst der geschriebene Wert (null = zurückgesetzt)
  let startLaeuft = 0;
  const startRoh = () => (startLokal !== undefined ? startLokal : ansicht?.werte?.startansicht ?? null);
  async function startSchreiben(wert) {
    startLokal = wert ?? null;
    startLaeuft++;
    let ok = false;
    try { ok = await ansichtSpeichern({ startansicht: wert ?? null }); } finally { startLaeuft--; }
    if (!ok && !startLaeuft) startLokal = undefined;
    return ok;
  }
  /** Die alte Startansicht aus dem Browser (localStorage, bis N11) wird einmal ins Modell übernommen und dann entfernt. */
  function startUebernehmen() {
    try {
      const schl = startSchluessel();
      const lokal = merken.lesen(schl, null);
      if (lokal === null || lokal === undefined) return;
      const u = startansichtUebernahme(ansicht?.werte?.startansicht ?? null, lokal, startansichtLesen);
      if (u.schreiben) startSchreiben({ azimutGrad: u.wert.azimutGrad, polarGrad: u.wert.polarGrad }).then((ok) => { if (ok) merken.entfernen(schl); });
      else if (u.aufraeumen) merken.entfernen(schl);
    } catch (e) { console.warn('3D: alte Startansicht nicht übernommen', e?.message ?? e); }
  }
  startUebernehmen();

  // ---------- 3D-Schnittbereich (N13) ----------
  schnitt = erzeugeSchnittbereich({
    wurzel, dom: renderer.domElement, renderer, kamera, szene, anfordern,
    gruppen: () => [modell, huelleGruppe],
    objekte: () => [...objekte.values()],
    auswahlObjekt: () => (gewaehlt ? objekte.get(gewaehlt) ?? null : null),
    ausrichtung: () => ausrichtungGrad(),
    gespeichert: () => ansicht?.werte?.schnittbereich ?? null,
    speichern: (wert) => ansichtSpeichern({ schnittbereich: wert }),
    beiBenutzt: () => { benutzt = true; },
  });

  // ---------- Grösse ----------
  function groesseAnpassen() {
    if (zerstoert || vr?.aktiv) return;   // N23: in VR bestimmt die Brille die Grösse (danach ruft vr3d.js dies wieder auf)
    const W = wurzel.clientWidth, H = wurzel.clientHeight;
    if (W < 2 || H < 2) return;
    if (renderer.getPixelRatio() !== pixeldichte()) renderer.setPixelRatio(pixeldichte());   // Fenster auf anderen Bildschirm gezogen, Zoom
    renderer.setSize(W, H);
    kamera.aspect = W / H;
    kamera.updateProjectionMatrix();
    if (!benutzt && objekte.size > 0) zoomAlles(); else anfordern();
  }
  window.addEventListener('resize', groesseAnpassen);
  const beobachter = typeof ResizeObserver === 'function' ? new ResizeObserver(groesseAnpassen) : null;
  beobachter?.observe(wurzel);

  // ---------- Körper ----------
  function entfernen(id) {
    const o = objekte.get(id);
    if (!o) return;
    modell.remove(o);
    freigeben(o);
    objekte.delete(id);
    begehen.neueObjekte();   // N22
  }

  function farbeSetzen(id) {
    const o = objekte.get(id);
    if (!o) return;
    hervorheben(o, id === gewaehlt ? 'auswahl' : (fremdSet.has(id) ? 'fremd' : null), id === gewaehlt ? farbeAuswahl : farbeFremd);
  }

  /** Baut den Körper aus den Daten des Kerns und setzt ihn ein; was die Ansicht (Gewerke, Klassen) ausblendet, entfällt. */
  function hinzufuegen(k) {
    entfernen(k.id);
    const el = element(k.id);
    if (el && !sichtbar(el, ansicht, element)) return;
    const stil = el ? nachbauStil(el, ansicht, element) : null;   // N12: Farbe und Deckkraft des Nachbaus aus der Unterlage
    const o = baueKoerper(k, { ...(huelleAn ? { blasser: BLASS_HUELLE } : {}), ...(stil ? { ueberschreiben: stil } : {}) });
    if (!o) return;
    modell.add(o);
    objekte.set(k.id, o);
    farbeSetzen(k.id);
    netzeAnhaengen(o);
    schnitt?.neueObjekte();
    begehen.neueObjekte();   // N22
  }

  // ---------- Hintergrund-Netze ----------
  /** Lädt und liest die Netzdatei einer Unterlage; Ergebnis eine Geometrie (oder null bei Fehler). Je Dateiname nur einmal. */
  function netzHolen(quelle, nur = 'hintergrund') {
    const schluessel = netzSchluessel(element(quelle));
    const alt = netze.get(quelle);
    if (alt && alt.schluessel === schluessel && alt.nur === nur && !(alt.fehlerZeit && Date.now() - alt.fehlerZeit > NETZ_WIEDERHOLEN)) return alt.zusage;
    if (alt) alt.zusage.then((g) => g?.dispose());   // andere Datei, andere Auswahl (N12) oder erneuter Versuch: alte Geometrie freigeben
    const eintrag = { schluessel, nur, zusage: null, fehlerZeit: 0 };
    eintrag.zusage = (async () => {
      const t0 = performance.now();
      try {
        const r = await fetch(netzUrl(quelle, schluessel));
        if (!r.ok) throw new Error((await r.text()) || `Fehler ${r.status}`);
        const puffer = await r.arrayBuffer();
        const t1 = performance.now();
        await new Promise((fertig) => setTimeout(fertig, 0));   // dem Browser Luft lassen, bevor das Lesen beginnt
        const netz = leseNetz(puffer, { nur });
        const g = netzGeometrie(netz);
        const t2 = performance.now();
        wurzel.dataset.netz = JSON.stringify({ quelle, nur, produkte: netz.produkte.length, hintergrund: netz.dreiecke, bytes: puffer.byteLength, ladenMs: Math.round(t1 - t0), lesenMs: Math.round(t2 - t1) });
        return g;
      } catch (e) {
        eintrag.fehlerZeit = Date.now();
        console.error('3D: Netz der Unterlage nicht geladen', quelle, e);
        return null;
      }
    })();
    netze.set(quelle, eintrag);
    return eintrag.zusage;
  }

  /** Hängt die Netze eines frisch gebauten Körpers an, sobald sie geladen sind (der Körper zeigt bis dahin nichts). */
  function netzeAnhaengen(o) {
    const halter = [];
    o.traverse((h) => { if (h.userData.netz && h.userData.netz !== true) halter.push(h); });
    for (const h of halter) {
      const n = h.userData.netz;
      netzHolen(n.quelle, netzAuswahl(ansicht)).then((g) => {
        if (zerstoert || !g || objekte.get(o.userData.id) !== o) return;   // Körper inzwischen ersetzt oder entfernt
        h.add(netzMesh(g, n.farbe, n.deckkraft));
        schnitt?.neueObjekte();
        vr?.netzeAnwenden();   // N23: in VR folgt das neue Netz der Wahl «IFC-Unterlage zeigen»
        if (!benutzt) zoomAlles(); else anfordern();
      });
    }
  }

  /** Gibt Netze frei, deren Unterlage es nicht mehr gibt oder deren Netzdatei gewechselt hat. Ausgeblendete Unterlagen behalten
   *  ihr Netz (Wiedereinblenden lädt nichts neu). */
  function netzeAufraeumen() {
    for (const [q, e] of [...netze]) {
      const el = element(q);
      if (el && netzSchluessel(el) === e.schluessel) continue;
      netze.delete(q);
      e.zusage.then((g) => g?.dispose());
    }
  }

  /** N12: Wird eine IFC-Unterlage geändert (Farbe oder Deckkraft des Nachbaus, Anzeigen), baut die Ansicht deren Nachbau-Körper neu. */
  function nachbauNeu(unterlagen) {
    if (unterlagen.length === 0) return;
    for (const k of daten.values()) if (unterlagen.includes(element(k.id)?.kennungen?.quelle)) hinzufuegen(k);
  }

  function hinweisAktualisieren() {
    wurzel.dataset.anzahl = String(objekte.size);   // Kontrolle (Browser-Tests): Zahl der Körper in der Szene
    hinweis.hidden = objekte.size > 0;
    if (objekte.size === 0 && !hinweis.dataset.fehler) hinweis.innerHTML = 'Keine Elemente sichtbar.<br>Gewerke- und Klassenfilter der Ansicht prüfen.';
  }

  /** Neuaufbau aus den vorhandenen Daten (Designwechsel, Filter der Ansicht geändert) – ohne neu zu holen. */
  function ausDatenNeu() {
    for (const id of [...objekte.keys()]) entfernen(id);
    for (const k of daten.values()) hinzufuegen(k);
    hinweisAktualisieren();
    anfordern();
  }

  /** Holt Körper vom Kern: ids = null → alle, sonst nur diese. */
  async function holenUndEinsetzen(ids) {
    const liste = await holen(koerperUrl(ids));
    if (zerstoert) return;
    const neu = new Map((Array.isArray(liste) ? liste : []).map((k) => [k.id, k]));
    if (!ids) {
      for (const id of [...daten.keys()]) if (!neu.has(id)) { daten.delete(id); entfernen(id); }
    } else {
      for (const id of ids) if (!neu.has(id)) { daten.delete(id); entfernen(id); }
    }
    for (const k of neu.values()) { daten.set(k.id, k); hinzufuegen(k); }
    netzeAufraeumen();
    hinweis.dataset.fehler = '';
    for (const id of [...fremdOffen]) if (objekte.has(id)) { fremdBlinken(id); fremdOffen.delete(id); }
    hinweisAktualisieren();
    anfordern();
  }

  /** Arbeitet die angesammelten Änderungen ab (eine Anfrage nach der anderen). */
  async function abarbeiten() {
    if (laeuft || zerstoert) return;
    laeuft = true;
    try {
      while ((anstehend.alle || anstehend.ids.size > 0) && !zerstoert) {
        const alle = anstehend.alle, ids = [...anstehend.ids], zoom = anstehend.zoom;
        anstehend.alle = false; anstehend.ids.clear(); anstehend.zoom = false;
        try {
          await holenUndEinsetzen(alle ? null : ids);
          if (zoom || (!benutzt && !hatGezoomt)) { hatGezoomt = true; zoomAlles(); }
        } catch (e) {
          console.error('3D: Körper nicht geladen', e);
          hinweis.hidden = false;
          hinweis.dataset.fehler = '1';
          hinweis.innerHTML = `Die 3D-Körper konnten nicht geladen werden:<br>${esc(e.message ?? e)}${e.vorschlag ? '<br>' + esc(e.vorschlag) : ''}`;
        }
      }
    } finally { laeuft = false; }
  }

  function planen(ms = ENTPRELLUNG) {
    clearTimeout(ladeTimer);
    ladeTimer = setTimeout(abarbeiten, ms);
  }

  /** Mitte und Abstand, bei denen alle Körper im Bild sind; stellt auch die Schnittebenen der Kamera und den grössten Abstand ein. */
  function einpassWerte() {
    const box = new THREE.Box3();
    for (const o of objekte.values()) box.expandByObject(o);
    if (box.isEmpty()) { box.set(new THREE.Vector3(-5, 0, -5), new THREE.Vector3(5, 3, 5)); }
    const sk = schnitt?.kugelWelt();   // N13: ist ein Schnittbereich aktiv, wird er eingepasst (nur das ist sichtbar)
    const kugel = sk ? new THREE.Sphere(sk.mitte, sk.radius) : box.getBoundingSphere(new THREE.Sphere());
    const r = Math.max(kugel.radius, 0.5);
    const dist = (r / Math.sin(THREE.MathUtils.degToRad(kamera.fov) / 2)) * 1.05;
    kamera.near = Math.max(dist / 500, 0.02);
    kamera.far = dist * 40 + 100;
    kamera.updateProjectionMatrix();
    steuerung.maxDistance = dist * 20;
    return { ziel: kugel.center.clone(), abstand: dist };
  }

  /** Alles zeigen, aus der Richtung der Startansicht (im Modell gespeichert oder Isometrie von vorne rechts oben); mit aktivem Schnittbereich
   *  dessen Inhalt. `richtung` (Ergebnis von zielWinkel) erzwingt eine andere Richtung. */
  function zoomAlles(richtung = null) {
    if (zerstoert || begehen.aktiv) return;   // beim Begehen (N15) bleibt die Kamera im Raum
    benutzt = false;
    rolle = 0;   // Modul G: «alles zeigen» stellt die Ansicht wieder aufrecht
    const e = einpassWerte();
    const z = richtung && richtung.richtung ? richtung : startansichtZiel(startansichtLesen(startRoh()), ausrichtungGrad() * Math.PI / 180);
    kamera.position.copy(e.ziel).addScaledVector(new THREE.Vector3(z.richtung.x, z.richtung.y, z.richtung.z), e.abstand);
    steuerung.target.copy(e.ziel);
    steuerung.update();
    anfordern();
  }

  /** N14 «Raum im 3D zeigen»: den ganzen Schnittbereich im Bild, Isometrie von vorne rechts oben (unabhängig von der Startansicht). */
  function zeigeBereich() {
    if (zerstoert) return;
    if (begehen.aktiv) begehenEnde();
    zoomAlles(zielWinkel(START_VORGABE, ausrichtungGrad() * Math.PI / 180));
    benutzt = true;       // bleibt so stehen: weder das erste Laden noch eine Grössenänderung stellt die Kamera neu ein
    hatGezoomt = true;
  }

  // ---------- Gesten (Modul G): navigieren ----------
  const DREHPUNKT_HALTEN_MS = 400;   // so lange nach der letzten Geste gilt derselbe Drehpunkt (eine Bewegung der Faust)
  let drehpunkt = null;             // { p: Vector3, auswahl, zeit }
  /** Richtet die Ansicht auf (Rolle 0), ohne sonst etwas zu ändern. */
  function aufrichten() {
    if (!rolle) return;
    rolle = 0;
    steuerung.update();
    anfordern();
  }
  /**
   * Vertrag Modul G: { drehen: [dx, dy], rollen, verschieben: [dx, dy], zoom } (Einheiten und Richtungen: navigieren-logik.js). Gedreht und gerollt wird um die Mitte
   * der Auswahl bzw. des Modells; danach zerlegt in die aufrechte Lage der Steuerung und die Rolle. Beim Begehen und in VR ohne Wirkung. Ergebnis: ob sich etwas tat.
   */
  function navigieren(eingabe = {}) {
    if (zerstoert || begehen.aktiv || vr?.aktiv) return false;
    const e = eingabe ?? {};
    const dreht = Array.isArray(e.drehen) || Number.isFinite(e.rollen);
    kamera.updateMatrixWorld();
    if (dreht) {
      const jetzt = performance.now();
      if (!drehpunkt || jetzt - drehpunkt.zeit > DREHPUNKT_HALTEN_MS || drehpunkt.auswahl !== gewaehlt) drehpunkt = { p: pivot() ?? steuerung.target.clone(), auswahl: gewaehlt };
      drehpunkt.zeit = jetzt;
    }
    const vorher = { position: kamera.position.toArray(), ziel: steuerung.target.toArray(), q: kamera.quaternion.toArray() };
    const neu = navigieren3d(vorher, e, {
      drehpunkt: dreht ? drehpunkt.p.toArray() : undefined, fovGrad: kamera.fov, seitenverhaeltnis: kamera.aspect,
      minAbstand: Math.max(kamera.near * 4, 0.3), maxAbstand: steuerung.maxDistance,
    });
    if (neu.position.every((x, i) => x === vorher.position[i]) && neu.q.every((x, i) => x === vorher.q[i])) return false;
    wuerfel.abbrechen?.();   // eine laufende Kamerafahrt des Würfels endet
    kamera.position.fromArray(neu.position);
    steuerung.target.fromArray(neu.ziel);
    // aufrechte Lage durch die Steuerung (sie begrenzt auch Abstand und Polarwinkel), dann die Rolle bis zur neuen Lage
    rolle = 0;
    steuerung.update();
    rolle = rolleZwischen(kamera.quaternion.toArray(), neu.q);
    if (Math.abs(rolle) < 1e-6) rolle = 0;
    else kamera.rotateZ(rolle);
    benutzt = true;
    hatGezoomt = true;
    anfordern();
    return true;
  }

  // ---------- Darstellung «Hülle» ----------
  function huelleEntfernen() {
    clearTimeout(huelleTimer);
    infoZeigen(null);
    if (huelleGruppe) { szene.remove(huelleGruppe); huelleFreigeben(huelleGruppe); huelleGruppe = null; }
    legende.hidden = true;
    legende.replaceChildren();
  }

  function infoZeigen(zeile) {
    if (huelleUmriss) { szene.remove(huelleUmriss); huelleUmriss.geometry.dispose(); huelleUmriss.material.dispose(); huelleUmriss = null; }
    if (!zeile) { infoFeld.hidden = true; infoFeld.replaceChildren(); return; }
    infoFeld.innerHTML = infoHtml(zeile);
    infoFeld.hidden = false;
    huelleUmriss = umrissLinie(zeile, farbeAuswahl);
    if (huelleUmriss) szene.add(huelleUmriss);
  }

  /** Holt die Raumgrenzen mit Geometrie und baut die Hüllflächen und die Legende neu (nur bei darstellung huelle). */
  async function huelleLaden() {
    const nr = ++huelleNr;
    clearTimeout(huelleTimer);
    if (!huelleAn) { huelleEntfernen(); anfordern(); return; }
    try {
      const t0 = performance.now();
      const a = await abfrage('raumgrenzen', { geometrie: true });
      if (nr !== huelleNr || zerstoert || !huelleAn) return;
      const t1 = performance.now();
      const zeilen = Array.isArray(a?.zeilen) ? a.zeilen : [];
      const neu = baueHuelle(zeilen);
      const t2 = performance.now();
      infoZeigen(null);
      if (huelleGruppe) { szene.remove(huelleGruppe); huelleFreigeben(huelleGruppe); }
      huelleGruppe = neu;
      szene.add(huelleGruppe);
      schnitt?.neueObjekte();
      legende.innerHTML = legendeHtml(legendeDaten(a), { titel: 'Hülle – Gebäude' });
      legende.hidden = false;
      wurzel.dataset.huelle = JSON.stringify({ zeilen: zeilen.length, flaechen: huelleGruppe.children.reduce((n, m) => n + m.userData.dreiecke, 0), holenMs: Math.round(t1 - t0), bauenMs: Math.round(t2 - t1) });
      anfordern();
    } catch (e) {
      if (nr !== huelleNr || zerstoert) return;
      console.error('3D: Raumgrenzen nicht geladen', e);
      legende.innerHTML = legendeHtml([], { titel: 'Hülle', fehler: `Raumgrenzen nicht geladen: ${e.message ?? e}${e.vorschlag ? ' – ' + e.vorschlag : ''}` });
      legende.hidden = false;
    }
  }

  // ---------- Treffer und Bedienung ----------
  const zeiger = new THREE.Vector2();
  /** Element unter dem Mauszeiger: bevorzugt Leitungen, Formteile, Apparate (Vorrang 2) vor Wänden und Decken (1) und
   *  diese vor Räumen (0), die durchscheinen; bei gleichem Vorrang der nächste Treffer. */
  function strahlFuer(e) {
    const r = renderer.domElement.getBoundingClientRect();
    if (r.width < 1 || r.height < 1) return false;
    zeiger.set(((e.clientX - r.left) / r.width) * 2 - 1, -((e.clientY - r.top) / r.height) * 2 + 1);
    strahl.setFromCamera(zeiger, kamera);
    return true;
  }
  function koerperTreffer(e) {
    if (!strahlFuer(e)) return null;
    const treffer = schnitt ? schnitt.filtern(strahl.intersectObjects(modell.children, true)) : strahl.intersectObjects(modell.children, true);   // N13: nur was nicht weggeschnitten ist
    // Treffer sind nach Abstand sortiert: höherer Vorrang gewinnt, bei Gleichstand der nächste
    let beste = null, bestVorrang = -1, distanz = Infinity;
    for (const t of treffer) {
      const id = t.object.userData.id;
      const obj = id ? objekte.get(id) : null;
      if (!obj) continue;
      const v = obj.userData.vorrang ?? 1;
      if (v > bestVorrang) { beste = id; bestVorrang = v; distanz = t.distance; }
    }
    return beste ? { id: beste, vorrang: bestVorrang, distanz } : null;
  }
  const trefferId = (e) => koerperTreffer(e)?.id ?? null;

  /** Klick in der Darstellung Hülle: die vorderste Hüllfläche, ausser ein Haustechnikkörper (Vorrang 2) liegt davor. */
  function huelleKlick(e) {
    if (!huelleAn || !huelleGruppe || !strahlFuer(e)) return null;
    const h = zeileAmTreffer(huelleGruppe, schnitt ? schnitt.filtern(strahl.intersectObjects(huelleGruppe.children, false)) : strahl.intersectObjects(huelleGruppe.children, false));
    if (!h) return null;
    const k = koerperTreffer(e);
    return k && k.vorrang >= 2 && k.distanz < h.distanz ? null : h;
  }

  let druck = null;
  const dom = renderer.domElement;
  // Mittlere Taste (N10): Doppelklick = alles zeigen; Umschalt + Mitte dreht um die Auswahl bzw. die Modellmitte (Aufnahmephase: vor der Steuerung);
  // kein Autoscroll des Browsers
  const mitteDk = mitteDoppelklick();
  const pivot = () => {
    const box = new THREE.Box3();
    const o = gewaehlt ? objekte.get(gewaehlt) : null;
    if (o) box.expandByObject(o); else for (const k of objekte.values()) box.expandByObject(k);
    return box.isEmpty() ? null : box.getCenter(new THREE.Vector3());
  };
  wurzel.addEventListener('pointerdown', (e) => {
    if (e.button !== 1) return;
    e.preventDefault();
    if (begehen.aktiv) return;   // N15: die mittlere Taste verschiebt beim Begehen (begehen3d.js)
    if (mitteDk.unten(e)) { e.stopPropagation(); zoomAlles(); return; }
    if (e.shiftKey) {
      const p = pivot();
      if (p) {
        const z = schwenkZiel(kamera.position, steuerung.target, p);
        if (new THREE.Vector3(z.x, z.y, z.z).distanceTo(kamera.position) < steuerung.maxDistance) steuerung.target.set(z.x, z.y, z.z);
      }
    }
  }, true);
  wurzel.addEventListener('pointermove', (e) => mitteDk.bewegt(e), true);
  wurzel.addEventListener('pointerup', (e) => mitteDk.oben(e), true);
  wurzel.addEventListener('pointercancel', (e) => mitteDk.oben(e), true);
  dom.addEventListener('mousedown', (e) => { if (e.button === 1) e.preventDefault(); });
  dom.addEventListener('auxclick', (e) => e.preventDefault());
  dom.addEventListener('pointerdown', (e) => {
    tip.hidden = true;
    if (e.button === 0) druck = { x: e.clientX, y: e.clientY, bewegt: false };
  });
  let hoverPlan = 0;
  dom.addEventListener('pointermove', (e) => {
    if (druck) {
      if (Math.hypot(e.clientX - druck.x, e.clientY - druck.y) > KLICK_TOLERANZ) druck.bewegt = true;
      return;
    }
    if (e.buttons || hoverPlan) return;
    const ex = e.clientX, ey = e.clientY;
    hoverPlan = requestAnimationFrame(() => {
      hoverPlan = 0;
      if (zerstoert) return;
      const id = trefferId({ clientX: ex, clientY: ey });
      const el = id ? element(id) : null;
      if (!el) { tip.hidden = true; return; }
      tip.textContent = [el.kennzeichen, el.klasse, el.system].filter(Boolean).join(' · ');
      const r = wurzel.getBoundingClientRect();
      tip.hidden = false;
      tip.style.left = `${Math.min(ex - r.left + 14, r.width - tip.offsetWidth - 4)}px`;
      tip.style.top = `${Math.min(ey - r.top + 16, r.height - tip.offsetHeight - 4)}px`;
    });
  });
  dom.addEventListener('pointerleave', () => { tip.hidden = true; });
  dom.addEventListener('pointerup', (e) => {
    const d = druck;
    druck = null;
    if (!d || e.button !== 0 || d.bewegt) return;
    const h = huelleKlick(e);
    if (h) {   // Hüllfläche: Infofeld, Bauteil wählen (Eigenschaften zeigen U und Aufbau)
      infoZeigen(h.zeile);
      auswaehlen((h.zeile.bauteil && element(h.zeile.bauteil)?.id) || null);
      return;
    }
    infoZeigen(null);
    auswaehlen(trefferId(e) ?? null);   // Klick ohne Ziehen; ins Leere hebt die Auswahl auf
  });
  dom.addEventListener('contextmenu', (e) => e.preventDefault());

  // ---------- Design ----------
  /** Design hell/dunkel gewechselt: Hintergrund und Auswahlfarben neu lesen, Körper mit den Farben des Designs neu bauen. */
  function designGeaendert() {
    if (zerstoert) return;
    szene.background.set(hintergrund());
    wuerfel.designGeaendert();
    farbeAuswahl.set(cssVar('--auswahl', '#1e90ff'));
    farbeFremd.set(cssVar('--fremd', '#d69e2e'));
    ausDatenNeu();   // Kamera bleibt, wie sie ist (Farben der Körper kommen aus dem Design)
  }
  window.addEventListener('hlks-design', designGeaendert);

  // ---------- Vertrag ----------
  function hervorhebenId(id) {
    const alt = gewaehlt;
    gewaehlt = id ?? null;
    if (alt) farbeSetzen(alt);
    if (gewaehlt) farbeSetzen(gewaehlt);
    anfordern();
  }

  function fremdBlinken(id) {
    if (!objekte.has(id)) return;
    clearTimeout(timer.get(id));
    fremdSet.add(id);
    farbeSetzen(id);
    timer.set(id, setTimeout(() => { fremdSet.delete(id); timer.delete(id); farbeSetzen(id); anfordern(); }, FREMD_DAUER));
  }

  function ansichtGeaendert(a) {
    try {
      ansicht = a ?? ansicht;
      if (!startLaeuft) startLokal = undefined;   // der Wert der Ansicht ist wieder massgebend
      schnitt?.setzen(ansicht?.werte?.schnittbereich ?? null);
      const war = huelleAn;
      huelleAn = istHuelle(ansicht);
      ausDatenNeu();   // Filter der Ansicht (Gewerke, Klassen) neu anwenden; bei Hülle auch blassere Körper
      if (war !== huelleAn) huelleLaden();
    } catch (e) { console.error('3D: Ansicht ändern fehlgeschlagen', e); }
  }

  gewaehlt = zustand.auswahl ?? null;
  groesseAnpassen();
  anstehend.alle = true;
  abarbeiten();
  if (huelleAn) huelleLaden();

  return {
    aktualisieren(ereignis) {
      try {
        if (huelleAn) { clearTimeout(huelleTimer); huelleTimer = setTimeout(huelleLaden, HUELLE_ENTPRELLUNG); }
        planwinkelHolen();   // N9: gerechneter Planwinkel, solange am Gebäude keiner steht
        if (!ereignis || ereignis.alles) { anstehend.alle = true; anstehend.zoom = true; planen(0); return; }
        const ids = (ereignis.ids ?? []).filter((id) => id !== ansicht?.id && element(id)?.schicht !== 'ansicht');   // Ansichten und Planblätter haben keine Körper
        // gelöschte Körper sofort weg; war es eine Wand, Tür, ein Fenster oder eine Decke, ändern sich die Nachbarn
        const ebenen = new Map((ereignis.geloescht ?? []).map((id) => [id, objekte.get(id)?.userData.ebene ?? null]));
        for (const id of ereignis.geloescht ?? []) { daten.delete(id); entfernen(id); }
        netzeAufraeumen();
        const betroffen = brauchtAlleKoerper([...ids, ...ebenen.keys()], (id) => element(id)?.klasse ?? null, (id) => ebenen.get(id) ?? objekte.get(id)?.userData.ebene ?? null);
        if (betroffen) anstehend.alle = true;
        for (const id of ids) anstehend.ids.add(id);
        nachbauNeu(ids.filter((id) => element(id)?.klasse === 'Unterlage'));   // N12: Farbe des Nachbaus geändert
        if (ereignis.quelle === 'fremd') for (const id of ids) fremdOffen.add(id);
        if (anstehend.alle || anstehend.ids.size > 0) planen(); else { hinweisAktualisieren(); anfordern(); }
      } catch (e) { console.error('3D: Aktualisieren fehlgeschlagen', e); }
    },
    ansichtGeaendert,
    auswahlGeaendert: hervorhebenId,
    zoomAlles: () => zoomAlles(),
    zeigeBereich,
    /** Quest App V1a (Problem-Marker): Kamera an position mit Blick auf ziel (Szene der 3D-Ansicht, m; problemmarker-text.js kamera); beendet ein Begehen. */
    blickpunkt({ position, ziel } = {}) {
      if (zerstoert || !Array.isArray(position) || !Array.isArray(ziel)) return false;
      if (begehen.aktiv) begehenEnde();
      kamera.position.set(position[0], position[1], position[2]);
      steuerung.target.set(ziel[0], ziel[1], ziel[2]);
      rolle = 0;   // Modul G: ein Blickpunkt steht aufrecht
      steuerung.update();
      benutzt = true;
      hatGezoomt = true;
      anfordern();
      return true;
    },
    get schnittbereich() { return schnitt; },
    /** N15: Raum begehen – standort = Antwort der Abfrage raum_begehen; augenhoeheMm übersteuert deren Augenhöhe. */
    begehen: (standort, augenhoeheMm = null) => { aufrichten(); return begehen.starten(standort, augenhoeheMm); },
    beendeBegehen: () => begehen.beenden(),
    get begehenAktiv() { return begehen.aktiv; },
    /** N23: «In VR begehen» (Meta Quest über Air Link): die VR-Steuerung der Ansicht (vr3d.js: knopf, pruefen, starten, beenden, aktiv, api) */
    get vr() { return vr; },
    /** N23: VR starten, am Standort der Abfrage raum_begehen (sonst am Standort des Begehens bzw. der Kamera); nur aus einer Benutzeraktion (Klick). */
    begehenVr: (standort = null) => { aufrichten(); return vr.starten(standort); },
    /** Modul G (Gesten): navigieren({ drehen, rollen, verschieben, zoom }) – Einheiten in navigieren-logik.js; 3D dreht und rollt. */
    navigieren,
    navigierenArt: '3d',
    /** Modul G: Ansicht wieder aufrecht (Rolle 0); die Rolle in Bogenmass (Kontrolle). */
    aufrichten,
    get rolle() { return rolle; },
    /** Element, das an dieser Bildschirmstelle gewählt würde (clientX, clientY), sonst null – weggeschnittene Teile zählen nicht (Kontrolle für Browser-Tests). */
    trefferBei: (clientX, clientY) => trefferId({ clientX, clientY }),
    /** Darstellung der Materialien je Körper { id: { ebene, m: [{ deckkraft, transparent, depthWrite, geclippt }] } } (Kontrolle für Browser-Tests, N22). */
    deckkraftAlle: () => Object.fromEntries([...objekte].map(([id, g]) => [id, {
      ebene: g.userData.ebene,
      m: g.userData.materialien.map((m) => ({ deckkraft: m.opacity, transparent: m.transparent, depthWrite: m.depthWrite, geclippt: !!m.clippingPlanes })),
    }])),
    zerstoeren() {
      zerstoert = true;
      cancelAnimationFrame(geplant);
      cancelAnimationFrame(hoverPlan);
      clearTimeout(ladeTimer);
      clearTimeout(huelleTimer);
      huelleNr++;
      for (const t of timer.values()) clearTimeout(t);
      timer.clear();
      window.removeEventListener('resize', groesseAnpassen);
      window.removeEventListener('hlks-design', designGeaendert);
      beobachter?.disconnect();
      steuerung.removeEventListener('change', anfordern);
      brillen.zerstoeren();
      vr?.zerstoeren();
      begehen.zerstoeren();
      wuerfel.zerstoeren();
      schnitt?.zerstoeren();
      steuerung.dispose();
      for (const id of [...objekte.keys()]) entfernen(id);
      infoZeigen(null);
      if (huelleGruppe) huelleFreigeben(huelleGruppe);
      for (const e of netze.values()) e.zusage.then((g) => g?.dispose());
      netze.clear();
      szene.clear();
      renderer.dispose();
      try { renderer.forceContextLoss(); } catch { /* egal */ }
      behaelter.replaceChildren();
    },
  };
}

export default { erstellen };
