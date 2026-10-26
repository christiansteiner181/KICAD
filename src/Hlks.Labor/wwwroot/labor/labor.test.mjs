// Tests der Labor-Seite (XR-Labor L0) ohne Browser: Marke des Labors für den Teil «Quest App» aus KICAD, Stand im Kopf, Zustand «Brille per adb», Teilnehmer, Verdrahtung.
// Aufruf:  node --test src/Hlks.Labor/wwwroot/labor/labor.test.mjs
// Die KICAD-Module liegen in src/Hlks.Web/wwwroot (der Labor-Server liefert sie als zweiten Wurzelordner aus, LaborOberflaeche.cs); hier werden sie über den Dateipfad geladen.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { LABOR_MARKE } from './labor-marke.js';
import { questAusApp, standText, teilnehmerZeilen, TEILNEHMER_TAKT_MS } from './labor-text.js';
import * as T from '../../../Hlks.Web/wwwroot/js/oberflaeche/quest-app-text.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');

test('Marke des Labors: alle Felder der KICAD-Marke, Werte wie im Server', () => {
  assert.ok(Object.isFrozen(LABOR_MARKE));
  assert.deepEqual(Object.keys(LABOR_MARKE).sort(), Object.keys(T.KICAD_MARKE).sort());
  assert.equal(LABOR_MARKE.app, 'XR-Labor');
  assert.equal(LABOR_MARKE.port, 5340);
  assert.equal(LABOR_MARKE.desktopPort, 5430);
  assert.equal(LABOR_MARKE.ablageSchluessel, null, 'am Labor nicht einstellbar: die Seite schreibt keine Benutzereinstellungen');
  assert.equal(LABOR_MARKE.brillenZeigen, false, 'das Labor hat keine 3D-Ansicht');
  assert.match(LABOR_MARKE.ablageVorgabe, /HLKS_CAD_werkzeuge\\labor_app$/);
  assert.match(LABOR_MARKE.bereitstellen, /quest_app_bereitstellen\.ps1 -Labor$/);
  // dieselben Werte wie der Server (LaborDienste.cs)
  const server = readFileSync(fileURLToPath(new URL('../../LaborDienste.cs', import.meta.url)), 'utf8');
  assert.match(server, /ZugangPort = 5340;/);
  assert.match(server, /DesktopPort = 5430;/);
  assert.match(server, /AblageOrdner = "labor_app";/);
  assert.match(server, /@"werkzeuge\\quest_app_bereitstellen\.ps1 -Labor"/);
  assert.match(server, /"XR-Labor"/);
});

test('Texte des Teils «Quest App» mit der Marke des Labors', () => {
  assert.equal(T.zugangAnzeige({ an: true, aktiv: false }, LABOR_MARKE).text, 'Er startet gleich (nur im XR-Labor auf Port 5430).');
  assert.match(T.zugangAnzeige({ an: false, aktiv: false }, LABOR_MARKE).text, /^Nur dieser PC erreicht XR-Labor\./);
  const app = { ablage: { fehler: null, version: null }, adbGefunden: true, brille: { wlan: false } };
  assert.equal(T.installierenKnopf({ q: questAusApp(app), app, zugang: { aktiv: true }, marke: LABOR_MARKE }).grund, 'Keine App abgelegt: werkzeuge\\quest_app_bereitstellen.ps1 -Labor ausführen.');
  assert.match(T.installierenKnopf({ q: questAusApp({ adbGefunden: false }), app: { ablage: { version: {} } }, zugang: { aktiv: true }, marke: LABOR_MARKE }).grund, /in KICAD unter «Quest verbinden» einrichten/);
  assert.match(T.firewallHinweis(LABOR_MARKE), /^XR-Labor liest Netzprofil/);
  assert.equal(T.installiertText({ adbGefunden: false }, LABOR_MARKE), 'adb fehlt (in KICAD unter «Quest verbinden» einrichten).');
});

test('Brille per adb aus GET /api/quest/app (das Labor hat kein /api/quest)', () => {
  assert.deepEqual(questAusApp(null), { adbGefunden: false, freigegeben: false, nichtFreigegeben: false });
  assert.deepEqual(questAusApp({ adbGefunden: true, brille: null }), { adbGefunden: true, freigegeben: false, nichtFreigegeben: false });
  assert.deepEqual(questAusApp({ adbGefunden: true, brille: { seriennummer: '1WMHH000000000' } }), { adbGefunden: true, freigegeben: true, nichtFreigegeben: false });
});

test('Stand im Kopf aus GET /api/status', () => {
  assert.equal(standText({ app: 'xr-labor', fassung: '0.1.0', commit: '3de90521ac09fedb2618a3eb82e79a6b59e148d3', beschaeftigt: false }), 'XR-Labor L0 · Fassung 0.1.0 · Commit 3de9052');
  assert.equal(standText({ app: 'xr-labor', fassung: '0.1.0', commit: null }), 'XR-Labor L0 · Fassung 0.1.0');
  assert.equal(standText({ app: 'kicad' }), 'Stand unbekannt', 'eine andere App auf dem Port ist nicht das Labor');
  assert.equal(standText(null), 'Stand unbekannt');
});

test('Teilnehmer im Kanal: Name der Brille, App, Gerät, seit, Posen', () => {
  const z = teilnehmerZeilen({ teilnehmer: [
    { id: 't1a2b3c4d', name: 'Christian', geraet: 'Quest 3', app: '0.1', brille: 'QUEST3-LABOR', brillenName: 'Quest Labor', seit: '2026-10-26T09:05:00Z', posen: 12 },
    { id: 't9', name: '<b>x</b>' },
    { name: 'ohne Id' },
    null,
  ] });
  assert.equal(z.length, 2);
  assert.equal(z[0].id, 't1a2b3c4d');
  assert.match(z[0].text, /^Quest Labor · App 0\.1 · Quest 3 · seit \d\d:\d\d · 12 Posen$/);
  assert.equal(z[1].text, '<b>x</b> · seit unbekannt · 0 Posen', 'Text bleibt Text; die Seite setzt ihn mit textContent');
  assert.deepEqual(teilnehmerZeilen(null), []);
  assert.equal(TEILNEHMER_TAKT_MS, 2000);
});

test('Verdrahtung: Seite, Module aus KICAD, keine Logik und kein innerHTML mit Netzdaten', () => {
  const html = lies('../index.html');
  for (const css of ['css/hlks.css', 'css/oberflaeche.css', 'css/quest.css', 'labor/labor.css']) assert.ok(html.includes(`href="${css}"`), css);
  assert.ok(html.includes('src="js/oberflaeche/design-start.js"'));
  assert.ok(html.includes('<div id="dialogschicht"></div>'), 'Rückfragen (Widerrufen) brauchen die Dialogschicht');
  assert.ok(html.includes('Labor folgt mit L1'));
  for (const id of ['labor-quest-app', 'labor-teilnehmer', 'labor-stand']) assert.ok(html.includes(`id="${id}"`), id);
  const js = lies('./labor.js');
  assert.match(js, /questAppTeil\(\{ quest: \(\) => questAusApp\(app\), beiGelesen: \(a\) => \{ app = a; \}, marke: LABOR_MARKE \}\)/);
  assert.doesNotMatch(js, /innerHTML/);
  assert.doesNotMatch(js, /senden\(|benutzerSetzen|kopplung\/paket/, 'die Seite selbst ändert nichts; Aktionen nur im Teil «Quest App»');
  // jeder Import aus KICAD zeigt auf eine Datei, die es in src/Hlks.Web/wwwroot gibt (dort holt der Server sie)
  const wurzel = new URL('../../../Hlks.Web/wwwroot/', import.meta.url);
  const importe = [...js.matchAll(/from '\.\.\/(js\/[^']+)'/g)].map((m) => m[1]);
  assert.ok(importe.includes('js/oberflaeche/quest-app-dialog.js'));
  for (const i of importe) assert.ok(existsSync(fileURLToPath(new URL(i, wurzel))), i);
  for (const css of ['css/hlks.css', 'css/oberflaeche.css', 'css/fensterrahmen.css', 'css/quest.css', 'js/oberflaeche/design-start.js'])
    assert.ok(existsSync(fileURLToPath(new URL(css, wurzel))), css);
});
