// Tests der Eigenschaftenpalette zur Schnitthöhe nach SIA 400 (Nachbearbeitung N21) ohne Browser: Zeilen unter «schnitthoehe».
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/schnitthoehe-palette.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';

const P = await import('./schnitthoehe-palette.js');

// Palettenbausteine wie in eigenschaften.js (HELFER), vereinfacht
const H = {
  zeile: (label, feld, o = {}) => `<div class="ep-zeile ${o.klasse ?? ''}"><label>${label}</label>${feld}</div>`,
  nurLesen: (t) => `<span class="ep-text">${t}</span>`,
};

const daten = {
  ansicht: 'AN-EG', geschoss: 'EG', norm: 'SIA 400:2000 Ziff. C.3.1.1', wert: 1100, gilt: 1100, nachSia: true, handwert: 1000,
  herkunft: 'SIA 400:2000 Ziff. C.3.1.1: Schnitt durch alle 2 Türen und Fenster des Geschosses EG; gemeinsamer Bereich 1100 … 2150 mm über OK Boden. Massgebend: F-1 (untere Grenze 1100 mm, Fenster); T-1 (obere Grenze 2150 mm, Tür).',
  hinweise: [], massgebend: ['F-1 (untere Grenze 1100 mm, Fenster)', 'T-1 (obere Grenze 2150 mm, Tür)'], oeffnungen: 2, geschnitten: 2, von: 1100, bis: 2150,
};

test('nach SIA: gerechnete Höhe schreibgeschützt mit Herkunft, ohne Eingabefeld', () => {
  const zeilen = P.schnitthoeheZeilen(daten, H);
  assert.ok(Array.isArray(zeilen) && zeilen.length >= 2);
  const html = zeilen.join('');
  assert.match(html, /1100 mm über OK Geschoss \(nach SIA 400\)/);
  assert.match(html, /SIA 400:2000 Ziff\. C\.3\.1\.1/);
  assert.match(html, /F-1/);                       // massgebende Öffnung in der Herkunft
  assert.doesNotMatch(html, /<input/);             // nicht bearbeitbar
  assert.match(html, /Handwert/);                  // der gespeicherte Handwert bleibt sichtbar
  assert.match(html, /1000 mm/);
});

test('Hinweise (nicht geschnittene Öffnungen) erscheinen als eigene Zeilen', () => {
  const html = P.schnitthoeheZeilen({ ...daten, hinweise: ['Nicht geschnitten bei 1500 mm: F-3 (Fenster, 2200 … 2500 mm).'] }, H).join('');
  assert.match(html, /ep-schnitthoehe-hinweis/);
  assert.match(html, /F-3/);
});

test('Schalter aus oder keine Antwort des Kerns: keine Ersatzzeilen, das Feld schnitthoehe bleibt bearbeitbar', () => {
  assert.equal(P.schnitthoeheZeilen({ ...daten, nachSia: false, gilt: 1500, handwert: 1500 }, H), null);
  assert.equal(P.schnitthoeheZeilen(null, H), null);
  assert.equal(P.schnitthoeheZeilen({ nachSia: true }, H), null);   // ohne Wert
});

test('nachSia: Antwort des Kerns vor dem Wert der Eigenschaft, ohne Wert ja', () => {
  assert.equal(P.nachSia(null, undefined), true);
  assert.equal(P.nachSia(null, false), false);
  assert.equal(P.nachSia({ nachSia: true }, false), true);
  assert.equal(P.nachSia({ nachSia: false }, undefined), false);
});

test('Text der Herkunft wird maskiert', () => {
  const html = P.schnitthoeheZeilen({ ...daten, herkunft: '<b>x</b>' }, H).join('');
  assert.doesNotMatch(html, /<b>x<\/b>/);
  assert.match(html, /&lt;b&gt;/);
});
