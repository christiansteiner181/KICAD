// Modul 7 Teil B3 in der Oberfläche, ohne Browser und ohne Server: die Bausteine der Knöpfe «Koordinationsplan» und «Aussparungsboxen» (koordinationsplan.js), die
// Verdrahtung im Menüband (Register Koordination, Symbole in beiden Sätzen) und die Eigenschaften umbruch und schrift des Ansichtsfensters im Katalog (sie erscheinen damit
// in der Eigenschaftenpalette). Die Oberfläche rechnet und färbt nichts selbst: der Kern zeichnet den Koordinationsplan und die Boxen.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/koordinationsplan.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import * as KP from './koordinationsplan.js';
import * as strich from './symbole.js';
import * as rv from './symbole-revit.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const katalogOrdner = process.env.HLKS_KATALOG || path.resolve(hier, '../../../../../kataloge');
const klassen = JSON.parse(readFileSync(path.join(katalogOrdner, 'klassen.json'), 'utf8'));
const klasse = (n) => klassen.find((k) => k.name === n);

const geschosse = [{ klasse: 'Geschoss', kennzeichen: 'UG' }, { klasse: 'Geschoss', kennzeichen: 'EG' }, { klasse: 'Geschoss', kennzeichen: '1.OG' }];
const ansicht = (kz, werte = {}, extra = {}) => ({ klasse: 'Ansicht', kennzeichen: kz, name: kz, geschoss: 'EG', werte: { art: 'grundriss', massstab: 50, ...werte }, ...extra });

// ---------------------------------------------------------------------------------------------- Vorschläge und Parameter

test('Kennzeichen- und Namensvorschlag wie bei den anderen Ansichten: AN-KO-<Geschoss> und «Koordination <Geschoss>»', () => {
  assert.equal(KP.kennzeichen('EG'), 'AN-KO-EG');
  assert.equal(KP.kennzeichen('1.OG'), 'AN-KO-1.OG');
  assert.equal(KP.name('EG'), 'Koordination EG');
  assert.equal(KP.DARSTELLUNG, 'koordination');
});

test('anlegenParameter: Grundriss mit darstellung koordination und aussparungsboxen, alle Gewerke (keine Eigenschaft gewerke), Geschoss als Zuordnung', () => {
  const p = KP.anlegenParameter({ geschoss: 'EG' });
  assert.deepEqual(p, {
    klasse: 'Ansicht', kennzeichen: 'AN-KO-EG', name: 'Koordination EG', geschoss: 'EG',
    eigenschaften: { art: 'grundriss', massstab: 50, detailgrad: 'mittel', darstellung: 'koordination', aussparungsboxen: true },
  });
  assert.ok(!('gewerke' in p.eigenschaften), 'alle Gewerke sichtbar');
  const q = KP.anlegenParameter({ geschoss: '1.OG', massstab: 100, kz: 'AN-KO-1.OG-2', anzeigeName: 'Koordination 1.OG (2)' });
  assert.equal(q.kennzeichen, 'AN-KO-1.OG-2');
  assert.equal(q.name, 'Koordination 1.OG (2)');
  assert.equal(q.eigenschaften.massstab, 100);
});

test('Die Eigenschaften, die die Parameter setzen, gibt es im Katalog der Klasse Ansicht (Werte der Auswahl eingeschlossen)', () => {
  const a = klasse('Ansicht').eigenschaften;
  const p = KP.anlegenParameter({ geschoss: 'EG' }).eigenschaften;
  for (const n of Object.keys(p)) assert.ok(a.some((e) => e.name === n), `Eigenschaft «${n}» fehlt im Katalog`);
  assert.ok(a.find((e) => e.name === 'darstellung').auswahl.includes(p.darstellung));
  assert.ok(a.find((e) => e.name === 'detailgrad').auswahl.includes(p.detailgrad));
  assert.equal(a.find((e) => e.name === 'aussparungsboxen').typ, 'bool');
});

test('massstabFuer: der Massstab des aktiven Grundrisses, sonst 50', () => {
  assert.equal(KP.massstabFuer(ansicht('A', { massstab: 100 })), 100);
  assert.equal(KP.massstabFuer(ansicht('A', { massstab: 33 })), 50);
  assert.equal(KP.massstabFuer(ansicht('A', { art: '3d', massstab: 100 })), 50);
  assert.equal(KP.massstabFuer(null), 50);
});

// ---------------------------------------------------------------------------------------------- aktives Geschoss, vorhandener Plan

test('aktivesGeschoss: das Geschoss der aktiven Ansicht; ohne Ansicht, ohne Geschoss oder bei einer Vorlage keines', () => {
  assert.equal(KP.aktivesGeschoss(ansicht('G', {}, { geschoss: '1.OG' }), geschosse), '1.OG');
  assert.equal(KP.aktivesGeschoss(ansicht('G', {}, { geschoss: 'eg' }), geschosse), 'EG', 'Schreibweise des Geschosses aus dem Modell');
  assert.equal(KP.aktivesGeschoss(null, geschosse), null);
  assert.equal(KP.aktivesGeschoss(ansicht('L', { art: 'liste' }, { geschoss: undefined }), geschosse), null);
  assert.equal(KP.aktivesGeschoss(ansicht('X', {}, { geschoss: 'GIBTS-NICHT' }), geschosse), null);
  assert.equal(KP.aktivesGeschoss(ansicht('V', { vorlage: true }), geschosse), null);
  assert.equal(KP.aktivesGeschoss({ klasse: 'Planblatt', kennzeichen: 'PB-1', geschoss: 'EG' }, geschosse), null);
});

test('planHinweis: sagt, was fehlt (Projekt, Geschoss, Ansicht); leer, wenn der Knopf möglich ist', () => {
  assert.match(KP.planHinweis({ projektOffen: false, aktiveAnsicht: null, geschosse: [] }), /Projekt öffnen/);
  assert.match(KP.planHinweis({ projektOffen: true, aktiveAnsicht: null, geschosse: [] }), /kein Geschoss/);
  assert.match(KP.planHinweis({ projektOffen: true, aktiveAnsicht: null, geschosse }), /Grundriss öffnen/);
  assert.match(KP.planHinweis({ projektOffen: true, aktiveAnsicht: ansicht('L', { art: 'liste' }, { geschoss: undefined }), geschosse }), /keinem Geschoss/);
  assert.equal(KP.planHinweis({ projektOffen: true, aktiveAnsicht: ansicht('G'), geschosse }), '');
});

test('vorhandener: Grundriss mit darstellung koordination im Geschoss; Vorlagen, andere Geschosse und andere Darstellungen zählen nicht; das Standardkennzeichen geht vor', () => {
  const ko = (kz, geschoss = 'EG', werte = {}) => ansicht(kz, { darstellung: 'koordination', ...werte }, { geschoss });
  const alle = [ansicht('AN-GR-EG'), ansicht('AN-AUS-EG', { darstellung: 'aussparungen' }), ko('AN-KO-UG', 'UG'), ko('VL-KO', 'EG', { vorlage: true }), ko('AN-X', 'EG', { art: '3d' })];
  assert.equal(KP.vorhandener(alle, 'EG'), null);
  alle.push(ko('AN-ZZ'));
  assert.equal(KP.vorhandener(alle, 'EG').kennzeichen, 'AN-ZZ');
  alle.push(ko('AN-KO-EG'));
  assert.equal(KP.vorhandener(alle, 'EG').kennzeichen, 'AN-KO-EG', 'Standardkennzeichen zuerst');
  assert.equal(KP.vorhandener(alle, 'eg').kennzeichen, 'AN-KO-EG', 'Schreibweise egal');
  assert.equal(KP.vorhandener(alle, 'UG').kennzeichen, 'AN-KO-UG');
  assert.equal(KP.vorhandener(alle, '1.OG'), null);
  assert.equal(KP.vorhandener([], 'EG'), null);
});

// ---------------------------------------------------------------------------------------------- Schalter Aussparungsboxen

test('boxenAn und boxenHinweis: nur im Grundriss; im Koordinationsplan immer ein (Schalter gesperrt), im Aussparungsplan nie (Schalter gesperrt)', () => {
  assert.equal(KP.boxenAn(ansicht('G')), false);
  assert.equal(KP.boxenAn(ansicht('G', { aussparungsboxen: true })), true);
  assert.equal(KP.boxenAn(ansicht('G', { aussparungsboxen: false })), false);
  assert.equal(KP.boxenAn(ansicht('K', { darstellung: 'koordination' })), true, 'Koordinationsplan: immer ein');
  assert.equal(KP.boxenAn(ansicht('A', { darstellung: 'aussparungen', aussparungsboxen: true })), false, 'Aussparungsplan: nie');
  assert.equal(KP.boxenAn(ansicht('D', { art: '3d', aussparungsboxen: true })), false);
  assert.equal(KP.boxenAn(null), false);

  assert.equal(KP.boxenHinweis(ansicht('G')), '');
  assert.equal(KP.boxenHinweis(ansicht('G', { darstellung: 'huelle' })), '');
  assert.match(KP.boxenHinweis(null), /Grundriss öffnen/);
  assert.match(KP.boxenHinweis(ansicht('D', { art: '3d' })), /nur im Grundriss/);
  assert.match(KP.boxenHinweis(ansicht('L', { art: 'liste' })), /nur im Grundriss/);
  assert.match(KP.boxenHinweis(ansicht('V', { vorlage: true })), /vorlage/i);
  assert.match(KP.boxenHinweis(ansicht('A', { darstellung: 'aussparungen' })), /Aussparungsplan.*keine Boxen/);
  assert.match(KP.boxenHinweis(ansicht('K', { darstellung: 'koordination' })), /immer eingeschaltet/);
});

test('boxenParameter: einschalten setzt true, ausschalten entfernt die Eigenschaft (Vorgabe aus); die Meldung nennt den neuen Zustand', () => {
  const aus = ansicht('AN-GR-EG');
  assert.deepEqual(KP.boxenParameter(aus), { element: 'AN-GR-EG', eigenschaften: { aussparungsboxen: true } });
  assert.equal(KP.boxenMeldung(aus), 'Aussparungsboxen eingeschaltet (AN-GR-EG).');
  const an = ansicht('AN-GR-EG', { aussparungsboxen: true });
  assert.deepEqual(KP.boxenParameter(an), { element: 'AN-GR-EG', eigenschaften: { aussparungsboxen: null } });
  assert.equal(KP.boxenMeldung(an), 'Aussparungsboxen ausgeschaltet (AN-GR-EG).');
});

// ---------------------------------------------------------------------------------------------- Verdrahtung

test('Verdrahtung: Knöpfe im Register Koordination, eigene Gruppe vor BCF, Symbole in beiden Sätzen, keine Kürzel, nur Befehle des Kerns', () => {
  const q = lies('./aktionen-koordination.js');
  for (const id of ['koordination.plan', 'koordination.boxen']) assert.ok(q.includes(`id: '${id}'`), id);
  // der Titel trägt eine Trennhilfe (U+200B), damit der lange Name im Knopf umbrechen kann
  assert.match(q, /id: 'koordination\.plan', titel: 'Koordinations(?:\\u200b|​)plan', symbol: 'koPlan', kuerzel: null, register: REGISTER, gruppe: 'Koordinationsplan'/);
  assert.match(q, /id: 'koordination\.boxen', titel: 'Aussparungs(?:\\u200b|​)boxen', symbol: 'koBoxen', kuerzel: null, register: REGISTER, gruppe: 'Koordinationsplan'/);
  assert.match(lies('./menueband.js'), /koordination: \['Vorgaben', 'Typenliste', 'Kollisionen', 'Aussparungen', 'Koordinationsplan', 'BCF'\]/);
  // nur Befehle des Kerns, keine Fachlogik
  assert.match(q, /sende\('element_anlegen', KP\.anlegenParameter/);
  assert.match(q, /sende\('element_aendern', KP\.boxenParameter/);
  assert.doesNotMatch(lies('./koordinationsplan.js'), /#[0-9A-Fa-f]{6}/, 'keine Farben in der Oberfläche');
  // Symbole in beiden Sätzen: Strich (symbole.js) und Revit-Stil (symbole-revit.js), beide Grössen
  const unbekannt = strich.symbol('gibtsnicht');
  for (const s of ['koPlan', 'koBoxen']) {
    assert.notEqual(strich.symbol(s), unbekannt, `Symbol «${s}» fehlt im Strichsatz`);
    assert.ok(rv.hatRevitFassung(s), `Symbol «${s}» fehlt im Revit-Satz`);
    assert.ok(rv.revitInhalt(s, false) && rv.revitInhalt(s, true), `Symbol «${s}» braucht 32 und 16`);
    assert.ok(rv.ZUORDNUNG.some((z) => z[0] === s), `Symbol «${s}» fehlt in der Zuordnungstabelle`);
  }
  // Kürzel nur, wenn frei: die Knöpfe haben keines
  assert.doesNotMatch(q.slice(q.indexOf("id: 'koordination.plan'")), /kuerzel: '[A-Z]/);
});

// ---------------------------------------------------------------------------------------------- Katalog: Optionen der Platzierung

test('Katalog: Ansichtsfenster hat umbruch (spalten | aus) und schrift (Zahl, mm); die Eigenschaftenpalette zeigt beides ohne eigenen Code', () => {
  const af = klasse('Ansichtsfenster').eigenschaften;
  const umbruch = af.find((e) => e.name === 'umbruch');
  assert.equal(umbruch.typ, 'auswahl');
  assert.deepEqual(umbruch.auswahl, ['spalten', 'aus']);
  assert.match(umbruch.beschreibung, /Blöcken/);
  assert.match(umbruch.beschreibung, /P28/);
  const schrift = af.find((e) => e.name === 'schrift');
  assert.equal(schrift.typ, 'zahl');
  assert.equal(schrift.einheit, 'mm');
  assert.ok(af.some((e) => e.name === 'titelZeigen'), 'bisherige Eigenschaft bleibt');
});
