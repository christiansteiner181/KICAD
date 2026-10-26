// Gesten am Arbeitsplatz (Modul G): die Laufzeit im Hauptfaden (Vertrag «Schnittstellen im Bau», entwurf/modul-gesten.md). Kamera (getUserMedia, 640 × 480),
// Bilder als ImageBitmap (übertragen, nicht kopiert) an den Worker gesten-worker.js mit MediaPipe aus /lib/mediapipe/, zurück kommen nur Punkte. Die Rechenregeln
// stehen in gesten-logik.js; hier nur Ablauf, Takt und Verteilung. Geladen wird dieses Modul erst, wenn die Gesten eingeschaltet werden (gesten-steuerung.js).
//
//   starten({ hand, kameraId, stellschrauben }) → Promise   wirft mit deutscher Meldung (Name des Kamerafehlers bleibt: NotAllowedError, NotFoundError …)
//   stoppen() → Promise, einstellen(teil), laeuft(), kameras() → [{ id, name }], abonnieren(fn) → abmelden, testmodus(an) → an
//   aufnahmeStarten(), aufnahmeBeenden() → { version, hand, bildbreite, bildhoehe, bilder: [{ t, punkte, welt, haendigkeit, score, weitere? }], eingaben }
//   diagnose() → Promise<{ bildrate, dauerMs, zielMs, ruhe, rueckfall, delegate, ladenMs, gesperrt }> (Messung, Browser-Test)
// Ereignisse: { art: 'zustand', zustand: 'aus' | 'bereit' | 'hand' | 'aktiv', geste, fehler? }, { art: 'punkte', punkte: 21 × [x, y] | null } (höchstens 15/s, wie im
// Spiegel), { art: 'schritt', geste: 'G2' | 'G3' | 'G6', richtung } (Bewegungsrichtung der Hand, wie der Benutzer sie im Spiegel sieht).
// Fliessende Gesten löst dieses Modul selbst aus: fenster.aktive()?.instanz?.navigieren?.({ drehen, rollen, verschieben, zoom }) – nicht im Testmodus. Schritt-Gesten
// führt gesten-ausloesen.js aus (auch im Testmodus werden sie gemeldet; die Oberfläche hält sie dann zurück).
// Takt: 30 Erkennungen/s, Rückfall auf 15/s, wenn die Erkennung länger braucht; nach 10 s ohne Hand 5/s, bis wieder eine Hand im Bild ist. Ist das Fenster
// versteckt, wird die Kamera freigegeben (LED aus) und beim Zurückkehren wieder geöffnet. Maus (pointerdown) und Tasten (keydown) sperren Schritt-Gesten kurz.
// DATENSCHUTZ: Das Kamerabild verlässt den Browser nie und wird nirgends gespeichert oder protokolliert; weiter gehen nur Punkte und Gestenereignisse.

import * as L from './gesten-logik.js';
import * as fenster from '../fenster.js';

const BREITE = 640, HOEHE = 480;   // Bild für die Erkennung (Breite fest, Höhe nach dem Seitenverhältnis der Kamera)
const KAMERA_BREITE = 1280, KAMERA_HOEHE = 720;   // angeforderte Kameraauflösung (16:9, volles Sichtfeld)
const TAKT_MS = 1000 / 30, RUECKFALL_MS = 1000 / 15, RUHE_TAKT_MS = 1000 / 5;
const RUHE_NACH_MS = 10000;
const PUNKTE_MS = 1000 / 15;
const RUECKFALL_AB_MS = 30, RUECKFALL_BIS_MS = 24, RUECKFALL_ZURUECK_S = 3;
const ANLAUF_BILDER = 20;   // die ersten Erkennungen sind langsamer (WASM wärmt auf): sie zählen nicht für den Rückfall
const LADEN_FRIST_MS = 60000;
const WAECHTER_MS = 1500;   // so lange darf ein Bild im Worker unterwegs sein
const BASIS = '/lib/mediapipe/';

const hoerer = new Set();
let lauf = null;          // laufende Erkennung (siehe starten)
let startet = null;       // Zusage eines laufenden Starts
let test = false;
let aufnahme = null;      // { t0, bilder, eingaben }
let einst = { hand: 'links', kameraId: null, stellschrauben: {} };
let logik = L.neuerZustand();
let letzteEingabe = -Infinity;
let gemeldet = { zustand: 'aus', geste: null };
let zuletzt = { gesperrt: [] };   // gesperrte Netzanfragen des letzten Laufs (diagnose nach stoppen)

function melden(e) {
  for (const fn of [...hoerer]) { try { fn(e); } catch (x) { console.error('Gesten: Abonnent', x); } }
}

function zustandMelden(zustand, geste = null, fehler = undefined) {
  if (!fehler && gemeldet.zustand === zustand && gemeldet.geste === geste) return;
  gemeldet = { zustand, geste };
  melden(fehler ? { art: 'zustand', zustand, geste, fehler } : { art: 'zustand', zustand, geste });
}

/** Fehler der Kamera → deutsche Meldung, der Name bleibt (die Oberfläche unterscheidet danach). */
function kameraFehler(e) {
  const name = e?.name ?? 'Error';
  const text = name === 'NotAllowedError' || name === 'SecurityError' ? 'Der Browser hat den Zugriff auf die Kamera nicht erlaubt.'
    : name === 'NotFoundError' || name === 'OverconstrainedError' ? 'Es wurde keine passende Kamera gefunden.'
    : name === 'NotReadableError' ? 'Die Kamera wird von einem anderen Programm benutzt.'
    : `Die Kamera liess sich nicht öffnen: ${e?.message ?? e}`;
  const f = new Error(text);
  f.name = name;
  return f;
}

async function kameraOeffnen(kameraId) {
  const md = typeof navigator !== 'undefined' ? navigator.mediaDevices : null;
  if (!md?.getUserMedia) { const f = new Error('Dieser Browser bietet keine Kamera an.'); f.name = 'NotFoundError'; throw f; }
  // Volles Sichtfeld: 640 × 480 schneidet bei den meisten eingebauten Kameras (Sensor 16:9) die Seiten ab – die Hand fiel dann schon
  // knapp neben der Mitte aus dem Bild (Abnahme Christian 26.10.2026: «zu kleiner Raum vor der Kamera»). Darum 16:9 anfordern; für
  // die Erkennung wird das Bild ohnehin auf BREITE verkleinert (bildHolen).
  const video = { width: { ideal: KAMERA_BREITE }, height: { ideal: KAMERA_HOEHE }, frameRate: { ideal: 30 } };
  if (kameraId) video.deviceId = { exact: kameraId };
  try { return await md.getUserMedia({ video, audio: false }); }
  catch (e) { throw kameraFehler(e); }
}

function kameraFreigeben(l) {
  for (const s of l.strom?.getTracks?.() ?? []) { try { s.stop(); } catch { /* egal */ } }
  l.strom = null;
  if (l.video) { try { l.video.pause(); } catch { /* egal */ } l.video.srcObject = null; }
}

async function kameraAnschliessen(l) {
  l.strom = await kameraOeffnen(einst.kameraId);
  const spur = l.strom.getVideoTracks()[0];
  spur?.addEventListener('ended', () => { if (lauf === l && !l.versteckt) abbrechen('Die Kamera ist nicht mehr verfügbar.'); });
  l.video.srcObject = l.strom;
  l.video.muted = true;
  l.video.playsInline = true;
  await l.video.play();
}

/** Ansicht, die fliessende Gesten bekommt, und ob sie drehen kann (3D) – navigierenArt der Ansicht: '3d' bzw. '2d'. */
function aktiveAnsicht() {
  const o = fenster.aktive();
  const inst = o?.instanz;
  if (!inst || typeof inst.navigieren !== 'function') return { inst: null, ansicht3d: true, seitenverhaeltnis: 1.6 };
  const f = o.kachel?.querySelector?.('.kachel-inhalt') ?? o.kachel;
  const w = f?.clientWidth ?? 0, h = f?.clientHeight ?? 0;
  return { inst, ansicht3d: inst.navigierenArt !== '2d', seitenverhaeltnis: w > 0 && h > 0 ? w / h : 1.6 };
}

// ---------------------------------------------------------------------------------------------------- Takt

function intervall(l) { return l.ruhe ? RUHE_TAKT_MS : l.rueckfall ? RUECKFALL_MS : TAKT_MS; }

function planen(l, ms) {
  clearTimeout(l.zeitgeber);
  if (lauf !== l || l.versteckt) return;
  l.zeitgeber = setTimeout(() => takt(l), Math.max(0, ms));
}

/** Ein Takt: ist der Worker frei und die Zeit um, geht das nächste Bild hinüber (höchstens eines unterwegs). */
async function takt(l) {
  if (lauf !== l || l.versteckt) return;
  const jetzt = performance.now();
  if (l.unterwegs) {
    if (jetzt - l.letztesBild < WAECHTER_MS) return;
    l.unterwegs = false;   // keine Antwort des Workers: das Bild gilt als verloren
  }
  const warten = l.letztesBild + intervall(l) - jetzt;
  if (warten > 4) { planen(l, warten); return; }   // bis 4 ms zu früh ist recht (Zeitgeber runden ohnehin auf)
  const v = l.video;
  if (!v || v.readyState < 2 || !v.videoWidth) { planen(l, 20); return; }
  l.unterwegs = true;
  l.letztesBild = jetzt;
  try {
    const b = BREITE, h = Math.max(1, Math.round((BREITE * v.videoHeight) / v.videoWidth));
    l.sv = h / b;
    // liefert die Kamera mehr als 640 Punkte Breite, wird verkleinert (die Erkennung braucht nicht mehr)
    const bild = v.videoWidth > BREITE ? await createImageBitmap(v, { resizeWidth: b, resizeHeight: h, resizeQuality: 'low' }) : await createImageBitmap(v);
    if (lauf !== l) { bild.close(); return; }
    l.worker.postMessage({ art: 'bild', bild, t: jetzt }, [bild]);
    planen(l, WAECHTER_MS + 50);   // Wächter; die Antwort plant den nächsten Takt früher
  } catch (e) {
    l.unterwegs = false;
    planen(l, 50);
  }
}

/** Ergebnis des Workers: Spiegelbild → Logik → Ereignisse, Navigation, Takt. */
function ergebnis(l, n) {
  l.unterwegs = false;
  const jetzt = performance.now();
  if (++l.erkannt > ANLAUF_BILDER) l.dauern.push(n.dauerMs ?? 0);
  if (l.dauern.length > 30) l.dauern.shift();
  l.zeiten.push(jetzt);
  while (l.zeiten.length && jetzt - l.zeiten[0] > 2000) l.zeiten.shift();
  rueckfallPruefen(l, jetzt);
  const haende = L.haendeImSpiegel(n.haende ?? []);
  if (haende.length) l.letzteHand = jetzt;
  l.ruhe = jetzt - l.letzteHand > RUHE_NACH_MS;
  if (aufnahme) aufnahme.bilder.push(L.bildFuerAufnahme(n.t - aufnahme.t0, haende));
  const a = aktiveAnsicht();
  const r = L.schritt(logik, { t: n.t, haende, letzteEingabe, ansicht3d: a.ansicht3d, seitenverhaeltnis: l.sv, ansichtSeitenverhaeltnis: a.seitenverhaeltnis });
  if (r.navigation && !test && a.inst) {
    try { a.inst.navigieren(r.navigation); } catch (e) { console.error('Gesten: navigieren', e); }
  }
  zustandMelden(r.zustand, r.geste);
  for (const e of r.ereignisse) melden(e);
  const keine = r.punkte === null;
  if ((!keine && jetzt - l.punkteZeit >= PUNKTE_MS) || keine !== l.punkteLeer) {
    l.punkteZeit = jetzt;
    l.punkteLeer = keine;
    melden({ art: 'punkte', punkte: r.punkte });
  }
  const rest = l.letztesBild + intervall(l) - performance.now();
  if (rest <= 4) takt(l); else planen(l, rest);
}

/** Rückfall auf 15/s, wenn die Erkennung im Mittel länger als RUECKFALL_AB_MS braucht; zurück auf 30/s nach RUECKFALL_ZURUECK_S Sekunden unter RUECKFALL_BIS_MS. */
function rueckfallPruefen(l, jetzt) {
  if (l.dauern.length < 10) return;
  const mittel = l.dauern.reduce((s, x) => s + x, 0) / l.dauern.length;
  if (!l.rueckfall && mittel > RUECKFALL_AB_MS) { l.rueckfall = true; l.schnellSeit = null; return; }
  if (l.rueckfall) {
    if (mittel < RUECKFALL_BIS_MS) { l.schnellSeit ??= jetzt; if (jetzt - l.schnellSeit > RUECKFALL_ZURUECK_S * 1000) { l.rueckfall = false; l.schnellSeit = null; } }
    else l.schnellSeit = null;
  }
}

// ---------------------------------------------------------------------------------------------------- Sperre neben Maus und Tastatur, Sichtbarkeit

function eingabe() {
  letzteEingabe = performance.now();
  if (aufnahme) aufnahme.eingaben.push(Math.round((letzteEingabe - aufnahme.t0) * 10) / 10);
}

async function sichtbarkeit() {
  const l = lauf;
  if (!l || typeof document === 'undefined') return;
  if (document.visibilityState === 'hidden') {
    if (l.versteckt) return;
    l.versteckt = true;
    clearTimeout(l.zeitgeber);
    kameraFreigeben(l);   // Kamera aus, solange KICAD nicht zu sehen ist
    logik = L.neuerZustand({ hand: einst.hand, stellschrauben: einst.stellschrauben });   // laufende Gesten enden (wie Faust lösen)
    zustandMelden('bereit');
    melden({ art: 'punkte', punkte: null });
    return;
  }
  if (!l.versteckt) return;
  try {
    await kameraAnschliessen(l);
    if (lauf !== l) { kameraFreigeben(l); return; }
    l.versteckt = false;
    l.letzteHand = performance.now();
    l.ruhe = false;
    planen(l, 0);
  } catch (e) {
    abbrechen(e?.message ?? String(e), e);
  }
}

// ---------------------------------------------------------------------------------------------------- Vertrag

/** Startet Kamera und Erkennung. Läuft sie schon, gelten nur die neuen Einstellungen. */
export async function starten({ hand = 'links', kameraId = null, stellschrauben = {}, delegate = 'CPU' } = {}) {
  if (lauf) { await einstellen({ hand, kameraId, stellschrauben }); return; }
  if (startet) return startet;
  einst = { hand: hand === 'rechts' ? 'rechts' : 'links', kameraId: kameraId || null, stellschrauben: { ...(stellschrauben ?? {}) } };
  logik = L.neuerZustand({ hand: einst.hand, stellschrauben: einst.stellschrauben });
  const l = {
    strom: null, video: null, worker: null, zeitgeber: 0, unterwegs: false, letztesBild: -Infinity, sv: HOEHE / BREITE,
    dauern: [], zeiten: [], erkannt: 0, rueckfall: false, schnellSeit: null, ruhe: false, letzteHand: performance.now(),
    punkteZeit: -Infinity, punkteLeer: true, versteckt: false, delegate: null, ladenMs: null, wartende: new Map(),
  };
  startet = (async () => {
    try {
      l.video = document.createElement('video');   // nie im Dokument, nie gezeichnet
      await kameraAnschliessen(l);
      l.worker = new Worker(new URL('./gesten-worker.js', import.meta.url), { type: 'module', name: 'KICAD-Gesten' });
      const bereit = new Promise((ja, nein) => {
        const frist = setTimeout(() => nein(new Error('Die Gestenerkennung wurde nicht rechtzeitig geladen.')), LADEN_FRIST_MS);
        l.worker.onmessage = (ev) => {
          const n = ev.data ?? {};
          if (n.art === 'bereit') { clearTimeout(frist); l.delegate = n.delegate; l.ladenMs = n.ladenMs; ja(); }
          else if (n.art === 'fehler') { clearTimeout(frist); nein(new Error(n.meldung)); }
        };
        l.worker.onerror = (ev) => { clearTimeout(frist); ev.preventDefault?.(); nein(new Error(`Die Gestenerkennung liess sich nicht laden: ${ev.message ?? 'Fehler im Worker'}`)); };
      });
      l.worker.postMessage({ art: 'start', basis: BASIS, modell: 'hand_landmarker.task', haende: 2, delegate });
      await bereit;
      l.worker.onmessage = (ev) => workerNachricht(l, ev.data ?? {});
      l.worker.onerror = (ev) => { ev.preventDefault?.(); if (lauf === l) abbrechen(`Die Gestenerkennung ist ausgefallen: ${ev.message ?? 'Fehler im Worker'}`); };
      lauf = l;
      window.addEventListener('pointerdown', eingabe, true);
      window.addEventListener('keydown', eingabe, true);
      document.addEventListener('visibilitychange', sichtbarkeit);
      zustandMelden('bereit');
      planen(l, 0);
    } catch (e) {
      kameraFreigeben(l);
      try { l.worker?.terminate(); } catch { /* egal */ }
      throw e;
    } finally {
      startet = null;
    }
  })();
  return startet;
}

function workerNachricht(l, n) {
  if (lauf !== l) return;
  if (n.art === 'ergebnis') ergebnis(l, n);
  else if (n.art === 'fehler') abbrechen(n.meldung);
  else if (n.art === 'diagnose' || n.art === 'gestoppt') { const w = l.wartende.get(n.art); l.wartende.delete(n.art); w?.(n); }
}

/** Beendet alles: Kamera frei, Worker beendet, Zustand «aus». */
export async function stoppen() {
  if (startet) { try { await startet; } catch { /* der Start ist ohnehin gescheitert */ } }
  const l = lauf;
  if (!l) { zustandMelden('aus'); return; }
  lauf = null;
  aufraeumen(l);
  await new Promise((fertig) => {
    const zeit = setTimeout(fertig, 300);
    l.worker.onmessage = (ev) => { if (ev.data?.art === 'gestoppt') { l.gesperrt = ev.data.gesperrt; clearTimeout(zeit); fertig(); } };
    try { l.worker.postMessage({ art: 'stopp' }); } catch { clearTimeout(zeit); fertig(); }
  });
  try { l.worker.terminate(); } catch { /* egal */ }
  zuletzt = { gesperrt: l.gesperrt ?? [] };
  zustandMelden('aus');
  melden({ art: 'punkte', punkte: null });
}

function aufraeumen(l) {
  clearTimeout(l.zeitgeber);
  kameraFreigeben(l);
  window.removeEventListener('pointerdown', eingabe, true);
  window.removeEventListener('keydown', eingabe, true);
  document.removeEventListener('visibilitychange', sichtbarkeit);
  logik = L.neuerZustand({ hand: einst.hand, stellschrauben: einst.stellschrauben });
}

/** Unerwartetes Ende (Kamera weg, Worker ausgefallen): aus, mit Fehler – die Oberfläche schaltet den Grundschalter dann aus. */
function abbrechen(text, ursache = null) {
  const l = lauf;
  if (!l) return;
  lauf = null;
  aufraeumen(l);
  try { l.worker.terminate(); } catch { /* egal */ }
  const f = new Error(text);
  if (ursache?.name) f.name = ursache.name;
  zustandMelden('aus', null, f);
  melden({ art: 'punkte', punkte: null });
}

/** Ändert Gestenhand, Kamera bzw. Stellschrauben (Teil). Eine andere Kamera wird sofort geöffnet. */
export async function einstellen(teil = {}) {
  const hand = teil.hand === 'rechts' || teil.hand === 'links' ? teil.hand : einst.hand;
  const stell = teil.stellschrauben && typeof teil.stellschrauben === 'object' ? { ...teil.stellschrauben } : einst.stellschrauben;
  const kameraNeu = 'kameraId' in teil && (teil.kameraId || null) !== einst.kameraId;
  einst = { hand, kameraId: 'kameraId' in teil ? teil.kameraId || null : einst.kameraId, stellschrauben: stell };
  // Stellschrauben vollständig ersetzen (nicht zusammenführen): was die Oberfläche nicht mehr setzt, gilt wieder mit der Vorgabe
  const neu = L.neuerZustand({ hand, stellschrauben: stell });
  if (hand !== logik.hand) logik = neu; else logik.werte = neu.werte;
  const l = lauf;
  if (kameraNeu && l && !l.versteckt) {
    clearTimeout(l.zeitgeber);
    kameraFreigeben(l);
    try { await kameraAnschliessen(l); planen(l, 0); }
    catch (e) { abbrechen(e?.message ?? String(e), e); throw e; }
  }
}

export const laeuft = () => !!lauf;

/** Kameras zur Auswahl; die Namen kennt der Browser erst, wenn die Kamera einmal erlaubt wurde. */
export async function kameras() {
  try {
    const liste = await navigator.mediaDevices.enumerateDevices();
    return liste.filter((d) => d.kind === 'videoinput').map((d, i) => ({ id: d.deviceId, name: d.label || `Kamera ${i + 1}` }));
  } catch { return []; }
}

export function abonnieren(fn) {
  if (typeof fn !== 'function') return () => {};
  hoerer.add(fn);
  return () => hoerer.delete(fn);
}

/** Testfeld: Ereignisse laufen weiter, fliessende Gesten bewegen keine Ansicht. Ohne Angabe nur lesen. */
export function testmodus(an) {
  if (an !== undefined) test = !!an;
  return test;
}

/** Aufnahme nur der Punkte (nie Bilder), Zeiten in ms ab dem Start; dazu die Zeiten der Maus- und Tastendrücke (Sperre). */
export function aufnahmeStarten() {
  aufnahme = { t0: performance.now(), bilder: [], eingaben: [] };
}

export function aufnahmeBeenden() {
  const a = aufnahme;
  aufnahme = null;
  if (!a) return null;
  return { version: L.AUFNAHME_VERSION, hand: einst.hand, bildbreite: BREITE, bildhoehe: Math.round(BREITE * (lauf?.sv ?? HOEHE / BREITE)), bilder: a.bilder, eingaben: a.eingaben };
}

/** Messwerte der laufenden Erkennung (Bilder/s im Mittel der letzten 2 s, Dauer je Bild im Worker) und gesperrte Netzanfragen. */
export async function diagnose() {
  const l = lauf;
  if (!l) return { laeuft: false, gesperrt: zuletzt.gesperrt };
  const z = l.zeiten;
  const bildrate = z.length > 1 ? ((z.length - 1) * 1000) / (z.at(-1) - z[0]) : 0;
  const dauerMs = l.dauern.length ? l.dauern.reduce((s, x) => s + x, 0) / l.dauern.length : null;
  const antwort = await new Promise((ja) => {
    const zeit = setTimeout(() => ja(null), 1000);
    l.wartende.set('diagnose', (n) => { clearTimeout(zeit); ja(n); });
    l.worker.postMessage({ art: 'diagnose' });
  });
  return {
    laeuft: true, bildrate, dauerMs, zielMs: intervall(l), ruhe: l.ruhe, rueckfall: l.rueckfall, versteckt: l.versteckt,
    kamera: l.strom?.getVideoTracks?.()[0]?.readyState === 'live',
    delegate: l.delegate, ladenMs: l.ladenMs, gesperrt: antwort?.gesperrt ?? [], bilderImWorker: antwort?.bilder ?? null, zustand: gemeldet.zustand,
  };
}
