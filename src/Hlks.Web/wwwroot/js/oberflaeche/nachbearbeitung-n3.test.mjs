// Tests der Nachbearbeitung N3 (fehlende Bedienorte) ohne Browser: CSV der Listenansicht (liste-csv.js), Tastaturkürzel nach Register (kuerzel-liste.js),
// Projektinformationen bearbeiten (projektinfo-text.js, Felder aus dem echten Katalog), Einlage- und Grundleitungspläne anlegen (einlageplaene-text.js),
// Knoten «Projektinformationen» im Projektbrowser, Verdrahtung (Aktionen, Symbole, Stylesheet, Tooltip-Zeile).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/nachbearbeitung-n3.test.mjs
// Alle Zahlen und Namen in den Beispieldaten sind FIKTIV und stehen nur hier.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import { aufbereiten } from '../ansichten/listenformat.js';
import * as CSV from '../ansichten/liste-csv.js';
import * as K from './kuerzel-liste.js';
import * as P from './projektinfo-text.js';
import * as E from './einlageplaene-text.js';
import * as S from './symbole.js';
import { baumBauen } from './projektbrowser-aeste.js';
import { gliederung } from './projektbrowser-gliederung.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const katalog = JSON.parse(lies('../../../../../kataloge/klassen.json'));
const klassen = Array.isArray(katalog) ? katalog : katalog.klassen;
const klasse = (n) => klassen.find((k) => k.name === n);

// ---------------------------------------------------------------------------------------------- Beispieldaten (Form wie die Antworten des Kerns)

const kollisionen = {
  ableitung: 'Kollisionsliste', projekt: 'Test N3', modellStand: { pruefsumme: 'k1', pakete: 9 }, erzeugt: '2026-10-19 10:00', anzahl: 3,
  zusammenfassung: { jeStatus: { bestaetigt: 1, neu: 2 }, jeArt: { hart: 2, weich: 1 }, jeTyp: { durchdringung: 1, kontakt: 2 } },
  zeilen: [
    { kollision: 'KOL-10', art: 'hart', typ: 'durchdringung', status: 'neu', mass_mm: 12.5, geschoss: 'EG', beteiligte: 'RO-1 (Rohr) ↔ W-1 (Wand)', bauteil: 'W-1 (Wand)', lage_mm: [3000, 0, 1000], kommentar: 'Rohr "gross"; verschieben', kennung: 'a1' },
    { kollision: 'KOL-2', art: 'weich', typ: 'kontakt', status: 'bestaetigt', mass_mm: 30, geschoss: 'EG', beteiligte: 'RO-1 (Rohr) ↔ KA-1 (Kanal)', bauteil: null, lage_mm: [2500.5, 100, 1500], kommentar: null, kennung: 'c3' },
    { kollision: 'KOL-3', art: 'hart', typ: 'kontakt', status: 'neu', mass_mm: null, geschoss: 'EG', beteiligte: 'KA-1 (Kanal) ↔ IfcBeam', bauteil: null, lage_mm: [1, 2, 3], kommentar: null, kennung: 'e5' },
  ],
};
const aussparungen = {
  ableitung: 'Aussparungsliste', projekt: 'Test N3', modellStand: { pruefsumme: 'a1', pakete: 11 }, erzeugt: '2026-10-19 10:05', geschoss: 'EG', anzahl: 2,
  jeStatus: { ausgefuehrt: 1, vorgeschlagen: 1 },
  zeilen: [
    { aussparung: 'AUS-2', nummer: 'A-EG-001', geschoss: 'EG', bauteil: 'W-1', form: 'rechteckig', masse: '440 × 240', tiefe: 200, koteUk: 1880, koteOk: 2120, art: 'aussparung', status: 'ausgefuehrt', leitungen: ['KA-1'], mitte: [2000, 0, 2000] },
    { aussparung: 'AUS-1', nummer: 'A-EG-002', geschoss: 'EG', bauteil: 'W-1', form: 'rund', masse: 'Ø 95', tiefe: 200, koteAchse: 1000, art: 'kernbohrung', status: 'vorgeschlagen', leitungen: ['RO-1', 'RO-2'], mitte: [3000, 0, 1000] },
  ],
};
const mengen = {
  ableitung: 'Mengen', projekt: 'Test N3', leitungslaengeTotal_m: 12.5,
  leitungen: [{ system: 'KW', klasse: 'Rohr', material: 'CNS', groesse: 'DN 20', stueck: 2, laenge_m: 12.5 }],
  bauteile: [{ klasse: 'WC', gewerk: 'sanitaer', stueck: 3 }],
};

const zeilenVon = (text) => text.replace(/^﻿/, '').split('\r\n');

// ---------------------------------------------------------------------------------------------- 3. CSV

test('CSV: UTF-8 mit BOM, Semikolon, Kopfzeile, CRLF, Dezimalkomma – Kollisionsliste', () => {
  const csv = CSV.csvAus(aufbereiten('kollisionen', kollisionen));
  assert.equal(csv.charCodeAt(0), 0xFEFF, 'BOM am Anfang');
  assert.equal(Buffer.from(csv, 'utf8').subarray(0, 3).toString('hex'), 'efbbbf', 'als UTF-8 geschrieben beginnt die Datei mit EF BB BF');
  const z = zeilenVon(csv);
  assert.equal(z.at(-1), '', 'endet mit Zeilenende');
  const kopf = z[0].split(';');
  assert.deepEqual(kopf.slice(0, 5), ['Kollision', 'Art', 'Typ', 'Status', 'Mass mm']);
  assert.ok(kopf.includes('Beteiligte') && kopf.includes('Geschoss'));
  assert.equal(z.length, 1 + 3 + 1, 'Kopf + drei Zeilen + leeres Ende');
  const erste = z[1];
  assert.match(erste, /^KOL-10;hart;durchdringung;neu;12,5;EG;/);   // Dezimalkomma wie in der Liste
  assert.match(z[2], /;bestätigt;/, 'Status mit Umlaut wie angezeigt');
  assert.match(z[2], /2500,5, 100, 1500/, 'Liste mit Komma im Feld');
  assert.ok(!csv.includes('\n') || csv.split('\n').length === csv.split('\r\n').length, 'nur CRLF');
});

test('CSV: Felder mit Semikolon, Anführungszeichen oder Zeilenumbruch stehen in Anführungszeichen (verdoppelt)', () => {
  assert.equal(CSV.csvFeld('a'), 'a');
  assert.equal(CSV.csvFeld('a;b'), '"a;b"');
  assert.equal(CSV.csvFeld('sag "hallo"'), '"sag ""hallo"""');
  assert.equal(CSV.csvFeld('zwei\nZeilen'), '"zwei\nZeilen"');
  assert.equal(CSV.csvFeld(null), '');
  assert.equal(CSV.csvFeld(undefined), '');
  assert.equal(CSV.csvFeld(0), '0');
  const z = zeilenVon(CSV.csvAus(aufbereiten('kollisionen', kollisionen)));
  assert.match(z[1], /"Rohr ""gross""; verschieben"/);
});

test('CSV: Aussparungsliste – Spalten wie in der Liste (leere Spalten entfallen), Reihenfolge der Zeilen wie die Daten', () => {
  const daten = aufbereiten('aussparungsliste', aussparungen);
  const z = zeilenVon(CSV.csvAus(daten));
  assert.deepEqual(z[0].split(';').slice(0, 4), ['Nummer', 'Aussparung', 'Geschoss', 'Status']);
  assert.ok(z[0].split(';').includes('Tiefe mm'));
  assert.match(z[1], /^A-EG-001;AUS-2;EG;ausgeführt;/);
  assert.match(z[2], /^A-EG-002;AUS-1;EG;vorgeschlagen;/);
  assert.equal(CSV.csvZeilenzahl(daten), 2);
  const spalten = daten.tabellen[0].spalten.length;
  for (const zeile of z.slice(0, 3)) assert.equal(zeile.split(';').length >= spalten, true, 'jede Zeile hat mindestens so viele Felder wie Spalten');
});

test('CSV: Sortierung der Ansicht wird übernommen (Zahl absteigend, leere am Ende)', () => {
  const daten = aufbereiten('kollisionen', kollisionen);
  const s = new Map([[0, { schluessel: 'mass_mm', absteigend: true }]]);
  const z = zeilenVon(CSV.csvAus(daten, s));
  assert.deepEqual(z.slice(1, 4).map((x) => x.split(';')[0]), ['KOL-2', 'KOL-10', 'KOL-3']);
  const auf = zeilenVon(CSV.csvAus(daten, new Map([[0, { schluessel: 'mass_mm', absteigend: false }]])));
  assert.deepEqual(auf.slice(1, 4).map((x) => x.split(';')[0]), ['KOL-10', 'KOL-2', 'KOL-3']);
});

test('CSV: Listen mit zwei Tabellen (Mengen) stehen untereinander, je mit Titelzeile und Leerzeile dazwischen', () => {
  const z = zeilenVon(CSV.csvAus(aufbereiten('mengen', mengen)));
  assert.equal(z[0], 'Leitungen');
  assert.equal(z[1], 'System;Klasse;Material;Grösse;Stück;Länge m');
  assert.equal(z[2], 'KW;Rohr;CNS;DN 20;2;12,50');
  assert.equal(z[3], '');
  assert.equal(z[4], 'Bauteile');
  assert.match(z[5], /^Klasse;Gewerk;Stück/);
  assert.match(z[6], /^WC;sanitaer;3/);
});

test('CSV: Fusszeilen (Summen) folgen unter der Tabelle; leere Liste ergibt eine leere Datei', () => {
  const daten = aufbereiten('raumgrenzen', { ableitung: 'Raumgrenzen', zeilen: [{ raum: 'R1', art: 'wand', flaeche_m2: 10, ua_WK: 2 }], summen: { jeAngrenzung: { aussen: { flaeche_m2: 10, ua_WK: 2 } } } });
  const z = zeilenVon(CSV.csvAus(daten));
  assert.match(z[1], /^R1;wand;/);
  assert.match(z[2], /^Summe aussen;/);
  assert.equal(CSV.csvAus(aufbereiten('kollisionen', { ableitung: 'Kollisionsliste', zeilen: [] })), CSV.BOM, 'leere Liste: keine Spalten, nur die Kennung der Datei');
  assert.equal(CSV.csvAus(null), CSV.BOM);
});

test('CSV: Dateiname – Titel, Projekt, Datum; unzulässige Zeichen entfallen; Eingabe wird auf .csv gebracht', () => {
  const d = aufbereiten('kollisionen', kollisionen);
  assert.equal(CSV.csvDateiname(d, '2026-10-19'), 'Kollisionsliste_Test_N3_2026-10-19.csv');
  assert.equal(CSV.csvDateiname({ titel: 'Liste: A/B?', projekt: '' }, '2026-01-02'), 'Liste_AB_2026-01-02.csv');
  assert.equal(CSV.csvDateiname(null, '2026-01-02'), 'Liste_2026-01-02.csv');
  assert.match(CSV.datumText(new Date(2026, 9, 19)), /^2026-10-19$/);
  assert.equal(CSV.csvDateinameSauber('meine Liste'), 'meine Liste.csv');
  assert.equal(CSV.csvDateinameSauber('x.CSV'), 'x.CSV');
  assert.equal(CSV.csvDateinameSauber('a/b:c.csv'), 'abc.csv');
  assert.equal(CSV.csvDateinameSauber('   '), 'Liste.csv');
  assert.equal(CSV.csvDateinameSauber('..'), 'Liste.csv');
});

// ---------------------------------------------------------------------------------------------- 4. Kürzel nach Register

const A = (titel, kuerzel, register, extra = {}) => ({ titel, kuerzel, kuerzelAlt: [], register, aktiv: () => true, ...extra });

test('Kürzel: jedes Register des Menübands hat einen Namen; «Beschriften», «Einfügen», «Hülle», «Koordination» und «Familie» sind darunter', () => {
  const menueband = lies('./menueband.js');
  const ids = [...menueband.match(/const REGISTER = \[(.*?)\];/s)[1].matchAll(/\['(\w+)', '([^']+)'\]/g)].map((m) => [m[1], m[2]]);
  assert.ok(ids.length >= 10);
  for (const [id, name] of ids) assert.equal(K.REGISTER_NAMEN[id], name, `Register ${id} heisst im Dialog wie im Menüband`);
  for (const r of ['beschriften', 'einfuegen', 'huelle', 'koordination', 'familie']) assert.ok(K.REGISTER_NAMEN[r], r);
  assert.equal(K.REGISTER_NAMEN.beschriften, 'Beschriften');
  assert.equal(K.REGISTER_NAMEN.einfuegen, 'Einfügen');
  assert.equal(K.REGISTER_NAMEN.huelle, 'Hülle');
});

test('Kürzel: TX, DI, DL, EL (Beschriften), CM (Familie platzieren) stehen unter ihrem Register, nur Aktionen ohne Register unter «Allgemein»', () => {
  const liste = [
    A('Text', 'TX', 'beschriften'), A('Bemassung', 'DI', 'beschriften'), A('Detaillinie', 'DL', 'beschriften'), A('Höhenkote', 'EL', 'beschriften'),
    A('Familie platzieren', 'CM', 'gebaeudetechnik'), A('Wand', 'WA', 'architektur'), A('Ändern', 'MD', 'aendern'), A('Löschen', 'DE', 'aendern', { kuerzelAlt: ['Entf'] }),
    A('Zoom alles', 'ZF', 'ansicht', { kuerzelAlt: ['ZE'] }), A('Tastaturkürzel', 'KS', 'verwalten'), A('Startbildschirm', 'Strg+D', null), A('Ohne Kürzel', null, 'ansicht'),
    A('Familie Test', 'FX', 'familie'), A('Einfügen Test', 'EX', 'einfuegen'), A('Hülle Test', 'HX', 'huelle'), A('Koordination Test', 'KX', 'koordination'), A('Auswahl', 'AX', 'kontext'),
    A('Fremd', 'ZZ', 'gibtsnicht'),
  ];
  const z = K.kuerzelZeilen(liste);
  const reg = (kuerzel) => z.find((x) => x.kuerzel.split(' / ')[0] === kuerzel)?.register;
  assert.equal(reg('TX'), 'Beschriften');
  assert.equal(reg('DI'), 'Beschriften');
  assert.equal(reg('DL'), 'Beschriften');
  assert.equal(reg('EL'), 'Beschriften');
  assert.equal(reg('CM'), 'Gebäudetechnik');
  assert.equal(reg('FX'), 'Familie');
  assert.equal(reg('EX'), 'Einfügen');
  assert.equal(reg('HX'), 'Hülle');
  assert.equal(reg('KX'), 'Koordination');
  assert.equal(reg('AX'), 'Auswahl');
  assert.equal(z.find((x) => x.titel === 'Löschen').kuerzel, 'DE / Entf');
  assert.equal(z.length, 17, 'Aktion ohne Kürzel fehlt');
  const allgemein = z.filter((x) => x.register === 'Allgemein').map((x) => x.kuerzel);
  assert.deepEqual(allgemein.sort(), ['Strg+D', 'ZZ'], 'nur ohne Register bzw. mit unbekanntem Register');
});

test('Kürzel: Reihenfolge wie im Menüband, innerhalb eines Registers wie registriert; Allgemein zuletzt; Hinweis nur bei gesperrten Aktionen', () => {
  const liste = [
    A('Verwalten X', 'VX', 'verwalten'), A('Allgemein X', 'GX', null), A('Beschriften X', 'BX', 'beschriften'), A('Datei X', 'DX', 'datei'), A('Beschriften Y', 'BY', 'beschriften'),
    A('Gesperrt', 'ZX', 'ansicht', { aktiv: () => false }),
  ];
  const z = K.kuerzelZeilen(liste, (a) => `Hinweis zu ${a.titel}`);
  assert.deepEqual(z.map((x) => x.kuerzel), ['DX', 'BX', 'BY', 'ZX', 'VX', 'GX']);
  assert.equal(z.find((x) => x.kuerzel === 'ZX').hinweis, 'Hinweis zu Gesperrt');
  assert.equal(z.find((x) => x.kuerzel === 'DX').hinweis, '');
  assert.equal(K.kuerzelZeilen([A('Text​ liste', 'TL', 'ansicht')])[0].titel, 'Text liste', 'weiche Trennzeichen entfallen');
});

// ---------------------------------------------------------------------------------------------- 2. Projektinformationen

const plan = () => P.feldPlan(klasse('Projektinformation'));
const feld = (name) => P.alleFelder(plan()).find((f) => f.name === name);

test('Projektinformation: der Plan nimmt alle Felder der Katalogklasse auf, bearbeitbare und nur lesbare', () => {
  const namen = klasse('Projektinformation').eigenschaften.map((e) => e.name).sort();
  assert.deepEqual(P.alleFelder(plan()).map((f) => f.name).sort(), namen, 'kein Feld des Katalogs fehlt, keins ist dazuerfunden');
  // Modul 4 Teil A: die Abflusskennzahl (Vorgabe für die Sanitär-Zusammenstellung) ist neu im Katalog und steht von selbst unter «Weitere»
  assert.deepEqual(plan().map((g) => g.titel), ['Projekt', 'Beschriftung und Bemassung', 'Lage', 'Weitere', 'Von anderen Funktionen gesetzt']);
  assert.deepEqual(plan().find((g) => g.titel === 'Weitere').felder.map((f) => f.name), ['titelband', 'abflusskennzahl']);   // Modul 7 Teil A: titelband (Titelband «Planinhalt» im Plankopf) steht neu in der Projektinformation
  const art = (n) => feld(n).art;
  assert.equal(art('abflusskennzahl'), 'zahl');
  assert.deepEqual(['zeile1', 'zeile2', 'projektnummer', 'phase'].map(art), ['text', 'text', 'text', 'text']);
  assert.equal(art('rohrbezeichnung'), 'auswahl');
  assert.equal(art('bemassungEinheiten'), 'einheiten');
  assert.equal(art('nordwinkel'), 'zahl');
  for (const n of ['kollisionslauf', 'ifcBezug', 'uebergang', 'hkap', 'familienAuswahl']) { assert.equal(art(n), 'lesen', n); assert.ok(feld(n).hinweis, `${n}: Hinweis, wo man es ändert`); }
});

test('Projektinformation: Auswahl (NW/DN), Einheit und Beschreibung kommen aus dem Katalog – nichts ist im Dialog festgelegt', () => {
  assert.deepEqual(feld('rohrbezeichnung').auswahl, ['NW', 'DN']);
  assert.equal(feld('nordwinkel').einheit, '°');
  assert.match(feld('bemassungEinheiten').beschreibung, /Einheit der Masszahlen je Gewerk/);
  const quelle = lies('./projektinfo-text.js');
  assert.ok(!/['"]NW['"]|['"]DN['"]|['"]cm['"]/.test(quelle.replace(/\/\/.*$/gm, '')), 'keine Werte im Code');
  const einheiten = klasse('Ansicht').eigenschaften.find((e) => e.name === 'bemassungEinheit').auswahl;
  assert.deepEqual(einheiten, ['m', 'cm', 'mm'], 'die Einheitenliste für die Auswahl liegt im Katalog (Ansicht.bemassungEinheit)');
  assert.match(lies('./projektinfo-dialog.js'), /bemassungEinheit'\)\?\.auswahl/);
});

test('Projektinformation: nur geänderte Felder gehen als element_aendern; unverändert ergibt keinen Befehl', () => {
  const p = plan();
  const werte = { zeile1: 'Objekt A', projektnummer: 'P-1', rohrbezeichnung: 'NW', nordwinkel: 12.5, bemassungEinheiten: { sanitaer: 'cm' }, kollisionslauf: 'Lauf 3' };
  const gleich = { zeile1: 'Objekt A', zeile2: '', projektnummer: ' P-1 ', phase: '', rohrbezeichnung: 'NW', nordwinkel: '12,5', bemassungEinheiten: { sanitaer: 'cm', lueftung: '' } };
  assert.deepEqual(P.aenderungen(p, werte, gleich), {}, 'Leerzeichen, Dezimalkomma und leere Einheit sind keine Änderung');
  assert.equal(P.aendernParameter('PI-1', {}), null);
  const aus = P.aenderungen(p, werte, { ...gleich, zeile1: 'Objekt B', phase: 'Bauprojekt', rohrbezeichnung: 'DN', nordwinkel: '', bemassungEinheiten: { sanitaer: 'mm', heizung: 'cm' } });
  assert.deepEqual(aus, { zeile1: 'Objekt B', phase: 'Bauprojekt', rohrbezeichnung: 'DN', nordwinkel: null, bemassungEinheiten: { sanitaer: 'mm', heizung: 'cm' } });
  assert.deepEqual(P.aendernParameter('PI-1', aus), { element: 'PI-1', eigenschaften: aus });
  // Eintrag entfernen: leere Auswahl und leere Tabelle → null
  const weg = P.aenderungen(p, werte, { ...gleich, rohrbezeichnung: '', projektnummer: '', bemassungEinheiten: { sanitaer: '' } });
  assert.deepEqual(weg, { projektnummer: null, rohrbezeichnung: null, bemassungEinheiten: null });
  // nur lesbare Felder werden nie gesendet
  const mit = P.aenderungen(p, werte, { kollisionslauf: 'anders', ifcBezug: 'x' });
  assert.deepEqual(mit, {});
});

test('Projektinformation: ohne Element wird eines angelegt (nur gesetzte Werte, Kennzeichen vergibt der Kern); ohne Werte kein Befehl', () => {
  const p = plan();
  const aus = P.aenderungen(p, {}, { zeile1: 'Objekt', phase: '', rohrbezeichnung: 'DN', nordwinkel: '15', bemassungEinheiten: { lueftung: 'mm' } });
  assert.deepEqual(P.anlegenParameter(aus), { klasse: 'Projektinformation', name: 'Projektinformation', eigenschaften: { zeile1: 'Objekt', rohrbezeichnung: 'DN', nordwinkel: 15, bemassungEinheiten: { lueftung: 'mm' } } });
  assert.equal(P.anlegenParameter(P.aenderungen(p, {}, { zeile1: '', phase: '' })), null);
  assert.equal(P.anlegenParameter({}), null);
});

test('Projektinformation: Zahlen in Schweizer Schreibweise, Fehler bei Text; lesbare Werte als Kurztext; das erste Element nach Kennzeichen', () => {
  assert.equal(P.zahlLesen('12,5'), 12.5);
  assert.equal(P.zahlLesen(' 3.25 '), 3.25);
  assert.equal(P.zahlLesen('-7'), -7);
  assert.equal(P.zahlLesen(''), null);
  assert.throws(() => P.zahlLesen('abc', 'Nordwinkel'), /Nordwinkel: bitte eine Zahl/);
  assert.throws(() => P.aenderungen(plan(), {}, { nordwinkel: 'x' }), /Nordwinkel/);
  assert.equal(P.lesendText(null), '–');
  assert.equal(P.lesendText('Lauf 2'), 'Lauf 2');
  assert.equal(P.lesendText({ x: 1, y: 2 }), 'x, y');
  assert.equal(P.lesendText([1, 2]), '2 Einträge');
  assert.equal(P.lesendText([1]), '1 Eintrag');
  assert.equal(P.lesendText({}), '–');
  assert.equal(P.einheitenTabelle({ a: '', b: ' cm ' }).b, 'cm');
  assert.equal(P.einheitenTabelle({ a: '' }), null);
  assert.equal(P.projektinfoVon([{ klasse: 'Raum', kennzeichen: 'A' }, { klasse: 'Projektinformation', kennzeichen: 'PI-2' }, { klasse: 'Projektinformation', kennzeichen: 'PI-1' }]).kennzeichen, 'PI-1');
  assert.equal(P.projektinfoVon([]), null);
  assert.equal(P.feldTitel('meinNeuesFeld'), 'Mein Neues Feld');
});

test('Projektinformation: ein neues Katalogfeld erscheint unter «Weitere» (Typ text, zahl, auswahl bearbeitbar, sonst nur lesen)', () => {
  const def = { eigenschaften: [{ name: 'zeile1', typ: 'text' }, { name: 'neuText', typ: 'text' }, { name: 'neuZahl', typ: 'zahl', einheit: 'm' }, { name: 'neuObjekt', typ: 'objekt' }, { name: 'neuWahl', typ: 'auswahl', auswahl: ['a', 'b'] }] };
  const p = P.feldPlan(def);
  assert.deepEqual(p.map((g) => g.titel), ['Projekt', 'Weitere']);
  assert.deepEqual(p[1].felder.map((f) => [f.name, f.art]), [['neuText', 'text'], ['neuZahl', 'zahl'], ['neuObjekt', 'lesen'], ['neuWahl', 'auswahl']]);
  assert.deepEqual(P.feldPlan(null), []);
});

// ---------------------------------------------------------------------------------------------- 1. Einlageplan

const verlegung = {
  abfrage: 'verlegung',
  plaene: [
    { plan: 'Einlagen D.ü.UG', art: 'einlage', bauteilart: 'decke', geschoss: 'UG', leitungen: 3, formteile: 1, laenge_m: 12.5, elemente: [] },
    { plan: 'Einlagen Bodenplatte UG', art: 'einlage', bauteilart: 'bodenplatte', geschoss: 'UG', leitungen: 1, formteile: 0, laenge_m: 2, elemente: [] },
    { plan: 'Einlagen Wände EG', art: 'einlage', bauteilart: 'wand', geschoss: 'EG', leitungen: 2, formteile: 2, laenge_m: 5.25, elemente: [] },
    { plan: 'Einlagen D.ü.EG', art: 'einlage', bauteilart: 'dach', geschoss: 'EG', leitungen: 1, formteile: 0, laenge_m: 1, elemente: [] },
  ],
};

test('Einlageplan: Planzeilen aus der Abfrage verlegung – Planschlüssel als Namensvorschlag, Bauteil, Geschoss, Inhalt', () => {
  const z = E.planZeilen(verlegung);
  assert.deepEqual(z.map((x) => [x.plan, x.bauteil, x.geschoss]), [
    ['Einlagen D.ü.UG', 'Decke', 'UG'], ['Einlagen Bodenplatte UG', 'Bodenplatte', 'UG'], ['Einlagen Wände EG', 'Wand', 'EG'], ['Einlagen D.ü.EG', 'Dach (wie Decke)', 'EG']]);
  assert.equal(E.planSumme(z[0]), '3 Leitungen, 1 Formteil, 12,50 m');
  assert.equal(E.planSumme(z[1]), '1 Leitung, 0 Formteile, 2,00 m');
  assert.deepEqual(E.planZeilen(null), []);
  assert.deepEqual(E.planZeilen({}), []);
  assert.deepEqual(E.geschossOptionen(z, ['UG', 'EG', 'OG']), ['UG', 'EG']);
  assert.deepEqual(E.geschossOptionen(z, ['EG']), ['EG', 'UG'], 'unbekannte hinten');
  assert.equal(E.planFilter(z, 'UG').length, 2);
  assert.equal(E.planFilter(z, '').length, 4);
  assert.equal(E.bauteilText(undefined), '–');
});

test('Einlageplan: Befehlsparameter – nur was gewählt ist, ohne Wahl gelten die Vorgaben des Kerns', () => {
  assert.deepEqual(E.befehlParameter({ art: 'einlage' }), { art: 'einlage' });
  assert.deepEqual(E.befehlParameter({ art: 'erdreich', geschoss: 'UG', massstab: '100', format: 'A2' }), { art: 'erdreich', geschoss: 'UG', massstab: 100, format: 'A2' });
  assert.deepEqual(E.befehlParameter({ art: 'einlage', geschoss: '', massstab: '', format: '' }), { art: 'einlage' });
  assert.deepEqual(E.befehlParameter({ art: 'einlage', massstab: 'x' }), { art: 'einlage' });
  assert.equal(E.BEFEHL, 'einlageplaene_anlegen');
  assert.deepEqual(Object.keys(E.ARTEN), ['einlage', 'erdreich']);
  assert.equal(E.ARTEN.einlage.titel, 'Einlageplan');
  assert.equal(E.ARTEN.erdreich.titel, 'Grundleitungsplan');
  // die Parameter des Befehls im Kern: massstab, format, geschoss, art
  const kern = readFileSync(path.resolve(hier, '../../../../../src/Hlks.Kern/Plan/EinlagePlaene.cs'), 'utf8');
  for (const n of ['massstab', 'format', 'geschoss', 'art']) assert.match(kern, new RegExp(`new\\("${n}"`));
  assert.deepEqual(E.FORMATE, ['A4', 'A3', 'A2', 'A1', 'A0']);
});

test('Einlageplan: Lauf des Befehls – angelegte Ansichten und Planblätter, Hinweise, Zusammenfassung', () => {
  const r = {
    angenommen: true, hinweise: ['Einlagen D.ü.UG: Ansicht EL-D.ü.UG angelegt, Planblatt PB-1 «Sanitär Einlagen D.ü.UG» angelegt.', ' ', null],
    aenderungen: [{ art: 'angelegt', klasse: 'Ansicht', kennzeichen: 'EL-D.ü.UG' }, { art: 'angelegt', klasse: 'Planblatt', kennzeichen: 'PB-1' }, { art: 'geaendert', klasse: 'Planblatt', kennzeichen: 'PB-1' },
      { art: 'angelegt', klasse: 'Ansichtsfenster', kennzeichen: 'AF-1' }],
  };
  assert.deepEqual(E.angelegt(r), { ansichten: ['EL-D.ü.UG'], planblaetter: ['PB-1'] });
  assert.equal(E.laufText(r), '1 Ansicht, 1 Planblatt');
  assert.deepEqual(E.hinweisZeilen(r), [r.hinweise[0]]);
  assert.equal(E.laufText({ aenderungen: [] }), 'nichts Neues');
  assert.equal(E.laufText({ aenderungen: [{ art: 'angelegt', klasse: 'Ansicht', kennzeichen: 'a' }, { art: 'angelegt', klasse: 'Ansicht', kennzeichen: 'b' }] }), '2 Ansichten');
  assert.deepEqual(E.hinweisZeilen(null), []);
});

// ---------------------------------------------------------------------------------------------- Projektbrowser: Knoten «Projektinformationen»

function ctxMit(elemente) {
  const nachKz = new Map(elemente.map((x) => [x.kennzeichen.toLowerCase(), x]));
  const nachId = new Map(elemente.map((x) => [x.id, x]));
  return {
    klassen: klassen, systemtypen: [], medien: [], gewerke: [], katalogKlassen: klassen,
    derKlasse: (k) => elemente.filter((x) => x.klasse === k),
    element: (b) => (b ? (nachId.get(b) ?? nachKz.get(String(b).toLowerCase()) ?? null) : null),
    plansaetze: null, familien: [], bibliothek: [], koordination: { kollisionen: null, aussparungen: null, typen: null, fehler: {} },
    idVon: (kz) => nachKz.get(String(kz).toLowerCase())?.id ?? null,
  };
}
const suche = (wurzeln, pruefung) => { let r = null; const lauf = (k) => { if (!r && pruefung(k)) r = k; (k.kinder ?? []).forEach(lauf); }; wurzeln.forEach(lauf); return r; };

test('Projektbrowser: Gebäude › Projektinformationen erscheint mit dem Element, Art «projektinfo»; ohne Element kein Knoten', () => {
  const mit = ctxMit([{ id: 'g1', kennzeichen: 'GEB', klasse: 'Gebaeude' }, { id: 'p1', kennzeichen: 'PI-1', klasse: 'Projektinformation', werte: {} }]);
  const b = baumBauen(gliederung('gebaeude'), mit);
  const gebaeude = b.find((k) => k.key === 'w:gebaeude');
  const n = gebaeude.kinder.find((k) => k.art === 'projektinfo');
  assert.ok(n, 'Knoten da');
  assert.equal(n.label, 'Projektinformationen');
  assert.equal(n.kz, 'PI-1');
  assert.equal(n.klasse, 'Projektinformation');
  assert.equal(n.sym, 'info');
  const ohne = baumBauen(gliederung('gebaeude'), ctxMit([{ id: 'g1', kennzeichen: 'GEB', klasse: 'Gebaeude' }]));
  assert.equal(suche(ohne, (k) => k.art === 'projektinfo'), null);
  // in einer Fachgliederung (Auswahl nach Gewerk) steht er nicht
  const san = baumBauen(gliederung('sanitaer'), mit);
  assert.equal(suche(san, (k) => k.art === 'projektinfo'), null);
});

// ---------------------------------------------------------------------------------------------- Verdrahtung

test('Verdrahtung: Aktionen im Register Ansicht › Erstellen (klein, kein neues Register), Symbole, Dialoge, Tooltip-Zeile', () => {
  const ansicht = lies('./aktionen-ansicht.js');
  for (const [id, symbol, dialog] of [['ansicht.einlageplan', 'einlageplan', "einlageplanDialog('einlage')"], ['ansicht.grundleitungsplan', 'grundleitungsplan', "einlageplanDialog('erdreich')"]]) {
    const zeile = ansicht.split('\n').find((z) => z.includes(`id: '${id}'`));
    assert.ok(zeile, id);
    assert.match(zeile, new RegExp(`symbol: '${symbol}'`));
    assert.match(zeile, /register: 'ansicht', gruppe: 'Erstellen', groesse: 'klein', nurSymbol: true/);
    assert.ok(zeile.includes(dialog));
    assert.match(zeile, /kuerzel: null/);
  }
  assert.match(ansicht, /id: 'liste\.csv'[\s\S]*symbol: 'listeCsv'/);
  assert.match(ansicht, /kollisionen: \['Kollisionsliste', 'KOLLISIONEN'\]/);
  assert.match(ansicht, /aussparungsliste: \['Aussparungsliste', 'AUSSPARUNGEN'\]/);
  // beide Listen sind Werte der Katalogeigenschaft Ansicht.liste
  const werte = klasse('Ansicht').eigenschaften.find((e) => e.name === 'liste').auswahl;
  assert.ok(werte.includes('kollisionen') && werte.includes('aussparungsliste'));
  // eigene Symbole (nicht das Ersatzquadrat) und verschieden von den Nachbarn
  const ersatz = S.symbol('gibtsnicht');
  for (const n of ['einlageplan', 'grundleitungsplan', 'listeCsv']) { assert.notEqual(S.symbol(n), ersatz, n); assert.ok(S.symbol(n).startsWith('<svg')); }
  assert.notEqual(S.symbol('einlageplan'), S.symbol('grundriss'));
  assert.notEqual(S.symbol('einlageplan'), S.symbol('grundleitungsplan'));
  // Tooltip endet bei allen Knöpfen mit der Hilfezeile (menueband.js hängt sie an)
  assert.match(lies('./menueband.js'), /tip \+= '\\n' \+ HILFE_ZEILE/);
  assert.match(lies('./hilfe.js'), /HILFE_ZEILE = 'F1: Hilfe zu diesem Befehl'/);
  // Stylesheet eingebunden, Dialogwerkzeuge dahinter
  assert.match(lies('../../index.html'), /css\/nachbearbeitung-n3\.css/);
  assert.ok(lies('../../css/nachbearbeitung-n3.css').includes('.epl-plaene'));
  // keine neuen Register im Menüband
  assert.match(lies('./menueband.js'), /const REGISTER = \[\['datei', 'Datei'\], \['architektur', 'Architektur'\], \['huelle', 'Hülle'\], \['gebaeudetechnik', 'Gebäudetechnik'\], \['einfuegen', 'Einfügen'\], \['beschriften', 'Beschriften'\], \['aendern', 'Ändern'\], \['koordination', 'Koordination'\], \['ansicht', 'Ansicht'\], \['xr', 'XR'\], \['verwalten', 'Verwalten'\]\]/);
});

test('Verdrahtung: der Einlageplan-Dialog benutzt nur Kernbefehl und Kernabfrage; der Projektinformationen-Dialog nur element_aendern/element_anlegen', () => {
  const epl = lies('./einlageplaene-dialog.js');
  assert.match(epl, /abfrage\('verlegung'/);
  assert.match(epl, /sende\(T\.BEFEHL/);
  assert.match(epl, /trockenlauf: true/);
  const pi = lies('./projektinfo-dialog.js');
  assert.match(pi, /'element_aendern' : 'element_anlegen'/);
  assert.ok(!/zustand\.elemente\.(set|delete)/.test(pi + epl), 'der Zustand wird nie direkt verändert');
  const projekt = lies('./aktionen-projekt.js');
  assert.match(projekt, /ausfuehren: \(\) => projektInfoDialog\(\)/);
  assert.match(projekt, /kuerzelZeilen\(\[\.\.\.aktionen\.values\(\)\], hinweisText\)/);
  const browser = lies('./projektbrowser.js');
  assert.match(browser, /art === 'projektinfo'\) ausfuehren\('verwalten\.projektinfo'\)/);
  assert.match(browser, /eintrag\('CSV …', 'liste\.csv', p\)/);
  const liste = lies('../ansichten/liste.js');
  assert.match(liste, /li-csv/);
  assert.match(liste, /liste-csv-dialog\.js/);
});

test('Menüband: nurSymbol zeigt einen Knopf auch in den Stufen G und M nur mit Symbol (Name im Tooltip); die Signatur der Messung kennt es', () => {
  const m = lies('./menueband.js');
  assert.match(m, /if \(a\.nurSymbol && stil === 'mittel'\) stil = 'klein';/);
  assert.match(m, /i\.nurSymbol \? 1 : 0/);
  assert.match(lies('./aktionen-ansicht.js'), /groesse: 'klein', nurSymbol: true/);
});
