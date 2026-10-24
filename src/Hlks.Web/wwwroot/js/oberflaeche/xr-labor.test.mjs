// Tests des Dialogs «XR-Labor» (Modul XR, Quest App P6) ohne Browser: Gruppen und Werte der Stellschrauben, Eingaben, Profile, Varianten, Status der Prüfaufgaben, Zahlen der
// Auswertung, Fragebogen (Körper, Hinweis auf fehlenden Wortlaut), HTML-Sicherheit und Verdrahtung. Gerechnet wird gegen die echten Kataloge in kataloge/ (xr_stellschrauben.json,
// xr_pruefaufgaben.json, xr_varianten.json, xr_fragebogen.json); die Prüfung dieser Dateien selbst machen die Web-Tests (XrLaborTests).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/xr-labor.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as T from './xr-labor-text.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');
const katalogDatei = (n) => JSON.parse(readFileSync(new URL(`../../../../../kataloge/${n}`, import.meta.url), 'utf8'));

// der Katalog, wie GET /api/xr/labor/katalog ihn liefert (ohne ETag und Profil)
const sch = katalogDatei('xr_stellschrauben.json');
const katalog = { gruppen: sch.gruppen, stellschrauben: sch.stellschrauben, muster: sch.muster, aufgaben: katalogDatei('xr_pruefaufgaben.json').aufgaben, varianten: katalogDatei('xr_varianten.json').varianten };
const fragebogen = katalogDatei('xr_fragebogen.json');
const eintrag = (k) => katalog.stellschrauben.find((s) => s.schluessel === k);

test('Gruppen: in der Reihenfolge des Katalogs, jede Stellschraube genau einmal', () => {
  const g = T.gruppen(katalog);
  assert.deepEqual(g.map((x) => x.id), ['begehen', 'haltung', 'anzeige', 'apps', 'wisch', 'pinch', 'komfort', 'sitzung', 'hebe', 'wegwischen', 'symbole', 'desktop']);   // V1a: hebe, wegwischen, symbole; V1c: desktop
  assert.equal(g.flatMap((x) => x.eintraege).length, katalog.stellschrauben.length);
  assert.deepEqual(g.find((x) => x.id === 'begehen').eintraege.map((s) => s.schluessel).slice(0, 4),
    ['begehen.ziel_ruhe_s', 'begehen.ziel_ruhe_radius_m', 'begehen.ziel_verzoegerung_s', 'begehen.drehen_sperre_s']);
  assert.deepEqual(T.gruppen(null), []);
  assert.deepEqual(T.gruppen({ gruppen: [{ id: 'x', titel: 'X' }], stellschrauben: [] }), []);   // leere Gruppen entfallen
});

test('«folgt P5»: seit Quest App P5b versteht die App alle Stellschrauben – keine trägt die Marke; die Regel kennzeichnet weiter «folgt_p5»', () => {
  for (const s of katalog.stellschrauben) assert.equal(T.folgtP5(s), false, s.schluessel);
  for (const k of ['anzeige.skala', 'apps.groesse_m', 'apps.abstand_m', 'apps.anordnung', 'wisch.mindestweg_m', 'wisch.mindesttempo_ms', 'wisch.richtung_toleranz_grad', 'pinch.schwelle_an_m',
    'pinch.schwelle_aus_m', 'fang.radius_m', 'komfort.tunnelblick', 'komfort.tunnelblick_staerke', 'sitzung.zoom_min', 'sitzung.zoom_max',
    'roentgen.winkel_grad', 'roentgen.reichweite_m', 'sitzung.schnitt_ruhe_s', 'sitzung.schnitt_toleranz_grad']) assert.ok(eintrag(k), k);
  assert.equal(T.folgtP5({ status: 'folgt_p5' }), true);
  assert.match(T.FOLGT_P5_HINWEIS, /P5/);
});

test('Aktueller Wert: gesetzt oder Vorgabe; «Vorgabe» ist nur nötig, wenn etwas abweicht', () => {
  const s = eintrag('begehen.ziel_ruhe_s');
  assert.equal(T.aktuellerWert(s, {}), 0.25);
  assert.equal(T.aktuellerWert(s, { 'begehen.ziel_ruhe_s': 0.4 }), 0.4);
  assert.equal(T.aktuellerWert(s, { 'begehen.ziel_ruhe_s': null }), 0.25);
  assert.equal(T.aktuellerWert(s, null), 0.25);
  assert.equal(T.istGesetzt(s, { 'begehen.ziel_ruhe_s': 0.25 }), true);
  assert.equal(T.istGesetzt(s, {}), false);
  assert.equal(T.istVorgabe(s, 0.25), true);
  assert.equal(T.istVorgabe(s, 0.3), false);
  assert.equal(T.istVorgabe(eintrag('komfort.tunnelblick'), true), true);
  assert.equal(T.istVorgabe(eintrag('apps.anordnung'), 'reihe'), false);
});

test('Werte als Text mit Einheit', () => {
  assert.equal(T.wertText(eintrag('begehen.ziel_ruhe_s'), 0.25), '0.25 s');
  assert.equal(T.wertText(eintrag('begehen.drehwinkel_grad'), 30), '30 °');
  assert.equal(T.wertText(eintrag('anzeige.skala'), 1.35), '1.35 ×');
  assert.equal(T.wertText(eintrag('sitzung.zoom_min'), 20), '1:20');
  assert.equal(T.wertText(eintrag('komfort.tunnelblick_staerke'), 0.3), '0.30');   // ohne Einheit
  assert.equal(T.wertText(eintrag('komfort.tunnelblick'), true), 'ein');
  assert.equal(T.wertText(eintrag('komfort.tunnelblick'), false), 'aus');
  assert.equal(T.wertText(eintrag('apps.anordnung'), 'reihe'), 'Im Raum verankert (wie bogen)');   // V1a: ein Fenster im Raum
  assert.equal(T.wertText(eintrag('apps.anordnung'), 'unbekannt'), 'unbekannt');
  assert.equal(T.wertText(eintrag('begehen.ziel_ruhe_s'), NaN), '–');
  assert.equal(T.nachkommastellen(0.05), 2);
  assert.equal(T.nachkommastellen(1), 0);
  assert.equal(T.nachkommastellen(0.001), 3);
});

test('Eingabe → Wert: Komma, Grenzen, Rundung auf den Schritt, Ja/Nein, Auswahl', () => {
  const s = eintrag('begehen.ziel_ruhe_s');   // 0 … 2, Schritt 0.05
  assert.deepEqual(T.wertAusEingabe(s, '0.4'), { ok: true, wert: 0.4 });
  assert.deepEqual(T.wertAusEingabe(s, '0,4'), { ok: true, wert: 0.4 });
  assert.deepEqual(T.wertAusEingabe(s, 0.333), { ok: true, wert: 0.33 });
  assert.deepEqual(T.wertAusEingabe(s, '2'), { ok: true, wert: 2 });
  assert.deepEqual(T.wertAusEingabe(s, '0'), { ok: true, wert: 0 });
  assert.equal(T.wertAusEingabe(s, '2.01').ok, false);
  assert.match(T.wertAusEingabe(s, '2.01').text, /0\.00 bis 2\.00/);
  assert.equal(T.wertAusEingabe(s, '-0.1').ok, false);
  assert.equal(T.wertAusEingabe(s, '').ok, false);
  assert.equal(T.wertAusEingabe(s, 'abc').ok, false);
  assert.equal(T.wertAusEingabe(s, '1e3').ok, false);
  assert.match(T.wertAusEingabe(s, 'abc').text, /keine Zahl/);
  const ja = eintrag('komfort.tunnelblick');
  assert.deepEqual(T.wertAusEingabe(ja, true), { ok: true, wert: true });
  assert.deepEqual(T.wertAusEingabe(ja, false), { ok: true, wert: false });
  const a = eintrag('apps.anordnung');
  assert.deepEqual(T.wertAusEingabe(a, 'bogen'), { ok: true, wert: 'bogen' });
  assert.equal(T.wertAusEingabe(a, 'kreis').ok, false);
});

test('Gültigkeit eines Werts wie im Server (XrLaborKatalog.WertFehler)', () => {
  const s = eintrag('begehen.ziel_ruhe_s');
  assert.equal(T.wertGueltig(s, 0), true);
  assert.equal(T.wertGueltig(s, 2), true);
  assert.equal(T.wertGueltig(s, 2.5), false);
  assert.equal(T.wertGueltig(s, '1'), false);
  assert.equal(T.wertGueltig(s, NaN), false);
  assert.equal(T.wertGueltig(eintrag('komfort.tunnelblick'), 1), false);
  assert.equal(T.wertGueltig(eintrag('komfort.tunnelblick'), false), true);
  assert.equal(T.wertGueltig(eintrag('apps.anordnung'), 'an_hand'), true);
  assert.equal(T.wertGueltig(eintrag('apps.anordnung'), 'x'), false);
});

test('Jede Vorgabe des Katalogs ist gültig und übersteht Anzeige und Eingabe unverändert', () => {
  for (const s of katalog.stellschrauben) {
    assert.equal(T.wertGueltig(s, s.vorgabe), true, s.schluessel);
    const r = T.wertAusEingabe(s, s.art === 'zahl' ? T.zahlText(s, s.vorgabe) : s.vorgabe);
    assert.deepEqual(r, { ok: true, wert: s.vorgabe }, s.schluessel);
    assert.ok(T.wertText(s, s.vorgabe).length > 0, s.schluessel);
  }
});

test('Verbundene Brillen: keine, eine, mehrere; ohne Antwort «wird gelesen»', () => {
  assert.equal(T.brillenInfo(null).stufe, 'offen');
  assert.match(T.brillenInfo(null).text, /gelesen/);
  const keine = T.brillenInfo({ teilnehmer: [], stellschrauben: {} });
  assert.equal(keine.anzahl, 0);
  assert.match(keine.text, /Keine Brille verbunden/);
  assert.match(keine.text, /gemerkt/);
  const eine = T.brillenInfo({ teilnehmer: [{ id: 't1', name: 'Christian', geraet: 'Quest 3', app: '0.1+abc1234', brillenName: 'Quest von Christian' }] });
  assert.equal(eine.anzahl, 1);
  assert.equal(eine.stufe, 'ok');
  assert.equal(eine.app, '0.1+abc1234');
  assert.match(eine.text, /Quest von Christian \(Quest 3, App 0\.1\+abc1234\)/);
  const zwei = T.brillenInfo({ teilnehmer: [{ name: 'A', geraet: 'Quest 3' }, { name: 'B', geraet: 'Quest 2', app: '0.2' }, null] });
  assert.equal(zwei.anzahl, 2);
  assert.equal(zwei.app, '0.2');
  assert.deepEqual(zwei.namen, ['A', 'B']);
});

test('Profile: Kennung aus dem Namen, Name bereinigt, Satz erzeugen, Liste, Senden', () => {
  assert.equal(T.profilKennung('Sitzung sitzend'), 'sitzung_sitzend');
  assert.equal(T.profilKennung('  Übung 1/2  '), 'uebung_1_2');
  assert.equal(T.profilKennung('Größe'), 'groesse');
  assert.equal(T.profilKennung('Café'), 'cafe');
  assert.equal(T.profilKennung('---'), '');
  assert.equal(T.profilKennung(''), '');
  assert.equal(T.profilKennung('a'.repeat(40)).length, 24);
  assert.match(T.profilKennung('A very long profile name with many words'), /^[a-z0-9][a-z0-9_]{0,23}$/);
  assert.equal(T.profilSchluessel('x_1'), 'labor.profil.x_1');
  assert.equal(T.profilName('  Test\u0007 1 '), 'Test  1');
  assert.equal(T.profilName('x'.repeat(60)).length, 40);

  const live = { 'begehen.ziel_ruhe_s': 0.5, 'labor.variante.bedienung': 'tablet' };
  const p = T.profilErzeugen('Sitzung', katalog, live);
  assert.equal(p.name, 'Sitzung');
  assert.equal(Object.keys(p.werte).length, katalog.stellschrauben.length + katalog.varianten.length);
  assert.equal(p.werte['begehen.ziel_ruhe_s'], 0.5);
  assert.equal(p.werte['begehen.ziel_ruhe_radius_m'], 0.3);   // Vorgabe, weil nicht gesetzt
  assert.equal(p.werte['labor.variante.bedienung'], 'tablet');
  assert.equal(p.werte['labor.variante.teleport'], 'zeigen_pinch');
  assert.ok(JSON.stringify(p).length < 4000, 'passt in die Grenze der Benutzereinstellungen (4000 Zeichen)');

  const liste = T.profileListe({
    'labor.profil.b': { name: 'Beta', werte: { a: 1, b: 2 } }, 'labor.profil.a': { name: 'alpha', werte: {} }, 'labor.profil.kaputt': 'text', 'andere.einstellung': { name: 'x' }, 'labor.profil_aktiv': 'b',
  }, 'b');
  assert.deepEqual(liste.map((x) => [x.kennung, x.name, x.anzahl, x.aktiv]), [['a', 'alpha', 0, false], ['b', 'Beta', 2, true]]);
  assert.deepEqual(T.profileListe(null, null), []);

  const senden = T.profilZumSenden({ werte: { 'begehen.ziel_ruhe_s': 0.4, 'begehen.ziel_ruhe_radius_m': 99, 'gibt.es.nicht': 1, 'komfort.tunnelblick': false, 'labor.variante.teleport': 'stick', 'labor.variante.messen': 'x' } }, katalog);
  assert.deepEqual(senden.senden, [
    { schluessel: 'begehen.ziel_ruhe_s', wert: 0.4 }, { schluessel: 'komfort.tunnelblick', wert: false }, { schluessel: 'labor.variante.teleport', wert: 'stick' },
  ]);
  assert.equal(senden.uebersprungen, 3);
  assert.deepEqual(T.profilZumSenden(null, katalog).senden, []);
  assert.match(T.profilGeladenText('Sitzung', senden), /«Sitzung» ist geladen: 3 Werte gesendet\. 3 Werte kennt der Katalog nicht/);
  assert.match(T.profilGeladenText('S', { senden: [{}], uebersprungen: 0 }), /1 Wert gesendet\.$/);
});

test('Varianten: Wahl aus den gesetzten Werten oder die Vorgabe', () => {
  const v = katalog.varianten.find((x) => x.name === 'bedienung');
  assert.equal(T.varianteWahl(v, {}), 'apps');
  assert.equal(T.varianteWahl(v, { 'labor.variante.bedienung': 'tablet' }), 'tablet');
  assert.equal(T.varianteWahl(v, { 'labor.variante.bedienung': 'unbekannt' }), 'apps');
  assert.deepEqual(T.variantenWahl(katalog, { 'labor.variante.etage': 'app' }), { bedienung: 'apps', teleport: 'zeigen_pinch', etage: 'app', messen: 'messband', umgebung: 'hell', symbole: 'verteilt', symbolwahl: 'stehen', feld_bei_app: 'seitlich' });
  assert.equal(T.varianteTitel(v, 'tablet'), 'Tablet an der Hand');
  assert.equal(T.varianteTitel(v, 'x'), 'x');
});

test('Prüfaufgaben: Knopf und Status', () => {
  const a = katalog.aufgaben.find((x) => x.id === 'drehen-90');
  // V1a: «messen-wand-wand» ist spielbar (p5); die Regel für «folgt_v1» prüft eine gedachte Aufgabe mit diesem Status
  const v1 = { ...katalog.aufgaben.find((x) => x.id === 'messen-wand-wand'), status: 'folgt_v1' };
  const mit = { anzahl: 1 };
  const ohne = { anzahl: 0 };
  assert.deepEqual(T.aufgabeKnopf({ aufgabe: a, brillen: mit }), { frei: true, grund: '' });
  assert.equal(T.aufgabeKnopf({ aufgabe: a, brillen: ohne }).frei, false);
  assert.match(T.aufgabeKnopf({ aufgabe: a, brillen: ohne }).grund, /Keine Brille/);
  assert.equal(T.aufgabeKnopf({ aufgabe: a, brillen: mit, laeuft: true }).frei, false);
  assert.equal(T.aufgabeKnopf({ aufgabe: v1, brillen: mit }).frei, false);
  assert.match(T.aufgabeKnopf({ aufgabe: v1, brillen: mit }).grund, /Version 1/);
  assert.equal(T.aufgabeKnopf({ aufgabe: a, brillen: null }).frei, false);

  assert.deepEqual(T.aufgabeStatus(v1, null), { art: 'folgt', text: 'folgt mit Version 1' });
  assert.deepEqual(T.aufgabeStatus(a, null), { art: 'offen', text: 'Noch nicht gemessen' });
  assert.deepEqual(T.aufgabeStatus(a, { laeufe: 0 }), { art: 'offen', text: 'Noch nicht gemessen' });
  assert.equal(T.aufgabeStatus(a, { laeufe: 2 }, { laeufeVorher: 2 }).art, 'gestartet');   // gestartet, noch keine neue Zeile
  assert.match(T.aufgabeStatus(a, null, { laeufeVorher: 0 }).text, /wartet auf die Messung/);
  const g = T.aufgabeStatus(a, { laeufe: 3, erfolge: 2, mitErfolgsangabe: 3, letzte: '2026-10-23T21:15:00' }, { laeufeVorher: 2 });
  assert.equal(g.art, 'gemessen');
  assert.match(g.text, /^3 Läufe · 2 von 3 erfolgreich · zuletzt 23\.10\.2026 21:15$/);
  assert.equal(T.aufgabeStatus(a, { laeufe: 1, erfolge: 0, mitErfolgsangabe: 0 }).text, '1 Lauf');
});

test('Zahlen der Auswertung', () => {
  assert.equal(T.dauerText(820), '820 ms');
  assert.equal(T.dauerText(4120.5), '4.1 s');
  assert.equal(T.dauerText(65000), '1 min 5 s');
  assert.equal(T.dauerText(null), '–');
  assert.equal(T.dauerText(NaN), '–');
  assert.equal(T.mittelText(0.3333), '0.33');
  assert.equal(T.mittelText(2), '2');
  assert.equal(T.mittelText(null), '–');
  assert.equal(T.mittelText(67.54, 1), '67.5');
  assert.equal(T.quoteText(0.75), '75 %');
  assert.equal(T.quoteText(null), '–');
  assert.deepEqual(T.auswertungZellen({ laeufe: 3, mittelZeitMs: 4000, fehlgriffe: 1, fehlgriffeMittel: 0.33, abbrueche: 0, abbruecheMittel: 0, erfolgsquote: 1 }, 'bedienung=apps'),
    ['bedienung=apps', '3', '4.0 s', '1 (Ø 0.33)', '0 (Ø 0)', '100 %']);
  assert.equal(T.varianteAnzeige(null), 'ohne Angabe');
  assert.equal(T.auswertungLeer(null), true);
  assert.equal(T.auswertungLeer({ leer: true }), true);
  assert.equal(T.auswertungLeer({ laeufe: 0, fragebogen: [] }), true);
  assert.equal(T.auswertungLeer({ laeufe: 1, fragebogen: [] }), false);
  assert.equal(T.auswertungLeer({ laeufe: 0, fragebogen: [{}] }), false);
  assert.match(T.LEERZUSTAND_TEXT, /Noch keine Messprotokolle/);
  const a = { aufgaben: [{ aufgabe: 'a', laeufe: 2 }, { aufgabe: 'b', laeufe: 0 }, { aufgabe: 'c' }] };
  assert.deepEqual(T.aufgabenMitLaeufen(a).map((x) => x.aufgabe), ['a']);
  assert.equal(T.fragebogenKurz({ sus: { punkte: 85 }, tlx: { raw: 35 }, komfort: { uebelkeit: 4, augen: 2, natuerlich: 6 } }), 'SUS 85 · TLX 35 · Übelkeit 4 · Augen 2 · natürlich 6');
  assert.equal(T.fragebogenKurz({ komfort: { uebelkeit: 0 } }), 'Übelkeit 0');
  assert.equal(T.fragebogenKurz({}), '–');
});

test('Fragebogen: Hinweis auf den fehlenden Wortlaut von SUS und NASA-TLX', () => {
  const antwort = { teile: fragebogen.teile.map((t) => ({ ...t, wortlautVorhanden: t.fragen.every((f) => f.wortlaut) })) };
  assert.deepEqual(T.teileOhneWortlaut(antwort), ['sus', 'nasa_tlx']);
  const h = T.wortlautHinweis(antwort);
  assert.match(h, /^SUS und NASA-TLX noch ohne Wortlaut/);
  assert.match(h, /Quelle/);
  assert.match(h, /Komfortteil funktioniert/);
  assert.match(T.wortlautHinweis({ teile: [{ id: 'komfort', wortlautVorhanden: true }, { id: 'sus', wortlautVorhanden: false }, { id: 'nasa_tlx', wortlautVorhanden: true }] }), /^SUS noch ohne Wortlaut/);
  assert.equal(T.wortlautHinweis({ teile: [{ id: 'komfort', wortlautVorhanden: true }, { id: 'sus', wortlautVorhanden: true }] }), null);
  assert.equal(T.wortlautHinweis(null), null);
  // die Datei selbst: Komfort mit Wortlaut, SUS und TLX nur Platzhalter, keine erfundene Fassung
  const komfort = fragebogen.teile.find((t) => t.id === 'komfort');
  assert.ok(komfort.fragen.every((f) => f.wortlaut));
  for (const id of ['sus', 'nasa_tlx']) {
    const t = fragebogen.teile.find((x) => x.id === id);
    assert.ok(t.fragen.every((f) => f.wortlaut === null && /Quelle durch Christian/.test(f.platzhalter)), id);
    assert.equal(t.wortlautQuelle, null);
  }
});

test('Fragebogen: Körper aus den Eingaben', () => {
  const basis = { name: ' Christian ', app: '0.1+abc', varianten: { bedienung: 'apps' }, komfort: { uebelkeit: '2', augen: '', natuerlich: '8' }, text: ' Drehen gut ', notiz: '', sus: null, tlx: null };
  const r = T.fragebogenKoerper(basis);
  assert.equal(r.ok, true);
  assert.deepEqual(r.koerper, { name: 'Christian', app: '0.1+abc', varianten: { bedienung: 'apps' }, komfort: { uebelkeit: 2, natuerlich: 8, text: 'Drehen gut' } });

  const nurText = T.fragebogenKoerper({ name: 'A', komfort: {}, text: 'nur Text' });
  assert.deepEqual(nurText.koerper, { name: 'A', komfort: { text: 'nur Text' } });
  assert.equal(T.fragebogenKoerper({ ...basis, name: ' ' }).ok, false);
  assert.match(T.fragebogenKoerper({ ...basis, name: '' }).text, /Namen/);
  assert.equal(T.fragebogenKoerper({ name: 'A', komfort: {}, text: '' }).ok, false);
  assert.match(T.fragebogenKoerper({ name: 'A', komfort: {}, text: '' }).text, /mindestens eine Frage/);
  assert.equal(T.fragebogenKoerper({ name: 'A', komfort: { uebelkeit: '11' } }).ok, false);
  assert.equal(T.fragebogenKoerper({ name: 'A', komfort: { augen: '2.5' } }).ok, false);
  assert.equal(T.fragebogenKoerper({ name: 'A', komfort: { natuerlich: '-1' } }).ok, false);

  // SUS: zehn Antworten 1 … 5 oder gar keine
  const sus = ['4', '2', '5', '1', '4', '2', '5', '2', '4', '1'];
  assert.deepEqual(T.fragebogenKoerper({ name: 'A', sus }).koerper.sus, [4, 2, 5, 1, 4, 2, 5, 2, 4, 1]);
  assert.equal(T.fragebogenKoerper({ name: 'A', sus: [...sus.slice(0, 9), ''] }).ok, false);
  assert.equal(T.fragebogenKoerper({ name: 'A', sus: [...sus.slice(0, 9), '6'] }).ok, false);
  assert.equal(T.fragebogenKoerper({ name: 'A', sus: Array(10).fill(''), komfort: { augen: '1' } }).koerper.sus, undefined);   // nichts angekreuzt = kein SUS
  // TLX: sechs Werte 0 … 100
  const tlx = { mental: '10', physical: '20', temporal: '30', performance: '40', effort: '50', frustration: '60' };
  assert.deepEqual(T.fragebogenKoerper({ name: 'A', tlx }).koerper.tlx, { mental: 10, physical: 20, temporal: 30, performance: 40, effort: 50, frustration: 60 });
  assert.equal(T.fragebogenKoerper({ name: 'A', tlx: { ...tlx, effort: '101' } }).ok, false);
  assert.equal(T.fragebogenKoerper({ name: 'A', tlx: { ...tlx, effort: '' } }).ok, false);
  assert.equal(T.fragebogenKoerper({ name: 'A', tlx: { mental: '10' } }).ok, false);
  assert.equal(T.ganzeZahl('7', 0, 10), 7);
  assert.equal(T.ganzeZahl(' 7 ', 0, 10), 7);
  assert.equal(T.ganzeZahl('7.0', 0, 10), null);
  assert.equal(T.ganzeZahl('', 0, 10), null);
  assert.match(T.gespeichertText({ datei: 'a.json', sus: { punkte: 85 }, komfort: { uebelkeit: 1 } }), /^Gespeichert \(a\.json\): SUS 85 · Übelkeit 1\.$/);
});

test('HTML-Sicherheit: Namen und Texte aus dem Netz gehen nur über esc() ins HTML', () => {
  assert.equal(T.esc('<img src=x onerror="a">'), '&lt;img src=x onerror=&quot;a&quot;&gt;');
  const dialog = lies('xr-labor-dialog.js');
  assert.match(dialog, /const esc = T\.esc;/);
  // jede Ersetzung ${…}, die einen Wert aus dem Netz oder den Katalogen trägt, ist durch esc() geschützt – bis auf diese geprüften Stellen (Meldungstexte, die meldungZeigen()
  // selbst mit esc() ausgibt; feste Wörter nach einem Vergleich; ein Selektor; die Zeichen von schluessel sind auf [a-z0-9-] reduziert)
  const roh = [...dialog.matchAll(/\$\{(?!esc\()([^}]*)\}/g)].map((m) => m[1]).filter((t) => /(\.name|\.titel|\.beschreibung|\.herkunft|\.wortlaut|\.platzhalter|\.app\b|\.quelle|\.ordner|\.anleitung|\.ort|\.erfolgskriterium|\.fehler|\.text\b|\.id\b|\.schluessel)/.test(t));
  const geprueft = new Set([
    "s.schluessel.replace(/[^a-z0-9]/g, '-')",          // Attribut id: nur Kleinbuchstaben, Ziffern, Bindestrich
    's.titel', 'r.text', 'vorhanden.name', 'profil?.name ?? kennung', 'v?.titel ?? name', 'a?.titel ?? id', 'x.titel',   // Meldungen und aria-label über meldungZeigen() bzw. tabelle(), beide mit esc()
    "o.id === wahl ? ' checked' : ''", `o.id === v.vorgabe ? ' <span class="leise">(Vorgabe)</span>' : ''`,                // feste Texte
    'skalaRadio(`xl-k-${f.id', 'sus.wortlautVorhanden ? skalaRadio(`xl-sus-${i',                                          // skalaRadio() schützt den Namen mit esc()
    "sus.wortlautVorhanden ? '' : 'disabled'", "tlx.wortlautVorhanden ? '' : 'disabled'", 'f.id',                          // feste Wörter; Selektor mit fester Id des Katalogs
    // verschachtelte Vorlagen, vom einfachen Muster beim ersten «}» abgeschnitten; die enthaltenen Werte stehen alle in esc(…)
    'T.BEREICHE.map((b) => `<button type="button" role="tab" class="xl-reiter-knopf" data-bereich="${esc(b.id)',
    'v.optionen.map((o, i) => `<label class="xl-option"><input type="radio" name="xl-var-${esc(v.name)',
    'f.wortlaut ? esc(f.wortlaut) : `<i class="xl-platzhalter">${esc(f.platzhalter ?? \'Wortlaut fehlt\')',
  ]);
  assert.deepEqual(roh.filter((t) => !geprueft.has(t)), []);
  assert.match(dialog, /name="\$\{esc\(name\)\}"/);
  assert.doesNotMatch(dialog, /\beval\(|new Function\(|document\.write/);
});

test('Verdrahtung: Menüband-Aktion, Symbol, Dialog nur über /api/xr, Benutzereinstellungen für die Profile', () => {
  const akt = lies('aktionen-quest.js');
  assert.match(akt, /id: 'ansicht\.xrLabor'.*register: 'ansicht'.*gruppe: 'Fenster'.*groesse: 'klein'/s);
  assert.match(akt, /import\('\.\/xr-labor-dialog\.js'\)/);
  assert.match(lies('symbole.js'), /xrLabor: '/);
  const dialog = lies('xr-labor-dialog.js');
  const pfade = [...dialog.matchAll(/'(\/api\/[^'`?]+)/g)].map((m) => m[1]);
  assert.ok(pfade.length >= 8);
  for (const p of pfade) assert.match(p, /^\/api\/xr\//, p);
  for (const p of ['/api/xr/stellschraube', '/api/xr/teilnehmer', '/api/xr/labor/katalog', '/api/xr/labor/auswertung', '/api/xr/labor/fragebogen', '/api/xr/labor/variante', '/api/xr/labor/aufgabe']) {
    assert.ok(pfade.includes(p), p);
  }
  assert.doesNotMatch(dialog, /fetch\(/);   // alles über holen/senden (api.js)
  assert.match(dialog, /benutzerSetzen\(T\.profilSchluessel\(kennung\)/);
  assert.equal(T.PROFIL_VORSATZ, 'labor.profil.');
  assert.equal(T.PROFIL_AKTIV, 'labor.profil_aktiv');
  assert.doesNotMatch(T.PROFIL_VORSATZ + T.PROFIL_AKTIV, /xr\./);   // «xr.…» ist für den Zugang je PC reserviert
  assert.match(lies('benutzer.js'), /export const benutzerAlle/);
  assert.match(lies('../../css/quest.css'), /\.dlg\.xr-labor \{ width: min\(760px, calc\(100vw - 16px\)\)/);
  assert.deepEqual(T.BEREICHE.map((b) => b.id), ['stellschrauben', 'profile', 'varianten', 'aufgaben', 'auswertung', 'fragebogen']);
});
