// Tests des Dialogs «Quest verbinden» (Modul XR) ohne Browser: Ampel aus dem Zustand, Schalter der Knöpfe, Einrichtung bei fehlendem adb, Hinweise, Verdrahtung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest.test.mjs
// Die Zustände haben die Form der Antwort von GET /api/quest (Server: QuestDienst.cs); Seriennummern und Adressen sind fiktiv.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as T from './quest-text.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');

const kein = { adbGefunden: false, adbHinweis: 'adb.exe wurde nicht gefunden.', suchorte: ['C:\\Users\\x\\platform-tools\\adb.exe'], geraete: [], questVerbunden: false, freigegeben: false, nichtFreigegeben: false, weiterleitung: false, wlan: false, port: 5130, url: 'http://localhost:5130' };
const ohneQuest = { adbGefunden: true, adbPfad: 'C:\\platform-tools\\adb.exe', adbQuelle: 'ort', geraete: [], questVerbunden: false, freigegeben: false, nichtFreigegeben: false, weiterleitung: false, wlan: false, port: 5130, url: 'http://localhost:5130' };
const nichtFrei = { ...ohneQuest, geraete: [{ seriennummer: '1WMHH000000000', zustand: 'unauthorized', wlan: false }], questVerbunden: true, nichtFreigegeben: true };
const frei = { ...ohneQuest, geraete: [{ seriennummer: '1WMHH000000000', zustand: 'device', wlan: false }], questVerbunden: true, freigegeben: true, seriennummer: '1WMHH000000000', ip: '192.168.1.50' };
const fertig = { ...frei, weiterleitung: true };
const stufen = (z) => T.ampel(z).map((p) => p.stufe);

test('Ampel: vier Punkte in fester Reihenfolge', () => {
  assert.deepEqual(T.ampel(fertig).map((p) => p.id), ['adb', 'quest', 'frei', 'weiter']);
  assert.deepEqual(T.ampel(fertig).map((p) => p.titel), ['adb gefunden', 'Quest verbunden', 'Freigegeben', 'Weiterleitung aktiv']);
});

test('Ampel: noch kein Zustand = alles offen', () => {
  assert.deepEqual(stufen(null), ['offen', 'offen', 'offen', 'offen']);
});

test('Ampel: adb fehlt = rot, der Rest grau mit Hinweis', () => {
  assert.deepEqual(stufen(kein), ['schlecht', 'offen', 'offen', 'offen']);
  assert.match(T.ampel(kein)[0].text, /nicht gefunden/);
  assert.match(T.ampel(kein)[1].text, /Zuerst adb einrichten/);
});

test('Ampel: adb da, keine Quest = Quest rot', () => {
  assert.deepEqual(stufen(ohneQuest), ['ok', 'schlecht', 'offen', 'offen']);
  assert.match(T.ampel(ohneQuest)[0].text, /platform-tools\\adb\.exe \(bekannter Ort\)/);
  assert.match(T.ampel(ohneQuest)[1].text, /Datenkabel/);
});

test('Ampel: Quest unauthorized = verbunden grün, freigegeben rot mit Anweisung', () => {
  assert.deepEqual(stufen(nichtFrei), ['ok', 'ok', 'schlecht', 'offen']);
  assert.match(T.ampel(nichtFrei)[2].text, /USB-Debugging erlauben/);
  assert.match(T.ampel(nichtFrei)[1].text, /nicht freigegeben/);
});

test('Ampel: freigegeben, Weiterleitung fehlt = rot mit Port; mit Weiterleitung alles grün', () => {
  assert.deepEqual(stufen(frei), ['ok', 'ok', 'ok', 'schlecht']);
  assert.match(T.ampel(frei)[3].text, /Verbinden/);
  assert.match(T.ampel(frei)[3].text, /5130/);
  assert.deepEqual(stufen(fertig), ['ok', 'ok', 'ok', 'ok']);
  assert.match(T.ampel(fertig)[3].text, /http:\/\/localhost:5130/);
});

test('Ampel: Fehler von adb (z. B. Zeitgrenze) macht den Quest-Punkt rot und zeigt den Fehler', () => {
  const z = { ...ohneQuest, fehler: 'adb hat bei «devices» nicht innerhalb von 10 s geantwortet.' };
  assert.deepEqual(stufen(z), ['ok', 'schlecht', 'offen', 'offen']);
  assert.match(T.ampel(z)[1].text, /nicht innerhalb/);
});

test('Geräteliste in einfachen Worten', () => {
  assert.equal(T.geraeteText([]), 'Keine');
  assert.equal(T.geraeteText([{ seriennummer: 'A1', zustand: 'device', wlan: false }, { seriennummer: '10.0.0.7:5555', zustand: 'offline', wlan: true }]), 'A1 (USB), 10.0.0.7:5555 (WLAN, offline)');
});

test('Knöpfe: Schalter aus dem Zustand', () => {
  const k = (z, l = false, ip = '') => T.knoepfe(z, l, ip);
  assert.deepEqual(k(null), { verbinden: false, wlan: false, ipVerbinden: false, trennen: false, neuPruefen: true, adbSpeichern: true });
  assert.deepEqual(k(kein, false, '192.168.1.50'), { verbinden: false, wlan: false, ipVerbinden: false, trennen: false, neuPruefen: true, adbSpeichern: true });   // ohne adb nichts ausser prüfen/Pfad
  assert.equal(k(ohneQuest, false, '192.168.1.50').ipVerbinden, true);   // die WLAN-Adresse geht auch ohne Kabel
  assert.equal(k(ohneQuest).verbinden, false);
  assert.equal(k(nichtFrei).verbinden, false);
  assert.equal(k(frei).verbinden, true);
  assert.equal(k(frei).wlan, true);
  assert.equal(k(frei).trennen, false);
  assert.equal(k(fertig).trennen, true);
  assert.equal(k({ ...fertig, wlan: true }).wlan, false);   // schon über WLAN
  const l = k(fertig, true, '192.168.1.50');   // während eines Aufrufs ist alles gesperrt
  assert.deepEqual([l.verbinden, l.wlan, l.ipVerbinden, l.trennen, l.neuPruefen], [false, false, false, false, false]);
});

test('IP-Vorprüfung entspricht den Regeln des Servers', () => {
  for (const ok of ['192.168.1.50', ' 10.0.0.7 ', '192.168.1.50:5555', '172.16.0.1:1', '223.1.1.1']) assert.equal(T.ipFormatOk(ok), true, ok);
  for (const schlecht of ['', 'localhost', '1.2.3', '999.1.1.1', '0.0.0.0', '127.0.0.1', '224.0.0.1', '192.168.1.5:0', '192.168.1.5:65536', '192.168.1.5; calc', '-s evil', '1.2.3.4.5', null, undefined])
    assert.equal(T.ipFormatOk(schlecht), false, String(schlecht));
});

test('Einrichtung: nur sichtbar, wenn adb fehlt; drei Schritte mit offizieller Seite, Entpackordner und «Erneut prüfen»', () => {
  assert.equal(T.einrichtungSichtbar(kein), true);
  assert.equal(T.einrichtungSichtbar(ohneQuest), false);
  assert.equal(T.einrichtungSichtbar(null), false);
  assert.equal(T.EINRICHTUNG.length, 3);
  assert.deepEqual(T.EINRICHTUNG.map((s) => s.nr), [1, 2, 3]);
  assert.equal(T.PLATFORM_TOOLS_URL, 'https://developer.android.com/tools/releases/platform-tools');
  assert.equal(T.ENTPACK_ORDNER, '%USERPROFILE%\\platform-tools');
  assert.match(T.EINRICHTUNG[1].text, /%USERPROFILE%\\platform-tools/);
  assert.match(T.EINRICHTUNG[1].text, /adb\.exe/);
  assert.match(T.EINRICHTUNG[2].text, /Erneut prüfen/);
});

test('Hinweise in einfachen Worten: Entwicklermodus, Kabel, Freigabe in der Brille, Treiber (nicht von KICAD), Adresse in der Quest', () => {
  const alle = T.HINWEISE.join('\n');
  assert.match(alle, /Entwicklermodus/);
  assert.match(alle, /Meta-Horizon-App/);
  assert.match(alle, /Entwickler-Organisation/);
  assert.match(alle, /Kabel.*Daten/);
  assert.match(alle, /USB-Debugging erlauben/);
  assert.match(alle, /Immer von diesem Computer/i);
  assert.match(alle, /Meta-ADB-Treiber/);
  assert.match(alle, /developers\.meta\.com/);
  assert.match(alle, /Administratorrechte/);
  assert.match(alle, /KICAD installiert den Treiber nicht selbst/);
  assert.match(alle, /http:\/\/localhost:<Port>/);
});

test('Nächster Schritt je Zustand', () => {
  assert.match(T.naechsterSchritt(null), /gelesen/);
  assert.match(T.naechsterSchritt(kein), /adb einrichten/);
  assert.match(T.naechsterSchritt(ohneQuest), /Kabel/);
  assert.match(T.naechsterSchritt(nichtFrei), /USB-Debugging erlauben/);
  assert.match(T.naechsterSchritt(frei), /«Verbinden»/);
  assert.match(T.naechsterSchritt(fertig), /http:\/\/localhost:5130/);
});

test('esc maskiert Schadtext; Fehleranzeige ohne HTML-Einschleusung im Text', () => {
  assert.equal(T.esc('<img src=x onerror="a()">&\''), '&lt;img src=x onerror=&quot;a()&quot;&gt;&amp;&#39;');
  assert.deepEqual(T.fehlerAnzeige({ message: 'M', vorschlag: 'V' }), { text: 'M', vorschlag: 'V' });
  assert.deepEqual(T.fehlerAnzeige(new Error('E')), { text: 'E', vorschlag: null });
});

test('Der Dialog setzt Server-Text nur maskiert in HTML (esc) und öffnet die Seite von Android in einem neuen Tab ohne Zugriff auf KICAD', () => {
  const d = lies('quest-dialog.js');
  assert.match(d, /target="_blank" rel="noopener noreferrer"/);
  assert.match(d, /href="\$\{esc\(T\.PLATFORM_TOOLS_URL\)\}"/);
  // kein Wert aus Zustand, Ampel oder Meldung steht ohne esc() in einer Vorlage
  assert.doesNotMatch(d, /\$\{(z|p|letzteMeldung|antwort)\.[^}]*\}/, 'ungeschützter Serverwert in einer Vorlage');
  assert.match(d, /\$\{esc\(p\.text\)\}/);
  assert.match(d, /\$\{esc\(letzteMeldung\.text\)\}/);
  assert.match(d, /\$\{esc\(T\.urlText\(z\)\)\}/);
  assert.doesNotMatch(d, /fetch\(/, 'der Dialog geht nur über api.js');
});

test('Verdrahtung: Aktion im Register Ansicht, Symbol, Stilblatt, Link in der 3D-Ansicht, graue Taste öffnet den Dialog', () => {
  assert.match(lies('aktionen-quest.js'), /id: 'ansicht\.questVerbinden'.*register: 'ansicht'.*gruppe: 'Fenster'/s);
  assert.match(lies('aktionen.js'), /import '\.\/aktionen-quest\.js'/);
  assert.match(lies('symbole.js'), /questVerbinden: '/);
  assert.match(lies('../../index.html'), /css\/quest\.css/);
  const vr = lies('../ansichten/vr3d.js');
  assert.match(vr, /import\('\.\.\/oberflaeche\/quest-dialog\.js'\)/);
  assert.match(vr, /Quest im Browser der Brille nutzen …/);
  assert.match(vr, /else questDialogOeffnen\(\)/);
  assert.match(lies('../../css/ansichten.css'), /\.d3-vr\[data-vr-status="aus"\] \.d3-vr-quest \{ display: inline; \}/);
});

test('OBERFLAECHE.md beschreibt den Dialog «Quest verbinden»', () => {
  const md = lies('../../../OBERFLAECHE.md');
  assert.match(md, /Quest verbinden/);
  assert.match(md, /GET \/api\/quest/);
  assert.match(md, /quest\.adb/);
  assert.match(md, /quest-dialog\.js/);
});
