// Tests der Palette «Problem-Marker» (Modul XR, XR-E, Quest App V1a) ohne Browser: Liste (Filter, Sortierung, Zahlen, überfällige Frist), Zeilen, Eingaben prüfen und
// Parameter für element_aendern, Parameter für bcf_export, Kamera der 3D-Ansicht aus dem Blickpunkt der Brille, Verdrahtung (Katalog, Menüband, Symbol, 3D-Ansicht).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/problemmarker.test.mjs        Alle Werte FIKTIV.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import * as T from './problemmarker-text.js';
import * as sym from './symbole.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const katalog = JSON.parse(lies('../../../../../kataloge/klassen.json'));
const klasse = (n) => (Array.isArray(katalog) ? katalog : katalog.klassen).find((k) => k.name === n);

const M = (kz, werte = {}, punkt = [2000, 1500, 1200], geschoss = 'EG') =>
  ({ id: 'id-' + kz, kennzeichen: kz, klasse: 'ProblemMarker', geschoss, geometrie: { art: 'punkt', punkte: [punkt] }, werte });

const HEUTE = '2026-10-24';
const marker = [
  M('PM-10', { status: 'offen', notiz: 'Kollision mit Lüftung\nzweite Zeile', benutzer: 'Quest Christian', zeit: '2026-10-24T14:05:00+02:00', quelle: 'brille',
    blickpunkt: { kopf: [2000, -500, 1700], richtung: [0, 1, 0], modus: 'begehen', massstab: 1 } }),
  M('PM-2', { status: 'erledigt', zustaendig: 'Muster Sanitär AG', frist: '2026-10-01' }),
  M('PM-3', { zustaendig: 'Planer', frist: '2026-10-20' }),
  { id: 'w', kennzeichen: 'W-1', klasse: 'Wand' },
];

test('Liste: Filter, offene zuerst, natürliche Reihenfolge, Zahlen mit überfälligen', () => {
  assert.deepEqual(T.liste(marker, 'offen').map((m) => m.kennzeichen), ['PM-3', 'PM-10']);
  assert.deepEqual(T.liste(marker, 'erledigt').map((m) => m.kennzeichen), ['PM-2']);
  assert.deepEqual(T.liste(marker, 'alle').map((m) => m.kennzeichen), ['PM-3', 'PM-10', 'PM-2']);
  assert.deepEqual(T.zahlen(marker, HEUTE), { offen: 2, erledigt: 1, ueberfaellig: 1 });   // PM-3 (Frist 20.10.), PM-2 ist erledigt
  assert.deepEqual(T.liste(null), []);
  assert.equal(T.status(M('x')), 'offen');
});

test('Zeile: Notiz gekürzt, gesetzt von und am, Frist, aus der Brille', () => {
  const z = T.zeile(marker[0], HEUTE);
  assert.equal(z.notiz, 'Kollision mit Lüftung');
  assert.equal(z.von, 'Quest Christian · 24.10.2026');
  assert.equal(z.brille, true);
  assert.equal(z.blickpunkt, true);
  assert.equal(T.zeile(marker[2], HEUTE).ueberfaellig, true);
  assert.equal(T.zeile(marker[2], HEUTE).frist, '20.10.2026');
  assert.equal(T.zeile(M('PM-9'), HEUTE).notiz, 'ohne Beschreibung');
  assert.equal(T.kurzNotiz(M('x', { notiz: 'x'.repeat(80) }), 10), 'xxxxxxxxx…');
  assert.equal(T.datumText('kein'), '');
});

test('Änderung: nur geänderte Werte, leer entfernt, Prüfung der Eingaben', () => {
  const m = marker[2];
  assert.deepEqual(T.aenderung(m, { status: 'erledigt', zustaendig: 'Planer', frist: '2026-10-20', notiz: '' }),
    { parameter: { element: 'id-PM-3', eigenschaften: { status: 'erledigt' } } });
  assert.deepEqual(T.aenderung(m, { status: 'offen', zustaendig: '  ', frist: '', notiz: 'Prüfen\r\n' }),
    { parameter: { element: 'id-PM-3', eigenschaften: { zustaendig: null, frist: null, notiz: 'Prüfen' } } });
  assert.deepEqual(T.aenderung(m, { status: 'offen', zustaendig: 'Planer', frist: '2026-10-20', notiz: '' }), { parameter: null });
  assert.match(T.aenderung(m, { status: 'zu', zustaendig: '', frist: '', notiz: '' }).fehler, /Status/);
  assert.match(T.aenderung(m, { status: 'offen', zustaendig: '', frist: '20.10.2026', notiz: '' }).fehler, /JJJJ-MM-TT/);
  assert.match(T.aenderung(m, { status: 'offen', zustaendig: 'x'.repeat(101), frist: '', notiz: '' }).fehler, /Zuständig/);
  assert.match(T.aenderung(m, { status: 'offen', zustaendig: '', frist: '', notiz: 'x'.repeat(501) }).fehler, /Notiz/);
});

test('BCF: Parameter mit klasse ProblemMarker und Status nach dem Filter, Dateiname', () => {
  assert.deepEqual(T.bcfParameter(' "C:\\Temp\\a.bcf" ', 'offen'), { datei: 'C:\\Temp\\a.bcf', klasse: 'ProblemMarker', status: 'offen' });
  assert.deepEqual(T.bcfParameter('a.bcf', 'alle'), { datei: 'a.bcf', klasse: 'ProblemMarker' });
  assert.equal(T.bcfDateiname('MFH: Musterweg', HEUTE), 'Problem-Marker_MFH_ Musterweg_2026-10-24.bcf');
  assert.equal(T.bcfDateiname('', HEUTE), 'Problem-Marker_Projekt_2026-10-24.bcf');
});

test('Kamera: am Kopf mit Blick auf die Stelle, höchstens 30 m, ohne Blickpunkt schräg von oben', () => {
  const k = T.kamera(marker[0]);
  assert.deepEqual(k.ziel, [2, 1.2, -1.5]);
  assert.deepEqual(k.position.map((x) => Math.round(x * 1e6) / 1e6), [2, 1.7, 0.5]);   // Kopf (2, −0.5, 1.7 m) → three (x, z, −y)
  // Sitzungstisch: Kopf 80 m über dem Modell → 30 m vor der Stelle in derselben Richtung
  const t = T.kamera(M('PM-5', { blickpunkt: { kopf: [2000, 1500, 81200], richtung: [0, 0, -1], modus: 'sitzungsraum', massstab: 50 } }));
  assert.deepEqual(t.position.map((x) => Math.round(x * 1e6) / 1e6), [2, 31.2, -1.5]);
  // Kopf an der Stelle: Blickrichtung
  const n = T.kamera(M('PM-6', { blickpunkt: { kopf: [2000, 1500, 1200], richtung: [1, 0, 0] } }));
  assert.deepEqual(n.ziel.map((x) => Math.round(x * 1e6) / 1e6), [3, 1.2, -1.5]);
  // ohne Blickpunkt: Südwest von oben, 3 m
  const o = T.kamera(M('PM-7'));
  const d = Math.hypot(o.position[0] - o.ziel[0], o.position[1] - o.ziel[1], o.position[2] - o.ziel[2]);
  assert.ok(Math.abs(d - 3) < 1e-9);
  assert.ok(o.position[1] > o.ziel[1] && o.position[0] < o.ziel[0]);
  assert.equal(T.kamera({ geometrie: null }), null);
});

test('Verdrahtung: Klasse im Katalog, Palette, Menüband, Symbol, Befehle, 3D-Ansicht', () => {
  const k = klasse('ProblemMarker');
  assert.ok(k, 'Klasse ProblemMarker in kataloge/klassen.json');
  assert.deepEqual(k.eigenschaften.find((e) => e.name === 'status').auswahl, T.STATUS);
  for (const n of ['notiz', 'zustaendig', 'frist', 'blickpunkt', 'beteiligte']) assert.ok(k.eigenschaften.some((e) => e.name === n), n);
  assert.ok(sym.SYMBOLE?.problemMarker ?? lies('./symbole.js').includes('problemMarker:'), 'Symbol problemMarker');
  assert.match(lies('./aktionen.js'), /import '\.\/problemmarker-panel\.js';/);
  const p = lies('./problemmarker-panel.js');
  assert.match(p, /id: 'problemmarker'/);
  assert.match(p, /id: 'koordination\.problemMarker'/);
  assert.match(p, /sende\('element_aendern'/);
  assert.match(p, /abfrage\('bcf_export'/);
  assert.match(p, /blickpunkt\?\.\(k\)/);
  assert.doesNotMatch(p, /innerHTML = [^`]*\$\{(?!esc\()[a-z]/i, 'HTML nur über esc()');
  assert.match(lies('../ansichten/ansicht3d.js'), /blickpunkt\(\{ position, ziel \} = \{\}\)/);
});
