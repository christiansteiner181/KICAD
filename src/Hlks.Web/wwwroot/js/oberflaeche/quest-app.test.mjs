// Tests des Teils «Quest App» im Dialog «Quest verbinden» (Modul XR, Quest App P7) ohne Browser: Anzeige von Netzzugang, Fassungen, Schalter der Knöpfe, Kopplungen, Anfragen
// (mit Warnung bei mehr als einer), Schritte der Installation, HTML-Sicherheit, Verdrahtung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/quest-app.test.mjs
// Die Zustände haben die Form der Antworten von GET /api/xr/zugang, /api/quest/app, /api/quest/app/netz (Server: XrZugang.cs, QuestApp.cs, QuestAppNetz.cs); Namen sind fiktiv.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as T from './quest-app-text.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');

const aus = { an: false, aktiv: false, port: 5140, suchPort: 5141, ips: ['192.168.1.20'], kopplungen: [], anfragen: [], teilnehmer: 0 };
const an = { ...aus, an: true, aktiv: true, fingerabdruckKurz: 'AB12 CD34 EF56 7890' };
const ablageOk = { ordner: 'C:\\x\\quest_app', quelle: 'vorgabe', vorhanden: true, version: { datei: 'a.apk', commit: 'abc1234', zeit: '2026-10-23T21:00:00', groesse: 7891448, sha256: 'a'.repeat(64), versionName: '0.1', versionCode: 1 }, apkPfad: 'C:\\x\\a.apk' };
const app = { paket: 'ch.hkplanung.xr', app: 'H+K XR', werkzeug: 'kicad', werkzeugName: 'KICAD', ablage: ablageOk, adbGefunden: true, brille: { seriennummer: '1WMHH000000000', wlan: false, modell: 'Quest 3', installiert: { installiert: false } }, vergleich: 'nicht_installiert' };
const quest = { adbGefunden: true, freigegeben: true, nichtFreigegeben: false };
const frei = (extra = {}) => T.installierenKnopf({ q: quest, app, zugang: an, weg: 'wlan', ...extra });

test('Netzzugang: aus, eingeschaltet aber nicht aktiv, aktiv', () => {
  assert.equal(T.zugangAnzeige(aus).stufe, 'offen');
  assert.match(T.zugangAnzeige(aus).titel, /aus/);
  assert.equal(T.zugangAnzeige({ ...aus, an: true, fehler: 'Port 5140 ist belegt.' }).stufe, 'schlecht');
  assert.match(T.zugangAnzeige({ ...aus, an: true, fehler: 'Port 5140 ist belegt.' }).text, /belegt/);
  const a = T.zugangAnzeige(an);
  assert.equal(a.stufe, 'ok');
  assert.match(a.text, /Port 5140/);
  assert.match(a.text, /AB12 CD34 EF56 7890/);
  assert.match(a.text, /192\.168\.1\.20/);
  assert.match(T.zugangAnzeige({ ...an, ips: [] }).text, /keine Adresse/);
  assert.equal(T.zugangAnzeige(null).stufe, 'offen');
});

test('Schalter: Beschriftung und gesendeter Wert sind das Gegenteil des Zustands', () => {
  assert.equal(T.schalterText(aus), 'Netzzugang einschalten');
  assert.equal(T.schalterText(an), 'Netzzugang ausschalten');
  assert.equal(T.schalterWert(aus), true);
  assert.equal(T.schalterWert(an), false);
  assert.equal(T.schalterWert(null), true);
});

test('Hinweise zu Netzprofil und Firewall: Stufen, unbekannte werden zu «info»', () => {
  const h = T.hinweisListe({ hinweise: [{ stufe: 'warnung', text: 'Netz «s-net 3» ist Öffentlich' }, { stufe: 'ok', text: 'in Ordnung' }, { stufe: '"><x', text: 'komisch' }, { text: 'ohne Stufe' }, { stufe: 'ok' }, null] });
  assert.deepEqual(h.map((x) => x.stufe), ['warnung', 'ok', 'info', 'info']);
  assert.deepEqual(T.hinweisListe(null), []);
  assert.equal(T.hinweisStufe('x" onmouseover="'), 'info');
  assert.match(T.FIREWALL_HINWEIS, /xr_firewall\.ps1/);
  assert.match(T.FIREWALL_HINWEIS, /Administrator/);
  assert.match(T.FIREWALL_HINWEIS, /nur/);
});

test('Fassungen: abgelegt, installiert, Vergleich', () => {
  assert.equal(T.abgelegtText(app), 'Fassung 0.1 (1) · Commit abc1234 · gebaut 23.10.2026 21:00 · 7.5 MB');
  assert.match(T.abgelegtText({ ablage: { vorhanden: false, fehler: 'In C:\\x liegt keine App (version.json fehlt).' } }), /keine App/);
  assert.equal(T.abgelegtText({ ablage: { vorhanden: true, version: null } }), 'Keine App abgelegt.');
  assert.equal(T.abgelegtText(null), 'Wird gelesen …');
  assert.equal(T.installiertText(app), 'Nicht installiert.');
  const inst = { ...app, brille: { ...app.brille, installiert: { installiert: true, versionName: '0.1', versionCode: 1, aktualisiert: '2026-10-23 21:15:42' } } };
  assert.equal(T.installiertText(inst), 'Fassung 0.1 (1) · aktualisiert 2026-10-23 21:15:42');
  assert.equal(T.installiertText({ ...app, brille: null }), 'Keine Brille verbunden.');
  assert.equal(T.installiertText({ adbGefunden: false }), 'adb fehlt (siehe oben).');
  assert.match(T.installiertText({ ...app, brille: { ...app.brille, installiert: null } }), /nicht lesen/);
  assert.equal(T.brilleText(app.brille), 'Quest 3 (USB)');
  assert.equal(T.brilleText({ wlan: true }), 'Quest (WLAN)');
  assert.equal(T.brilleText(null), 'Keine Brille verbunden');
  assert.match(T.vergleichText('nicht_installiert'), /noch nicht in der Brille/);
  assert.match(T.vergleichText('andere_nummer'), /aktualisieren/);
  assert.match(T.vergleichText('gleiche_nummer'), /Commit/);
  assert.equal(T.vergleichText('unbekannt'), '');
});

test('Zeit und Grösse in einfachen Worten', () => {
  assert.equal(T.zeitText(new Date(2026, 9, 23, 7, 5).toISOString()), '23.10.2026 07:05');
  assert.equal(T.zeitText('kaputt'), 'unbekannt');
  assert.equal(T.zeitText(null), 'unbekannt');
  assert.equal(T.groesseText(7891448), '7.5 MB');
  assert.equal(T.groesseText(0), 'unbekannt');
});

test('Weg: am Kabel «über Kabel», sonst «über WLAN»', () => {
  assert.deepEqual(T.WEGE.map((w) => w.id), ['wlan', 'kabel']);
  assert.equal(T.wegVorgabe({ wlan: false }), 'kabel');
  assert.equal(T.wegVorgabe({ wlan: true }), 'wlan');
  assert.equal(T.wegVorgabe(null), 'wlan');
});

test('«App installieren» ist frei, wenn alles da ist – und sagt sonst warum nicht', () => {
  assert.deepEqual(frei(), { frei: true, grund: '' });
  assert.deepEqual(frei({ weg: 'kabel' }), { frei: true, grund: '' });
  assert.match(frei({ zugang: aus }).grund, /Netzzugang einschalten/);
  assert.equal(frei({ zugang: aus }).frei, false);
  assert.equal(frei({ zugang: null }).frei, false);
  assert.match(frei({ app: { ...app, ablage: { ...ablageOk, fehler: 'Prüfsumme passt nicht', apkPfad: null } } }).grund, /Prüfsumme/);
  assert.match(frei({ app: { ...app, ablage: { vorhanden: false, version: null, fehler: null } } }).grund, /quest_app_bereitstellen/);
  assert.match(frei({ app: null }).grund, /quest_app_bereitstellen/);
  assert.match(frei({ q: { ...quest, adbGefunden: false } }).grund, /adb fehlt/);
  assert.match(frei({ q: { ...quest, freigegeben: false } }).grund, /Keine freigegebene Brille/);
  assert.match(frei({ q: { ...quest, freigegeben: false, nichtFreigegeben: true } }).grund, /USB-Debugging erlauben/);
  assert.equal(frei({ laeuft: true }).frei, false);
  assert.equal(frei({ weg: 'usb' }).frei, false);
  assert.equal(frei({ weg: undefined }).frei, true, 'ohne Angabe gilt WLAN');
});

test('Beschriftung des Knopfs: installieren oder aktualisieren', () => {
  assert.equal(T.installierenTitel(app), 'App installieren');
  assert.equal(T.installierenTitel({ ...app, brille: { ...app.brille, installiert: { installiert: true } } }), 'App aktualisieren');
  assert.equal(T.installierenTitel(null), 'App installieren');
});

test('Schritte und Meldung der Installation', () => {
  const ok = { ok: true, schritte: [{ id: 'zugang', text: 'Netzzugang aktiv', ok: true }, { id: 'starten', text: 'App gestartet.', ok: true }], meldung: 'Quest 3 ist installiert und gekoppelt, die App startet.' };
  assert.deepEqual(T.schritteListe(ok).map((s) => s.id), ['zugang', 'starten']);
  assert.deepEqual(T.ergebnisMeldung(ok), { art: 'ok', text: ok.meldung, vorschlag: null });
  const teil = { ...ok, schritte: [...ok.schritte.slice(0, 1), { id: 'starten', text: 'die Startaktivität liess sich nicht bestimmen', ok: false }], meldung: 'installiert, Start gescheitert' };
  assert.equal(T.ergebnisMeldung(teil).art, 'warnung');
  const fehl = { ok: false, schritte: [{ id: 'installieren', text: 'x', ok: false }], fehler: 'Die Installation ist fehlgeschlagen.', vorschlag: 'Brille aufsetzen' };
  assert.deepEqual(T.ergebnisMeldung(fehl), { art: 'fehler', text: 'Die Installation ist fehlgeschlagen.', vorschlag: 'Brille aufsetzen' });
  assert.equal(T.ergebnisMeldung({ ok: false }).art, 'fehler');
  assert.equal(T.ergebnisMeldung(null), null);
  assert.deepEqual(T.schritteListe({ schritte: [null, { text: 5 }, { id: 'a', text: 'ok' }] }), [{ id: 'a', text: 'ok', ok: false }]);
  assert.deepEqual(T.schritteListe(null), []);
});

test('Alte Apps entfernen (H+K XR E1): Knopf nur sichtbar, wenn die Brille noch eine alte App hat; Rückfrage nennt die Apps', () => {
  const alt = (liste) => ({ ...app, brille: { ...app.brille, alteApps: liste } });
  const kicad = { paket: 'ch.hkplanung.kicad.quest', name: 'KICAD' };
  const labor = { paket: 'ch.hkplanung.xrlabor', name: 'XR-Labor' };
  assert.deepEqual(T.alteApps(alt([kicad, labor])), [kicad, labor]);
  assert.deepEqual(T.alteApps(app), [], 'ohne Angabe des Servers keine alten Apps');
  assert.deepEqual(T.alteApps(null), []);
  assert.deepEqual(T.alteApps({ brille: null }), []);
  assert.deepEqual(T.alteApps(alt([null, { paket: 5, name: 'x' }, { paket: 'a.b' }, kicad])), [kicad], 'nur vollständige Einträge');
  // sichtbar nur mit mindestens einer alten App, frei nur, wenn nichts läuft
  assert.deepEqual(T.alteAppsKnopf({ app }), { sichtbar: false, frei: false, liste: [] });
  assert.deepEqual(T.alteAppsKnopf({ app: alt([]) }), { sichtbar: false, frei: false, liste: [] });
  assert.equal(T.alteAppsKnopf({ app: alt([kicad]) }).sichtbar, true);
  assert.equal(T.alteAppsKnopf({ app: alt([kicad]) }).frei, true);
  assert.deepEqual(T.alteAppsKnopf({ app: alt([kicad]), laeuft: true }), { sichtbar: true, frei: false, liste: [kicad] });
  // Rückfrage und Hinweis nennen genau die installierten alten Apps und dass «H+K XR» bleibt
  assert.match(T.alteAppsFrage([kicad, labor]), /«KICAD» und «XR-Labor»/);
  assert.match(T.alteAppsFrage([labor]), /«XR-Labor» von der Brille entfernen\? «H\+K XR» ersetzt sie und bleibt/);
  assert.doesNotMatch(T.alteAppsFrage([labor]), /KICAD/);
  assert.match(T.alteAppsHinweis([kicad]), /ist noch die alte App «KICAD» installiert/);
  assert.match(T.alteAppsHinweis([kicad, labor]), /sind noch die alten Apps «KICAD» und «XR-Labor» installiert/);
  // das Ergebnis des Entfernens hat dieselbe Form wie das der Installation; ohne Text gilt der eigene Standard
  assert.deepEqual(T.ergebnisMeldung({ ok: true, schritte: [], meldung: 'Entfernt: «KICAD».' }), { art: 'ok', text: 'Entfernt: «KICAD».', vorschlag: null });
  assert.equal(T.ergebnisMeldung({ ok: false }, 'Die alten Apps liessen sich nicht entfernen.').text, 'Die alten Apps liessen sich nicht entfernen.');
  assert.equal(T.ergebnisMeldung({ ok: false }).text, 'Die Installation ist fehlgeschlagen.');
});

test('Alte Apps entfernen: der Dialog fragt vor dem Aufruf und schickt «bestaetigt» nur danach', () => {
  const d = lies('./quest-app-dialog.js');
  assert.match(d, /<div class="qa-block qa-alte" hidden>/, 'ohne alte Apps ist der Block versteckt');
  assert.match(d, /Alte Apps entfernen …/);
  assert.match(d, /q\('\.qa-alte'\)\.hidden = !alt\.sichtbar/);
  // der Aufruf steht in der Antwort der Rückfrage (then → if (!ok) return), nie davor
  const klick = d.slice(d.indexOf("t.matches('.qa-alte-entfernen')"), d.indexOf("t.matches('.qa-widerrufen')"));
  assert.match(klick, /bestaetigen\(T\.alteAppsFrage\(liste\)/);
  assert.ok(klick.indexOf('bestaetigen(') < klick.indexOf("senden('/api/quest/app/alte-entfernen', { bestaetigt: true })"));
  assert.match(klick, /if \(!ok\) return;/);
  assert.equal([...d.matchAll(/\/api\/quest\/app\/alte-entfernen'/g)].length, 1, 'genau ein Aufruf des Endpunkts');
  // Server: nur mit Bestätigung (QuestAppEndpunkte.cs) und nur die zwei Pakete (QuestApp.cs)
  const ep = lies('../../../QuestAppEndpunkte.cs');
  assert.match(ep, /MapPost\("\/alte-entfernen"/);
  assert.match(ep, /if \(!Param\.Bool\(p, "bestaetigt"\)\)/);
});

// ---------------------------------------------------------------- Netz einrichten (Christian 26.10.2026)

const netzBedarf = { gelesen: true, einrichten: { noetig: true, netze: ['s-net 3'], regelnFehlen: true } };
const netzFertig = { gelesen: true, einrichten: { noetig: false, netze: [], regelnFehlen: false } };

test('Netz einrichten: Knopf gross bei Hinweisen, sonst klein «Erneut einrichten …», unsichtbar ohne gelesenen Netzstand', () => {
  const g = T.einrichtenKnopf({ netz: netzBedarf });
  assert.deepEqual(g, { sichtbar: true, hervorgehoben: true, titel: 'Netz einrichten …', frei: true });
  const k = T.einrichtenKnopf({ netz: netzFertig });
  assert.deepEqual(k, { sichtbar: true, hervorgehoben: false, titel: 'Erneut einrichten …', frei: true });
  assert.equal(T.einrichtenKnopf({ netz: { gelesen: false } }).sichtbar, false);
  assert.equal(T.einrichtenKnopf({ netz: null }).sichtbar, false);
  assert.equal(T.einrichtenKnopf({ netz: netzBedarf, laeuft: true }).frei, false);
  assert.equal(T.einrichtenKnopf({ netz: netzBedarf, wartet: true }).frei, false, 'solange auf das Ergebnis gewartet wird, nicht ein zweites Mal');
});

test('Netz einrichten: die Rückfrage nennt Netz, Regeln, Ports und die Rückfrage von Windows', () => {
  const f = T.einrichtenFrage(netzBedarf);
  assert.match(f, /«s-net 3»/);
  assert.match(f, /«Privat»/);
  assert.match(f, /«KICAD XR»/);
  for (const port of ['5140', '5141', '5340', '5341']) assert.match(f, new RegExp(port), port);
  assert.match(f, /Administratorrechten/);
  assert.match(T.einrichtenFrage(netzFertig), /Netzprofil bleibt/);
  assert.match(T.einrichtenFrage({ gelesen: true, einrichten: { noetig: true, netze: ['A', 'B'], regelnFehlen: false } }), /«A» und «B»/);
});

test('Netz einrichten: Meldungen zum Start, zum Abbruch der Rückfrage und zum Ende', () => {
  assert.equal(T.einrichtenMeldung({ gestartet: true }).art, 'ok');
  const n = T.einrichtenMeldung({ gestartet: false, grund: 'Administratorrechte nicht erteilt. Es wurde nichts geändert.' });
  assert.deepEqual(n, { art: 'fehler', text: 'Administratorrechte nicht erteilt. Es wurde nichts geändert.' });
  assert.equal(T.einrichtenMeldung({ gestartet: false }).art, 'fehler');
  assert.equal(T.einrichtenMeldung(null).art, 'fehler');
  assert.equal(T.einrichtenEnde('fertig').art, 'ok');
  assert.match(T.einrichtenEnde('fertig').text, /Netz eingerichtet/);
  assert.equal(T.einrichtenEnde('zeit').art, 'warnung');
});

test('Netz einrichten: Warten bis die Hinweise weg sind, höchstens 2 Minuten, alle 3 s', () => {
  assert.equal(T.EINRICHTEN_TAKT_MS, 3000);
  assert.equal(T.EINRICHTEN_GRENZE_MS, 120000);
  assert.equal(T.einrichtenStand({ netz: netzBedarf, vergangenMs: 3000 }), 'wartet');
  assert.equal(T.einrichtenStand({ netz: netzFertig, vergangenMs: 3000 }), 'fertig');
  assert.equal(T.einrichtenStand({ netz: netzBedarf, vergangenMs: 120000 }), 'zeit');
  assert.equal(T.einrichtenStand({ netz: netzBedarf, vergangenMs: 119999 }), 'wartet');
  assert.equal(T.einrichtenStand({ netz: { gelesen: false }, vergangenMs: 5000 }), 'wartet', 'ein nicht gelesener Netzstand ist nie «fertig»');
  assert.equal(T.einrichtenStand({ netz: null, vergangenMs: 200000 }), 'zeit');
});

test('Netz einrichten: der Dialog fragt vor dem Aufruf, schickt «bestaetigt» nur danach und liest den Netzstand neu', () => {
  const d = lies('./quest-app-dialog.js');
  assert.match(d, /<div class="qa-einrichten-zeile" hidden>/, 'ohne gelesenen Netzstand ist der Knopf versteckt');
  assert.match(d, /Netz einrichten …/);
  const klick = d.slice(d.indexOf("t.matches('.qa-einrichten')"), d.indexOf("t.matches('.qa-installieren')"));
  assert.match(klick, /bestaetigen\(T\.einrichtenFrage\(netz\)/);
  assert.ok(klick.indexOf('bestaetigen(') < klick.indexOf("senden('/api/quest/app/netz/einrichten', { bestaetigt: true })"));
  assert.match(klick, /if \(!ok\) return;/);
  assert.equal([...d.matchAll(/\/api\/quest\/app\/netz\/einrichten'/g)].length, 1, 'genau ein Aufruf des Endpunkts');
  assert.match(d, /lies\('\/api\/quest\/app\/netz\?neu=1'\)/);
  assert.match(d, /T\.EINRICHTEN_TAKT_MS/);
  // gilt für KICAD und das XR-Labor: gleiche Oberfläche, keine eigene Fassung im Labor
  assert.doesNotMatch(lies('../../../../Hlks.Labor/wwwroot/labor/labor.js'), /netz\/einrichten/);
  // Server: nur mit Bestätigung, nur das feste Skript aus dem Programmordner (QuestAppEndpunkte.cs, QuestAppEinrichten.cs)
  const ep = lies('../../../QuestAppEndpunkte.cs');
  assert.match(ep, /MapPost\("\/netz\/einrichten"/);
  assert.match(ep, /if \(!Param\.Bool\(p, "bestaetigt"\)\)/);
});

test('Gekoppelte Brillen: Name, seit, verbunden bzw. zuletzt', () => {
  const z = { kopplungen: [
    { id: 'a1b2c3d4e5f60718', brille: '1WMHH000000000', name: 'Quest Christian', weg: 'paket', angelegt: new Date(2026, 9, 23, 21, 15).toISOString(), zuletzt: new Date(2026, 9, 23, 21, 20).toISOString(), verbunden: true },
    { id: '0011223344556677', brille: 'QUEST-B', name: 'Quest Beat', weg: 'code', angelegt: new Date(2026, 9, 22, 8, 0).toISOString(), verbunden: false },
    { id: 5 }, null,
  ] };
  const l = T.kopplungsListe(z);
  assert.equal(l.length, 2);
  assert.equal(T.kopplungText(l[0]), 'Quest Christian · seit 23.10.2026 21:15 · verbunden');
  assert.equal(T.kopplungText(l[1]), 'Quest Beat · seit 22.10.2026 08:00 · noch nie verbunden');
  assert.equal(l[0].weg, 'Paket');
  assert.equal(l[1].weg, 'Code');
  assert.deepEqual(T.kopplungsListe(null), []);
  assert.match(T.kopplungText({ ...l[1], zuletzt: '23.10.2026 09:00' }), /zuletzt 23\.10\.2026 09:00/);
});

test('Code in Dreiergruppen; ohne Aufdecken kein Code', () => {
  assert.equal(T.codeGruppe('123456'), '123 456');
  assert.equal(T.codeGruppe('000042'), '000 042');
  assert.equal(T.codeGruppe(null), null);
  assert.equal(T.codeGruppe('12345'), null);
  assert.equal(T.codeGruppe('<b>123</b>'), null, 'nur Ziffern');
});

test('Offene Anfragen: nur unerledigte, Rest in Sekunden, Code erst nach dem Aufdecken', () => {
  const jetzt = Date.parse('2026-10-23T20:00:00Z');
  const z = { anfragen: [
    { anfrage: 'A'.repeat(22), brille: 'QUEST-X', name: 'Quest X', ip: '192.168.1.77', code: '123456', bis: '2026-10-23T20:04:30Z', bestaetigt: false, abgelehnt: false, versuche: 0 },
    { anfrage: 'B'.repeat(22), brille: 'QUEST-Y', name: 'Quest Y', ip: '192.168.1.78', code: null, bis: '2026-10-23T20:05:00Z', bestaetigt: false, abgelehnt: false },
    { anfrage: 'C'.repeat(22), brille: 'ALT', name: 'Alt', code: '654321', bis: '2026-10-23T20:05:00Z', bestaetigt: true, abgelehnt: false },
    { anfrage: 'D'.repeat(22), brille: 'ABG', name: 'Abg', code: '654321', bis: '2026-10-23T20:05:00Z', bestaetigt: false, abgelehnt: true },
  ] };
  const l = T.anfragenListe(z, jetzt);
  assert.equal(l.length, 2);
  assert.equal(l[0].code, '123 456');
  assert.equal(l[0].restSekunden, 270);
  assert.equal(l[0].ip, '192.168.1.77');
  assert.equal(l[1].code, null);
  assert.equal(T.anfragenListe({ anfragen: [{ anfrage: 'A', brille: 'x', name: 'n', bis: '2020-01-01T00:00:00Z' }] }, jetzt)[0].restSekunden, 0);
  assert.deepEqual(T.anfragenListe(null), []);
});

test('Bestätigen nur mit Code, ohne laufenden Vorgang und vor Ablauf', () => {
  assert.equal(T.bestaetigenFrei({ code: '123 456', restSekunden: 100 }), true);
  assert.equal(T.bestaetigenFrei({ code: null, restSekunden: 100 }), false);
  assert.equal(T.bestaetigenFrei({ code: '123 456', restSekunden: 0 }), false);
  assert.equal(T.bestaetigenFrei({ code: '123 456', restSekunden: null }), true);
  assert.equal(T.bestaetigenFrei({ code: '123 456', restSekunden: 100 }, true), false);
  assert.equal(T.bestaetigenFrei(null), false);
});

test('Warnung bei mehr als einer offenen Anfrage', () => {
  assert.equal(T.anfragenWarnung([]), null);
  assert.equal(T.anfragenWarnung([{}]), null);
  const w = T.anfragenWarnung([{}, {}]);
  assert.match(w, /2 Kopplungsanfragen offen/);
  assert.match(w, /alle ablehnen/);
  assert.match(T.anfragenWarnung([{}, {}, {}]), /3 Kopplungsanfragen/);
  assert.match(T.ERSATZ_HINWEIS, /nichts eingetippt/);
  assert.match(T.ERSATZ_HINWEIS, /6-stelligen Code/);
});

test('Namen und Kennungen aus dem Netz gehen nur über esc() ins HTML', () => {
  assert.equal(T.esc('<img src=x onerror=alert(1)>'), '&lt;img src=x onerror=alert(1)&gt;');
  assert.equal(T.esc('a"b\'c&d'), 'a&quot;b&#39;c&amp;d');
  const q = lies('./quest-app-dialog.js');
  const stellen = [...q.matchAll(/\$\{([^}]*(?:\{[^}]*\}[^}]*)*)\}/g)].map((m) => m[1]);
  const gefaehrlich = /\b(?:k|a|x|h)\.(?:name|brille|ip|id|anfrage|text|code)\b|meldungText\.(?:text|vorschlag)|\bmeldungText\b/;
  const ungeschuetzt = stellen.filter((s) => gefaehrlich.test(s) && !/esc\(|T\.kopplungText|k\.verbunden|a\.code \?|a\.restSekunden|^x\.id === 'wlan'|^meldungText\.art ===|^CSS\.escape\(/.test(s));   // Ausnahmen: feste Werte (Wege, Art der Meldung) und ein CSS-Selektor
  assert.deepEqual(ungeschuetzt, [], `ungeschützt: ${ungeschuetzt.join(' | ')}`);
  // Eigenschaften, die der Server liefert, stehen nie in einem Attribut ohne esc()
  assert.doesNotMatch(q, /data-[a-z-]+="\$\{(?!esc\(|CSS\.escape\(|a\.restSekunden \?\?|x\.ok \?)/);
});

test('Das Werkzeug quest_app_bereitstellen.ps1: nur ASCII, baut und legt ab, kein adb, kein Download', () => {
  const s = readFileSync(new URL('../../../../../werkzeuge/quest_app_bereitstellen.ps1', import.meta.url), 'utf8');
  assert.doesNotMatch(s, /[^\x00-\x7f]/, 'PowerShell 5.1: Datei bewusst nur ASCII');
  assert.match(s, /dotnet.*publish \$projekt -c Release -f net10\.0-android/);
  assert.match(s, /HLKS_CAD_werkzeuge\\hk_xr/);
  assert.match(s, /ch\.hkplanung\.xr-Signed\.apk/);
  assert.match(s, /Hlks\.Quest\.Android\.csproj/, 'H+K XR E1: eine App für alle Werkzeuge, gebaut aus src/Hlks.Quest');
  const code = s.replace(/^#.*$/gm, '');
  assert.doesNotMatch(code, /Hlks\.Labor\.Quest|labor_app|xrlabor-Signed|quest_app\\|kicad\.quest/, 'die zweite App und die alten Ablagen gibt es nicht mehr');
  assert.equal([...code.matchAll(/\$Labor\b/g)].length, 2, '-Labor bleibt nur als Parameter, der einen Hinweis ausgibt (keine Verzweigung des Baus)');
  assert.match(code, /if \(\$Labor\) \{ Meldung 'Hinweis: -Labor gibt es nicht mehr/);
  for (const feld of ['datei', 'commit', 'zeit', 'groesse', 'sha256', 'versionName', 'versionCode']) assert.match(s, new RegExp(`\\b${feld}\\b`), feld);
  assert.match(s, /Get-FileHash -Algorithm SHA256/);
  assert.match(s, /\[switch\]\$OhneBau/);
  assert.doesNotMatch(s.replace(/^#.*$/gm, ''), /\badb(\.exe)?\b|Invoke-WebRequest|Invoke-RestMethod|DownloadFile|Start-BitsTransfer|curl|wget/i, 'ruft kein adb auf und lädt nichts herunter');
  assert.match(s, /LASTEXITCODE -ne 0/, 'ein fehlgeschlagener Bau bricht ab');
});

test('Verdrahtung: Dialog, Takt, Endpunkte', () => {
  const d = lies('./quest-dialog.js');
  assert.match(d, /<details class="qd-browser" open>/, 'der Browser-Prototyp ist einklappbar');
  assert.ok(d.indexOf('qd-app-platz') < d.indexOf('qd-browser'), 'die Quest App steht oben, der Browser-Prototyp darunter');
  assert.doesNotMatch(d, /qd-zur-app/);
  assert.match(d, /browserOffen\(app, z\)/);
  assert.match(d, /appTeil\.zustandNeu\(\)/, 'nach jedem Vorgang im Dialog liest der Teil adb, Brille und Fassung neu');
  assert.match(d, /import \{ questAppTeil \} from '\.\/quest-app-dialog\.js'/);
  assert.match(d, /qd-app-platz/);
  assert.match(d, /appTeil\.beenden\(\)/);
  const t = lies('./quest-app-dialog.js');
  for (const pfad of ['/api/xr/zugang', '/api/quest/app', '/api/quest/app/netz', '/api/quest/app/installieren', '/api/xr/kopplung/widerrufen', '/api/xr/kopplung/bestaetigen', '/api/xr/kopplung/ablehnen'])
    assert.ok(t.includes(`'${pfad}`) || t.includes(`\`${pfad}`), pfad);
  assert.match(t, /bestaetigen\(`Die Kopplung von/, 'Widerrufen fragt nach');
  assert.match(t, /clearInterval\(timer\)/, 'der Takt endet mit dem Dialog');
  assert.equal(T.ZUGANG_TAKT_MS, 2000);
  assert.equal(T.ABLAGE_SCHLUESSEL, 'quest.app_ablage');
  // keine Fachlogik in der Oberfläche: das Paket mit Schlüssel kommt nie hierher (der Server legt es selbst in die Brille)
  assert.doesNotMatch(t, /kopplung\/paket/);
  assert.doesNotMatch(t, /schluessel\b.*=.*ergebnis/);
});

test('Nach dem Widerruf: «neu koppeln» statt «aktualisieren» (Kennung der Brille gegen die Kopplungen)', () => {
  const inst = { ...app, brille: { ...app.brille, kennung: '1WMHH000000000', installiert: { installiert: true, versionName: '0.1', versionCode: 1 } } };
  const gekoppelt = { ...an, kopplungen: [{ id: 'a1b2c3d4e5f60718', brille: '1WMHH000000000', name: 'Quest 3' }] };
  assert.equal(T.neuKoppeln(inst, gekoppelt), false);
  assert.equal(T.neuKoppeln(inst, { ...an, kopplungen: [] }), true);
  assert.equal(T.neuKoppeln(inst, { ...an, kopplungen: [{ brille: 'ANDERE' }] }), true);
  assert.equal(T.neuKoppeln(inst, { ...an, kopplungen: [{ brille: '1wmhh000000000' }] }), false, 'Gross-/Kleinschreibung egal');
  assert.equal(T.neuKoppeln(app, { ...an, kopplungen: [] }), false, 'nicht installiert: nichts neu zu koppeln');
  assert.equal(T.neuKoppeln({ ...inst, brille: { ...inst.brille, kennung: null } }, an), false, 'ohne Kennung keine Aussage');
  assert.equal(T.neuKoppeln(inst, null), false);
  assert.equal(T.installierenTitel(inst, gekoppelt), 'App aktualisieren');
  assert.equal(T.installierenTitel(inst, { ...an, kopplungen: [] }), 'App neu koppeln');
  assert.equal(T.installierenTitel(app, an), 'App installieren');
  assert.match(T.NEU_KOPPELN_HINWEIS, /neu koppeln/);
});

test('Kabelweg-Satz und Einklappen des Browser-Prototyps', () => {
  assert.match(T.appWegText({ appWeg: 'kabel' }), /nicht auf WLAN-adb um/);
  assert.equal(T.appWegText({ appWeg: 'wlan' }), '');
  assert.equal(T.appWegText(null), '');
  const inst = { ...app, brille: { ...app.brille, installiert: { installiert: true } } };
  assert.equal(T.browserOffen(app, quest), true, 'App nicht installiert: Browser-Prototyp offen');
  assert.equal(T.browserOffen(inst, quest), false, 'App installiert: eingeklappt');
  assert.equal(T.browserOffen(inst, { ...quest, adbGefunden: false }), true, 'fehlt adb, steht die Einrichtung dort: offen');
  assert.equal(T.browserOffen(null, null), true);
  assert.equal(T.appInstalliert(inst), true);
});

test('App-Marke: Vorgabe KICAD mit den bisherigen Texten, eine andere Marke ersetzt Namen, Ports und Werkzeug', () => {
  // XR-Labor L0 (entwurf/labor-app.md): der Teil «Quest App» dient auch dem Labor-Server; ohne Marke bleibt alles wie in KICAD
  const m = T.KICAD_MARKE;
  assert.ok(Object.isFrozen(m));
  assert.equal(m.app, 'KICAD', 'das Werkzeug am PC');
  assert.equal(m.appName, 'H+K XR', 'die App in der Brille (H+K XR E1: eine App für alle Werkzeuge)');
  assert.equal(T.APP_NAME, 'H+K XR');
  assert.equal(m.ablageVorgabe, '%USERPROFILE%\\HLKS_CAD_werkzeuge\\hk_xr');
  assert.match(m.einleitung, /^Die App «H\+K XR» in der Brille/);
  assert.match(m.einleitung, /ersetzt die bisherigen Apps «KICAD» und «XR-Labor»/);
  assert.equal(m.port, 5140);
  assert.equal(m.desktopPort, 5130);
  assert.equal(m.ablageSchluessel, T.ABLAGE_SCHLUESSEL);
  assert.equal(m.brillenZeigen, true);
  assert.equal(T.zugangAnzeige({ ...aus, an: true }).text, 'Er startet gleich (nur im KICAD auf Port 5130).');
  assert.equal(T.zugangAnzeige(aus).text, 'Nur dieser PC erreicht KICAD. Zum Installieren und Verbinden der App einschalten – am besten nur für die Dauer des Tests.');
  assert.equal(T.installierenKnopf({ q: quest, app: { ...app, ablage: { ...ablageOk, version: null } }, zugang: an }).grund, 'Keine App abgelegt: werkzeuge\\quest_app_bereitstellen.ps1 ausführen.');
  assert.equal(T.installierenKnopf({ q: { ...quest, adbGefunden: false }, app, zugang: an }).grund, 'adb fehlt: oben im Dialog einrichten.');
  assert.equal(T.appWegText({ appWeg: 'kabel' }), 'Kabelweg: Die App ist per Kabel gekoppelt. KICAD stellt die Brille nicht auf WLAN-adb um und stellt die Weiterleitung (adb reverse) nach jeder Änderung der adb-Verbindung wieder her.');
  assert.equal(T.FIREWALL_HINWEIS, 'KICAD ändert Netzprofil und Firewall nur auf Knopfdruck («Netz einrichten …»), nach Rückfrage und mit Bestätigung durch Windows (Administratorrechte). Ersatzweise: Doppelklick auf werkzeuge\\xr_netz_einrichten.cmd (stuft das Netz als privat ein und legt die Regeln an); nur die Regeln: PowerShell als Administrator, dann «powershell -ExecutionPolicy Bypass -File werkzeuge\\xr_firewall.ps1» (zeigt mit -Liste Regeln und Netzprofil).');
  const probe = { ...m, app: 'Probe', desktopPort: 5999, bereitstellen: 'probe.ps1', adbEinrichten: 'anderswo einrichten' };
  assert.equal(T.zugangAnzeige({ ...aus, an: true }, probe).text, 'Er startet gleich (nur im Probe auf Port 5999).');
  assert.match(T.zugangAnzeige(aus, probe).text, /^Nur dieser PC erreicht Probe\./);
  assert.equal(T.installierenKnopf({ q: quest, app: { ...app, ablage: { ...ablageOk, version: null } }, zugang: an, marke: probe }).grund, 'Keine App abgelegt: probe.ps1 ausführen.');
  assert.equal(T.installierenKnopf({ q: { ...quest, adbGefunden: false }, app, zugang: an, marke: probe }).grund, 'adb fehlt: anderswo einrichten.');
  assert.match(T.appWegText({ appWeg: 'kabel' }, probe), /Probe stellt die Brille nicht/);
  assert.match(T.firewallHinweis(probe), /^Probe ändert Netzprofil und Firewall nur auf Knopfdruck/);
  assert.equal(T.installiertText({ adbGefunden: false }, { ...probe, adbFehlt: 'adb fehlt anderswo.' }), 'adb fehlt anderswo.');
  assert.match(lies('./quest-app-dialog.js'), /T\.installiertText\(app, appMarke\)/);
  // der Dialog nimmt die Marke als Option und setzt ihre Werte nur über esc() ins HTML; ohne Ablage-Schlüssel und Brillen-Schalter fehlen diese Teile
  const d = lies('./quest-app-dialog.js');
  assert.match(d, /marke = T\.KICAD_MARKE \}\)/);
  for (const feld of ['einleitung', 'port', 'ablageVorgabe', 'bereitstellen']) assert.match(d, new RegExp(`esc\\(appMarke\\.${feld}\\)`), feld);
  assert.match(d, /\$\{appMarke\.brillenZeigen \? `<div class="qa-block qa-anzeige">/);
  assert.match(d, /\$\{appMarke\.ablageSchluessel \? `<div class="qd-zeile qa-ablage">/);
  assert.match(d, /benutzerSetzen\(appMarke\.ablageSchluessel,/);
  assert.match(d, /T\.zugangAnzeige\(zugang, appMarke\)/);
  assert.match(d, /marke: appMarke \}\)/);
  // Befund 26.10.2026 (Prüfbild): hiess die Marke «m», verdeckte das Meldungsfeld «const m» in zeichneInstallieren sie (ReferenceError beim Öffnen).
  // Der Name der Marke kommt im Modul genau einmal als Deklaration vor.
  assert.equal([...d.matchAll(/\b(?:const|let|var)\s+appMarke\b/g)].length, 1);
});

test('Style: der Dialog passt in jedes Fenster', () => {
  const css = lies('../../css/quest.css');
  assert.match(css, /\.dlg\.quest-verbinden \{[^}]*max-height: calc\(100vh - 24px\)/);
  assert.match(css, /\.dlg\.quest-verbinden \{[^}]*width: min\(640px, calc\(100vw - 16px\)\)/);
  assert.match(css, /\.dlg\.quest-verbinden \.dlg-inhalt \{[^}]*overflow: auto/);
});