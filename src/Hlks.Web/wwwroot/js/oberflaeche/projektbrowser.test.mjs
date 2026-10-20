// Tests der Gliederungen des Projektbrowsers (G1, entwurf/projektbrowser-gliederung.md) ohne Browser: die Tabelle der Gliederungen und Äste, die Zuordnung zum Gewerk
// (Klasse, System, Ansicht, Planblatt, Familie), «Weitere» vollständig (die Summe aller Elemente ist in jeder Gliederung die von «Gebäude»), die Suche, die Koordination
// (Kollisionen nach Status, Aussparungen nach Geschoss), die Farben der Gewerke (aus dem Katalog) und ihr Kontrast in hell und dunkel.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/projektbrowser.test.mjs
// Die Daten sind FIKTIV; Klassen, Systemtypen, Medien und Gewerke sind die echten Kataloge (kataloge/*.json), damit die Zuordnung zum Gewerk gegen die Wirklichkeit geprüft wird.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as G from './projektbrowser-gliederung.js';
import { baumBauen, baumBauenListe } from './projektbrowser-aeste.js';

const datei = (n) => new URL(n, import.meta.url);
const lies = (n) => readFileSync(datei(n), 'utf8');
const katalog = (n) => JSON.parse(lies('../../../../../kataloge/' + n).replace(/^﻿/, ''));

// ---------------------------------------------------------------------------------------------- Kataloge (echt) wie die Abfrage katalog sie liefert

/** Klassen mit aufgelöstem Gewerk (Basisklasse, wie der Kern: d.Gewerk ??= bd.Gewerk). */
function klassenAufgeloest() {
  const alle = katalog('klassen.json');
  const nach = new Map(alle.map((k) => [k.name, k]));
  return alle.filter((k) => !k.abstrakt).map((k) => {
    let g = k.gewerk, b = k.basis;
    while (!g && b) { const bd = nach.get(b); g = bd?.gewerk; b = bd?.basis; }
    return { name: k.name, schicht: k.schicht || nach.get(k.basis)?.schicht, basis: k.basis, gewerk: g, beschreibung: k.beschreibung };
  });
}
const GEW = katalog('gewerke.json');

// ---------------------------------------------------------------------------------------------- Beispielprojekt (fiktiv)

let nr = 0;
const el = (klasse, kz, extra = {}) => ({ id: 'id' + (++nr), klasse, kennzeichen: kz, name: '', werte: {}, bezuege: {}, ...extra });

function projekt() {
  nr = 0;
  const e = [];
  const neu = (...a) => { const x = el(...a); e.push(x); return x; };
  neu('Gebaeude', 'GEB-1', { name: 'Haus' });
  neu('Geschoss', 'UG', { teilVon: 'GEB-1', name: 'Untergeschoss', werte: { kote: -2800 } });
  neu('Geschoss', 'EG', { teilVon: 'GEB-1', name: 'Erdgeschoss', werte: { kote: 0 } });
  neu('Geschoss', 'OG', { teilVon: 'GEB-1', name: 'Obergeschoss', werte: { kote: 2800 } });
  const flaeche = { art: 'polygon', punkte: [[0, 0], [4000, 0], [4000, 3000]] };
  neu('Raum', 'R-EG-1', { geschoss: 'EG', name: 'Bad', geometrie: flaeche });
  neu('Raum', 'R-OG-1', { geschoss: 'OG', name: 'Küche', geometrie: flaeche });
  neu('Raum', 'R-HK-1', { geschoss: 'OG', name: 'Büro aus HKAP', kennungen: { hkap: 'H-1' } });   // noch ohne Geometrie
  // Anlagen, Systeme, Stränge (Sanitär, Heizung, Lüftung, gemischt, leer)
  neu('Anlage', 'A-SAN', { name: 'Sanitär' }); neu('Anlage', 'A-HZG', { name: 'Heizzentrale' }); neu('Anlage', 'A-LUE', { name: 'Lüftung' });
  neu('Anlage', 'A-MIX', { name: 'Technikzentrale' }); neu('Anlage', 'A-LEER', { name: 'Noch leer' });
  neu('System', 'KW', { name: 'Trinkwasser kalt', teilVon: 'A-SAN', werte: { systemtyp: 'Trinkwasser kalt' } });
  neu('System', 'SW', { name: 'Schmutzwasser', teilVon: 'A-SAN', werte: { systemtyp: 'Schmutzwasser' } });
  neu('System', 'HZ-VL', { name: 'Heizung Vorlauf', teilVon: 'A-HZG', werte: { systemtyp: 'Heizung Vorlauf' } });
  neu('System', 'ZUL', { name: 'Zuluft', teilVon: 'A-LUE', werte: { systemtyp: 'Zuluft' } });
  neu('System', 'MIX-WW', { name: 'Warmwasser Technik', teilVon: 'A-MIX', werte: { systemtyp: 'Trinkwasser warm' } });
  neu('System', 'MIX-HZ', { name: 'Heizung Technik', teilVon: 'A-MIX', werte: { systemtyp: 'Heizung Rücklauf' } });
  neu('System', 'LOSE', { name: 'System ohne Anlage', werte: { systemtyp: 'Abluft' } });
  neu('System', 'UNBEKANNT', { name: 'System ohne Typ' });
  neu('Strang', 'ST-KW1', { teilVon: 'KW' }); neu('Strang', 'ST-HZ1', { teilVon: 'HZ-VL' }); neu('Strang', 'ST-ZUL1', { teilVon: 'ZUL' });
  // Ansichten
  const an = (kz, werte, extra = {}) => neu('Ansicht', kz, { name: kz, ...extra, werte });
  an('AN-S', { art: 'grundriss', gewerke: ['sanitaer', 'architektur'] });
  an('AN-H', { art: 'grundriss', gewerke: ['heizung'] });
  an('AN-L', { art: '3d', gewerke: ['lueftung'] });
  an('AN-ARCH', { art: 'grundriss', gewerke: ['architektur'] });
  an('AN-LISTE', { art: 'liste', liste: 'raumliste' });
  an('AN-SYS', { art: 'schema', systeme: ['HZ-VL'] });            // kein Gewerk eingetragen, aber ein Heizungssystem
  an('AN-MULTI', { art: 'schnitt', gewerke: ['heizung', 'sanitaer'] });
  an('AN-AUS', { art: 'grundriss', darstellung: 'aussparungen' });
  an('AN-EINL', { art: 'grundriss', darstellung: 'einlagen', gewerke: ['sanitaer'] });
  an('AN-KOLL', { art: 'liste', liste: 'kollisionen' });
  an('AN-HUELLE', { art: 'grundriss', darstellung: 'huelle' });
  an('AN-RG', { art: 'liste', liste: 'raumgrenzen' });
  an('AN-VORL', { art: 'grundriss', vorlage: true, gewerke: ['sanitaer'] });
  // Planblätter mit Ansichtsfenstern
  const pb = (kz, nummer, werte = {}) => neu('Planblatt', kz, { werte: { nummer, titel: 'Plan ' + nummer, ...werte } });
  pb('PB-S', 'S01', { gewerk: 'sanitaer' }); pb('PB-H', 'H01', { gewerk: 'heizung' }); pb('PB-K', 'K01'); pb('PB-X', 'X01'); pb('PB-AUS', 'A01');
  const fenster = (kz, blatt, ansicht) => neu('Ansichtsfenster', kz, { teilVon: blatt, bezuege: { ansicht } });
  fenster('AF-1', 'PB-S', 'AN-S'); fenster('AF-2', 'PB-H', 'AN-H'); fenster('AF-3', 'PB-AUS', 'AN-AUS');
  // Unterlagen, Hülle
  neu('Unterlage', 'UL-IFC', { name: 'Architekt.ifc', werte: { art: 'ifc' } }); neu('Unterlage', 'UL-PDF', { name: 'Grundriss.pdf', werte: { art: 'pdf' } });
  neu('Material', 'MA-1', { name: 'Beton' }); neu('Aufbau', 'AU-1', { name: 'Aussenwand' }); neu('Waermebruecke', 'WB-1', { name: 'Balkon' });
  // Kollisionen und Aussparungen im Modell (Elemente), die Listen kommen aus den Abfragen (unten)
  neu('Kollision', 'KO-1'); neu('Kollision', 'KO-2'); neu('Kollision', 'KO-3'); neu('Aussparung', 'AS-1'); neu('Aussparung', 'AS-2'); neu('Aussparung', 'AS-3');
  neu('Typmaterial', 'TM-1', { werte: { typname: 'Wand Beton' } });
  return e;
}

const blatt = (planblatt, nummer, gewerk, gewerkName, herkunft = 'gesetzt') => ({ planblatt, nummer, titel: 'Plan ' + nummer, format: 'A1', gewerk, gewerkName, gewerkHerkunft: herkunft, gewerkQuelle: 'Test' });
const PLANSAETZE = { plansaetze: [
  { schluessel: 'auto:heizung', automatisch: true, gewerk: 'heizung', gewerkName: 'Heizung', name: 'Plansatz Heizung', blaetter: [blatt('PB-H', 'H01', 'heizung', 'Heizung')] },
  { schluessel: 'auto:sanitaer', automatisch: true, gewerk: 'sanitaer', gewerkName: 'Sanitär', name: 'Plansatz Sanitär', blaetter: [blatt('PB-S', 'S01', 'sanitaer', 'Sanitär')] },
  { schluessel: 'auto:koordination', automatisch: true, gewerk: 'koordination', gewerkName: 'Koordination', name: 'Plansatz Koordination', blaetter: [blatt('PB-K', 'K01', 'koordination', 'Koordination', 'vorschlag')] },
  { schluessel: 'auto:allgemein', automatisch: true, gewerk: 'allgemein', gewerkName: 'Allgemein', name: 'Plansatz Allgemein', blaetter: [blatt('PB-AUS', 'A01', 'allgemein', 'Allgemein', 'vorschlag')] },
] };   // PB-X fehlt absichtlich: der Kern hat das Blatt noch nicht gemeldet

const FAMILIEN = [
  { kennzeichen: 'F-WC', name: 'WC Muster', id: 'fid1', kategorie: 'WC', status: 'freigegeben', version: 1, typen: ['Standard'], gueltig: true },
  { kennzeichen: 'F-HK', name: 'Heizkörper Muster', id: 'fid2', kategorie: 'Heizkoerper', status: 'freigegeben', version: 1, typen: ['22-500'], gueltig: true },
  { kennzeichen: 'F-LG', name: 'Lüftungsgerät Muster', id: 'fid3', kategorie: 'Lueftungsgeraet', status: 'entwurf', version: 1, typen: ['L1'], gueltig: true },
  { kennzeichen: 'F-PU', name: 'Pumpe Muster', id: 'fid4', kategorie: 'Pumpe', status: 'freigegeben', version: 1, typen: ['P1'], gueltig: true },
];
const BIBLIOTHEK = [
  { id: 'B-WT', name: 'Waschtisch Bibliothek', kategorie: 'Waschtisch', status: 'freigegeben', version: 2, sichtbar: true, imProjekt: false, quelle: 'bibliothek' },
  { id: 'B-UNSICHTBAR', name: 'Nicht gewählt', kategorie: 'Waschtisch', status: 'freigegeben', version: 1, sichtbar: false, imProjekt: false, quelle: 'bibliothek' },
];

const KOLLISIONEN = { kollisionslauf: '2026-10-19 10:00 · Lauf 1 · hlks-architektur, hlks-hlks', zeilen: [
  { kollision: 'KO-1', art: 'hart', typ: 'durchdringung', paar: 'hlks-architektur', status: 'neu', geschoss: 'EG', beteiligte: 'T-1 gegen W-EG-S' },
  { kollision: 'KO-2', art: 'hart', typ: 'durchdringung', paar: 'hlks-hlks', status: 'neu', geschoss: 'EG', beteiligte: 'T-1 gegen T-2' },
  { kollision: 'KO-3', art: 'weich', typ: 'kontakt', paar: 'hlks-architektur', status: 'bestaetigt', geschoss: 'OG', beteiligte: 'T-3 gegen D-OG' },
  { kollision: 'KO-ALT', art: 'hart', typ: 'durchdringung', paar: 'hlks-vorwand', status: 'neu', geschoss: 'EG', beteiligte: 'alt' },   // Paar nicht geprüft: die Palette blendet es aus
] };
const AUSSPARUNGEN = { anzahl: 3, zeilen: [
  { aussparung: 'AS-1', nummer: 'A-EG-001', geschoss: 'EG', status: 'vorgeschlagen', masse: '200×200', art: 'aussparung' },
  { aussparung: 'AS-2', nummer: 'A-EG-002', geschoss: 'EG', status: 'freigegeben', masse: 'Ø 80', art: 'kernbohrung' },
  { aussparung: 'AS-3', nummer: 'A-OG-001', geschoss: 'OG', status: 'vorgeschlagen', masse: '300×150', art: 'aussparung' },
] };
const TYPEN = { unbekannt: [{ schluessel: 'Wand X', entitaeten: 'IfcWall' }], typen: [{ element: 'TM-1', typname: 'Wand Beton', kategorie: 'beton' }] };

function ctxVon(elemente, extra = {}) {
  const nachKz = new Map(elemente.map((x) => [x.kennzeichen.toLowerCase(), x]));
  const nachId = new Map(elemente.map((x) => [x.id, x]));
  return {
    klassen: klassenAufgeloest(), systemtypen: katalog('systemtypen.json'), medien: katalog('medien.json'), gewerke: GEW.gewerke, katalogKlassen: klassenAufgeloest(),
    derKlasse: (k) => elemente.filter((x) => x.klasse === k),
    element: (b) => (b ? (nachId.get(b) ?? nachKz.get(String(b).toLowerCase()) ?? null) : null),
    plansaetze: PLANSAETZE, familien: FAMILIEN, bibliothek: BIBLIOTHEK,
    koordination: { kollisionen: null, aussparungen: null, typen: null, fehler: {} },
    idVon: (kz) => nachKz.get(String(kz).toLowerCase())?.id ?? null,
    ...extra,
  };
}
const ctxMitDaten = () => {
  const c = ctxVon(projekt());
  return { ...c, koordination: { kollisionen: sicht(KOLLISIONEN), aussparungen: AUSSPARUNGEN, typen: TYPEN, fehler: {} } };
};
import * as KT from './koordination-text.js';
const sicht = (d) => KT.kollisionSicht(d, KT.KOLLISION_FILTER_VORGABE(), { zuletzt: [] });

const def = (id) => G.gliederung(id);
const baum = (id, c = ctxMitDaten()) => baumBauen(def(id), c);
const suche = (wurzeln, key) => { let r = null; const lauf = (k) => { if (k.key === key) r = k; (k.kinder ?? []).forEach(lauf); }; wurzeln.forEach(lauf); return r; };
const kzs = (k) => G.blaetter(k).filter((x) => x.art !== 'ansichtsfenster').map((x) => x.kz).filter(Boolean).sort();
/** Kennzeichen der Planblätter eines Astes (Blätter mit Ansichtsfenstern darunter zählen nicht als Blatt). */
const plaene = (k) => { const l = []; const lauf = (x) => { if (x.art === 'planblatt') l.push(x.kz); (x.kinder ?? []).forEach(lauf); }; lauf(k); return l.sort(); };
const labels = (k) => (k.kinder ?? []).map((x) => x.label);

// ---------------------------------------------------------------------------------------------- Tabelle

test('Tabelle: sechs Gliederungen in der Reihenfolge der Spezifikation, Gebäude zuerst und Vorgabe', () => {
  assert.deepEqual(G.GLIEDERUNGEN.map((g) => g.id), ['gebaeude', 'sanitaer', 'heizung', 'lueftung', 'koordination', 'energie']);
  assert.deepEqual(G.GLIEDERUNGEN.map((g) => g.titel), ['Gebäude', 'Sanitär', 'Heizung', 'Lüftung', 'Koordination', 'Energie und Umwelt']);
  assert.equal(G.GLIEDERUNG_VORGABE, 'gebaeude');
  assert.equal(G.gliederung(undefined).id, 'gebaeude');
  assert.equal(G.gliederung('gibtsnicht').id, 'gebaeude');
  assert.equal(G.gliederung('').id, 'gebaeude');
  assert.equal(G.gliederungGueltig('heizung'), true);
  assert.equal(G.gliederungGueltig('kaelte'), false);
});

test('Tabelle: Äste je Gliederung in der Reihenfolge der Spezifikation', () => {
  const q = (id) => def(id).aeste.map((a) => a.quelle);
  assert.deepEqual(q('gebaeude'), ['ansichten', 'vorlagen', 'plaene', 'gebaeude', 'huelle', 'systeme', 'unterlagen', 'familien']);
  for (const id of ['sanitaer', 'heizung', 'lueftung']) assert.deepEqual(q(id), ['ansichten', 'plaene', 'systeme', 'familien', 'gebaeude'], id);
  assert.deepEqual(q('koordination'), ['kollisionen', 'aussparungen', 'typenliste', 'unterlagen', 'plaene', 'ansichten']);
  assert.deepEqual(q('energie'), ['huelle', 'gebaeude', 'ansichten']);
});

test('Tabelle: jeder Eintrag hat eine bekannte Quelle, ein Symbol, einen Titel und ein Gewerk im Katalog; die Kennungen passen zum Server', () => {
  const quellen = new Set([...G.QUELLEN_REIHE, ...G.QUELLEN_ZUSATZ]);
  const ids = new Set(GEW.gewerke.map((g) => g.id));
  for (const g of G.GLIEDERUNGEN) {
    assert.match(g.id, /^[a-z][a-z0-9_]{0,31}$/, 'Kennung ' + g.id + ' passt zur Prüfung des Servers');
    assert.ok(g.titel && g.sym, g.id);
    if (g.gewerk) assert.ok(ids.has(g.gewerk), `Gewerk ${g.gewerk} fehlt in gewerke.json`);
    for (const a of g.aeste) assert.ok(quellen.has(a.quelle), `${g.id}: Quelle ${a.quelle}`);
  }
  assert.match(lies('../../../BenutzerEinstellungen.cs'), new RegExp('"' + G.EINSTELLUNG_GLIEDERUNG.replace('.', '\\.') + '"'), 'Schlüssel wie im Server');
});

test('Offen: Schlüssel im Browser-Speicher (Gebäude wie bisher) und Vorgabe je Gliederung', () => {
  assert.equal(G.offenSchluessel('gebaeude'), 'browserOffen');
  assert.equal(G.offenSchluessel('sanitaer'), 'browserOffen.sanitaer');
  assert.deepEqual(G.offenVorgabe(def('gebaeude')), ['w:ansichten', 'w:gebaeude', 'w:systeme', 'w:unterlagen']);
  assert.deepEqual(G.offenVorgabe(def('sanitaer')), ['w:ansichten', 'w:plaene', 'w:systeme', 'w:familien', 'w:gebaeude']);
  assert.ok(!G.offenVorgabe(def('sanitaer')).includes('x:weitere'), '«Weitere» ist anfangs eingeklappt');
});

// ---------------------------------------------------------------------------------------------- Gebäude wie heute

test('Gebäude: die acht Äste wie bisher, Pläne nach Gewerk, keine «Weitere»', () => {
  const w = baum('gebaeude');
  assert.deepEqual(w.map((k) => k.key), ['w:ansichten', 'w:vorlagen', 'w:plaene', 'w:gebaeude', 'w:huelle', 'w:systeme', 'w:unterlagen', 'w:familien']);
  assert.deepEqual(w.map((k) => k.label), ['Ansichten', 'Ansichtsvorlagen', 'Pläne', 'Gebäude', 'Hülle', 'Systeme', 'Unterlagen', 'Familien']);
  const plaene = suche(w, 'w:plaene');
  assert.deepEqual(labels(plaene), ['Heizung', 'Sanitär', 'Koordination', 'Allgemein', 'Weitere']);
  assert.equal(suche(w, 'p:gewerk:heizung').gewerk, 'heizung');
  assert.equal(suche(w, 'p:gewerk:neu').kinder.length, 1, 'PB-X: der Kern hat es noch nicht gemeldet');
  assert.equal(w.find((k) => k.key === 'x:weitere'), undefined);
  const geb = suche(w, 'w:gebaeude');
  assert.deepEqual(labels(geb), ['Haus', 'Nicht platzierte Räume']);
  assert.deepEqual(labels(geb.kinder[0]), ['Obergeschoss', 'Erdgeschoss', 'Untergeschoss'], 'Geschosse von oben nach unten');
  assert.equal(suche(w, 'w:ansichten').zahl, 12, 'Ansichten ohne Vorlage');
  const fam = suche(w, 'w:familien');
  assert.ok(fam.kinder.some((k) => k.key === 'f:katalog'), 'der Knoten «Katalog» steht in den Familien');
  assert.ok(suche(w, 'g:liste').kontext === 'liste');
});

test('Gebäude: Kategorien der Familien und Anlagen mit einem einzigen Gewerk tragen dessen Farbe', () => {
  const w = baum('gebaeude');
  assert.equal(suche(w, 'fk:WC').gewerk, 'sanitaer');
  assert.equal(suche(w, 'fk:Heizkoerper').gewerk, 'heizung');
  assert.equal(suche(w, 'fk:Pumpe').gewerk, undefined, 'Pumpe hat im Katalog kein Gewerk');
  const sys = suche(w, 'w:systeme');
  const anlage = (kz) => sys.kinder.find((k) => k.kz === kz);
  assert.equal(anlage('A-SAN').gewerk, 'sanitaer');
  assert.equal(anlage('A-HZG').gewerk, 'heizung');
  assert.equal(anlage('A-MIX').gewerk, undefined, 'gemischte Anlage: keine Farbe');
  assert.equal(anlage('A-LEER').gewerk, undefined);
});

// ---------------------------------------------------------------------------------------------- Zuordnung zum Gewerk

test('Gewerk der Klasse: aus dem Katalog, geerbt von der Basisklasse; allgemein und leer sind «ohne»', () => {
  const c = ctxVon([]);
  assert.equal(G.klassenGewerk(c, 'WC'), 'sanitaer');            // Basis Apparat
  assert.equal(G.klassenGewerk(c, 'Waschtisch'), 'sanitaer');
  assert.equal(G.klassenGewerk(c, 'Heizkoerper'), 'heizung');
  assert.equal(G.klassenGewerk(c, 'Lueftungsgeraet'), 'lueftung');
  assert.equal(G.klassenGewerk(c, 'Kanal'), 'lueftung');
  assert.equal(G.klassenGewerk(c, 'Wasserzaehler'), 'sanitaer');
  assert.equal(G.klassenGewerk(c, 'Raum'), 'architektur');
  assert.equal(G.klassenGewerk(c, 'Ansicht'), '', 'allgemein');
  assert.equal(G.klassenGewerk(c, 'Pumpe'), '', 'kein Gewerk');
  assert.equal(G.klassenGewerk(c, 'GibtsNicht'), '');
});

test('Gewerk des Systems: Systemtyp, sonst Medium; Strang über sein System; Anlage über ihre Systeme', () => {
  const c = ctxVon(projekt());
  const e = (kz) => c.element(kz);
  assert.equal(G.systemGewerk(c, e('KW')), 'sanitaer');
  assert.equal(G.systemGewerk(c, e('HZ-VL')), 'heizung');
  assert.equal(G.systemGewerk(c, e('ZUL')), 'lueftung');
  assert.equal(G.systemGewerk(c, e('LOSE')), 'lueftung', 'Abluft');
  assert.equal(G.systemGewerk(c, e('UNBEKANNT')), '');
  assert.equal(G.systemGewerk(c, null), '');
  assert.equal(G.systemGewerk(c, { werte: { medium: 'KW' } }), 'sanitaer', 'über das Medium, wenn der Systemtyp fehlt');
  assert.equal(G.strangGewerk(c, e('ST-HZ1')), 'heizung');
  assert.equal(G.strangGewerk(c, { teilVon: 'gibtsnicht' }), '');
});

test('Gewerke der Ansicht: «gewerke» und die Systeme der Ansicht; Thema aus Darstellung und Liste', () => {
  const c = ctxVon(projekt());
  const a = (kz) => c.element(kz);
  assert.deepEqual(G.ansichtGewerke(c, a('AN-S')), ['sanitaer', 'architektur']);
  assert.deepEqual(G.ansichtGewerke(c, a('AN-SYS')), ['heizung'], 'über das System HZ-VL');
  assert.deepEqual(G.ansichtGewerke(c, a('AN-LISTE')), []);
  assert.deepEqual(G.ansichtGewerke(c, a('AN-MULTI')), ['heizung', 'sanitaer']);
  assert.equal(G.hauptGewerk(['architektur', 'sanitaer']), 'sanitaer', 'Fachgewerk vor Architektur');
  assert.equal(G.hauptGewerk(['architektur']), 'architektur');
  assert.equal(G.hauptGewerk([]), '');
  assert.equal(G.ansichtThema(a('AN-AUS')), 'koordination');
  assert.equal(G.ansichtThema(a('AN-EINL')), 'koordination');
  assert.equal(G.ansichtThema(a('AN-KOLL')), 'koordination');
  assert.equal(G.ansichtThema(a('AN-HUELLE')), 'energie');
  assert.equal(G.ansichtThema(a('AN-RG')), 'energie');
  assert.equal(G.ansichtThema(a('AN-S')), '');
  assert.equal(G.ansichtThema(a('AN-LISTE')), '');
});

test('Gewerk des Planblatts: Plansatz (gesetzt oder Vorschlag), sonst die Eigenschaft; Thema über die Ansichten des Blatts', () => {
  const c = ctxVon(projekt());
  assert.equal(G.planGewerk(c, c.element('PB-S')), 'sanitaer');
  assert.equal(G.planGewerk(c, c.element('PB-K')), 'koordination');
  assert.equal(G.planGewerk(c, c.element('PB-AUS')), '', 'allgemein');
  assert.equal(G.planGewerk(c, c.element('PB-X')), '', 'weder Plansatz noch Eigenschaft');
  assert.equal(G.planGewerk({ ...c, plansaetze: null }, c.element('PB-H')), 'heizung', 'ohne Antwort des Kerns: die Eigenschaft');
  assert.equal(G.planThema(c, c.element('PB-AUS')), 'koordination');
  assert.equal(G.planThema(c, c.element('PB-S')), '');
});

test('Gewerk der Familie: das ihrer Kategorie (Klasse)', () => {
  const c = ctxVon([]);
  assert.deepEqual(FAMILIEN.map((f) => G.MERKMALE.familien(c, f).haupt), ['sanitaer', 'heizung', 'lueftung', '']);
  assert.equal(G.MERKMALE.familien(c, BIBLIOTHEK[0]).haupt, 'sanitaer');
});

test('Auswahl: Gewerk ($ = das der Gliederung), Thema, Art; eine Liste von Auswahlen genügt mit einer', () => {
  const d = { gewerk: 'heizung' };
  const m = { gewerke: ['heizung', 'sanitaer'], thema: 'koordination', art: 'ifc' };
  assert.equal(G.passt({ gewerk: '$' }, m, d), true);
  assert.equal(G.passt({ gewerk: 'lueftung' }, m, d), false);
  assert.equal(G.passt({ thema: 'koordination' }, m, d), true);
  assert.equal(G.passt({ thema: 'energie' }, m, d), false);
  assert.equal(G.passt({ art: ['ifc', 'dwg'] }, m, d), true);
  assert.equal(G.passt({ art: ['pdf'] }, m, d), false);
  assert.equal(G.passt([{ gewerk: 'lueftung' }, { thema: 'koordination' }], m, d), true);
  assert.equal(G.passt([{ gewerk: 'lueftung' }, { thema: 'energie' }], m, d), false);
  assert.equal(G.passt({ gewerk: '$', thema: 'energie' }, m, d), false, 'alle Bedingungen eines Eintrags müssen stimmen');
  assert.equal(G.passt(undefined, m, d), true);
  assert.equal(G.passt({ unbekannt: 1 }, m, d), false);
});

// ---------------------------------------------------------------------------------------------- Fachgliederungen

test('Sanitär: oben nur Sanitär-Ansichten, -Pläne, -Anlagen und -Familien, Räume, darunter «Weitere» (eingeklappt)', () => {
  const w = baum('sanitaer');
  assert.deepEqual(w.map((k) => k.key), ['w:ansichten', 'w:plaene', 'w:systeme', 'w:familien', 'w:gebaeude', 'x:weitere']);
  assert.deepEqual(w.map((k) => k.label), ['Ansichten', 'Pläne', 'Anlagen', 'Familien', 'Räume', 'Weitere']);
  const ansichten = G.blaetter(suche(w, 'w:ansichten')).map((k) => k.kz).sort();
  assert.deepEqual(ansichten, ['AN-EINL', 'AN-MULTI', 'AN-S'], 'Gewerk sanitaer in «gewerke» (auch zusammen mit anderen)');
  assert.deepEqual(plaene(suche(w, 'w:plaene')), ['PB-S']);
  const anlagen = suche(w, 'w:systeme');
  assert.deepEqual(anlagen.kinder.map((k) => k.kz), ['A-MIX', 'A-SAN'], 'Anlagen mit mindestens einem Sanitärsystem');
  assert.deepEqual(kzs(anlagen), ['MIX-WW', 'ST-KW1', 'SW'], 'Blätter: Strang bzw. System ohne Strang');
  const sysKz = (a) => a.kinder.map((s) => s.kz);
  assert.deepEqual(sysKz(anlagen.kinder.find((k) => k.kz === 'A-MIX')), ['MIX-WW'], 'von der gemischten Anlage nur das Sanitärsystem');
  assert.deepEqual(sysKz(anlagen.kinder.find((k) => k.kz === 'A-SAN')), ['KW', 'SW']);
  const fam = suche(w, 'w:familien');
  assert.deepEqual(fam.kinder.map((k) => k.label), ['Waschtisch', 'WC'], 'Kategorien des Gewerks, auch die Bibliotheksfamilie nach Auswahl');
  assert.equal(fam.zahl, 2);
  assert.ok(!fam.kinder.some((k) => k.key === 'f:katalog'), 'der Katalog steht unter «Weitere»');
  assert.equal(suche(w, 'w:gebaeude').label, 'Räume');
});

test('Heizung und Lüftung: dieselbe Tabelle, andere Elemente', () => {
  const h = baum('heizung');
  assert.deepEqual(kzs(suche(h, 'w:ansichten')), ['AN-H', 'AN-MULTI', 'AN-SYS']);
  assert.deepEqual(plaene(suche(h, 'w:plaene')), ['PB-H']);
  assert.deepEqual(suche(h, 'w:systeme').kinder.map((k) => k.kz), ['A-HZG', 'A-MIX']);
  assert.deepEqual(suche(h, 'w:familien').kinder.map((k) => k.label), ['Heizkoerper']);
  const l = baum('lueftung');
  assert.deepEqual(kzs(suche(l, 'w:ansichten')), ['AN-L']);
  assert.deepEqual(plaene(suche(l, 'w:plaene')), []);
  assert.deepEqual(suche(l, 'w:systeme').kinder.map((k) => k.kz).concat(suche(l, 'g:ohne-anlage').kinder.map((k) => k.kz)).filter(Boolean), ['A-LUE', 'LOSE']);
  assert.deepEqual(suche(l, 'w:familien').kinder.map((k) => k.label), ['Lueftungsgeraet']);
});

test('Fachäste tragen die Farbe ihres Gewerks, «Räume» nicht', () => {
  const w = baum('sanitaer');
  for (const k of ['w:ansichten', 'w:plaene', 'w:systeme', 'w:familien']) assert.equal(suche(w, k).gewerk, 'sanitaer', k);
  assert.equal(suche(w, 'w:gebaeude').gewerk, undefined);
  assert.equal(suche(w, 'x:weitere').gewerk, undefined);
  assert.equal(suche(baum('heizung'), 'w:ansichten').gewerk, 'heizung');
  assert.equal(suche(baum('lueftung'), 'w:plaene').gewerk, 'lueftung');
});

test('«Weitere»: die anderen Gewerke je in ihrer Farbe, «Ohne Gewerk» zuletzt, dann Hülle, Unterlagen, Vorlagen, Katalog', () => {
  const w = baum('sanitaer');
  const weitere = suche(w, 'x:weitere');
  const gruppen = weitere.kinder.filter((k) => k.key.startsWith('x:gw:'));
  assert.deepEqual(gruppen.map((k) => k.label), ['Heizung', 'Lüftung', 'Architektur', 'Koordination', 'Ohne Gewerk']);
  assert.deepEqual(gruppen.map((k) => k.gewerk), ['heizung', 'lueftung', 'architektur', 'koordination', undefined]);
  assert.ok(!gruppen.some((k) => k.gewerk === 'sanitaer'), 'das eigene Gewerk hat keine Gruppe unter «Weitere»');
  const heizung = gruppen[0];
  assert.deepEqual(heizung.kinder.map((k) => k.label), ['Ansichten', 'Pläne', 'Systeme', 'Familien']);
  assert.deepEqual(kzs(heizung.kinder[0]), ['AN-H', 'AN-SYS'].filter((x) => x !== 'AN-MULTI'), 'AN-MULTI steht schon oben (Sanitär)');
  assert.deepEqual(plaene(heizung.kinder[1]), ['PB-H']);
  const uebrige = weitere.kinder.filter((k) => !k.key.startsWith('x:gw:')).map((k) => k.label);
  assert.deepEqual(uebrige, ['Ansichtsvorlagen', 'Hülle', 'Unterlagen', 'Katalog']);
  const ohne = gruppen.at(-1);
  assert.ok(kzs(ohne).includes('AN-LISTE') && kzs(ohne).includes('PB-X') && kzs(ohne).includes('F-PU') && kzs(ohne).includes('UNBEKANNT') && kzs(ohne).includes('A-LEER'), 'Elemente ohne Gewerk: ' + kzs(ohne));
});

test('«Weitere» hat Schlüssel mit Präfix, die Ursprungsschlüssel bleiben; kein Schlüssel kommt in einem Baum doppelt vor', () => {
  for (const g of G.GLIEDERUNGEN) {
    const alle = [];
    const lauf = (k) => { alle.push(k.key); (k.kinder ?? []).forEach(lauf); };
    baum(g.id).forEach(lauf);
    const doppelt = alle.filter((k, i) => alle.indexOf(k) !== i);
    assert.deepEqual(doppelt, [], g.id);
  }
  const k = suche(baum('sanitaer'), 'x:w:vorlagen');
  assert.ok(k, 'Vorlagen unter Weitere mit Präfix');
  assert.equal(G.identitaet(k), 'w:vorlagen');
});

test('Zahlen an den Ästen: Fachäste, Weitere (Summe der Blätter) und Gruppen', () => {
  const w = baum('sanitaer');
  assert.equal(suche(w, 'w:ansichten').zahl, 3);
  assert.equal(suche(w, 'w:familien').zahl, 2);
  const weitere = suche(w, 'x:weitere');
  assert.equal(weitere.zahl, G.blattZahl(weitere));
  assert.ok(weitere.zahl > 10);
  for (const gr of weitere.kinder.filter((k) => k.key.startsWith('x:gw:'))) assert.equal(gr.zahl, G.blattZahl(gr), gr.label);
});

// ---------------------------------------------------------------------------------------------- Vollständigkeit

/** Die Menge der Elemente einer Gliederung ohne die Zusatzäste der Koordination (Kollisionen, Aussparungen, Typenliste: nur dort). */
function elementeOhneZusatz(wurzeln) {
  const m = G.elementMenge(wurzeln);
  const zusatz = new Set();
  const lauf = (k) => { if (['kollision', 'aussparung', 'typ', 'typ-unbekannt', 'hinweis'].includes(k.art)) zusatz.add(G.identitaet(k)); (k.kinder ?? []).forEach(lauf); };
  wurzeln.forEach(lauf);
  for (const z of zusatz) m.delete(z);
  return m;
}

test('«Weitere» ist vollständig: die Summe aller Elemente ist in jeder Gliederung die von «Gebäude»', () => {
  const c = ctxMitDaten();
  const gebaeude = G.elementMenge(baumBauen(def('gebaeude'), c));
  assert.ok(gebaeude.size > 60, 'das Beispielprojekt ist nicht leer: ' + gebaeude.size);
  for (const g of G.GLIEDERUNGEN) {
    const m = elementeOhneZusatz(baumBauen(g, c));
    assert.deepEqual([...gebaeude].filter((x) => !m.has(x)), [], `${g.id}: fehlt gegenüber «Gebäude»`);
    assert.deepEqual([...m].filter((x) => !gebaeude.has(x)), [], `${g.id}: zusätzlich gegenüber «Gebäude»`);
  }
});

test('Kein Element steht in einer Gliederung zweimal (ausser einer Anlage, die Systeme mehrerer Gewerke hat)', () => {
  const c = ctxMitDaten();
  for (const g of G.GLIEDERUNGEN) {
    const zaehler = new Map();
    const lauf = (k) => { if (k.art !== 'ast') zaehler.set(G.identitaet(k), (zaehler.get(G.identitaet(k)) ?? 0) + 1); (k.kinder ?? []).forEach(lauf); };
    baumBauen(g, c).forEach(lauf);
    const doppelt = [...zaehler].filter(([, n]) => n > 1).map(([k]) => k);
    const nurAnlagen = doppelt.every((k) => k === 'e:' + c.element('A-MIX').id);
    assert.ok(nurAnlagen, `${g.id}: doppelt ${doppelt.join(', ')}`);
  }
  // die gemischte Anlage steht in Sanitär oben und unter «Weitere» (Heizung), jedes ihrer Systeme genau einmal
  const w = baum('sanitaer');
  assert.equal(suche(w, 'w:systeme').kinder.filter((k) => k.kz === 'A-MIX').length, 1);
  assert.equal(G.blaetter(w.find((k) => k.key === 'x:weitere')).filter((k) => k.kz === 'MIX-HZ').length, 1);
});

test('Vollständigkeit auch ohne Daten: leeres Projekt, nur Katalog', () => {
  const c = ctxVon([], { plansaetze: null, familien: [], bibliothek: [] });
  const gebaeude = G.elementMenge(baumBauen(def('gebaeude'), c));
  for (const g of G.GLIEDERUNGEN) assert.deepEqual([...elementeOhneZusatz(baumBauen(g, c))].sort(), [...gebaeude].sort(), g.id);
  assert.equal(baum('sanitaer', c).find((k) => k.key === 'x:weitere')?.zahl, G.blattZahl(baum('sanitaer', c).find((k) => k.key === 'x:weitere')));
});

test('Erweiterbar ohne neuen Code: eine neue Gliederung (Elektro) ist ein Eintrag der Tabelle', () => {
  const elektro = { id: 'elektro', titel: 'Elektro', sym: 'rohr', gewerk: 'elektro', aeste: [
    { quelle: 'ansichten', auswahl: { gewerk: '$' }, farbe: true }, { quelle: 'systeme', titel: 'Anlagen', auswahl: { gewerk: '$' }, farbe: true }, { quelle: 'gebaeude', titel: 'Räume' }] };
  const tabelle = [...G.GLIEDERUNGEN, elektro];
  assert.equal(G.gliederung('elektro', tabelle).titel, 'Elektro');
  const c = ctxMitDaten();
  // das Projekt hat kein Elektrosystem: alles steht unter «Weitere»
  const w = baumBauen(elektro, c);
  assert.deepEqual(w.map((k) => k.label), ['Ansichten', 'Anlagen', 'Räume', 'Weitere']);
  const gebaeude = G.elementMenge(baumBauen(def('gebaeude'), c));
  assert.deepEqual([...elementeOhneZusatz(w)].sort(), [...gebaeude].sort());
  assert.deepEqual(w[0].gewerk, 'elektro');
});

// ---------------------------------------------------------------------------------------------- Koordination

test('Koordination: Äste in der Reihenfolge der Spezifikation, Fachfarbe, Weitere mit Fachästen, Hülle, Familien, Vorlagen', () => {
  const w = baum('koordination');
  assert.deepEqual(w.map((k) => k.label), ['Kollisionen', 'Aussparungen', 'Typenliste', 'Unterlagen', 'Pläne', 'Ansichten', 'Weitere']);
  assert.ok(w.slice(0, 6).every((k) => k.gewerk === 'koordination'));
  assert.deepEqual(kzs(suche(w, 'w:unterlagen')), ['UL-IFC'], 'IFC');
  assert.deepEqual(plaene(suche(w, 'w:plaene')), ['PB-AUS', 'PB-K'], 'Plan des Gewerks Koordination und Plan mit Aussparungsansicht');
  assert.deepEqual(kzs(suche(w, 'w:ansichten')), ['AN-AUS', 'AN-EINL', 'AN-KOLL']);
  const weitere = suche(w, 'x:weitere');
  const namen = weitere.kinder.map((k) => k.label);
  for (const n of ['Heizung', 'Lüftung', 'Sanitär', 'Hülle', 'Ansichtsvorlagen', 'Katalog', 'Unterlagen']) assert.ok(namen.includes(n), n + ' in ' + namen);
  assert.deepEqual(kzs(suche(w, 'x:w:unterlagen') ?? { kinder: [] }), ['UL-PDF']);
});

test('Koordination › Kollisionen nach Status, darunter das Paar; nicht geprüfte Paare ausgeblendet wie in der Palette', () => {
  const w = baum('koordination');
  const k = suche(w, 'w:kollisionen');
  assert.equal(k.zahl, 3, 'KO-ALT (Paar Vorwände) ist wie in der Palette ausgeblendet');
  assert.deepEqual(labels(k), ['neu', 'bestätigt']);
  assert.deepEqual(k.kinder.map((s) => s.zahl), [2, 1]);
  assert.deepEqual(labels(k.kinder[0]), ['Architektur', 'HLKS']);
  assert.deepEqual(kzs(k.kinder[0]), ['KO-1', 'KO-2']);
  assert.equal(k.kontext, 'kollisionen');
  const zeile = G.blaetter(k).find((x) => x.kz === 'KO-1');
  assert.equal(zeile.art, 'kollision');
  assert.ok(zeile.id, 'verweist auf das Element');
  assert.match(zeile.sub, /T-1 gegen W-EG-S/);
  // mit dem Schalter der Palette («auch nicht geprüfte Paare») wären es vier – derselbe Weg, dieselbe Sicht
  assert.equal(KT.kollisionSicht(KOLLISIONEN, { ...KT.KOLLISION_FILTER_VORGABE(), auchNichtGeprueft: true }).zeilen.length, 4);
});

test('Koordination › Aussparungen nach Geschoss (oben zuerst), darunter der Status', () => {
  const w = baum('koordination');
  const a = suche(w, 'w:aussparungen');
  assert.equal(a.zahl, 3);
  assert.deepEqual(labels(a), ['OG Obergeschoss', 'EG Erdgeschoss']);
  assert.deepEqual(a.kinder.map((g) => g.zahl), [1, 2]);
  assert.deepEqual(labels(a.kinder[1]), ['vorgeschlagen', 'freigegeben']);
  const zeile = G.blaetter(a).find((x) => x.kz === 'AS-1');
  assert.equal(zeile.art, 'aussparung');
  assert.equal(zeile.label, 'A-EG-001 · AS-1');
  assert.equal(zeile.sub, '200×200');
  assert.equal(a.kontext, 'aussparungen');
});

test('Koordination › Typenliste: unbekannte Typen und Einträge; Ast mit Kontext für den Dialog', () => {
  const t = suche(baum('koordination'), 'w:typenliste');
  assert.deepEqual(labels(t), ['Unbekannte Typen', 'Einträge']);
  assert.equal(t.zahl, 2);
  assert.equal(t.kontext, 'typenliste');
  assert.equal(G.blaetter(t).find((x) => x.kz === 'TM-1').klasse, 'Typmaterial');
});

test('Koordination › ohne Daten: Hinweis «Wird geladen …», bei Fehler dessen Text; die Äste bleiben', () => {
  const c = { ...ctxVon(projekt()), koordination: { kollisionen: null, aussparungen: null, typen: null, fehler: { aussparungen: 'Kein Projekt offen' } } };
  const w = baum('koordination', c);
  assert.equal(labels(suche(w, 'w:kollisionen'))[0], 'Wird geladen …');
  assert.match(labels(suche(w, 'w:aussparungen'))[0], /Fehler: Kein Projekt offen/);
  assert.equal(suche(w, 'w:kollisionen').zahl, undefined);
  assert.equal(suche(w, 'w:kollisionen').kinder[0].art, 'hinweis');
});

test('Koordination › leere Listen: die Äste stehen mit Zahl 0 da', () => {
  const c = { ...ctxVon(projekt()), koordination: { kollisionen: sicht({ zeilen: [] }), aussparungen: { zeilen: [] }, typen: { unbekannt: [], typen: [] }, fehler: {} } };
  const w = baum('koordination', c);
  assert.equal(suche(w, 'w:kollisionen').zahl, 0);
  assert.equal(suche(w, 'w:aussparungen').zahl, 0);
  assert.equal(suche(w, 'w:typenliste').zahl, 0);
});

test('Energie und Umwelt: Hülle, Gebäude → Geschoss → Räume, Ansichten mit Darstellung «Hülle» und Listen Raumgrenzen/Hüllbauteile', () => {
  const w = baum('energie');
  assert.deepEqual(w.map((k) => k.label), ['Hülle', 'Gebäude', 'Ansichten', 'Weitere']);
  assert.deepEqual(labels(suche(w, 'w:huelle')), ['Materialien', 'Aufbauten', 'Wärmebrücken']);
  assert.deepEqual(kzs(suche(w, 'w:ansichten')), ['AN-HUELLE', 'AN-RG']);
  assert.ok(w.slice(0, 3).every((k) => k.gewerk === 'energie'));
  const namen = suche(w, 'x:weitere').kinder.map((k) => k.label);
  for (const n of ['Heizung', 'Sanitär', 'Unterlagen', 'Ansichtsvorlagen', 'Katalog']) assert.ok(namen.includes(n), n + ' in ' + namen);
});

// ---------------------------------------------------------------------------------------------- Suche

test('Suche durchsucht die ganze Gliederung samt «Weitere» und behält Treffer mit ihren Vorfahren', () => {
  const w = baum('sanitaer');
  const treffer = w.map((k) => G.filtern(k, 'an-h')).filter(Boolean);   // AN-H liegt unter «Weitere» › Heizung › Ansichten (und der Treffer AN-HUELLE unter «Ohne Gewerk»)
  assert.deepEqual(treffer.map((k) => k.key), ['x:weitere']);
  const heizung = treffer[0].kinder.find((k) => k.label === 'Heizung');
  assert.ok(heizung, 'Gruppe Heizung bleibt');
  assert.deepEqual(heizung.kinder.map((k) => k.label), ['Ansichten', 'Pläne'], 'Pläne: das Ansichtsfenster AF-2 zeigt AN-H');
  assert.deepEqual(kzs(heizung.kinder[0]), ['AN-H']);
  assert.deepEqual(kzs(treffer[0]).filter((x) => x.startsWith('AN-')), ['AN-H', 'AN-HUELLE']);
  // ein Treffer oben steht oben, die Treffer unter «Weitere» zusätzlich
  const oben = w.map((x) => G.filtern(x, 'an-s')).filter(Boolean).map((x) => x.key);
  assert.ok(oben.includes('w:ansichten'), oben.join());
  assert.deepEqual(w.map((x) => G.filtern(x, 'gibtesnicht')).filter(Boolean), []);
  assert.equal(G.filtern(w[0], ''), w[0], 'ohne Suchtext unverändert');
});
test('Suche: Treffer unter «Weitere» klappen auf (alle Äste mit Treffern sind offen), Zahl zählt die Treffer', () => {
  const w = baum('sanitaer').map((k) => G.filtern(k, 'vorlage')).filter(Boolean);
  assert.ok(w.some((k) => k.key === 'x:weitere'));
  const offen = G.astSchluessel(w);
  assert.ok(offen.has('x:weitere') && offen.has('x:w:vorlagen'), [...offen].join());
  const weitere = w.find((k) => k.key === 'x:weitere');
  assert.equal(weitere.zahl, weitere.kinder.length);
  // Treffer in Koordination
  const ko = baum('koordination').map((k) => G.filtern(k, 'ko-3')).filter(Boolean);
  assert.deepEqual(kzs(ko[0]), ['KO-3']);
  assert.deepEqual(G.astSchluessel(ko).has('ko:bestaetigt'), true);
});

test('Suche trifft Beschriftung, Zusatz und Klasse; Gross- und Kleinschreibung egal', () => {
  const w = baum('gebaeude');
  const hit = (s) => w.map((k) => G.filtern(k, s)).filter(Boolean).length > 0;
  assert.ok(hit('erdgeschoss') && hit('wc') && hit('architekt.ifc') && hit('heizkoerper'));
  assert.ok(!hit('xyz-nicht'));
});

// ---------------------------------------------------------------------------------------------- Farben und Kontrast (nur aus dem Katalog)

test('Farben kommen aus dem Katalog gewerke.json; weder CSS noch Skripte legen sie fest', () => {
  const quellen = ['../../css/projektbrowser.css', 'projektbrowser.js', 'projektbrowser-aeste.js', 'projektbrowser-gliederung.js'].map(lies).join('\n');
  for (const g of GEW.gewerke) {
    assert.ok(!quellen.toUpperCase().includes(g.pastell.toUpperCase()), `Pastellton ${g.pastell} (${g.id}) steht nicht im Code`);
    assert.ok(!quellen.toUpperCase().includes(g.koordination.toUpperCase()), `Koordinationsfarbe ${g.koordination} (${g.id}) steht nicht im Code`);
  }
  const c = ctxVon([]);
  for (const g of GEW.gewerke) assert.deepEqual(G.gewerkFarben(c, g.id), g);
  assert.equal(G.gewerkFarben(c, 'allgemein'), null);
  assert.equal(G.gewerkFarben({ gewerke: [{ id: 'x', pastell: 'rot', koordination: '#000000' }] }, 'x'), null, 'ungültige Farbe: keine Hinterlegung');
  assert.equal(G.gewerkFarben({}, 'sanitaer'), null, 'Katalog ohne Gewerke: nichts gefärbt');
});

/** Wert einer CSS-Variable aus dem Block `selektor { … }` einer Datei. */
function cssVar(datei_, selektor, name) {
  const css = lies(datei_);
  const i = css.indexOf(selektor + ' {');
  assert.ok(i >= 0, selektor);
  const block = css.slice(i, css.indexOf('}', i));
  const m = block.match(new RegExp(`${name}:\\s*(#[0-9a-fA-F]{6})`));
  assert.ok(m, `${name} in ${selektor}`);
  return m[1];
}

test('Kontrast: Text auf der Hinterlegung der Gewerke mindestens 4,5 : 1 (WCAG AA) in hell und dunkel', () => {
  const hellText = G.hexRgb(cssVar('../../css/hlks.css', ':root', '--text'));
  const hellBrowser = G.hexRgb(cssVar('../../css/hlks.css', ':root', '--flaeche'));       // --browser: var(--flaeche)
  const dunkelText = G.hexRgb(cssVar('../../css/hlks.css', ':root[data-design="dunkel"]', '--text'));
  const dunkelBrowser = G.hexRgb(cssVar('../../css/hlks.css', ':root[data-design="dunkel"]', '--browser'));
  const hellLeise = G.hexRgb(cssVar('../../css/projektbrowser.css', ':root', '--gw-leise'));
  const dunkelLeise = G.hexRgb(cssVar('../../css/projektbrowser.css', ':root[data-design="dunkel"]', '--gw-leise'));
  const deckkraft = GEW.dunkelDeckkraft;
  assert.ok(deckkraft > 0 && deckkraft < 1);
  const zeilen = [];
  let min = 99;
  for (const g of GEW.gewerke) {
    const pastell = G.hexRgb(g.pastell);
    const dunkelGrund = G.ueberlagern(pastell, deckkraft, dunkelBrowser);
    const w = {
      hell: G.kontrast(hellText, pastell), hellLeise: G.kontrast(hellLeise, pastell),
      dunkel: G.kontrast(dunkelText, dunkelGrund), dunkelLeise: G.kontrast(dunkelLeise, dunkelGrund),
    };
    zeilen.push(`${g.id.padEnd(13)} hell ${w.hell.toFixed(1)} (leise ${w.hellLeise.toFixed(1)}) · dunkel ${w.dunkel.toFixed(1)} (leise ${w.dunkelLeise.toFixed(1)})`);
    for (const [art, v] of Object.entries(w)) { min = Math.min(min, v); assert.ok(v >= 4.5, `${g.id} ${art}: ${v.toFixed(2)}`); }
    // der Streifen (Koordinationsfarbe) ist kein Text: nur sichtbar gegen den Grund (mindestens 1,5 : 1 gegen den Pastellton, sonst verschwimmt er)
    assert.ok(G.kontrast(G.hexRgb(g.koordination), pastell) >= 1.5, `${g.id}: Streifen gegen Pastellton`);
  }
  console.log('# Kontrast Text auf Hinterlegung (Browser hell ' + cssVarHex(hellBrowser) + ', dunkel ' + cssVarHex(dunkelBrowser) + ', Deckkraft ' + deckkraft + '), kleinster Wert ' + min.toFixed(2) + '\n# ' + zeilen.join('\n# '));
});
const cssVarHex = (rgb) => '#' + rgb.map((c) => c.toString(16).padStart(2, '0')).join('');

test('Kontrastberechnung: bekannte Werte (Schwarz auf Weiss 21, gleiche Farbe 1)', () => {
  assert.equal(Math.round(G.kontrast([0, 0, 0], [255, 255, 255])), 21);
  assert.equal(G.kontrast([10, 20, 30], [10, 20, 30]), 1);
  assert.deepEqual(G.ueberlagern([255, 0, 0], 0.5, [0, 0, 0]), [128, 0, 0]);
  assert.deepEqual(G.hexRgb('#FAD4D4'), [250, 212, 212]);
});

// ---------------------------------------------------------------------------------------------- Verdrahtung

test('Verdrahtung: Projektbrowser nutzt Tabelle, Äste, Benutzereinstellungen und die Koordinationsdaten; keine zweite Abfrage der Koordination im Browser', () => {
  const pb = lies('projektbrowser.js');
  for (const s of ["from './projektbrowser-gliederung.js'", "from './projektbrowser-aeste.js'", "from './benutzer.js'", "from './koordination-daten.js'", 'imGrundrissZeigen', 'benutzerSetzen(EINSTELLUNG_GLIEDERUNG']) assert.ok(pb.includes(s), s);
  assert.ok(!/abfrage\(/.test(pb), 'der Browser fragt nichts selbst: Daten kommen aus den Datenmodulen');
  const kd = lies('koordination-daten.js');
  for (const s of ["abfrage('kollisionen'", "abfrage('aussparungsliste'", "abfrage('typmaterial'", 'T.kollisionSicht', "'Kollision', 'Aussparung', 'Typmaterial'"]) assert.ok(kd.includes(s), s);
  assert.match(lies('../../index.html'), /css\/projektbrowser\.css/);
  const b = lies('benutzer.js');
  assert.ok(b.includes('/api/benutzer/einstellungen'));
});

test('Server und Oberfläche: gleicher Schlüssel, gleiche Kennungen wie im Server-Test', () => {
  assert.equal(G.EINSTELLUNG_GLIEDERUNG, 'projektbrowser.gliederung');
  for (const id of ['gebaeude', 'sanitaer', 'heizung', 'lueftung', 'koordination', 'energie']) assert.ok(G.gliederungGueltig(id), id);
});

// ================================================================================================ G2: mehrere Gliederungen zugleich

const FACH = ['sanitaer', 'heizung', 'lueftung', 'koordination', 'energie'];
/** Alle Teilmengen der Fachgliederungen mit mindestens zwei Einträgen (in Listenreihenfolge). */
const kombinationen = () => { const l = []; for (let m = 1; m < 1 << FACH.length; m++) { const k = FACH.filter((_, i) => m & (1 << i)); if (k.length > 1) l.push(k); } return l; };
const mehrere = (ids, c = ctxMitDaten()) => baumBauenListe(G.gliederungenNormieren(ids).map((i) => def(i)), c);

test('G2 Wert: ein alter Einzelwert gilt als Liste mit einem Eintrag; Listen werden in Reihenfolge der Tabelle gebracht', () => {
  assert.deepEqual(G.gliederungenNormieren('sanitaer'), ['sanitaer'], 'alter Einzelwert');
  assert.deepEqual(G.gliederungenNormieren(['sanitaer']), ['sanitaer']);
  assert.deepEqual(G.gliederungenNormieren(['heizung', 'sanitaer']), ['sanitaer', 'heizung'], 'Reihenfolge der Liste, nicht der Wahl');
  assert.deepEqual(G.gliederungenNormieren(['energie', 'lueftung', 'heizung', 'sanitaer', 'koordination']), FACH);
  assert.deepEqual(G.gliederungenNormieren(['sanitaer', 'sanitaer', 'gibtsnicht']), ['sanitaer'], 'Doppelte und Unbekannte entfallen');
  for (const leer of [undefined, null, '', [], ['gibtsnicht'], 'gibtsnicht', 42]) assert.deepEqual(G.gliederungenNormieren(leer), ['gebaeude'], 'Vorgabe bei ' + JSON.stringify(leer));
  assert.deepEqual(G.gliederungenNormieren('gebaeude'), ['gebaeude']);
});

test('G2 Wert: «Gebäude» ist exklusiv – neben anderen entfällt es; mindestens eine bleibt gewählt', () => {
  assert.deepEqual(G.gliederungenNormieren(['gebaeude', 'sanitaer']), ['sanitaer']);
  assert.deepEqual(G.gliederungenNormieren(['gebaeude', 'sanitaer', 'heizung']), ['sanitaer', 'heizung']);
  assert.equal(def('gebaeude').exklusiv, true);
  assert.ok(G.GLIEDERUNGEN.filter((g) => g.id !== 'gebaeude').every((g) => !g.exklusiv));
});

test('G2 Umschalten: Häkchen setzen und entfernen; Gebäude ersetzt alles; Fachgliederung hebt Gebäude auf; das letzte Häkchen bleibt', () => {
  let w = ['gebaeude'];
  w = G.gliederungUmschalten(w, 'sanitaer'); assert.deepEqual(w, ['sanitaer'], 'Sanitär hebt Gebäude auf');
  w = G.gliederungUmschalten(w, 'heizung'); assert.deepEqual(w, ['sanitaer', 'heizung']);
  w = G.gliederungUmschalten(w, 'lueftung'); assert.deepEqual(w, ['sanitaer', 'heizung', 'lueftung']);
  w = G.gliederungUmschalten(w, 'heizung'); assert.deepEqual(w, ['sanitaer', 'lueftung'], 'Häkchen entfernen');
  w = G.gliederungUmschalten(w, 'koordination'); assert.deepEqual(w, ['sanitaer', 'lueftung', 'koordination'], 'in Listenreihenfolge, nicht in der Reihenfolge des Klickens');
  w = G.gliederungUmschalten(w, 'gebaeude'); assert.deepEqual(w, ['gebaeude'], 'Gebäude hebt die übrigen auf');
  w = G.gliederungUmschalten(w, 'gebaeude'); assert.deepEqual(w, ['gebaeude'], 'Gebäude lässt sich nicht abwählen');
  w = G.gliederungUmschalten(['sanitaer'], 'sanitaer'); assert.deepEqual(w, ['sanitaer'], 'das letzte Häkchen bleibt');
  assert.deepEqual(G.gliederungUmschalten(['sanitaer'], 'gibtsnicht'), ['sanitaer'], 'unbekannte Kennung ignoriert');
  assert.deepEqual(G.gliederungUmschalten('heizung', 'sanitaer'), ['sanitaer', 'heizung'], 'auch ein alter Einzelwert als Ausgang');
  assert.deepEqual(G.gliederungUmschalten(['sanitaer', 'heizung'], 'sanitaer'), ['heizung']);
});

test('G2 Anzeige im Feld: «Sanitär», «Sanitär + Heizung», bei mehr als zwei «Sanitär + 2»', () => {
  assert.equal(G.gliederungAnzeige(['gebaeude']), 'Gebäude');
  assert.equal(G.gliederungAnzeige(['sanitaer']), 'Sanitär');
  assert.equal(G.gliederungAnzeige(['sanitaer', 'heizung']), 'Sanitär + Heizung');
  assert.equal(G.gliederungAnzeige(['sanitaer', 'heizung', 'lueftung']), 'Sanitär + 2');
  assert.equal(G.gliederungAnzeige(FACH), 'Sanitär + 4');
  assert.equal(G.gliederungAnzeige(['heizung', 'energie']), 'Heizung + Energie und Umwelt');
});

test('G2 Aufklappzustand: eine Gliederung unter ihrer Kennung, mehrere unter «mehrere»; Gleichheit von Listen', () => {
  assert.equal(G.offenKennung(['sanitaer']), 'sanitaer');
  assert.equal(G.offenKennung(['sanitaer', 'heizung']), 'mehrere');
  assert.equal(G.offenSchluessel(G.offenKennung(['sanitaer', 'heizung'])), 'browserOffen.mehrere');
  assert.ok(G.gleicheListe(['a', 'b'], ['a', 'b']) && !G.gleicheListe(['a'], ['a', 'b']) && !G.gleicheListe(['a', 'b'], ['b', 'a']));
});

test('G2 Blöcke: Sanitär + Heizung als zwei Blöcke untereinander, dann Gemeinsames einmal, dann «Weitere»', () => {
  const w = mehrere(['heizung', 'sanitaer']);
  assert.deepEqual(w.map((k) => k.label), ['Sanitär', 'Heizung', 'Räume', 'Weitere'], 'Listenreihenfolge, «Räume» einmal nach den Blöcken');
  assert.deepEqual(w.map((k) => k.key), ['b:sanitaer', 'b:heizung', 'w:gebaeude', 'x:weitere']);
  assert.deepEqual(w.slice(0, 2).map((k) => k.gewerk), ['sanitaer', 'heizung'], 'Kopf in der Gewerkfarbe');
  assert.ok(w.slice(0, 2).every((k) => k.art === 'ast' && k.quelle === 'block' && k.anfangsOffen));
  assert.deepEqual(w[0].kinder.map((k) => k.label), ['Ansichten', 'Pläne', 'Anlagen', 'Familien'], 'darunter die Fachäste wie bei einer einzelnen Gliederung');
  assert.deepEqual(w[1].kinder.map((k) => k.label), ['Ansichten', 'Pläne', 'Anlagen', 'Familien']);
  assert.equal(w[2].gewerk, undefined, '«Räume» ist ungefärbt');
  assert.equal(w[3].gewerk, undefined);
});

test('G2 Blöcke: je Block dieselben Elemente wie in der einzelnen Gliederung', () => {
  const c = ctxMitDaten();
  const w = mehrere(['sanitaer', 'heizung'], c);
  for (const id of ['sanitaer', 'heizung']) {
    const einzeln = baum(id, c);
    const block = suche(w, 'b:' + id);
    for (const ast of ['w:ansichten', 'w:plaene', 'w:systeme', 'w:familien']) {
      const e = suche(einzeln, ast), b = suche(block.kinder, `b:${id}:${ast}`);
      assert.ok(b, `${id} ${ast}`);
      assert.deepEqual(kzs(b), kzs(e), `${id} ${ast}`);
      assert.equal(b.zahl, e.zahl, `${id} ${ast}: Zahl`);
      assert.equal(b.gewerk, id, `${id} ${ast}: Farbe`);
    }
    assert.ok(block.kinder.every((k) => k.key.startsWith(`b:${id}:`)), 'Schlüssel mit der Kennung des Blocks');
    assert.equal(block.zahl, block.kinder.reduce((s, k) => s + G.blattZahl(k), 0), 'Zahl am Kopf = Summe der Blätter');
  }
});

test('G2 Blöcke: eine Ansicht mit Sanitär und Heizung steht in beiden Blöcken, sonst steht kein Element doppelt', () => {
  const w = mehrere(['sanitaer', 'heizung']);
  const inBlock = (id) => kzs(suche(w, 'b:' + id));
  assert.ok(inBlock('sanitaer').includes('AN-MULTI') && inBlock('heizung').includes('AN-MULTI'), 'AN-MULTI gehört zu beiden Gewerken');
  const doppelt = inBlock('sanitaer').filter((k) => inBlock('heizung').includes(k));
  assert.deepEqual(doppelt, ['AN-MULTI'], 'als Blatt steht nur die Ansicht mit beiden Gewerken in beiden Blöcken (die gemischte Anlage unten)');
  const amix = (id) => suche(w, 'b:' + id).kinder.find((k) => k.key.endsWith('w:systeme')).kinder.find((k) => k.kz === 'A-MIX').kinder.map((s) => s.kz);
  assert.deepEqual(amix('sanitaer'), ['MIX-WW'], 'von der gemischten Anlage in jedem Block nur das eigene System');
  assert.deepEqual(amix('heizung'), ['MIX-HZ']);
});

test('G2 Blöcke: doppelte Elemente in allen Kombinationen nur bei echter Mehrfachzugehörigkeit zu den Gewerken beider Blöcke', () => {
  const c = ctxMitDaten();
  for (const ids of kombinationen()) {
    const w = mehrere(ids, c);
    const wo = new Map();   // Kennzeichen → Blöcke, in denen das Element steht
    for (const b of w.filter((k) => k.quelle === 'block')) for (const k of G.blaetter(b)) { if (!k.kz || k.art === 'ansichtsfenster') continue; wo.set(k.kz, [...(wo.get(k.kz) ?? []), b.key.slice(2)]); }
    for (const [kz, bloecke] of wo) {
      if (bloecke.length < 2) continue;
      const e = c.element(kz);
      assert.ok(e, `${ids}: ${kz}`);
      assert.ok(['Ansicht', 'Planblatt'].includes(e.klasse), `${ids}: ${kz} (${e.klasse}) steht in ${bloecke} – nur Ansichten und Pläne dürfen zu mehreren Gewerken gehören`);
      const m = e.klasse === 'Ansicht' ? G.MERKMALE.ansichten(c, e) : G.MERKMALE.plaene(c, e);
      for (const id of bloecke) {
        const ast = def(id).aeste.find((a) => a.quelle === (e.klasse === 'Ansicht' ? 'ansichten' : 'plaene'));
        assert.ok(G.passt(ast.auswahl, m, def(id)), `${ids}: ${kz} passt nicht zur Auswahl von ${id}`);
      }
    }
  }
});

test('G2 Summe: in jeder Kombination ist die Summe aller Elemente die von «Gebäude» – nichts geht verloren, nichts kommt dazu', () => {
  const c = ctxMitDaten();
  const gebaeude = G.elementMenge(baumBauenListe([def('gebaeude')], c));
  for (const ids of kombinationen()) {
    const m = elementeOhneZusatz(mehrere(ids, c));
    assert.deepEqual([...gebaeude].filter((x) => !m.has(x)), [], `${ids}: fehlt gegenüber «Gebäude»`);
    assert.deepEqual([...m].filter((x) => !gebaeude.has(x)), [], `${ids}: zusätzlich gegenüber «Gebäude»`);
  }
  // auch ohne Daten
  const leer = ctxVon([], { plansaetze: null, familien: [], bibliothek: [] });
  const g0 = G.elementMenge(baumBauenListe([def('gebaeude')], leer));
  for (const ids of kombinationen()) assert.deepEqual([...elementeOhneZusatz(mehrere(ids, leer))].sort(), [...g0].sort(), 'ohne Daten ' + ids);
});

test('G2 «Weitere» enthält nur, was in keinem gewählten Block vorkommt', () => {
  const c = ctxMitDaten();
  for (const ids of kombinationen()) {
    const w = mehrere(ids, c);
    const weitere = w.find((k) => k.key === 'x:weitere');
    const oben = new Set(G.blaetter({ kinder: w.filter((k) => k.key !== 'x:weitere'), art: 'ast' }).map(G.identitaet));   // Blätter: eine Anlage mit Systemen mehrerer Gewerke darf geteilt stehen, ihre Systeme nicht
    for (const x of G.blaetter(weitere ?? { art: 'ast' }).map(G.identitaet)) assert.ok(!oben.has(x), `${ids}: ${x} steht oben und unter «Weitere»`);
    // die Gewerke der gewählten Gliederungen haben unter «Weitere» keine Gruppe mehr
    const gruppen = (weitere?.kinder ?? []).filter((k) => k.key.startsWith('x:gw:')).map((k) => k.gewerk);
    for (const id of ids) assert.ok(!gruppen.includes(def(id).gewerk), `${ids}: Gruppe ${id} unter «Weitere»`);
  }
  // Sanitär + Heizung: die Lüftung steht unter «Weitere», die gemischte Anlage ist mit beiden Systemen oben und nicht noch einmal unten
  const w = mehrere(['sanitaer', 'heizung'], c);
  const weitere = suche(w, 'x:weitere');
  assert.deepEqual(weitere.kinder.filter((k) => k.key.startsWith('x:gw:')).map((k) => k.label), ['Lüftung', 'Architektur', 'Koordination', 'Ohne Gewerk']);
  assert.ok(!kzs(weitere).includes('MIX-HZ') && !kzs(weitere).includes('MIX-WW') && !kzs(weitere).includes('AN-MULTI'));
  assert.equal(weitere.zahl, G.blattZahl(weitere));
});

test('G2 «Weitere» im leeren Projekt: nur der Katalog bleibt übrig', () => {
  const w = mehrere(['sanitaer', 'heizung'], ctxVon([], { plansaetze: null, familien: [], bibliothek: [] }));
  assert.deepEqual(w.map((k) => k.label), ['Sanitär', 'Heizung', 'Räume', 'Weitere']);
  assert.deepEqual(suche(w, 'x:weitere').kinder.map((k) => k.label), ['Katalog']);
});

test('G2 Gemeinsames: «Räume» einmal nach den Blöcken, wenn mehrere Blöcke es haben; Energie und Umwelt teilt sich seine Gebäude-Zeile mit ihnen', () => {
  for (const ids of [['sanitaer', 'heizung'], ['sanitaer', 'heizung', 'lueftung'], ['heizung', 'energie'], ['sanitaer', 'energie'], ['lueftung', 'koordination']]) {
    const w = mehrere(ids);
    assert.equal(w.filter((k) => k.key === 'w:gebaeude').length, 1, ids + ': ein gemeinsamer Ast «Räume»');
    assert.equal(w.find((k) => k.key === 'w:gebaeude').label, 'Räume');
    assert.equal(w.find((k) => k.key === 'w:gebaeude').gewerk, undefined);
    const alle = []; const lauf = (k) => { alle.push(k.key); (k.kinder ?? []).forEach(lauf); }; w.forEach(lauf);
    assert.deepEqual(alle.filter((k, i) => alle.indexOf(k) !== i), [], ids + ': kein Schlüssel doppelt');
    assert.equal(w.findIndex((k) => k.key === 'w:gebaeude'), ids.length, ids + ': gleich nach den Blöcken');
    for (const b of w.filter((k) => k.quelle === 'block')) assert.ok(!b.kinder.some((k) => k.key.endsWith('w:gebaeude')), `${ids}: ${b.key} hat «Räume» nicht noch einmal`);
  }
  // Energie und Umwelt allein mit Koordination: hier hat nur Energie einen Gebäude-Ast (in Farbe) – er bleibt im Block
  const ke = mehrere(['koordination', 'energie']);
  assert.deepEqual(ke.map((k) => k.label), ['Koordination', 'Energie und Umwelt', 'Weitere']);
  assert.deepEqual(suche(ke, 'b:energie').kinder.map((k) => k.label), ['Hülle', 'Gebäude', 'Ansichten']);
});

test('G2 Koordination als Block: Kollisionen, Aussparungen und Typenliste stehen im Block, die Fachäste der anderen Blöcke daneben', () => {
  const w = mehrere(['sanitaer', 'koordination']);
  assert.deepEqual(w.map((k) => k.label), ['Sanitär', 'Koordination', 'Räume', 'Weitere']);
  const ko = suche(w, 'b:koordination');
  assert.deepEqual(ko.kinder.map((k) => k.label), ['Kollisionen', 'Aussparungen', 'Typenliste', 'Unterlagen', 'Pläne', 'Ansichten']);
  assert.equal(suche(ko.kinder, 'b:koordination:w:kollisionen').kontext, 'kollisionen', 'Kontext bleibt für das Kontextmenü');
  assert.equal(suche(ko.kinder, 'b:koordination:w:kollisionen').zahl, 3);
  assert.deepEqual(kzs(suche(ko.kinder, 'b:koordination:w:unterlagen')), ['UL-IFC']);
  // die Pläne: PB-AUS (Aussparungsansicht) und PB-K nur bei Koordination, PB-S nur bei Sanitär
  assert.deepEqual(plaene(suche(w, 'b:koordination:w:plaene')), ['PB-AUS', 'PB-K']);
  assert.deepEqual(plaene(suche(w, 'b:sanitaer:w:plaene')), ['PB-S']);
});

test('G2 Schlüssel sind in jeder Kombination eindeutig; Elemente behalten ihren Ursprungsschlüssel für die Auswahl', () => {
  for (const ids of kombinationen()) {
    const alle = [];
    const lauf = (k) => { alle.push(k.key); (k.kinder ?? []).forEach(lauf); };
    mehrere(ids).forEach(lauf);
    assert.deepEqual(alle.filter((k, i) => alle.indexOf(k) !== i), [], ids.join('+'));
  }
  const an = G.blaetter(suche(mehrere(['sanitaer', 'heizung']), 'b:heizung:w:ansichten')).find((k) => k.kz === 'AN-MULTI');
  assert.match(an.key, /^b:heizung:e:/);
  assert.match(G.identitaet(an), /^e:/, 'identitaet ohne Block-Präfix: so erkennt man dasselbe Element in zwei Blöcken');
});

test('G2 Eine Gliederung in der Liste ergibt denselben Baum wie bisher; «Gebäude» exklusiv ergibt die acht Äste', () => {
  const c = ctxMitDaten();
  for (const g of G.GLIEDERUNGEN) assert.deepEqual(baumBauenListe([g], c), baumBauen(g, c), g.id);
  assert.deepEqual(mehrere(['gebaeude', 'sanitaer'], c).map((k) => k.key), baum('sanitaer', c).map((k) => k.key), 'Gebäude neben Sanitär entfällt');
  assert.deepEqual(mehrere(['gebaeude'], c).map((k) => k.key), baum('gebaeude', c).map((k) => k.key));
  assert.equal(mehrere(['gebaeude'], c).length, 8);
});

test('G2 Alle fünf Fachgliederungen: fünf Blöcke in Listenreihenfolge, danach Räume und Weitere', () => {
  const w = mehrere(FACH);
  assert.deepEqual(w.map((k) => k.label), ['Sanitär', 'Heizung', 'Lüftung', 'Koordination', 'Energie und Umwelt', 'Räume', 'Weitere']);
  assert.deepEqual(w.slice(0, 5).map((k) => k.gewerk), ['sanitaer', 'heizung', 'lueftung', 'koordination', 'energie']);
  const weitere = suche(w, 'x:weitere');
  assert.ok(!weitere.kinder.some((k) => k.key.startsWith('x:gw:heizung')), 'die gewählten Gewerke haben keine Gruppe unter «Weitere»');
});

test('G2 Suche durchsucht alle Blöcke und «Weitere» und behält Treffer mit ihren Vorfahren', () => {
  const w = mehrere(['sanitaer', 'heizung']);
  const t = w.map((k) => G.filtern(k, 'an-multi')).filter(Boolean);
  assert.deepEqual(t.map((k) => k.key), ['b:sanitaer', 'b:heizung'], 'AN-MULTI steht in beiden Blöcken');
  assert.deepEqual(kzs(t[0]), ['AN-MULTI']);
  const l = w.map((k) => G.filtern(k, 'an-l')).filter(Boolean);   // AN-L (Lüftung) liegt unter «Weitere»
  assert.ok(l.some((k) => k.key === 'x:weitere'));
  assert.ok(G.astSchluessel(t).has('b:sanitaer') && G.astSchluessel(t).has('b:heizung'), 'die Köpfe der Blöcke klappen für die Treffer auf');
});

test('G2 Erweiterbar: eine weitere Fachgliederung (Elektro) lässt sich mit den anderen zu Blöcken kombinieren', () => {
  const elektro = { id: 'elektro', titel: 'Elektro', sym: 'rohr', gewerk: 'elektro', aeste: [
    { quelle: 'ansichten', auswahl: { gewerk: '$' }, farbe: true }, { quelle: 'systeme', titel: 'Anlagen', auswahl: { gewerk: '$' }, farbe: true }, { quelle: 'gebaeude', titel: 'Räume' }] };
  const tabelle = [...G.GLIEDERUNGEN, elektro];
  assert.deepEqual(G.gliederungenNormieren(['elektro', 'sanitaer'], tabelle), ['sanitaer', 'elektro']);
  assert.deepEqual(G.gliederungUmschalten(['sanitaer'], 'elektro', tabelle), ['sanitaer', 'elektro']);
  const w = baumBauenListe([def('sanitaer'), elektro], ctxMitDaten());
  assert.deepEqual(w.map((k) => k.label), ['Sanitär', 'Elektro', 'Räume', 'Weitere']);
});

// ---------------------------------------------------------------------------------------------- Kälte ist Teil der Heizung (Entscheid Christian 20.10.2026)

test('Kälte: es gibt keine eigene Gliederung; eine gespeicherte Kennung «kaelte» wird als «heizung» gelesen', () => {
  assert.equal(G.GLIEDERUNGEN.some((g) => g.id === 'kaelte' || g.gewerk === 'kaelte'), false);
  assert.deepEqual(G.gliederungenNormieren('kaelte'), ['heizung']);
  assert.deepEqual(G.gliederungenNormieren(['kaelte']), ['heizung']);
  assert.deepEqual(G.gliederungenNormieren(['kaelte', 'heizung']), ['heizung'], 'keine Doppelten');
  assert.deepEqual(G.gliederungenNormieren(['sanitaer', 'kaelte']), ['sanitaer', 'heizung'], 'Reihenfolge der Tabelle');
  assert.deepEqual(G.gliederungUmschalten(['sanitaer'], 'kaelte'), ['sanitaer'], 'Umschalten einer unbekannten Kennung ändert nichts');
});

test('Kälte: ein Kältesystem (Gewerk heizung im Katalog) steht in der Gliederung «Heizung», nicht unter «Weitere»; alte Gewerkangaben «kaelte» zählen als heizung', () => {
  const c = ctxMitDaten();
  // die Systemtypen kommen aus kataloge/systemtypen.json
  for (const typ of ['Kälte Vorlauf', 'Kälte Rücklauf', 'Kälte Vorlauf Glykol', 'Kälte Rücklauf Glykol']) assert.equal(G.systemGewerk(c, { werte: { systemtyp: typ } }), 'heizung', typ);
  assert.deepEqual(G.ansichtGewerke(c, { werte: { gewerke: ['kaelte'] } }), ['heizung']);
  assert.deepEqual(G.ansichtGewerke(c, { werte: { gewerke: ['kaelte', 'heizung', 'sanitaer'] } }), ['heizung', 'sanitaer']);
  assert.equal(G.passt({ gewerk: '$' }, { gewerke: G.ansichtGewerke(c, { werte: { gewerke: ['kaelte'] } }) }, def('heizung')), true);
});

test('G2 Verdrahtung: Wert als Liste im Browser, Server nimmt Listen an', () => {
  const pb = lies('projektbrowser.js');
  for (const s of ['gliederungUmschalten', 'gliederungenNormieren', 'gliederungAnzeige', 'baumBauenListe', 'aria-multiselectable']) assert.ok(pb.includes(s), s);
  assert.match(pb, /benutzerSetzen\(EINSTELLUNG_GLIEDERUNG, \[\.\.\.ids\]\)/, 'gespeichert wird die Liste');
  const cs = lies('../../../BenutzerEinstellungen.cs');
  assert.ok(cs.includes('JsonArray'), 'der Server prüft Listen');
});
