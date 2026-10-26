// Gestenerkennung im eigenen Faden (Modul G, Modul-Worker): MediaPipe Tasks Vision 1.1.1 HandLandmarker aus /lib/mediapipe/ (lokal, kein CDN), Laufmodus VIDEO,
// zwei Hände. Gestartet von gesten.js: new Worker(…/gesten-worker.js, { type: 'module' }). Der WASM-Lader ist die Modul-Variante (vision_wasm_module_internal.js),
// die MediaPipe in einem Modul-Worker per import() lädt (importScripts gibt es hier nicht).
//
// Nachrichten an den Worker:
//   { art: 'start', basis: '/lib/mediapipe/', modell: 'hand_landmarker.task', haende: 2, delegate: 'CPU' | 'GPU' }  → { art: 'bereit', delegate, ladenMs }
//   { art: 'bild', bild: ImageBitmap (übertragen), t }  → { art: 'ergebnis', t, haende: [{ punkte, welt, haendigkeitRoh, score }], dauerMs }
//   { art: 'stopp' }  → { art: 'gestoppt', gesperrt }       { art: 'diagnose' } → { art: 'diagnose', gesperrt, bilder }
// Fehler: { art: 'fehler', meldung }.
// DATENSCHUTZ: Das Bild bleibt in diesem Faden und wird nach der Erkennung sofort freigegeben (close). Zurück gehen nur Zahlen (ergebnisAusMediapipe). Jede
// Netzanfrage ausser auf die eigenen Dateien unter /lib/mediapipe/ ist gesperrt (MediaPipe 1.1.1 meldet sonst Nutzungsdaten an Google, siehe lib/mediapipe/LIESMICH.md);
// der Server liefert diesen Worker zudem mit «Content-Security-Policy: connect-src 'self'» aus.

import { ergebnisAusMediapipe, netzErlaubt } from './gesten-logik.js';

// <netzsperre> – die einzige Stelle in js/gesten/, die Netzschnittstellen anfasst: sie werden hier abgeriegelt, bevor MediaPipe geladen wird.
const herkunft = self.location.origin;
const gesperrt = [];
const sperren = (url) => { if (gesperrt.length < 20) gesperrt.push(String(url).slice(0, 200)); };
const netzOriginal = self.fetch.bind(self);
self.fetch = (eingabe, init) => {
  const url = typeof eingabe === 'string' ? eingabe : eingabe?.url ?? String(eingabe);
  if (!netzErlaubt(url, herkunft)) { sperren(url); return Promise.reject(new TypeError('Netzanfrage gesperrt (Gesten-Worker)')); }
  return netzOriginal(eingabe, init);
};
if (typeof self.XMLHttpRequest === 'function') {
  const oeffnen = self.XMLHttpRequest.prototype.open;
  self.XMLHttpRequest.prototype.open = function (methode, url, ...rest) {
    if (!netzErlaubt(url, herkunft)) { sperren(url); throw new TypeError('Netzanfrage gesperrt (Gesten-Worker)'); }
    return oeffnen.call(this, methode, url, ...rest);
  };
}
for (const name of ['WebSocket', 'EventSource', 'WebTransport']) {
  try { Object.defineProperty(self, name, { value: undefined, configurable: false, writable: false }); } catch { /* nicht vorhanden */ }
}
// </netzsperre>

// Die WASM-Laufzeit von MediaPipe schreibt Hinweise auf stderr, das im Browser als console.error erscheint (z. B. «INFO: Created TensorFlow Lite XNNPACK delegate
// for CPU.», Befund gesten.browser.mjs 26.10.2026): solche Zeilen als Hinweis ausgeben, echte Fehler bleiben Fehler.
const fehlerAusgabe = console.error.bind(console);
console.error = (...a) => (typeof a[0] === 'string' && /^(INFO|I\d{4}|W\d{4})[: ]/.test(a[0]) ? console.info(...a) : fehlerAusgabe(...a));

let erkenner = null;
let letzteZeit = -1;
let bilder = 0;
let laedt = null;

async function laden({ basis = '/lib/mediapipe/', modell = 'hand_landmarker.task', haende = 2, delegate = 'CPU' } = {}) {
  const t0 = performance.now();
  const b = new URL(basis, self.location.href);
  if (!netzErlaubt(b.href, herkunft)) throw new Error('Die Erkennung darf nur aus /lib/mediapipe/ geladen werden.');
  const { FilesetResolver, HandLandmarker } = await import(new URL('vision_bundle.mjs', b).href);
  const dateien = await FilesetResolver.forVisionTasks(new URL('wasm', b).href.replace(/\/$/, ''), true);   // true = Modul-Variante (import statt importScripts)
  const optionen = (d) => ({
    baseOptions: { modelAssetPath: new URL(modell, b).href, delegate: d },
    runningMode: 'VIDEO', numHands: haende, minHandDetectionConfidence: 0.5, minHandPresenceConfidence: 0.5, minTrackingConfidence: 0.5,   // tiefere Werte (0,4/0,3) liessen unsichere Punkte durch, die Faust brach öfter ab (Abnahme 26.10.2026)
  });
  let genutzt = delegate;
  try {
    erkenner = await HandLandmarker.createFromOptions(dateien, optionen(delegate));
  } catch (e) {
    if (delegate !== 'GPU') throw e;
    genutzt = 'CPU';   // ohne WebGL im Worker: Rückfall auf die CPU
    erkenner = await HandLandmarker.createFromOptions(dateien, optionen('CPU'));
  }
  return { delegate: genutzt, ladenMs: Math.round(performance.now() - t0) };
}

self.onmessage = async (ev) => {
  const n = ev.data ?? {};
  try {
    if (n.art === 'start') {
      laedt ??= laden(n);
      const r = await laedt;
      self.postMessage({ art: 'bereit', delegate: r.delegate, ladenMs: r.ladenMs });
    } else if (n.art === 'bild') {
      const bild = n.bild;
      try {
        if (!erkenner || !bild) { self.postMessage({ art: 'ergebnis', t: n.t, haende: [], dauerMs: 0 }); return; }
        const t = Math.max(Number(n.t) || 0, letzteZeit + 1);   // VIDEO verlangt steigende Zeiten (ms)
        letzteZeit = t;
        const t0 = performance.now();
        const r = erkenner.detectForVideo(bild, t);
        const dauerMs = performance.now() - t0;
        bilder++;
        self.postMessage({ art: 'ergebnis', t: n.t, haende: ergebnisAusMediapipe(r).haende, dauerMs });
      } finally {
        bild?.close?.();   // das Bild gleich freigeben: es wird nirgends behalten
      }
    } else if (n.art === 'stopp') {
      try { erkenner?.close(); } catch { /* egal */ }
      erkenner = null;
      laedt = null;
      self.postMessage({ art: 'gestoppt', gesperrt: [...gesperrt] });
    } else if (n.art === 'diagnose') {
      self.postMessage({ art: 'diagnose', gesperrt: [...gesperrt], bilder });
    }
  } catch (e) {
    if (n.art === 'start') laedt = null;
    self.postMessage({ art: 'fehler', meldung: `Gestenerkennung: ${e?.message ?? e}` });
  }
};
