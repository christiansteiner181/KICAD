// Datenschutz der Gesten (Modul G, entwurf/modul-gesten.md, Leitlinie 6 «Das Bild bleibt im Browser»): statische Prüfung aller Dateien in js/gesten/ und eine
// Laufzeitprobe der Netzsperre des Workers (in Node, mit einer Attrappe des Worker-Umfelds; MediaPipe wird dabei nicht geladen).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/gesten/gesten-datenschutz.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readdirSync, readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const hier = path.dirname(fileURLToPath(import.meta.url));
const dateien = readdirSync(hier).filter((d) => /\.(m?js)$/.test(d) && !d.endsWith('.test.mjs') && !d.endsWith('.browser.mjs'));
const quelle = (d) => readFileSync(path.join(hier, d), 'utf8');
/** Quelltext ohne Kommentare (Zeilen- und Blockkommentare; Zeichenketten bleiben). */
const ohneKommentare = (s) => s.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:'"`\\])\/\/.*$/gm, '$1');
/** Der Block der Netzsperre im Worker (die einzige erlaubte Stelle für Netzschnittstellen). */
const SPERRE = /\/\/ <netzsperre>[\s\S]*?\/\/ <\/netzsperre>/;

test('js/gesten: die Dateien der Erkennung sind da', () => {
  for (const d of ['gesten.js', 'gesten-logik.js', 'gesten-worker.js']) assert.ok(dateien.includes(d), d);
});

test('js/gesten: kein fetch, XMLHttpRequest, WebSocket, EventSource, sendBeacon – ausser der Netzsperre des Workers, die sie abriegelt', () => {
  for (const d of dateien) {
    let s = ohneKommentare(quelle(d));
    if (d === 'gesten-worker.js') {
      assert.match(quelle(d), SPERRE, 'Block der Netzsperre');
      s = ohneKommentare(quelle(d).replace(SPERRE, ''));
      const block = quelle(d).match(SPERRE)[0];
      assert.match(block, /self\.fetch = /);
      assert.match(block, /netzErlaubt\(url, herkunft\)/);
      assert.match(block, /XMLHttpRequest\.prototype\.open = /);
      assert.match(block, /'WebSocket', 'EventSource'/);
    }
    for (const verboten of [/\bfetch\s*\(/, /XMLHttpRequest/, /WebSocket/, /EventSource/, /sendBeacon/, /importScripts/]) assert.doesNotMatch(s, verboten, `${d}: ${verboten}`);
  }
});

test('js/gesten: niemand importiert api.js (kein Weg zum Server)', () => {
  for (const d of dateien) {
    const s = ohneKommentare(quelle(d));
    assert.doesNotMatch(s, /from\s+['"][^'"]*\bapi\.js['"]/, d);
    assert.doesNotMatch(s, /import\(\s*['"][^'"]*\bapi\.js['"]/, d);
  }
});

test('js/gesten: kein Kamerabild in Dateien, Speicher oder Konsole', () => {
  for (const d of dateien) {
    const s = ohneKommentare(quelle(d));
    for (const verboten of [/toDataURL/, /getImageData/, /\.toBlob\s*\(/, /MediaRecorder/, /captureStream/, /indexedDB/, /convertToBlob/, /readPixels/]) assert.doesNotMatch(s, verboten, `${d}: ${verboten}`);
  }
  for (const d of ['gesten.js', 'gesten-worker.js', 'gesten-logik.js']) {
    const s = ohneKommentare(quelle(d));
    assert.doesNotMatch(s, /localStorage|sessionStorage|console\.log/, d);
    for (const z of s.match(/console\.\w+\([^\n]*/g) ?? []) assert.doesNotMatch(z, /\b(bild|video|strom|v)\b(?!['"])/, `${d}: ${z}`);
  }
});

test('Worker: zurück gehen nur Zahlen der Hände bzw. kurze Meldungen – das Bild nie', () => {
  const s = ohneKommentare(quelle('gesten-worker.js'));
  const posts = s.match(/postMessage\([^;]*\)/g) ?? [];
  assert.ok(posts.length >= 4, posts.join('\n'));
  for (const p of posts) {
    assert.doesNotMatch(p, /\bbild\b/, p);
    assert.match(p, /^postMessage\(\{ art: '(bereit|ergebnis|gestoppt|diagnose|fehler)'/, p);
  }
  assert.match(s, /haende: ergebnisAusMediapipe\(r\)\.haende/, 'Ergebnis nur über ergebnisAusMediapipe');
  assert.match(s, /bild\?\.close\?\.\(\)/, 'das Bild wird sofort freigegeben');
  // MediaPipe wird erst nach der Netzsperre geladen (dynamisch), nur aus /lib/mediapipe/
  const roh = quelle('gesten-worker.js');
  assert.ok(roh.indexOf("import(new URL('vision_bundle.mjs'") > roh.indexOf('// </netzsperre>'));
  assert.doesNotMatch(roh, /^import .*vision_bundle/m);
});

test('gesten.js: das Bild geht nur als übertragene ImageBitmap an den Worker; weiter gegeben werden nur Punkte und Ereignisse', () => {
  const s = ohneKommentare(quelle('gesten.js'));
  const posts = s.match(/postMessage\([^;]*\)/g) ?? [];
  const mitBild = posts.filter((p) => /\bbild\b/.test(p));
  assert.deepEqual(mitBild, ["postMessage({ art: 'bild', bild, t: jetzt }, [bild])"]);
  assert.match(s, /createImageBitmap\(v, /);
  for (const m of s.match(/melden\(\{[^}]*\}/g) ?? []) assert.match(m, /art: '(zustand|punkte)'/, m);
  assert.doesNotMatch(s, /appendChild|\.append\(|document\.body/, 'das Video kommt nie ins Dokument');
  // Aufnahmen: nur Punkte (bildFuerAufnahme) und Zeiten
  assert.match(s, /aufnahme\.bilder\.push\(L\.bildFuerAufnahme\(/);
});

test('Laufzeit der Netzsperre: der Worker sperrt Google-Telemetrie und alles ausser /lib/mediapipe/', async () => {
  // Attrappe des Worker-Umfelds: self, location, fetch, XMLHttpRequest, postMessage
  const gesendet = [], geholt = [], nachrichten = [];
  globalThis.self = globalThis;
  Object.defineProperty(globalThis, 'location', { value: new URL('http://localhost:5199/js/gesten/gesten-worker.js'), configurable: true });
  globalThis.fetch = async (u) => { geholt.push(String(u)); return { ok: true, status: 200 }; };
  globalThis.XMLHttpRequest = class { open(m, u) { gesendet.push(String(u)); } };
  globalThis.postMessage = (n) => nachrichten.push(n);
  await import(pathToFileURL(path.join(hier, 'gesten-worker.js')).href + '?probe');
  await assert.rejects(() => fetch('https://odml.pa.googleapis.com/v1/log', { method: 'POST', body: 'x' }), /gesperrt/);
  await assert.rejects(() => fetch('/api/befehl'), /gesperrt/);
  await assert.rejects(() => fetch(new Request('https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision/wasm/x.wasm')), /gesperrt/);
  await fetch('http://localhost:5199/lib/mediapipe/hand_landmarker.task');
  assert.deepEqual(geholt, ['http://localhost:5199/lib/mediapipe/hand_landmarker.task']);
  assert.throws(() => new XMLHttpRequest().open('POST', 'https://odml.pa.googleapis.com/v1/log'), /gesperrt/);
  new XMLHttpRequest().open('GET', '/lib/mediapipe/wasm/vision_wasm_module_internal.wasm');
  assert.deepEqual(gesendet, ['/lib/mediapipe/wasm/vision_wasm_module_internal.wasm']);
  assert.equal(globalThis.WebSocket, undefined);
  assert.equal(globalThis.EventSource, undefined);
  // ohne geladenen Erkenner: ein Bild gibt eine leere Antwort und wird freigegeben
  let zu = 0;
  await self.onmessage({ data: { art: 'bild', bild: { close() { zu++; } }, t: 5 } });
  assert.equal(zu, 1);
  await self.onmessage({ data: { art: 'diagnose' } });
  const d = nachrichten.find((n) => n.art === 'diagnose');
  assert.equal(d.gesperrt.length, 4);
  assert.ok(d.gesperrt.every((u) => !u.includes('/lib/mediapipe/')));
  assert.deepEqual(nachrichten.find((n) => n.art === 'ergebnis'), { art: 'ergebnis', t: 5, haende: [], dauerMs: 0 });
  // laden nur aus /lib/mediapipe/
  await self.onmessage({ data: { art: 'start', basis: 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision/' } });
  assert.match(nachrichten.at(-1).meldung, /nur aus \/lib\/mediapipe\//);
});
