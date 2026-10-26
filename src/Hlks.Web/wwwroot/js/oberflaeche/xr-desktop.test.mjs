// Tests der Anzeige «Brille sieht den Bildschirm» (Modul XR, XR-I «Desktop in der Brille», Quest App V1c) ohne Browser: Regeln (xr-desktop-text.js) und
// Verdrahtung (xr-desktop-status.js, statusleiste.js, quest.css).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/xr-desktop.test.mjs
// Die Zustände haben die Form der Antwort von GET /api/xr/desktop/zustand (Server: XrDesktop.cs); Namen sind fiktiv.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as T from './xr-desktop-text.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');
const strom = (x = {}) => ({ id: 'd1', kopplung: 'k1', brille: 'QUEST-1', name: 'Quest Christian', gestartet: true, pausiert: false, monitor: '\\\\.\\DISPLAY1',
  monitorName: 'Bildschirm 1', breite: 1920, hoehe: 1080, bildrate: 12, bilderJeSekunde: 11.5, kbitJeSekunde: 840.25, ...x });
const z = (...s) => ({ erfassung: 'bildschirm', bildschirme: [], verbunden: s.length, aktiv: s.filter((x) => !x.pausiert).length, stroeme: s });

test('ohne Brille: keine Anzeige', () => {
  assert.equal(T.desktopAnzeige(null), null);
  assert.equal(T.desktopAnzeige({}), null);
  assert.equal(T.desktopAnzeige(z()), null);
  // verbunden, aber noch kein «start»: nicht anzeigen (es wird nichts erfasst)
  assert.equal(T.desktopAnzeige(z(strom({ gestartet: false }))), null);
});

test('eine Brille sieht mit: rot mit Name, Monitor, Rate und Beenden', () => {
  const a = T.desktopAnzeige(z(strom()));
  assert.equal(a.stufe, 'aktiv');
  assert.equal(a.text, 'Brille sieht den Bildschirm');
  assert.equal(a.knopf, 'Beenden');
  assert.match(a.titel, /«Quest Christian»/);
  assert.match(a.titel, /Bildschirm 1, 1920 × 1080/);
  assert.match(a.titel, /11\.5 Bilder\/s, 840\.3 kbit\/s/);
  assert.match(a.titel, /Maus und Tastatur/);
  // ohne Namen die Kennung der Brille
  assert.match(T.desktopAnzeige(z(strom({ name: '' }))).titel, /«QUEST-1»/);
  // ruhiger Bildschirm (nach 2 s ohne Änderung erfasst KICAD seltener): steht im Titel, sonst nicht
  assert.doesNotMatch(a.titel, /ruhig/);
  assert.match(T.desktopAnzeige(z(strom({ ruhig: true, ruheBildrate: 2.5 }))).titel, /ruhig – nur 2\.5 Erfassungen\/s/);
});

test('zwei Brillen, eine pausiert: aktiv zählt nur die mitsehenden', () => {
  const a = T.desktopAnzeige(z(strom(), strom({ id: 'd2', name: 'Gast', pausiert: true })));
  assert.equal(a.stufe, 'aktiv');
  assert.match(a.titel, /«Quest Christian»/);
  const b = T.desktopAnzeige(z(strom(), strom({ id: 'd2', name: 'Gast' })));
  assert.equal(b.text, '2 Brillen sehen den Bildschirm');
  assert.match(b.titel, /^2 Brillen sehen diesen Bildschirm/);
});

test('nur pausiert: graue Anzeige, nichts wird übertragen', () => {
  const a = T.desktopAnzeige(z(strom({ pausiert: true })));
  assert.equal(a.stufe, 'pausiert');
  assert.equal(a.text, 'Bildschirm in der Brille pausiert');
  assert.match(a.titel, /nichts übertragen/);
  assert.equal(a.knopf, 'Beenden');
});

test('Meldung nach Beenden', () => {
  assert.equal(T.beendetText({ beendet: 0 }), 'Keine Brille sah den Bildschirm.');
  assert.equal(T.beendetText({ beendet: 1 }), 'Übertragung an die Brille beendet.');
  assert.equal(T.beendetText({ beendet: 2 }), 'Übertragung an 2 Brillen beendet.');
  assert.equal(T.beendetText(null), 'Keine Brille sah den Bildschirm.');
});

test('Verdrahtung: Statusleiste, Endpunkte, roter Rand, kein Eingriff in den PC', () => {
  const s = lies('./xr-desktop-status.js');
  assert.match(s, /holen\(T\.ZUSTAND\)/);
  assert.match(s, /senden\(T\.BEENDEN, \{\}\)/);
  assert.match(s, /setInterval\(lesen, T\.TAKT_MS\)/);
  assert.match(s, /classList\.toggle\('xr-desktop-aktiv'/);
  assert.equal(T.ZUSTAND, '/api/xr/desktop/zustand');
  assert.equal(T.BEENDEN, '/api/xr/desktop/beenden');
  assert.equal(T.TAKT_MS, 2000);
  const sl = lies('./statusleiste.js');
  assert.match(sl, /class="sl-desktop" hidden/);
  assert.match(sl, /xrDesktopStatusEinrichten\(w\.querySelector\('\.sl-desktop'\)\)/);
  const css = lies('../../css/quest.css');
  assert.match(css, /\.sl-desktop\.aktiv/);
  assert.match(css, /html\.xr-desktop-aktiv body::after/);
  assert.match(css, /pointer-events: none/);   // der Rand nimmt keine Klicks
  // die Oberfläche schickt nichts an die Brille ausser «Beenden» (Bedienung nur mit Maus und Tastatur am PC)
  assert.doesNotMatch(s, /\/api\/xr\/desktop['"]/);
});
