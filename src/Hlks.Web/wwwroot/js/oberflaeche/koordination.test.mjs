// Tests der Texte, Prüfungen und Parameter für das Register «Koordination» (Modul 3b, Auftrag C1) ohne Browser:
// Projektvorgaben (Quelle Pflicht, keine Vorgabewerte), Kollisionen prüfen, Kollisionsliste (Filter, Mehrfachauswahl, Status), Aussparungen (Liste, Status, Vorschlagen,
// Nummerieren, Abgleich), Aussparungsplan, IFC für den Architekten, BCF (Export, Import mit Vorschau), Verdrahtung (Menüband, Symbole, Aktionen, Befehle).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/koordination.test.mjs
// Die Beispieldaten haben die Form der Antworten des Kerns (Vorgaben.cs, Kollisionsliste.cs, AussparungAbgleich.cs, BcfBefehle.cs); alle Zahlen sind FIKTIV («Testwert»)
// und stehen nur in den Tests – die Oberfläche selbst kennt keine Vorgabewerte. Ist HLKS_TEST_SERVER gesetzt (Server mit offenem Projekt), prüft ein zusätzlicher Test
// die Bausteine gegen die echten Antworten des Kerns (nur lesend und Trockenläufe).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import * as T from './koordination-text.js';
import * as sym from './symbole.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const kernDatei = (rel) => { const p = path.resolve(hier, '../../../../Hlks.Kern', rel); return existsSync(p) ? readFileSync(p, 'utf8') : null; };
const katalog = JSON.parse(lies('../../../../../kataloge/klassen.json'));
const klasse = (n) => (Array.isArray(katalog) ? katalog : katalog.klassen).find((k) => k.name === n);
const eig = (k, n) => klasse(k).eigenschaften.find((e) => e.name === n);

// ---------------------------------------------------------------------------------------------- Beispieldaten (Form wie im Kern, Werte FIKTIV)

const FIKTIV = 'FIKTIV – Testwert C1';
const vorgabenLeer = {
  element: null, werte: {}, ohneQuelle: [], abstandWeichSeiten: ['architektur', 'fremd', 'heizung', 'hlks', 'lueftung', 'sanitaer'],
  fehlend: [
    { name: 'spiel_aussparung', beschreibung: 'Spiel je Seite', wirkung: 'nötig für Aussparungsvorschläge (aussparungen_vorschlagen)' },
    { name: 'abstand_weich', beschreibung: 'Mindestabstände', wirkung: 'kollision_pruefen lässt die weiche Prüfung für Gewerkpaare ohne Wert aus (Leitung–Leitung: abstand_leitungen)' },
    { name: 'abstand_zusammenfassen', beschreibung: 'Zusammenfassen', wirkung: 'nötig zum Zusammenfassen benachbarter Aussparungen' },
    { name: 'abstand_leitungen', beschreibung: 'Leitung–Leitung', wirkung: 'kollision_pruefen lässt die weiche Prüfung Leitung–Leitung aus' },
    { name: 'toleranz_hart', beschreibung: 'Toleranz', wirkung: 'kollision_pruefen meldet jede Durchdringung (auch aufliegende Leitungen) als harte Kollision' },
  ],
  nichtMehrVerwendet: [{ name: 'grenze_kernbohrung', gesetzt: false, hinweis: 'nicht mehr verwendet (Entscheid Christian 19.10.2026): aussparungen_vorschlagen schlägt immer rechteckige Aussparungen vor, Kernbohrungen nur von Hand mit aussparung_form' }],
  hinweise: ['Noch keine Projektvorgaben – mit vorgaben_setzen anlegen (je Wert mit Quelle).'],
};
const vorgabenGesetzt = {
  element: 'VG-1',
  werte: {
    spiel_aussparung: { wert: 20, einheit: 'mm', quelle: FIKTIV, gesetzt: 'mensch, T, 2026-10-19 05:12', beschreibung: 'Spiel je Seite' },
    abstand_weich: { wert: { 'architektur-sanitaer': 50, 'heizung-sanitaer': 30 }, einheit: 'mm je Gewerkpaar', quelle: FIKTIV, gesetzt: 'mensch, T, 2026-10-19 05:12' },
  },
  ohneQuelle: ['toleranz_hart'], abstandWeichSeiten: vorgabenLeer.abstandWeichSeiten, nichtMehrVerwendet: vorgabenLeer.nichtMehrVerwendet,
  fehlend: vorgabenLeer.fehlend.filter((f) => !['spiel_aussparung', 'abstand_weich'].includes(f.name)), hinweise: [],
};
// altes Projekt: grenze_kernbohrung ist noch gesetzt (Form der Antwort: Vorgaben.cs, verwendet = false)
const vorgabenAlt = {
  ...vorgabenGesetzt,
  werte: { ...vorgabenGesetzt.werte, grenze_kernbohrung: { wert: 150, einheit: 'mm', quelle: FIKTIV, gesetzt: 'mensch, T, 2026-10-01 08:00', beschreibung: 'Grenze', verwendet: false, hinweis: 'nicht mehr verwendet (Entscheid Christian 19.10.2026): …' } },
  nichtMehrVerwendet: [{ name: 'grenze_kernbohrung', gesetzt: true, hinweis: 'nicht mehr verwendet (Entscheid Christian 19.10.2026): aussparungen_vorschlagen schlägt immer rechteckige Aussparungen vor' }],
};

const kollisionen = {
  ableitung: 'Kollisionsliste', anzahl: 3, kollisionslauf: '2026-10-19 05:12 · Lauf 1 · hlks-architektur, hlks-hlks',
  zusammenfassung: { jeStatus: { neu: 2, bestaetigt: 1 }, jeArt: { hart: 2, weich: 1 } },
  zeilen: [
    { kollision: 'KOL-1', art: 'hart', typ: 'durchdringung', status: 'neu', mass_mm: 175, geschoss: 'EG', beteiligte: 'T-1 (Rohr) ↔ W-EG-S (Wand)', bauteil: 'W-EG-S (Wand)', lage_mm: [3000, 0, 1000], kommentar: null, pruefung: '2026-10-19 05:12 · Lauf 1',
      beteiligteListe: [{ kennung: 'a', kennzeichen: 'T-1', klasse: 'Rohr', text: 'T-1 (Rohr)' }, { kennung: 'b', kennzeichen: 'W-EG-S', klasse: 'Wand', text: 'W-EG-S (Wand)' }] },
    { kollision: 'KOL-2', art: 'weich', typ: 'kontakt', status: 'neu', mass_mm: 35.5, geschoss: 'EG', beteiligte: 'T-1 (Rohr) ↔ T-3 (Rohr)', bauteil: null, lage_mm: [3060, 500, 1000], kommentar: 'Architekt fragen\nzweite Zeile',
      beteiligteListe: [{ kennung: 'a', kennzeichen: 'T-1', klasse: 'Rohr', text: 'T-1 (Rohr)' }, { kennung: 'c', kennzeichen: 'T-3', klasse: 'Rohr', text: 'T-3 (Rohr)' }] },
    { kollision: 'KOL-3', art: 'hart', typ: 'kontakt', status: 'bestaetigt', mass_mm: 2.4, geschoss: null, beteiligte: 'IfcWall 3x (UL-1) ↔ T-2 (Rohr)', bauteil: null, lage_mm: [6000, 0, 1000], kommentar: null,
      beteiligteListe: [{ kennung: 'UL-1:3x', globalId: '3x', unterlage: 'UL-1', entitaet: 'IfcWall', text: 'IfcWall 3x (UL-1)' }, { kennung: 'd', kennzeichen: 'T-2', klasse: 'Rohr', text: 'T-2 (Rohr)' }] },
  ],
};
const aussparungen = {
  ableitung: 'Aussparungsliste', anzahl: 3, jeStatus: { vorgeschlagen: 2, freigegeben: 1 },
  zeilen: [
    { aussparung: 'AUS-1', nummer: 'A-EG-001', geschoss: 'EG', bauteil: 'W-EG-S', bauteilArt: 'Wand', unterlage: null, form: 'rechteckig', masse: '215 × 95', tiefe: 350, koteUk: 952.5, koteOk: 1047.5, koteAchse: null,
      koteUkGeschoss: 952.5, koteOkGeschoss: 1047.5, koteAchseGeschoss: null, art: 'aussparung', status: 'vorgeschlagen', leitungen: ['T-1', 'T-3'],
      formVonHand: false, formWaehlbar: false, formSperre: 'Sammelaussparung (2 Leitungen) – bleibt rechteckig' },
    { aussparung: 'AUS-2', nummer: 'A-EG-002', geschoss: 'EG', bauteil: 'W-EG-S', bauteilArt: 'Wand', unterlage: null, form: 'rund', masse: 'Ø 95', tiefe: 350, koteUk: 1000, koteOk: null, koteAchse: 1000,
      koteUkGeschoss: null, koteOkGeschoss: null, koteAchseGeschoss: 1000, art: 'kernbohrung', status: 'freigegeben', leitungen: ['T-2'],
      formVonHand: true, formWaehlbar: false, formSperre: 'Status freigegeben – zuerst aussparung_status vorgeschlagen' },
    { aussparung: 'AUS-3', nummer: null, geschoss: '1.OG', bauteil: '3x', bauteilArt: 'IfcWall', unterlage: 'UL-1', form: 'rund', masse: 'Ø 80', tiefe: 200, koteUk: 3500, koteAchse: 3500,
      koteAchseGeschoss: 600, art: 'kernbohrung', status: 'veraltet', leitungen: ['T-9'],
      formVonHand: false, formWaehlbar: true, formSperre: null },
  ],
};
// Zeilen für die Form: AUS-4 rechteckig und wählbar, AUS-5 von Hand rund und wählbar (Status vorgeschlagen), AUS-6 mit Material (Teil B3c)
const formZeile = (kz, o = {}) => ({ aussparung: kz, nummer: null, geschoss: 'EG', bauteil: 'W-EG-S', bauteilArt: 'Wand', unterlage: null, form: 'rechteckig', masse: '215 × 95', tiefe: 350,
  koteUk: 952.5, koteOk: 1047.5, koteAchse: null, koteUkGeschoss: 952.5, koteOkGeschoss: 1047.5, koteAchseGeschoss: null, art: 'aussparung', status: 'vorgeschlagen', leitungen: ['T-4'],
  formVonHand: false, formWaehlbar: true, formSperre: null, ...o });
const aussparungenForm = { anzahl: 3, jeStatus: { vorgeschlagen: 3 }, zeilen: [
  formZeile('AUS-4'),
  formZeile('AUS-5', { form: 'rund', masse: 'Ø 95', art: 'kernbohrung', koteUk: null, koteOk: null, koteAchse: 1000, koteAchseGeschoss: 1000, formVonHand: true, leitungen: ['T-5'] }),
  formZeile('AUS-6', { formWaehlbar: false, formSperre: 'ohne Leitung – Mass nicht rechenbar', leitungen: [], material: 'Beton', materialUnbekannt: false }),
] };

// ---------------------------------------------------------------------------------------------- Listen: Spiegel des Kerns und des Katalogs

test('Listen der Auswahlen entsprechen Katalog und Kern (Status, Art, Typ, Paare, Vorgaben)', () => {
  assert.deepEqual(T.KOLLISION_STATUS, eig('Kollision', 'status').auswahl);
  assert.deepEqual(T.KOLLISION_ARTEN, eig('Kollision', 'art').auswahl);
  assert.deepEqual(T.KOLLISION_TYPEN, eig('Kollision', 'typ').auswahl);
  assert.deepEqual(T.AUSSPARUNG_STATUS, eig('Aussparung', 'status').auswahl);
  assert.deepEqual([...T.VORGABEN, ...T.VORGABEN_NICHT_MEHR].map((v) => v.name).sort(), klasse('Vorgaben').eigenschaften.map((e) => e.name).sort(), 'jede Vorgabe des Katalogs hat eine Zeile im Dialog oder ist «nicht mehr verwendet»');
  for (const v of T.VORGABEN) assert.equal(v.art, eig('Vorgaben', v.name).typ === 'objekt' ? 'objekt' : 'zahl', v.name);
  assert.deepEqual(T.VORGABEN_NICHT_MEHR.map((v) => v.name), ['grenze_kernbohrung'], 'Entscheid Christian 19.10.2026: die Grenze Kernbohrung entfällt');
  assert.ok(!T.VORGABEN.some((v) => v.name === 'grenze_kernbohrung'), 'kein Feld für die Grenze Kernbohrung');
  const k = kernDatei('Kollision/Kollisionspruefung.cs');
  if (k) {
    // Paare des Kerns (seit Teil B3d alle fünf): jedes steht im Dialog und der Dialog kennt keine anderen
    const kernPaare = [...k.matchAll(/public const string Hlks(?:Architektur|Hlks|Fremd|Bekleidung|Vorwand) = "([^"]+)"/g)].map((m) => m[1]);
    assert.ok(kernPaare.length >= 3);
    for (const id of kernPaare) assert.ok(T.PAARE.some((p) => p.id === id), `Paar ${id} des Kerns im Dialog`);
    assert.deepEqual(T.PAARE.map((p) => p.id).filter((id) => !kernPaare.includes(id)), [], 'der Dialog bietet kein Paar an, das der Kern nicht kennt');
    assert.ok(!T.PAARE_VORGABE.includes('hlks-bekleidung') && !T.PAARE_VORGABE.includes('hlks-vorwand'), 'Bekleidungen und Vorwände sind nicht in der Vorgabe');
    const vorgabe = /PaareVorgabe = \[([^\]]*)\]/.exec(k)[1].match(/Hlks\w+/g).map((n) => ({ HlksArchitektur: 'hlks-architektur', HlksHlks: 'hlks-hlks', HlksFremd: 'hlks-fremd' }[n]));
    assert.deepEqual(T.PAARE_VORGABE, vorgabe, 'Paare ohne Angabe wie im Kern');
  }
  const e = kernDatei('Aussparungen/AussparungDaten.cs');
  if (e) for (const s of T.AUSSPARUNG_STATUS) assert.ok(new RegExp(`"${s}"`).test(e), `Status ${s} im Kern`);
});

test('Texte des Kerns für Menschen: Befehlsnamen heissen wie die Knöpfe', () => {
  assert.equal(T.fuerMenschen('Noch keine Projektvorgaben – mit vorgaben_setzen anlegen (je Wert mit Quelle).'), 'Noch keine Projektvorgaben – mit «Projektvorgaben …» anlegen (je Wert mit Quelle).');
  assert.equal(T.fuerMenschen('nötig für Aussparungsvorschläge (aussparungen_vorschlagen)'), 'nötig für Aussparungsvorschläge («Aussparungen vorschlagen …»)');
  assert.match(T.fuerMenschen('kollision_pruefen meldet jede Durchdringung'), /^«Kollisionen prüfen …» meldet/);
  assert.equal(T.fuerMenschen('toleranz_hart fehlt'), 'toleranz_hart fehlt', 'Namen von Vorgaben bleiben');
  assert.equal(T.ablehnung({ fehler: 'Vorgaben nicht gesetzt: x', vorschlag: 'Jeden Wert mit Quelle angeben' }), 'Vorgaben nicht gesetzt: x – Vorschlag: Jeden Wert mit Quelle angeben');
  assert.equal(T.statusText('bestaetigt'), 'bestätigt');
  assert.equal(T.statusText('ausgefuehrt'), 'ausgeführt');
  assert.equal(T.statusText('neu'), 'neu');
});

// ---------------------------------------------------------------------------------------------- Projektvorgaben

test('Vorgaben: ohne Werte im Kern ist das Formular leer – die Oberfläche kennt keine Vorgabewerte', () => {
  const f = T.vorgabenFormular(vorgabenLeer);
  for (const v of T.VORGABEN_ZAHL) assert.deepEqual(f.felder[v.name], { wert: '', quelle: '' }, v.name);
  assert.deepEqual(f.weich, { zeilen: [], quelle: '' });
  const a = T.vorgabenAuswertung(f, JSON.parse(JSON.stringify(f)));
  assert.deepEqual(a, { fehler: [], warnungen: [], werte: {} });
  assert.equal(T.vorgabenParameter(f, JSON.parse(JSON.stringify(f))), null, 'nichts zu senden');
  // keine Zahl der Vorgaben im Quelltext des Dialogs (Zahlen nur als Beschriftung/Auswahl, nie als Wert eines Feldes)
  assert.ok(!/value="\d/.test(T.vorgabenHtml(vorgabenLeer, f)), 'kein Feld ist vorbelegt');
});

test('Vorgaben: gesetzte Werte und Gewerkpaare kommen mit Quelle ins Formular', () => {
  const f = T.vorgabenFormular(vorgabenGesetzt);
  assert.deepEqual(f.felder.spiel_aussparung, { wert: '20', quelle: FIKTIV });
  assert.deepEqual(f.felder.toleranz_hart, { wert: '', quelle: '' }, 'Wert ohne Quelle gilt nicht und steht nicht im Feld');
  assert.deepEqual(f.weich.zeilen, [{ a: 'architektur', b: 'sanitaer', wert: '50' }, { a: 'heizung', b: 'sanitaer', wert: '30' }]);
  assert.equal(f.weich.quelle, FIKTIV);
});

test('Vorgaben: ohne Quelle abgelehnt, Zahlen geprüft, nur Geändertes gesendet', () => {
  const bisher = T.vorgabenFormular(vorgabenLeer);
  const f = JSON.parse(JSON.stringify(bisher));
  f.felder.spiel_aussparung.wert = '20';
  let a = T.vorgabenAuswertung(f, bisher);
  assert.match(a.fehler[0], /Spiel um Leitungen bei Aussparungen: Quelle fehlt/);
  assert.deepEqual(a.werte, {}, 'ohne Quelle geht nichts an den Kern');
  f.felder.spiel_aussparung.quelle = '   ';
  assert.equal(T.vorgabenAuswertung(f, bisher).fehler.length, 1, 'nur Leerzeichen sind keine Quelle');
  f.felder.spiel_aussparung.quelle = FIKTIV;
  a = T.vorgabenAuswertung(f, bisher);
  assert.deepEqual(a.fehler, []);
  assert.deepEqual(T.vorgabenParameter(f, bisher), { werte: { spiel_aussparung: { wert: 20, quelle: FIKTIV } } });
  f.felder.abstand_zusammenfassen.wert = '1,5e2';   // Komma/Exponent: Zahl 150
  f.felder.abstand_zusammenfassen.quelle = FIKTIV;
  assert.equal(T.vorgabenParameter(f, bisher).werte.abstand_zusammenfassen.wert, 150);
  f.felder.abstand_leitungen.wert = 'abc'; f.felder.abstand_leitungen.quelle = FIKTIV;
  f.felder.toleranz_hart.wert = '-1'; f.felder.toleranz_hart.quelle = FIKTIV;
  a = T.vorgabenAuswertung(f, bisher);
  assert.equal(a.fehler.length, 2);
  assert.match(a.fehler.join(' '), /keine Zahl/);
  assert.match(a.fehler.join(' '), /negativ/);
  const nurQuelle = JSON.parse(JSON.stringify(bisher)); nurQuelle.felder.toleranz_hart.quelle = 'x';
  assert.match(T.vorgabenAuswertung(nurQuelle, bisher).fehler[0], /Quelle ohne Wert/);
});

test('Vorgaben: unveränderte Werte werden nicht gesendet, Leeren entfernt, geänderter Wert bei gleicher Quelle warnt', () => {
  const bisher = T.vorgabenFormular(vorgabenGesetzt);
  const f = JSON.parse(JSON.stringify(bisher));
  assert.equal(T.vorgabenParameter(f, bisher), null);
  f.felder.spiel_aussparung.wert = '25';
  let a = T.vorgabenAuswertung(f, bisher);
  assert.deepEqual(a.werte, { spiel_aussparung: { wert: 25, quelle: FIKTIV } });
  assert.match(a.warnungen[0], /Quelle gleich geblieben/);
  f.felder.spiel_aussparung.quelle = 'Projekt, 2026-10-20';
  a = T.vorgabenAuswertung(f, bisher);
  assert.deepEqual(a.warnungen, [], 'neue Quelle: keine Warnung');
  f.felder.spiel_aussparung.wert = ''; f.felder.spiel_aussparung.quelle = '';
  assert.deepEqual(T.vorgabenAuswertung(f, bisher).werte, { spiel_aussparung: null }, 'geleerter Wert wird null (entfernt)');
});

test('Vorgaben: Abstand weich je Gewerkpaar – eine Quelle, Reihenfolge der Seiten zählt nicht, Doppelte abgelehnt', () => {
  assert.equal(T.paarSchluessel('sanitaer', 'Architektur'), 'architektur-sanitaer');
  assert.equal(T.paarSchluessel('architektur', 'sanitaer'), 'architektur-sanitaer');
  const bisher = T.vorgabenFormular(vorgabenLeer);
  const f = JSON.parse(JSON.stringify(bisher));
  f.weich.zeilen.push({ a: 'sanitaer', b: 'architektur', wert: '40' }, { a: '', b: '', wert: '' });
  let a = T.vorgabenAuswertung(f, bisher);
  assert.match(a.fehler.join(' '), /Abstand weich: Quelle fehlt/);
  f.weich.quelle = FIKTIV;
  a = T.vorgabenAuswertung(f, bisher);
  assert.deepEqual(a.fehler, []);
  assert.deepEqual(a.werte, { abstand_weich: { wert: { 'architektur-sanitaer': 40 }, quelle: FIKTIV } }, 'leere Zeilen entfallen, Schlüssel geordnet');
  f.weich.zeilen.push({ a: 'architektur', b: 'sanitaer', wert: '45' });
  assert.match(T.vorgabenAuswertung(f, bisher).fehler.join(' '), /steht doppelt/);
  f.weich.zeilen.pop();
  f.weich.zeilen.push({ a: 'heizung', b: '', wert: '30' });
  assert.match(T.vorgabenAuswertung(f, bisher).fehler.join(' '), /beide Seiten/);
  f.weich.zeilen.pop();
  f.weich.zeilen.push({ a: 'heizung', b: 'sanitaer', wert: 'x' });
  assert.match(T.vorgabenAuswertung(f, bisher).fehler.join(' '), /keine Zahl/);
  // alle Zeilen entfernen: Objekt wird entfernt
  const mitPaaren = T.vorgabenFormular(vorgabenGesetzt);
  const leerer = JSON.parse(JSON.stringify(mitPaaren)); leerer.weich.zeilen = []; leerer.weich.quelle = '';
  assert.deepEqual(T.vorgabenAuswertung(leerer, mitPaaren).werte, { abstand_weich: null });
  // gleiche Paare in anderer Reihenfolge: nichts zu senden
  const getauscht = JSON.parse(JSON.stringify(mitPaaren)); getauscht.weich.zeilen = getauscht.weich.zeilen.map((z) => ({ ...z, a: z.b, b: z.a })).reverse();
  assert.equal(T.vorgabenParameter(getauscht, mitPaaren), null);
  // anderer Wert, gleiche Quelle: Warnung
  const anders = JSON.parse(JSON.stringify(mitPaaren)); anders.weich.zeilen[0].wert = '55';
  assert.match(T.vorgabenAuswertung(anders, mitPaaren).warnungen[0], /Paare geändert/);
});

test('Vorgaben: «Quelle in leere Zeilen übernehmen»', () => {
  const f = T.vorgabenFormular(vorgabenLeer);
  assert.equal(T.quelleUebernehmen(f), 0, 'ohne ausgefüllte Quelle nichts zu tun');
  f.felder.spiel_aussparung = { wert: '20', quelle: 'Bürostandard' };
  f.felder.abstand_zusammenfassen.wert = '100';
  f.felder.toleranz_hart.wert = '';           // ohne Wert bleibt leer
  f.weich.zeilen.push({ a: 'heizung', b: 'sanitaer', wert: '30' });
  assert.equal(T.quelleUebernehmen(f), 2);
  assert.equal(f.felder.abstand_zusammenfassen.quelle, 'Bürostandard');
  assert.equal(f.felder.toleranz_hart.quelle, '');
  assert.equal(f.weich.quelle, 'Bürostandard');
});

test('Vorgaben: HTML zeigt Quelle (Pflicht), Zustand mit Wirkung des Kerns und Wert ohne Quelle', () => {
  const h = T.vorgabenHtml(vorgabenGesetzt, T.vorgabenFormular(vorgabenGesetzt));
  assert.match(h, /Vorgaben nur mit Quelle/);
  assert.match(h, /VG-1 · 1 von 3 Vorgaben gesetzt, 2 fehlen · Abstandsprüfung mit 1 Wert\(en\)/);
  for (const v of T.VORGABEN_ZAHL) assert.match(h, new RegExp(`data-name="${v.name}"`));
  assert.match(h, /Quelle \*/);
  assert.match(h, /gesetzt<\/span> <span class="leise">mensch, T, 2026-10-19/);
  assert.match(h, /fehlt<\/span> <span class="leise">– nötig zum Zusammenfassen benachbarter Aussparungen/);
  assert.match(T.vorgabenHtml(vorgabenLeer, T.vorgabenFormular(vorgabenLeer)), /nicht gesetzt – Abstandsprüfung aus/, 'fehlende Abstände sind kein Mangel: die Abstandsprüfung ist aus');
  assert.match(T.vorgabenHtml(vorgabenLeer, T.vorgabenFormular(vorgabenLeer)), /fehlt<\/span> <span class="leise">– «Kollisionen prüfen …» meldet jede Durchdringung/);
  assert.match(h, /Ohne Quelle \(gelten nicht\): toleranz_hart/);
  assert.match(h, /Wert ohne Quelle – gilt nicht/);
  // Auswahl der Gewerkpaare kommt aus dem Kern
  assert.match(h, /<option value="hlks">alle HLKS-Gewerke<\/option>/);
  assert.match(h, /<option value="sanitaer" selected>Sanitär<\/option>/);
  assert.match(T.vorgabenHtml(vorgabenLeer, T.vorgabenFormular(vorgabenLeer)), /Noch keine Projektvorgaben angelegt/);
});

test('Vorgaben: kein Feld «grenze_kernbohrung» mehr; Pflicht sind Spiel und Zusammenfassen, toleranz_hart ist wählbar', () => {
  const f = T.vorgabenFormular(vorgabenLeer);
  assert.deepEqual(Object.keys(f.felder).sort(), ['abstand_leitungen', 'abstand_zusammenfassen', 'spiel_aussparung', 'toleranz_hart']);
  const h = T.vorgabenHtml(vorgabenLeer, f);
  assert.doesNotMatch(h, /data-name="grenze_kernbohrung"/);
  assert.doesNotMatch(h, /Grenze Kernbohrung/);
  assert.doesNotMatch(h, /kp-nichtmehr/, 'kein Hinweis, wenn der Wert nicht gesetzt ist');
  assert.deepEqual(T.VORGABEN.filter((v) => v.gruppe === 'pflicht').map((v) => v.name), ['spiel_aussparung', 'abstand_zusammenfassen']);
  assert.deepEqual(T.VORGABEN.filter((v) => v.gruppe === 'wahl').map((v) => v.name), ['toleranz_hart']);
  assert.match(h, /data-name="spiel_aussparung"[\s\S]*?Pflicht für Aussparungen/);
  assert.match(h, /data-name="toleranz_hart"[\s\S]*?wählbar/);
  assert.match(h, /Noch keine Projektvorgaben angelegt/);
  // Zusammenzählung ohne Abstände: zwei Pflichtwerte und toleranz_hart
  assert.deepEqual(T.vorgabenZaehlung(vorgabenLeer), { gesetzt: 0, gesamt: 3 });
  assert.deepEqual(T.vorgabenZaehlung(vorgabenGesetzt), { gesetzt: 1, gesamt: 3 });
});

test('Vorgaben: Abstände weich und Mindestabstand stehen im eingeklappten Bereich «Abstandsprüfung (aus)» mit Erklärung', () => {
  const f = T.vorgabenFormular(vorgabenLeer);
  const h = T.vorgabenHtml(vorgabenLeer, f);
  assert.match(h, /<details class="kp-abstand"><summary>Abstandsprüfung \(aus\)<\/summary>/, 'eingeklappt (kein open)');
  const innen = h.slice(h.indexOf('<details class="kp-abstand"'), h.indexOf('</details>'));
  assert.match(innen, /Entscheid Christian 19\.10\.2026: keine Abstandsprüfung/, 'Erklärung');
  assert.match(innen, /data-name="abstand_leitungen"/);
  assert.match(innen, /class="v-weich"/);
  assert.match(innen, /v-weich-quelle/);
  assert.match(innen, /nicht gesetzt – Abstandsprüfung aus/, 'kein «fehlt», ohne Abstände bleibt die Prüfung aus');
  const aussen = h.replace(innen, '');
  assert.doesNotMatch(aussen, /data-name="abstand_leitungen"|class="v-weich"/, 'ausserhalb des Bereichs stehen keine Abstände');
  assert.match(T.vorgabenHtml(vorgabenLeer, f, { abstandOffen: true }), /<details class="kp-abstand" open>/, 'aufgeklappt, wenn der Dialog es merkt');
  assert.match(T.vorgabenHtml(vorgabenGesetzt, T.vorgabenFormular(vorgabenGesetzt)), /<summary>Abstandsprüfung \(1 Wert\(e\) gesetzt\)<\/summary>/, 'mit gesetztem Wert sagt die Überschrift es');
  // Bearbeiten geht wie bisher (Quelle Pflicht, Gewerkpaare, ein Objekt)
  const bisher = T.vorgabenFormular(vorgabenLeer), g = JSON.parse(JSON.stringify(bisher));
  g.felder.abstand_leitungen = { wert: '60', quelle: FIKTIV };
  assert.deepEqual(T.vorgabenParameter(g, bisher), { werte: { abstand_leitungen: { wert: 60, quelle: FIKTIV } } });
  g.felder.abstand_leitungen.quelle = '';
  assert.match(T.vorgabenAuswertung(g, bisher).fehler[0], /Quelle fehlt/);
  assert.ok(T.ABSTAND_ERKLAERUNG.includes('Die weiche Prüfung bleibt aus'));
});

test('Vorgaben: alter Wert von grenze_kernbohrung → Hinweis «nicht mehr verwendet» mit «entfernen» (null an den Kern)', () => {
  assert.equal(T.nichtMehrGesetzt(vorgabenLeer, 'grenze_kernbohrung'), false);
  assert.equal(T.nichtMehrGesetzt(vorgabenAlt, 'grenze_kernbohrung'), true);
  assert.equal(T.nichtMehrGesetzt({ werte: {}, ohneQuelle: ['grenze_kernbohrung'] }, 'grenze_kernbohrung'), true, 'auch ein Wert ohne Quelle');
  const bisher = T.vorgabenFormular(vorgabenAlt);
  assert.deepEqual(bisher.alt.grenze_kernbohrung, { gesetzt: true, wert: '150', quelle: FIKTIV, entfernen: false });
  assert.ok(!('grenze_kernbohrung' in bisher.felder), 'kein Eingabefeld');
  const h = T.vorgabenHtml(vorgabenAlt, bisher);
  assert.match(h, /data-alt="grenze_kernbohrung"/);
  assert.match(h, /nicht mehr verwendet \(Entscheid Christian 19\.10\.2026\)/);
  assert.match(h, /Gesetzt ist 150 mm \(Quelle: FIKTIV/);
  assert.match(h, /<button type="button" class="chip v-alt-weg"[^>]*>entfernen<\/button>/);
  assert.doesNotMatch(h, /data-name="grenze_kernbohrung"/);
  assert.doesNotMatch(h, /Ohne Quelle \(gelten nicht\): [^<]*grenze_kernbohrung/);
  assert.match(h, /«Aussparungen vorschlagen …» schlägt immer rechteckige Aussparungen vor/, 'Grund des Kerns in der Sprache der Knöpfe');
  // unverändert: nichts zu senden
  assert.equal(T.vorgabenParameter(JSON.parse(JSON.stringify(bisher)), bisher), null);
  // «entfernen» vormerken → vorgaben_setzen { werte: { grenze_kernbohrung: null } }
  const f = JSON.parse(JSON.stringify(bisher));
  f.alt.grenze_kernbohrung.entfernen = true;
  assert.deepEqual(T.vorgabenParameter(f, bisher), { werte: { grenze_kernbohrung: null } });
  assert.deepEqual(T.vorgabenAuswertung(f, bisher).fehler, [], 'braucht keine Quelle');
  const h2 = T.vorgabenHtml(vorgabenAlt, f);
  assert.match(h2, /Wird beim Speichern entfernt/);
  assert.match(h2, /v-alt-behalten/);
  assert.doesNotMatch(h2, /v-alt-weg/);
  // zusammen mit einer anderen Änderung
  f.felder.spiel_aussparung.wert = '25';
  assert.deepEqual(T.vorgabenParameter(f, bisher), { werte: { grenze_kernbohrung: null, spiel_aussparung: { wert: 25, quelle: FIKTIV } } });
  // ist der Wert nicht gesetzt, schickt eine Vormerkung nichts
  const leer = T.vorgabenFormular(vorgabenLeer), l2 = JSON.parse(JSON.stringify(leer));
  l2.alt.grenze_kernbohrung.entfernen = true;
  assert.equal(T.vorgabenParameter(l2, leer), null);
});

test('Vorgaben: Vorschau des Trockenlaufs (Hinweise oder Ablehnung des Kerns)', () => {
  const ok = T.vorgabenVorschauHtml({ angenommen: true, hinweise: ['VG-1: spiel_aussparung = 20 mm (Quelle: x).', 'Noch ohne Wert: abstand_weich (Wirkung: Abfrage vorgaben).'] }, { warnungen: ['Achtung'] });
  assert.match(ok, /Trockenlauf – nichts wurde geändert/);
  assert.match(ok, /spiel_aussparung = 20 mm/);
  assert.match(ok, /<li>Achtung<\/li>/);
  const nein = T.vorgabenVorschauHtml({ angenommen: false, fehler: 'Vorgaben nicht gesetzt: spiel_aussparung: Quelle fehlt', vorschlag: 'Jeden Wert mit Quelle angeben' }, null);
  assert.match(nein, /fehlerkasten/);
  assert.match(nein, /Quelle fehlt/);
});

// ---------------------------------------------------------------------------------------------- Kollisionen prüfen

test('Prüfen: Parameter nur bei Abweichung von den Vorgaben des Kerns', () => {
  const basis = { paare: ['hlks-hlks', 'hlks-architektur'], unterlagen: [], alleUnterlagen: ['UL-1', 'UL-2'], geschoss: '', weich: undefined };
  assert.deepEqual(T.pruefenParameter(basis), {}, 'Vorgabe: Kern entscheidet (Paare, Umfang); ohne Angabe zur weichen Prüfung geht nichts mit');
  assert.deepEqual(T.pruefenParameter({ ...basis, weich: true }), { weich: true }, 'die weiche Prüfung geht ausdrücklich mit, auch wenn sie an ist');
  assert.deepEqual(T.pruefenParameter({ ...basis, weich: false }), { weich: false }, 'und ausdrücklich aus');
  assert.deepEqual(T.pruefenParameter({ ...basis, paare: ['hlks-architektur', 'hlks-hlks', 'hlks-fremd'] }), { paare: ['hlks-architektur', 'hlks-hlks', 'hlks-fremd'] });
  assert.deepEqual(T.pruefenParameter({ ...basis, paare: ['hlks-hlks'] }), { paare: ['hlks-hlks'] });
  assert.deepEqual(T.pruefenParameter({ ...basis, unterlagen: ['UL-2'] }), { unterlagen: ['UL-2'] });
  assert.deepEqual(T.pruefenParameter({ ...basis, unterlagen: ['UL-1', 'UL-2'] }), {}, 'alle angekreuzt = alle');
  assert.deepEqual(T.pruefenParameter({ ...basis, geschoss: 'EG', weich: false }), { geschoss: 'EG', weich: false });
  assert.equal(T.pruefenPruefen({ ...basis, paare: [] }), 'Mindestens ein Paar wählen.');
  assert.equal(T.pruefenPruefen(basis), true);
});

test('Prüfen: Paar «Bekleidungen» ist zuschaltbar, aber nie in der Vorgabe', () => {
  const bekl = T.PAARE.find((p) => p.id === 'hlks-bekleidung');
  assert.ok(bekl, 'Paar im Dialog');
  assert.equal(bekl.titel, 'Bekleidungen (Unterdecken, Verkleidungen, Bodenbeläge, Dämmungen)');
  assert.ok(!T.PAARE_VORGABE.includes('hlks-bekleidung'), 'standardmässig aus');
  const basis = { paare: [...T.PAARE_VORGABE], unterlagen: [], alleUnterlagen: [], geschoss: '' };
  assert.deepEqual(T.pruefenParameter(basis), {}, 'aus: nichts zu senden');
  assert.deepEqual(T.pruefenParameter({ ...basis, paare: [...T.PAARE_VORGABE, 'hlks-bekleidung'] }), { paare: ['hlks-architektur', 'hlks-hlks', 'hlks-bekleidung'] }, 'zugeschaltet: die Paarliste geht mit, in der Reihenfolge der Liste');
  assert.deepEqual(T.pruefenParameter({ ...basis, paare: ['hlks-bekleidung'] }), { paare: ['hlks-bekleidung'] }, 'auch allein');
  assert.deepEqual(T.pruefenParameter({ ...basis, paare: ['hlks-fremd', 'hlks-architektur', 'hlks-hlks', 'hlks-bekleidung'] }).paare, ['hlks-architektur', 'hlks-hlks', 'hlks-fremd', 'hlks-bekleidung']);
  // ein Paar, das der Kern (noch) nicht kennt, kommt als Fehler mit Vorschlag zurück und steht im Dialog
  const l = T.laufHtml({ angenommen: false, fehler: 'Paar «hlks-bekleidung» gibt es nicht.', vorschlag: 'paare: hlks-architektur, hlks-hlks, hlks-fremd' }, { trocken: true });
  assert.match(l, /fehlerkasten/);
  assert.match(l, /Paar «hlks-bekleidung» gibt es nicht\./);
  assert.match(l, /Vorschlag: paare: hlks-architektur/);
});

test('Prüfen: weiche Prüfung ist aus, solange keine Abstände gesetzt sind (Entscheid Christian 19.10.2026)', () => {
  assert.equal(T.weichVorgabe(vorgabenLeer), false, 'keine Abstände: aus');
  assert.equal(T.weichVorgabe(vorgabenGesetzt), true, 'abstand_weich gesetzt: an');
  assert.equal(T.weichVorgabe({ werte: { abstand_leitungen: { wert: 60, quelle: FIKTIV } } }), true, 'abstand_leitungen gesetzt: an');
  assert.equal(T.weichVorgabe({ werte: { spiel_aussparung: { wert: 20, quelle: FIKTIV } } }), false, 'andere Vorgaben ändern nichts');
  assert.equal(T.weichVorgabe(null), false);
  assert.match(T.WEICH_HINWEIS_OHNE, /Keine Abstände in den Projektvorgaben gesetzt/);
  assert.match(T.WEICH_HINWEIS_OHNE, /Abstandsprüfung ist aus/);
  // fehlende Abstände sind nur ein Thema, wenn die weiche Prüfung läuft
  assert.deepEqual(T.fehlendeFuerPruefung(vorgabenLeer, { weich: false }).map((f) => f.name), ['toleranz_hart']);
  assert.deepEqual(T.fehlendeFuerPruefung(vorgabenLeer, { weich: true }).map((f) => f.name), ['abstand_weich', 'abstand_leitungen', 'toleranz_hart']);
  assert.deepEqual(T.fehlendeFuerPruefung(vorgabenLeer).map((f) => f.name), ['abstand_weich', 'abstand_leitungen', 'toleranz_hart'], 'ohne Angabe wie bisher');
});

test('Prüfen: fehlende Vorgaben mit Wirkung, Hinweise des Kerns in der Liste', () => {
  const fehlend = T.fehlendeFuerPruefung(vorgabenLeer);
  assert.deepEqual(fehlend.map((f) => f.name), ['abstand_weich', 'abstand_leitungen', 'toleranz_hart']);
  const h = T.fehlendeHtml(fehlend);
  assert.match(h, /Fehlende Projektvorgaben/);
  assert.match(h, /toleranz_hart<\/b> – «Kollisionen prüfen …» meldet jede Durchdringung/);
  assert.equal(T.fehlendeHtml([]), '');
  const lauf = {
    angenommen: true, aenderungen: [],
    hinweise: ['Prüflauf 1 (hlks-architektur, hlks-hlks): 128 HLKS-Prüfkörper, 564 Paare geprüft; 160 Kollisionen – hart 91, weich 69.', '  hlks-architektur: 81 Paare, hart 36.',
      'Status: 160 neu.', 'Weiche Prüfung übersprungen (Vorgabe fehlt, vorgaben_setzen): abstand_weich «sanitaer-sanitaer» – 141 Paar(e).', 'Dauer 1.2 s.'],
  };
  const l = T.laufHtml(lauf, { trocken: true });
  assert.match(l, /Vorschau \(Trockenlauf – nichts wurde geändert\)/);
  assert.match(l, /class="kp-kopf-zeile">Prüflauf 1/);
  assert.match(l, /class="kp-ein">hlks-architektur: 81 Paare/);
  assert.match(l, /class="kp-warn">Weiche Prüfung übersprungen \(Vorgabe fehlt, «Projektvorgaben …»\)/);
  assert.doesNotMatch(l, /class="[^"]*kp-warn[^"]*">Status/);
  assert.match(T.laufHtml(lauf), /<h4 class="kp-ergebnis">Ergebnis/);
  const nein = T.laufHtml({ angenommen: false, fehler: 'Geschoss «X» gibt es nicht.', vorschlag: 'kollision_pruefen mit einem Geschoss' });
  assert.match(nein, /fehlerkasten/);
  assert.match(nein, /«Kollisionen prüfen …» mit einem Geschoss/);
});

// ---------------------------------------------------------------------------------------------- Kollisionsliste

test('Kollisionsliste: Filter → Parameter der Abfrage', () => {
  const f = T.KOLLISION_FILTER_VORGABE();
  assert.deepEqual(T.kollisionFilterParameter(f), {}, 'alles: keine Parameter');
  assert.deepEqual(T.kollisionFilterParameter({ ...f, status: ['bestaetigt', 'neu'], art: 'hart', typ: 'durchdringung', geschoss: 'EG', element: ' T-1 ' }),
    { status: 'neu,bestaetigt', art: 'hart', typ: 'durchdringung', geschoss: 'EG', element: 'T-1' }, 'Reihenfolge wie im Katalog');
  assert.equal(T.kollisionFilterParameter({ ...f, status: [] }), null, 'kein Status: leere Liste, keine Abfrage');
  assert.notEqual(T.KOLLISION_FILTER_VORGABE().status, T.KOLLISION_STATUS, 'jede Sitzung bekommt eine eigene Kopie');
});

test('Kollisionsliste: Tabelle mit Zeilen, Auswahl, aktiver Zeile und Kennzahlen', () => {
  const h = T.kollisionTabelleHtml(kollisionen, { auswahl: new Set(['KOL-2']), aktiv: 'KOL-1' });
  assert.equal((h.match(/class="kp-zeile/g) ?? []).length, 3);
  assert.match(h, /<tr class="kp-zeile aktiv" data-kz="KOL-1">/);
  assert.match(h, /data-kz="KOL-2"><td><input type="checkbox" class="kp-wahl"[^>]* checked>/);
  assert.match(h, /kp-art-hart">hart/);
  assert.match(h, /kp-st-bestaetigt">bestätigt/);
  assert.match(h, /<td class="zahl">175<\/td>/);
  assert.match(h, /<td class="zahl">35\.5<\/td>/);
  assert.match(h, /<td>–<\/td><td title="IfcWall/, 'Kollision ohne Geschoss');
  assert.match(h, /Architekt fragen zweite Zeile/, 'Kommentar einzeilig verkürzt');
  assert.equal(T.kollisionKopf(kollisionen), '3 Kollisionen · neu 2, bestätigt 1 · hart 2, weich 1 · letzter Prüflauf: 2026-10-19 05:12 · Lauf 1 · hlks-architektur, hlks-hlks');
  assert.match(T.kollisionTabelleHtml({ zeilen: [], hinweis: 'Noch keine Kollisionen – Prüflauf' }), /Noch keine Kollisionen/);
  assert.match(T.kollisionTabelleHtml({ zeilen: [] }), /Keine Kollision passt zum Filter/);
  const viele = { zeilen: Array.from({ length: 600 }, (_, i) => ({ kollision: `KOL-${i + 1}`, art: 'hart', typ: 'kontakt', status: 'neu' })) };
  const v = T.kollisionTabelleHtml(viele, { max: 500 });
  assert.equal((v.match(/class="kp-zeile/g) ?? []).length, 500);
  assert.match(v, /und 100 weitere/);
});

test('Kollisionsliste: Einzelheiten – Beteiligte aus dem Modell sind anklickbar, IFC-Produkte nur Text', () => {
  const d = T.kollisionDetailHtml(kollisionen.zeilen[0]);
  assert.match(d, /data-ref="T-1"/);
  assert.match(d, /data-ref="W-EG-S"/);
  assert.match(d, /Lage x 3000 · y 0 · z 1000 mm/);
  const k = T.kollisionDetailHtml(kollisionen.zeilen[2]);
  assert.match(k, /class="chip kp-ifc"[^>]*>IfcWall 3x \(UL-1\)/);
  assert.match(k, /data-ref="T-2"/);
  assert.match(T.kollisionDetailHtml(kollisionen.zeilen[1]), /class="kp-kommentar">Architekt fragen\nzweite Zeile/);
  assert.match(T.kollisionDetailHtml(null), /Eine Zeile wählen/);
});

test('Kollisionsliste: Mehrfachauswahl und Status setzen', () => {
  const z = kollisionen.zeilen;
  assert.deepEqual(T.ausgewaehlte(new Set(['KOL-3', 'KOL-1']), z, 'KOL-2'), ['KOL-3', 'KOL-1'], 'Häkchen gehen vor');
  assert.deepEqual(T.ausgewaehlte(new Set(), z, 'KOL-2'), ['KOL-2'], 'ohne Häkchen die aktive Zeile');
  assert.deepEqual(T.ausgewaehlte(new Set(), z, 'KOL-9'), [], 'aktive Zeile gibt es nicht mehr');
  assert.deepEqual(T.ausgewaehlte(new Set(['KOL-9', 'KOL-1']), z, null), ['KOL-1'], 'gefilterte Kollisionen fallen aus der Auswahl');
  assert.deepEqual(T.kollisionStatusParameter({ kollisionen: ['KOL-1', 'KOL-2'], status: 'akzeptiert', kommentar: '', kommentarEntfernen: false }), { kollisionen: ['KOL-1', 'KOL-2'], status: 'akzeptiert' }, 'Kommentar unverändert');
  assert.deepEqual(T.kollisionStatusParameter({ kollisionen: ['KOL-1'], status: 'bestaetigt', kommentar: ' mit Architekt besprochen ' }), { kollisionen: ['KOL-1'], status: 'bestaetigt', kommentar: 'mit Architekt besprochen' });
  assert.deepEqual(T.kollisionStatusParameter({ kollisionen: ['KOL-1'], status: 'neu', kommentar: '', kommentarEntfernen: true }), { kollisionen: ['KOL-1'], status: 'neu', kommentar: '' }, 'leerer Kommentar entfernt ihn');
  assert.match(T.kollisionStatusPruefen({ kollisionen: [], status: 'neu' }), /Zuerst Kollisionen wählen/);
  assert.match(T.kollisionStatusPruefen({ kollisionen: ['KOL-1'], status: 'gelb' }), /Status wählen/);
  assert.equal(T.kollisionStatusPruefen({ kollisionen: ['KOL-1'], status: 'erledigt' }), true);
});

// ---------------------------------------------------------------------------------------------- Nachtrag C1c: Paar Vorwände, Paare des letzten Laufs, Typenliste

test('Paar «Installationsvorwände (z. B. Geberit GIS)»: zuschaltbar, nicht vorgewählt, Parameter in der Reihenfolge der Liste', () => {
  const vw = T.PAARE.find((p) => p.id === 'hlks-vorwand');
  assert.ok(vw, 'Paar hlks-vorwand im Dialog');
  assert.equal(vw.titel, 'Installationsvorwände (z. B. Geberit GIS)');
  assert.match(vw.hinweis, /nicht in der Vorgabe/);
  assert.match(vw.hinweis, /Leitungen in Vorwänden sind weder Kollision noch Aussparung, solange das Paar aus ist/);
  assert.ok(!T.PAARE_VORGABE.includes('hlks-vorwand'), 'standardmässig aus');
  assert.deepEqual(T.PAARE.map((p) => p.id), ['hlks-architektur', 'hlks-hlks', 'hlks-fremd', 'hlks-bekleidung', 'hlks-vorwand']);
  const basis = { paare: [...T.PAARE_VORGABE], unterlagen: [], alleUnterlagen: [], geschoss: '' };
  assert.deepEqual(T.pruefenParameter(basis), {}, 'ohne Abweichung geht nichts mit');
  assert.deepEqual(T.pruefenParameter({ ...basis, paare: [...T.PAARE_VORGABE, 'hlks-vorwand'] }), { paare: ['hlks-architektur', 'hlks-hlks', 'hlks-vorwand'] });
  assert.deepEqual(T.pruefenParameter({ ...basis, paare: ['hlks-vorwand', 'hlks-bekleidung', 'hlks-fremd', 'hlks-hlks', 'hlks-architektur'] }).paare, ['hlks-architektur', 'hlks-hlks', 'hlks-fremd', 'hlks-bekleidung', 'hlks-vorwand']);
  for (const id of T.PAARE.map((p) => p.id)) assert.ok(T.PAAR_KURZ[id], `Kurzname für ${id}`);
  assert.equal(T.paarText('hlks-vorwand'), 'Vorwände');
  assert.equal(T.paarText('hlks-neu'), 'hlks-neu', 'unbekanntes Paar: die Kennung');
});

const paarZeile = (kz, paar, o = {}) => ({ kollision: kz, art: 'hart', typ: 'kontakt', paar, status: 'neu', mass_mm: 10, geschoss: 'EG', beteiligte: 'A ↔ B', kommentar: null, ...o });
const kolPaare = {
  ableitung: 'Kollisionsliste', anzahl: 6, kollisionslauf: '2026-10-19 10:36 · Lauf 2 · hlks-architektur, hlks-hlks',
  zusammenfassung: { jeStatus: { neu: 5, erledigt: 1 }, jeArt: { hart: 6 }, jePaar: { 'hlks-architektur': 3, 'hlks-hlks': 1, 'hlks-vorwand': 1, 'hlks-bekleidung': 1 } },
  zeilen: [paarZeile('KOL-1', 'hlks-architektur'), paarZeile('KOL-2', 'hlks-architektur', { typ: 'durchdringung' }), paarZeile('KOL-3', 'hlks-architektur', { status: 'erledigt' }),
    paarZeile('KOL-4', 'hlks-hlks', { art: 'weich' }), paarZeile('KOL-5', 'hlks-vorwand'), paarZeile('KOL-6', 'hlks-bekleidung')],
};

test('Paare des letzten Laufs: der Kern nennt sie in kollisionslauf, sonst gelten die Vorgabe und die zuletzt gewählten Paare', () => {
  assert.deepEqual(T.laufPaare('2026-10-19 10:36 · Lauf 2 · hlks-architektur, hlks-hlks'), ['hlks-architektur', 'hlks-hlks']);
  assert.deepEqual(T.laufPaare('2026-10-19 10:36 · Lauf 3 · hlks-vorwand'), ['hlks-vorwand']);
  assert.deepEqual(T.laufPaare('2026-10-19 10:36 · Lauf 3 · hlks-architektur, hlks-bekleidung, hlks-vorwand, hlks-hlks, hlks-fremd'), ['hlks-architektur', 'hlks-bekleidung', 'hlks-vorwand', 'hlks-hlks', 'hlks-fremd']);
  assert.equal(T.laufPaare('2026-10-19 09:55 · Lauf 2 · 120 Paare'), null, 'älteres Format ohne Paarnamen');
  assert.equal(T.laufPaare('Lauf 2'), null);
  assert.equal(T.laufPaare(null), null, 'noch kein Lauf');
  assert.equal(T.laufPaare(''), null);
  assert.deepEqual(T.geprueftePaare(kolPaare), ['hlks-architektur', 'hlks-hlks']);
  assert.deepEqual(T.geprueftePaare({ kollisionslauf: null }), T.PAARE_VORGABE, 'ohne Lauf: die Vorgabe des Kerns');
  assert.deepEqual(T.geprueftePaare({ kollisionslauf: '2026-10-19 09:55 · Lauf 2 · 120 Paare' }, ['hlks-vorwand']), ['hlks-architektur', 'hlks-hlks', 'hlks-vorwand'], 'Vorgabe plus zuletzt gewählte');
  assert.deepEqual(T.geprueftePaare({}, ['hlks-hlks', 'hlks-fremd']), ['hlks-architektur', 'hlks-hlks', 'hlks-fremd'], 'ohne doppelte');
  assert.deepEqual(T.geprueftePaare(kolPaare, ['hlks-fremd']), ['hlks-architektur', 'hlks-hlks'], 'der Kern geht vor: die Wahl im Dialog zählt nur im Rückfall');
});

test('Kollisionsliste: nicht geprüfte Paare sind standardmässig ausgeblendet, ein Schalter blendet sie ein (mit Zahl), ein gewähltes Paar zeigt sich immer', () => {
  const f = T.KOLLISION_FILTER_VORGABE();
  assert.equal(f.paar, ''); assert.equal(f.auchNichtGeprueft, false);
  const s = T.kollisionSicht(kolPaare, f);
  assert.deepEqual(s.zeilen.map((z) => z.kollision), ['KOL-1', 'KOL-2', 'KOL-3', 'KOL-4'], 'Vorwand- und Bekleidungs-Kollision ausgeblendet (Paar im letzten Lauf nicht geprüft)');
  assert.equal(s.anzahl, 4); assert.equal(s.ausgeblendet, 2); assert.equal(s.nichtGeprueft, 2); assert.equal(s.paarHerkunft, 'lauf');
  assert.deepEqual(s.zusammenfassung.jeStatus, { erledigt: 1, neu: 3 }, 'Zahlen neu gezählt, Schlüssel geordnet');
  assert.deepEqual(s.zusammenfassung.jeArt, { hart: 3, weich: 1 });
  assert.deepEqual(s.zusammenfassung.jePaar, { 'hlks-architektur': 3, 'hlks-hlks': 1 });
  assert.deepEqual(s.jePaarAntwort, { 'hlks-architektur': 3, 'hlks-bekleidung': 1, 'hlks-hlks': 1, 'hlks-vorwand': 1 }, 'die Zahlen der Antwort für den Paar-Filter');
  assert.equal(kolPaare.zeilen.length, 6, 'die Antwort bleibt unverändert');
  assert.equal(T.kollisionKopf(s), '4 Kollisionen · 2 nicht geprüfte ausgeblendet · erledigt 1, neu 3 · hart 3, weich 1 · letzter Prüflauf: 2026-10-19 10:36 · Lauf 2 · hlks-architektur, hlks-hlks');
  assert.equal(T.ungeprueftText(s, false), 'auch nicht geprüfte Paare (2 ausgeblendet)');

  const an = T.kollisionSicht(kolPaare, { ...f, auchNichtGeprueft: true });
  assert.equal(an.zeilen.length, 6); assert.equal(an.ausgeblendet, 0); assert.equal(an.nichtGeprueft, 2);
  assert.equal(T.ungeprueftText(an, true), 'auch nicht geprüfte Paare (2 eingeblendet)');
  assert.doesNotMatch(T.kollisionKopf(an), /ausgeblendet/);

  const vw = T.kollisionSicht(kolPaare, { ...f, paar: 'hlks-vorwand' });
  assert.deepEqual(vw.zeilen.map((z) => z.kollision), ['KOL-5'], 'ausdrücklich gewähltes Paar: sichtbar, auch wenn es nicht geprüft wurde');
  assert.equal(vw.ausgeblendet, 0);
  assert.match(T.ungeprueftText(vw, false), /2 vorhanden, mit dem gewählten Paar sichtbar/);
  assert.deepEqual(T.kollisionSicht(kolPaare, { ...f, paar: 'hlks-hlks' }).zeilen.map((z) => z.kollision), ['KOL-4']);
  assert.deepEqual(T.kollisionSicht(kolPaare, { ...f, paar: 'hlks-fremd' }).zeilen, [], 'Paar ohne Kollision: leer');
  assert.match(T.ungeprueftText(T.kollisionSicht({ ...kolPaare, zeilen: [paarZeile('KOL-1', 'hlks-architektur')] }, f), false), /keine Kollision betroffen/);
});

test('Kollisionsliste: Rückfall ohne Paare des Laufs (älterer Kern, kein Lauf), Zeilen ohne Paar, null', () => {
  const alt = { ...kolPaare, kollisionslauf: '2026-10-19 09:55 · Lauf 2 · 120 Paare' };
  const s = T.kollisionSicht(alt, T.KOLLISION_FILTER_VORGABE());
  assert.equal(s.paarHerkunft, 'vorgabe');
  assert.deepEqual(s.geprueftePaare, T.PAARE_VORGABE);
  assert.equal(s.zeilen.length, 4, 'Vorgabe: Architektur und HLKS');
  assert.match(T.ungeprueftTitel(s), /nennt die Paare des letzten Laufs nicht/);
  const mitWahl = T.kollisionSicht(alt, T.KOLLISION_FILTER_VORGABE(), { zuletzt: ['hlks-vorwand'] });
  assert.deepEqual(mitWahl.zeilen.map((z) => z.kollision), ['KOL-1', 'KOL-2', 'KOL-3', 'KOL-4', 'KOL-5'], 'im Dialog zuletzt gewählt: Vorwände zählen als geprüft');
  assert.match(T.ungeprueftTitel(T.kollisionSicht(kolPaare, T.KOLLISION_FILTER_VORGABE())), /Der letzte Prüflauf hat geprüft: Architektur, HLKS\./);
  // ältere Kerne liefern kein Paar: nichts wird ausgeblendet
  const ohne = T.kollisionSicht(kollisionen, T.KOLLISION_FILTER_VORGABE());
  assert.equal(ohne.zeilen.length, 3); assert.equal(ohne.ausgeblendet, 0);
  assert.equal(T.kollisionKopf(ohne), T.kollisionKopf(kollisionen), 'Kopf unverändert, wenn nichts ausgeblendet ist');
  assert.equal(T.kollisionSicht(null, {}), null);
  assert.equal(T.kollisionSicht({ zeilen: [], anzahl: 0, hinweis: 'Kein Status gewählt.' }, {}).hinweis, 'Kein Status gewählt.');
});

test('Kollisionsliste: Paar-Spalte, Vermerk «nicht geprüft», Auswahl des Paar-Filters mit Zahlen', () => {
  const an = T.kollisionSicht(kolPaare, { ...T.KOLLISION_FILTER_VORGABE(), auchNichtGeprueft: true });
  const h = T.kollisionTabelleHtml(an, {});
  assert.match(h, /<th>Typ<\/th><th>Paar<\/th><th>Status<\/th>/);
  assert.match(h, /<td title="hlks-architektur">Architektur<\/td>/);
  assert.match(h, /class="kp-paar-aus"[^>]*>Vorwände \(nicht geprüft\)<\/td>/);
  assert.match(h, /class="kp-paar-aus"[^>]*>Bekleidungen \(nicht geprüft\)<\/td>/);
  assert.equal((h.match(/kp-paar-aus/g) ?? []).length, 2, 'nur die nicht geprüften Paare tragen den Vermerk');
  assert.doesNotMatch(T.kollisionTabelleHtml(kollisionen, {}), /<th>Paar<\/th>/, 'ohne Paar in der Antwort keine Spalte');
  // alles ausgeblendet: der Hinweis nennt die Zahl und den Schalter
  const nur = T.kollisionSicht({ ...kolPaare, zeilen: [paarZeile('KOL-5', 'hlks-vorwand')] }, T.KOLLISION_FILTER_VORGABE());
  assert.match(T.kollisionTabelleHtml(nur, {}), /Keine Kollision in den geprüften Paaren \(Architektur, HLKS\) – 1 Kollision\(en\) nicht geprüfter Paare sind ausgeblendet/);
  // viele Zeilen: der Hinweis «weitere» zählt die Paar-Spalte mit
  const viele = { zeilen: Array.from({ length: 600 }, (_, i) => paarZeile(`KOL-${i + 1}`, 'hlks-architektur')), kollisionslauf: kolPaare.kollisionslauf };
  assert.match(T.kollisionTabelleHtml(T.kollisionSicht(viele, T.KOLLISION_FILTER_VORGABE()), { max: 500 }), /colspan="10"[^>]*>… und 100 weitere/);
  const o = T.paarOptionenHtml(T.kollisionSicht(kolPaare, T.KOLLISION_FILTER_VORGABE()), '');
  assert.match(o, /<option value="">\(alle Paare\)<\/option>/);
  assert.match(o, /<option value="hlks-architektur">Architektur \(3\)<\/option>/);
  assert.match(o, /<option value="hlks-vorwand">Vorwände \(1, nicht geprüft\)<\/option>/);
  assert.match(o, /<option value="hlks-fremd">Fremd-IFC \(0\)<\/option>/, 'Paar ohne Kollision: 0, kein Vermerk');
  assert.match(T.paarOptionenHtml(T.kollisionSicht(kolPaare, T.KOLLISION_FILTER_VORGABE()), 'hlks-hlks'), /<option value="hlks-hlks" selected>/);
  assert.doesNotThrow(() => T.paarOptionenHtml(null, ''), 'vor dem ersten Laden');
  assert.doesNotMatch(T.paarOptionenHtml(null, ''), /\(\d/, 'ohne Antwort keine Zahlen');
});

// ---- Typenliste

const KAT_WIRKUNG = { beton: 'Rohbau: Aussparung; Testtext', mauerwerk: 'Rohbau: Aussparung; Testtext', bekleidung: 'keine Aussparung; Kollision nur im Paar hlks-bekleidung', installationsvorwand: 'keine Aussparung; Kollision nur im Paar hlks-vorwand' };
const GRUPPE = { beton: 'Beton', mauerwerk: 'Mauerwerk', leichtbau: 'Leichtbau', holz: 'Holz', stahl: 'Stahl/Metall', daemmung: 'Dämmung', bekleidung: 'Bekleidung/Ausbau', installationsvorwand: 'Installationsvorwand',
  bodenaufbau: 'Bodenaufbau', luftschicht: 'Luftschicht' };
// Die Kategorien des Kerns stehen im Katalog (Typmaterial.kategorie); der Test hält keine feste Zahl, damit neue Kategorien (B3e: bodenaufbau, luftschicht) ihn nicht brechen.
const kategorienKatalog = eig('Typmaterial', 'kategorie')?.auswahl ?? [];
const kategorienKern = kategorienKatalog.map((k) => ({
  kategorie: k, gruppe: GRUPPE[k] ?? k, aussparung: k === 'beton' || k === 'mauerwerk', wirkung: KAT_WIRKUNG[k] ?? 'keine Aussparung; eine Durchdringung bleibt eine Kollision (ohne P20)' }));
// Form der Antwort von typmaterial (TypmaterialAbfrage.Lesen); Typnamen und Zahlen FIKTIV
const typmaterial = {
  ableitung: 'Typenliste', filter: { betroffen: false },
  typen: [
    { element: 'TM-1', typname: 'Typ Alpha 20', kategorie: 'beton', gruppe: 'Beton', aussparung: true, wirkung: KAT_WIRKUNG.beton, quelle: 'FIKTIV Quelle A', gesetzt: 'mensch, CS, 2026-10-19 12:07', bauteile: 3, aussparungen: 3, durchgaenge: 160, typnamen: ['DirectShape:Typ Alpha 20'], entitaeten: 'IfcSlab 3' },
    { element: 'TM-2', typname: 'Typ Tippfehler', kategorie: 'holz', gruppe: 'Holz', aussparung: false, wirkung: 'keine Aussparung; eine Durchdringung bleibt eine Kollision (ohne P20)', quelle: 'FIKTIV Quelle B', gesetzt: 'mensch, CS, 2026-10-19 12:08', bauteile: 0, aussparungen: 0, durchgaenge: 0, typnamen: [], entitaeten: '' },
  ],
  unbekannt: [   // Reihenfolge des Kerns: nach Aussparungen + Durchgängen; die Oberfläche ordnet nach Durchgängen
    { schluessel: 'Typ Beta', typnamen: ['DirectShape:Typ Beta'], entitaeten: 'IfcSlab 3', bauteile: 3, aussparungen: 15, durchgaenge: 95, material: 'Farbe, weiss 3' },
    { schluessel: 'Typ <Gamma> & "Co"', typnamen: ['DirectShape:Typ <Gamma> & "Co"', 'Typ <Gamma> & "Co"'], entitaeten: 'IfcWall 1', bauteile: 1, aussparungen: 0, durchgaenge: 160, material: '<Unnamed> 1' },
    { schluessel: 'Typ Delta', typnamen: ['DirectShape:Typ Delta'], entitaeten: 'IfcRoof 10', bauteile: 10, aussparungen: 8, durchgaenge: 95, material: 'Farbe, weiss 6, <Unnamed> 4' },
    { schluessel: 'Typ Epsilon', typnamen: ['Typ Epsilon'], entitaeten: 'IfcColumn 4', bauteile: 4, aussparungen: 0, durchgaenge: 0, material: '(keine Zuordnung) 4' },
  ],
  ausTypname: [
    { schluessel: '_IW GIS Test', typnamen: ['DirectShape:_IW GIS Test'], entitaeten: 'IfcWall 25', bauteile: 25, aussparungen: 18, durchgaenge: 63, gruppe: 'Installationsvorwand', kategorie: 'installationsvorwand', aussparung: false, annahme: 'ANNAHME Prüfliste (Nachtrag B3d): «GIS» = Installationsvorwand' },
    { schluessel: 'Typ Beton Test', typnamen: ['Typ Beton Test'], entitaeten: 'IfcSlab 2', bauteile: 2, aussparungen: 0, durchgaenge: 33, gruppe: 'Beton', kategorie: 'beton', aussparung: true },
    { schluessel: 'Typ Mehrdeutig', typnamen: ['Typ Mehrdeutig'], entitaeten: 'IfcWall 2', bauteile: 2, aussparungen: 0, durchgaenge: 1, gruppe: 'Beton, Holz', kategorie: null, aussparung: true },
  ],
  kategorien: kategorienKern,
  ohneQuelle: ['TM-3'], doppelt: [{ typname: 'Typ Doppelt', gilt: 'TM-4', weitere: ['TM-5'] }],
  hinweise: [
    'TM-2 «Typ Tippfehler» passt auf kein Bauteil der Unterlagen bzw. von KICAD – Schreibweise prüfen.',
    'TM-3 «Typ Ohne» ohne Quelle – gilt nicht; mit typmaterial_setzen und Quelle neu setzen.',
    '«Typ Doppelt» steht mehrfach (TM-4, TM-5) – es gilt TM-4 (zuletzt gesetzt); typmaterial_setzen für diesen Typ räumt auf.',
    'Unterlage UL-2: Netzdatei fehlt bzw. ist nicht lesbar – ihre Typen fehlen hier.',
    'Durchgänge = Kollisionen typ durchdringung des letzten Prüflaufs (kollision_pruefen); Aussparungen = gültige Aussparungen (aussparungen_vorschlagen).',
  ],
  zusammenfassung: { eintraege: 2, bauteile: 100, mitTypname: 100, jeHerkunft: { keine: 18, material: 60, typenliste: 3, typname: 19 }, unbekannt: 18, unbekannteTypen: 4, unbekanntOhneTypname: 0 },
};
const typmaterialLeer = { ableitung: 'Typenliste', filter: { betroffen: false }, typen: [], unbekannt: [], ausTypname: [], kategorien: kategorienKern, ohneQuelle: [], doppelt: [],
  hinweise: ['Die Typenliste ist leer – mit typmaterial_setzen festlegen (je Typ Kategorie und Quelle).', 'Durchgänge = Kollisionen typ durchdringung des letzten Prüflaufs (kollision_pruefen); Aussparungen = gültige Aussparungen (aussparungen_vorschlagen).'],
  zusammenfassung: { eintraege: 0, bauteile: 0, mitTypname: 0, jeHerkunft: {}, unbekannt: 0, unbekannteTypen: 0, unbekanntOhneTypname: 0 } };

test('Typenliste: die Kategorien kommen vom Kern und passen zum Katalog; die Oberfläche hat keine eigene Liste', () => {
  const auswahl = eig('Typmaterial', 'kategorie')?.auswahl;
  // Nicht mehr «acht»: B3e (Entscheid Christian 19.10.2026) ergänzt bodenaufbau und luftschicht; geprüft wird gegen die Liste des Katalogs, nicht gegen eine feste Zahl.
  assert.ok(Array.isArray(auswahl) && auswahl.length >= 8, 'Katalog: Kategorien vorhanden');
  for (const kat of ['beton', 'mauerwerk', 'leichtbau', 'holz', 'stahl', 'daemmung', 'bekleidung', 'installationsvorwand', 'bodenaufbau', 'luftschicht']) assert.ok(auswahl.includes(kat), `Katalog enthält ${kat}`);
  assert.equal(new Set(auswahl).size, auswahl.length, 'keine doppelte Kategorie');
  assert.deepEqual(T.typenKategorien(typmaterial).map((k) => k.kategorie), auswahl, 'Auswahl = Katalog (Kern: Typenliste.Kategorien)');
  const k = kernDatei('Kollision/Typenliste.cs');
  if (k) for (const kat of auswahl) assert.ok(new RegExp(`\\["${kat}"\\] = Materialgruppe\\.`).test(k), `Kategorie ${kat} im Kern`);
  const o = T.kategorieOptionenHtml(typmaterial, 'beton');
  assert.match(o, /<option value="">\(nicht festlegen\)<\/option>/);
  assert.match(o, /<option value="beton" title="Rohbau: Aussparung; Testtext" selected>Beton \(Aussparung\)<\/option>/);
  assert.match(o, /<option value="stahl"[^>]*>Stahl\/Metall<\/option>/, 'Beschriftung = Gruppe des Kerns');
  assert.match(o, /<option value="installationsvorwand"[^>]*>Installationsvorwand<\/option>/);
  assert.equal((o.match(/<option/g) ?? []).length, auswahl.length + 1, 'eine Option je Kategorie des Katalogs plus «nicht festlegen»');
  assert.match(o, /<option value="bodenaufbau"[^>]*>Bodenaufbau<\/option>/, 'B3e: bodenaufbau in der Auswahl');
  assert.match(o, /<option value="luftschicht"[^>]*>Luftschicht<\/option>/, 'B3e: luftschicht in der Auswahl');
  assert.equal(T.kategorieWirkung(typmaterial, 'bekleidung'), KAT_WIRKUNG.bekleidung);
  assert.equal(T.kategorieWirkung(typmaterial, ''), '');
  assert.equal(T.kategorieName(typmaterial, 'daemmung'), 'Dämmung');
  assert.deepEqual(T.typenKategorien({}), [], 'ohne Liste des Kerns keine Auswahl (nichts geraten)');
  assert.match(T.kategorieOptionenHtml({}, ''), /^<option value="">\(nicht festlegen\)<\/option>$/);
});

test('Typenliste: unbekannte Typen nach Durchgängen absteigend', () => {
  const s = T.unbekanntSortiert(typmaterial.unbekannt).map((r) => r.schluessel);
  assert.deepEqual(s, ['Typ <Gamma> & "Co"', 'Typ Beta', 'Typ Delta', 'Typ Epsilon'], '160 · 95 (15 Aussparungen) · 95 (8) · 0');
  assert.deepEqual(T.unbekanntSortiert(undefined), []);
  assert.equal(typmaterial.unbekannt[0].schluessel, 'Typ Beta', 'die Antwort bleibt unverändert');
  assert.equal(T.typenStartReiter(typmaterial), 'unbekannt');
  assert.equal(T.typenStartReiter({ unbekannt: [], ausTypname: [{ schluessel: 'x' }] }), 'bestaetigen');
  assert.equal(T.typenStartReiter(typmaterialLeer), 'liste');
  assert.deepEqual(T.typenAbfrageParameter({ betroffen: false }), {});
  assert.deepEqual(T.typenAbfrageParameter({ betroffen: true }), { betroffen: true }, 'Schalter «nur betroffene» geht als Parameter an den Kern');
});

test('Typenliste: Unbekannte Typen festlegen – Kategorie je Zeile, eine Quelle für alle (Pflicht, nie vorbelegt)', () => {
  const f = T.typenFormular();
  assert.equal(f.quelle, '', 'Quelle vorbelegt leer');
  assert.equal(f.reiter, 'unbekannt');
  assert.deepEqual(T.typenParameter(T.typenAuswertung(f, typmaterial)), null, 'nichts gewählt: nichts zu senden');
  assert.match(T.typenPruefen(T.typenAuswertung(f, typmaterial)), /Zuerst etwas wählen/);
  f.unbekannt['Typ Beta'] = 'beton';
  f.unbekannt['Typ Delta'] = 'leichtbau';
  f.unbekannt['Typ Epsilon'] = '';   // «(nicht festlegen)»
  let a = T.typenAuswertung(f, typmaterial);
  assert.deepEqual(a.typen, [{ typname: 'Typ Beta', kategorie: 'beton' }, { typname: 'Typ Delta', kategorie: 'leichtbau' }]);
  assert.equal(a.fehler.length, 1);
  assert.match(a.fehler[0], /^Quelle fehlt/);
  assert.equal(T.typenParameter(a), null, 'ohne Quelle sendet die Oberfläche nichts');
  assert.match(T.typenPruefen(a), /Quelle fehlt/);
  f.quelle = '   ';
  assert.equal(T.typenParameter(T.typenAuswertung(f, typmaterial)), null, 'Leerschläge sind keine Quelle');
  f.quelle = '  FIKTIV Quelle C ';
  a = T.typenAuswertung(f, typmaterial);
  assert.deepEqual(a.fehler, []);
  assert.deepEqual(T.typenParameter(a), { typen: [{ typname: 'Typ Beta', kategorie: 'beton' }, { typname: 'Typ Delta', kategorie: 'leichtbau' }], quelle: 'FIKTIV Quelle C' }, 'Quelle getrimmt, eine für alle');
  assert.equal(T.typenPruefen(a), true);
  assert.equal(T.typenKnopfText(f, typmaterial), 'Festlegen (2)');
  assert.equal(T.typenKnopfText(T.typenFormular(), typmaterial), 'Festlegen');
  f.unbekannt['Typ Beta'] = 'gibtsnicht';
  assert.match(T.typenAuswertung(f, typmaterial).fehler.join(' '), /Kategorie «gibtsnicht» gibt es nicht/);
  assert.equal(T.typenParameter(T.typenAuswertung(f, typmaterial)), null);
  const wieder = T.typenParameter(T.typenAuswertung({ ...T.typenFormular(), quelle: 'Q', unbekannt: { 'Typ <Gamma> & "Co"': 'holz' } }, typmaterial));
  assert.deepEqual(wieder, { typen: [{ typname: 'Typ <Gamma> & "Co"', kategorie: 'holz' }], quelle: 'Q' }, 'der Schlüssel geht unverändert als typname (ohne «Familie:»)');
});

test('Typenliste: Material aus Typname bestätigen – erkannte Kategorie vorbelegt, bestätigt wird mit Häkchen und Quelle', () => {
  const f = { ...T.typenFormular(), reiter: 'bestaetigen' };
  const z = typmaterial.ausTypname;
  assert.deepEqual(T.bestaetigenZeile(f, z[0]), { an: false, kategorie: 'installationsvorwand' }, 'Vorbelegung: erkannte Kategorie, nicht angehakt');
  assert.deepEqual(T.bestaetigenZeile(f, z[2]), { an: false, kategorie: '' }, 'mehrdeutig: keine Vorbelegung');
  assert.equal(T.typenParameter(T.typenAuswertung(f, typmaterial)), null, 'nichts angehakt');
  f.bestaetigen['_IW GIS Test'] = { an: true, kategorie: 'installationsvorwand' };
  f.bestaetigen['Typ Beton Test'] = { an: true, kategorie: 'mauerwerk' };   // der Mensch ändert die Kategorie
  assert.match(T.typenAuswertung(f, typmaterial).fehler[0], /^Quelle fehlt/);
  f.quelle = 'FIKTIV Quelle D';
  assert.deepEqual(T.typenParameter(T.typenAuswertung(f, typmaterial)), { typen: [{ typname: '_IW GIS Test', kategorie: 'installationsvorwand' }, { typname: 'Typ Beton Test', kategorie: 'mauerwerk' }], quelle: 'FIKTIV Quelle D' });
  assert.equal(T.typenKnopfText(f, typmaterial), 'Bestätigen (2)');
  f.bestaetigen['Typ Mehrdeutig'] = { an: true, kategorie: '' };
  assert.match(T.typenAuswertung(f, typmaterial).fehler.join(' '), /«Typ Mehrdeutig»: der Kern erkennt keine eindeutige Kategorie/);
  assert.equal(T.typenParameter(T.typenAuswertung(f, typmaterial)), null, 'ohne Kategorie nichts senden');
});

test('Typenliste: Einträge ändern (Kategorie, Quelle) und entfernen – jede Zeile mit eigener Quelle', () => {
  const f = { ...T.typenFormular(), reiter: 'liste' };
  const t = typmaterial.typen;
  assert.deepEqual(T.listeZeile(f, t[0]), { kategorie: 'beton', quelle: 'FIKTIV Quelle A', entfernen: false }, 'Vorbelegung: der Eintrag');
  assert.equal(T.typenParameter(T.typenAuswertung(f, typmaterial)), null, 'unverändert: nichts zu senden');
  f.liste['TM-1'] = { kategorie: 'beton', quelle: 'FIKTIV Quelle A', entfernen: false };
  assert.equal(T.typenParameter(T.typenAuswertung(f, typmaterial)), null, 'angefasst, aber gleich: nichts');
  f.liste['TM-1'] = { kategorie: 'leichtbau', quelle: 'FIKTIV Quelle A', entfernen: false };
  let a = T.typenAuswertung(f, typmaterial);
  assert.deepEqual(a.typen, [{ typname: 'Typ Alpha 20', kategorie: 'leichtbau', quelle: 'FIKTIV Quelle A' }]);
  assert.equal(a.warnungen.length, 1);
  assert.match(a.warnungen[0], /TM-1 «Typ Alpha 20»: Kategorie geändert, Quelle gleich geblieben – gilt «FIKTIV Quelle A» auch für Leichtbau\?/);
  assert.deepEqual(T.typenParameter(a), { typen: [{ typname: 'Typ Alpha 20', kategorie: 'leichtbau', quelle: 'FIKTIV Quelle A' }] }, 'die Quelle steht an der Zeile, keine gemeinsame');
  f.liste['TM-1'] = { kategorie: 'leichtbau', quelle: 'FIKTIV Quelle E', entfernen: false };
  a = T.typenAuswertung(f, typmaterial);
  assert.deepEqual(a.warnungen, [], 'neue Quelle: keine Rückfrage');
  assert.equal(a.typen[0].quelle, 'FIKTIV Quelle E');
  f.liste['TM-1'] = { kategorie: 'leichtbau', quelle: '  ', entfernen: false };
  assert.match(T.typenAuswertung(f, typmaterial).fehler[0], /TM-1 «Typ Alpha 20»: Quelle fehlt/);
  f.liste['TM-1'] = { kategorie: 'beton', quelle: 'FIKTIV Quelle A', entfernen: false };
  f.liste['TM-2'] = { kategorie: 'holz', quelle: 'FIKTIV Quelle B', entfernen: true };
  a = T.typenAuswertung(f, typmaterial);
  assert.deepEqual(T.typenParameter(a), { typen: [{ typname: 'Typ Tippfehler', kategorie: null }] }, 'entfernen: kategorie null, keine Quelle nötig');
  assert.equal(T.typenKnopfText(f, typmaterial), 'Änderungen speichern (1)');
  assert.equal(T.typenKnopfText({ ...T.typenFormular(), reiter: 'liste' }, typmaterial), 'Änderungen speichern');
  const nur = T.typenNachherHtml([{ typname: 'x', kategorie: null }]);
  assert.match(nur, /1 Typ aus der Typenliste entfernt\./);
});

test('Typenliste: Tabellen – Zeilen, Auswahl je Zeile, Zahlen, Warnungen; alles maskiert', () => {
  const u = T.typenUnbekanntHtml(typmaterial, { ...T.typenFormular(), unbekannt: { 'Typ Beta': 'beton' } });
  const reihen = [...u.matchAll(/<tr class="t-zeile[^"]*" data-s="([^"]*)"/g)].map((m) => m[1]);
  assert.deepEqual(reihen, ['Typ &lt;Gamma&gt; &amp; &quot;Co&quot;', 'Typ Beta', 'Typ Delta', 'Typ Epsilon'], 'nach Durchgängen absteigend, Schlüssel maskiert');
  assert.match(u, /<th>Typname<\/th><th class="zahl"[^>]*>Bauteile<\/th><th class="zahl"[^>]*>Aussparungen<\/th><th class="zahl"[^>]*>Durchgänge<\/th><th>Kategorie<\/th><th>Wirkung<\/th>/);
  assert.match(u, /data-s="Typ Beta"><td class="t-name"[^>]*><b>Typ Beta<\/b><div class="leise t-sub">IfcSlab 3 · Material im IFC: Farbe, weiss 3<\/div><\/td><td class="zahl">3<\/td><td class="zahl">15<\/td><td class="zahl t-viel">95<\/td>/);
  assert.match(u, /class="t-zeile gewaehlt" data-s="Typ Beta"/, 'gewählte Zeile hervorgehoben');
  assert.match(u, /<td class="t-wirkung leise">Rohbau: Aussparung; Testtext<\/td>/, 'Wirkung der gewählten Kategorie');
  assert.match(u, /title="DirectShape:Typ Epsilon|title="Typ Epsilon"/, 'volle Typnamen im Tooltip');
  assert.match(u, /DirectShape:Typ &lt;Gamma&gt; &amp; &quot;Co&quot;&#10;|DirectShape:Typ &lt;Gamma&gt; &amp; &quot;Co&quot;\n/, 'mehrere Typnamen im Tooltip, maskiert');
  assert.doesNotMatch(u, /<Gamma>|<Unnamed>/, 'nichts Unmaskiertes');
  assert.equal((u.match(/<select class="t-kat"/g) ?? []).length, 4);
  assert.match(T.typenUnbekanntHtml(typmaterialLeer, T.typenFormular()), /Keine unbekannten Typen/);
  assert.match(T.typenUnbekanntHtml({ ...typmaterialLeer, filter: { betroffen: true } }, T.typenFormular()), /Kein unbekannter Typ mit Aussparungen oder Durchgängen/);

  const b = T.typenBestaetigenHtml(typmaterial, { ...T.typenFormular(), reiter: 'bestaetigen', bestaetigen: { 'Typ Beton Test': { an: true, kategorie: 'beton' } } });
  assert.match(b, /erkannt: Installationsvorwand/);
  assert.match(b, /class="kp-warn t-sub"[^>]*>ANNAHME Prüfliste/, 'Annahme des Kerns sichtbar');
  assert.match(b, /data-s="_IW GIS Test">.*?<option value="installationsvorwand" title="[^"]*" selected>/s, 'erkannte Kategorie vorgewählt');
  assert.match(b, /class="t-zeile gewaehlt" data-s="Typ Beton Test"/);
  assert.equal((b.match(/class="t-an"[^>]*checked/g) ?? []).length, 1, 'nur das Bestätigte ist angehakt');
  const mehrdeutig = b.slice(b.indexOf('data-s="Typ Mehrdeutig"'));
  assert.match(mehrdeutig, /<option value="">\(wählen\)<\/option>/, 'mehrdeutig: «(wählen)»');
  assert.doesNotMatch(mehrdeutig, / selected>/, 'keine Kategorie vorgewählt');
  assert.match(b, /<input type="checkbox" class="t-an-alle">/);
  assert.match(T.typenBestaetigenHtml(typmaterialLeer, T.typenFormular()), /Kein Typ hat sein Material nur aus dem Typnamen/);

  const l = T.typenListeHtml(typmaterial, { ...T.typenFormular(), reiter: 'liste', liste: { 'TM-2': { kategorie: 'holz', quelle: 'FIKTIV Quelle B', entfernen: true } } });
  assert.match(l, /data-e="TM-1"><td>TM-1<\/td><td class="t-name"[^>]*><b>Typ Alpha 20<\/b><div class="leise t-sub">IfcSlab 3<\/div><\/td>/);
  assert.match(l, /<input type="text" class="t-zq" value="FIKTIV Quelle A"/, 'Quelle der Zeile änderbar');
  assert.match(l, /mensch, CS, 2026-10-19 12:07/, 'wer wann gesetzt hat');
  assert.match(l, /class="t-zeile t-weg t-ohne" data-e="TM-2"/, 'zum Entfernen vorgemerkt, ohne Bauteil');
  assert.match(l, /passt auf kein Bauteil – Schreibweise prüfen/, 'Eintrag ohne Treffer: Vermerk');
  assert.equal((l.match(/class="t-entf"[^>]*checked/g) ?? []).length, 1);
  assert.match(l, /<td class="zahl t-viel">160<\/td>/);
  assert.match(T.typenListeHtml(typmaterialLeer, T.typenFormular()), /Die Typenliste ist leer/);
});

test('Typenliste: Kopf, Hinweise (ohne Quelle, doppelt, Schreibweise prüfen) gut sichtbar, Reiter mit Zahlen und «nur betroffene»', () => {
  const kopf = T.typenKopfHtml(typmaterial);
  assert.match(kopf, /<b>2<\/b> Einträge in der Typenliste · 100 Bauteile \(unbekannt 18, nach Materialzuordnung 60, nach Typenliste 3, aus dem Typnamen \(zu bestätigen\) 19\) · <b>4<\/b> unbekannte Typen/);
  assert.match(kopf, /class="kp-warn"><b>1<\/b> Eintrag\/Einträge ohne Quelle/);
  assert.match(kopf, /class="kp-warn"><b>1<\/b> Typ\(en\) doppelt/);
  assert.match(T.typenKopfHtml({ ...typmaterial, zusammenfassung: { ...typmaterial.zusammenfassung, eintraege: 1 } }), /<b>1<\/b> Eintrag in der Typenliste/);
  assert.doesNotMatch(T.typenKopfHtml(typmaterialLeer), /ohne Quelle|doppelt/);
  assert.equal(T.typenKopfHtml(null), '');
  const t = T.typenHinweiseTeilen(typmaterial);
  assert.equal(t.warnungen.length, 4, 'die vier Hinweise zum Prüfen');
  assert.equal(t.leise.length, 1);
  const w = T.typenWarnungenHtml(typmaterial);
  assert.match(w, /class="hkap-kasten kp-typen-warn" role="alert"><b>Zu prüfen \(4\)<\/b>/);
  assert.match(w, /<li class="kp-warn">TM-2 «Typ Tippfehler» passt auf kein Bauteil der Unterlagen bzw\. von KICAD – Schreibweise prüfen\.<\/li>/);
  assert.match(w, /<li class="kp-warn">TM-3 «Typ Ohne» ohne Quelle – gilt nicht; mit «Typenliste …» und Quelle neu setzen\.<\/li>/, 'Befehlsnamen heissen wie die Knöpfe');
  assert.match(w, /«Typ Doppelt» steht mehrfach \(TM-4, TM-5\)/);
  assert.match(w, /Netzdatei fehlt bzw\. ist nicht lesbar/);
  assert.match(w, /class="leise t-leise">Durchgänge = Kollisionen typ durchdringung des letzten Prüflaufs/, 'Erklärung nur leise');
  assert.doesNotMatch(T.typenWarnungenHtml(typmaterialLeer), /kp-typen-warn/, 'leere Liste ist kein Fehler');
  assert.match(T.typenWarnungenHtml(typmaterialLeer), /Die Typenliste ist leer/);
  const r = T.typenReiterHtml(typmaterial, { ...T.typenFormular(), reiter: 'bestaetigen' });
  assert.match(r, /Unbekannte Typen \(4\)/); assert.match(r, /Material aus Typname \(3\)/); assert.match(r, /Typenliste \(2\)/);
  assert.match(r, /t-reiter-knopf aktiv" data-reiter="bestaetigen"/);
  assert.match(r, /<input type="checkbox" class="t-betroffen"> nur betroffene/);
  const rb = T.typenReiterHtml({ ...typmaterial, unbekannt: typmaterial.unbekannt.slice(0, 2), zusammenfassung: { ...typmaterial.zusammenfassung, unbekannteTypen: 4 } }, { ...T.typenFormular(), betroffen: true });
  assert.match(rb, /Unbekannte Typen \(2 von 4\)/, 'mit Schalter: «n von N»');
  assert.match(rb, /class="t-betroffen" checked/);
});

test('Typenliste: Inhalt der Reiter (Quelle nur bei den beiden ersten), Vorschau des Kerns, Hinweis nach der Änderung', () => {
  const f = T.typenFormular();
  const u = T.typenInhaltHtml(typmaterial, f);
  assert.match(u, /<input type="text" class="t-quelle" value="" placeholder="[^"]+" data-kein-enter="1">/, 'Quelle leer und ohne Vorgabewert');
  assert.match(u, /class="kp-knopf kp-primaer t-los">Festlegen<\/button>/);
  assert.match(u, /Sortiert nach Durchgängen/);
  assert.match(T.typenInhaltHtml(typmaterial, { ...f, quelle: 'A "B"' }), /class="t-quelle" value="A &quot;B&quot;"/);
  assert.match(T.typenInhaltHtml(typmaterial, { ...f, reiter: 'bestaetigen' }), /Bestätigen<\/button>/);
  assert.doesNotMatch(T.typenInhaltHtml(typmaterial, { ...f, reiter: 'liste' }), /class="t-quelle"/, 'in der Liste trägt jede Zeile ihre Quelle');
  assert.doesNotMatch(T.typenInhaltHtml(typmaterialLeer, f), /class="t-quelle"/, 'nichts zu festlegen: kein Quellenfeld');
  // Vorschau
  const ok = { angenommen: true, aenderungen: [{}], hinweise: ['TM-1: «Typ Beta» → beton (Quelle: Q; neu) – Rohbau: Aussparung.', '«Typ Beta» (beton): 3 Bauteil(e), 15 Aussparung(en), 95 Durchgang/Durchgänge («Typ Beta»).', '«Typ X» passt auf kein Bauteil der Unterlagen bzw. von KICAD – Schreibweise prüfen (Abfrage typmaterial zeigt die Typnamen).'] };
  const v = T.typenVorschauHtml(ok, { warnungen: ['TM-1: Kategorie geändert, Quelle gleich geblieben – gilt «A» auch für Holz?'] });
  assert.match(v, /<ul class="hinweisliste warnung"><li>TM-1: Kategorie geändert/);
  assert.match(v, /Vorschau des Kerns \(Trockenlauf – nichts wurde geändert\)/);
  assert.match(v, /3 Bauteil\(e\), 15 Aussparung\(en\), 95 Durchgang\/Durchgänge/, 'je Typ betroffene Bauteile, Aussparungen, Durchgänge');
  assert.match(v, /class="kp-warn">«Typ X» passt auf kein Bauteil .*die Typenliste zeigt die Typnamen/, 'Schreibweise prüfen in Warnfarbe');
  const nein = T.typenVorschauHtml({ angenommen: false, fehler: 'Typenliste nicht gesetzt: «x»: Quelle fehlt (eigene «quelle» oder die gemeinsame).', vorschlag: 'Je Eintrag typname und kategorie mit Quelle' }, {});
  assert.match(nein, /class="fehlerkasten" role="alert"><b>Typenliste nicht gesetzt: «x»: Quelle fehlt/);
  assert.match(nein, /Vorschlag: Je Eintrag typname/);
  assert.match(T.typenVorschauHtml({ angenommen: true, hinweise: [] }, {}), /Keine Änderung/);
  // nach der Änderung: Knöpfe öffnen die vorhandenen Dialoge
  const n = T.typenNachherHtml([{ typname: 'a', kategorie: 'beton' }, { typname: 'b', kategorie: 'holz' }, { typname: 'c', kategorie: null }]);
  assert.match(n, /<b>2 Typen festgelegt, 1 entfernt\.<\/b>/);
  assert.match(n, /data-nach="vorschlagen">Aussparungen neu vorschlagen …<\/button>/);
  assert.match(n, /data-nach="pruefen">Kollisionen prüfen …<\/button>/);
  assert.match(T.typenNachherHtml([{ typname: 'a', kategorie: 'beton' }]), /<b>1 Typ festgelegt\.<\/b>/);
  assert.match(T.typenNachherHtml([{ typname: 'a', kategorie: null }, { typname: 'b', kategorie: null }]), /2 Typen aus der Typenliste entfernt\./);
});

test('Typenliste: Dialog – nur Abfrage typmaterial und Befehl typmaterial_setzen, Trockenlauf zuerst, nie eine eigene Quelle', () => {
  const dlg = lies('./koordination-dialoge.js');
  const teil = dlg.slice(dlg.indexOf('export async function typenDialog()'));
  assert.match(teil, /frage\('typmaterial', T\.typenAbfrageParameter\(form\)\)/);
  assert.match(teil, /sende\('typmaterial_setzen', par, \{ trockenlauf: true \}\)/, 'Vorschau als Trockenlauf');
  assert.match(teil, /sende\('typmaterial_setzen', par\)/, 'dann der echte Befehl');
  assert.ok(teil.indexOf("{ trockenlauf: true }") < teil.indexOf("sende('typmaterial_setzen', par);"), 'Trockenlauf steht vor dem echten Aufruf');
  assert.match(teil, /vorschauStand\?\.key !== JSON\.stringify\(par\)/, 'gesendet wird nur, was die letzte Vorschau zeigte');
  assert.doesNotMatch(teil, /quelle\s*[:=]\s*['"`][^'"`]/, 'keine eingebaute Quelle');
  assert.doesNotMatch(teil, /\bkategorie\s*[:=]\s*['"`](beton|mauerwerk|leichtbau|holz|stahl|daemmung|bekleidung|installationsvorwand)/, 'keine eingebaute Kategorie');
  assert.match(teil, /vorschlagenDialog\(\) : pruefenDialog\(\)/, 'die Knöpfe nach der Änderung öffnen die vorhandenen Dialoge');
  assert.doesNotMatch(teil, /sende\('aussparungen_vorschlagen'|sende\('kollision_pruefen'/, 'und lösen nichts selbst aus');
  const text = lies('./koordination-text.js');
  assert.doesNotMatch(text.slice(text.indexOf('// ---- Anzeige')), /'(beton|mauerwerk|leichtbau|daemmung|installationsvorwand)'/, 'die Anzeige kennt keine Kategorie-Namen (sie kommen vom Kern)');
});

test('Zusatz B4d-UI: Spalte «Kurzzeichen» in der Aussparungsliste (leer bei älterem Kern), Schalter und Darstellungen der Ansicht kommen aus dem Katalog', () => {
  const z = [formZeile('AUS-7', { kurzzeichen: 'S DD' }), formZeile('AUS-8', { kurzzeichen: 'WD' }), formZeile('AUS-9')];   // AUS-9: Kern ohne das Feld
  const h = T.aussparungTabelleHtml(z, {});
  assert.match(h, /<th>Nummer<\/th><th title="[^"]*">Kurzzeichen<\/th><th>Aussparung<\/th>/, 'Spalte steht hinter der Nummer');
  assert.match(h, /<td class="kp-kurzzeichen">S DD<\/td>/);
  assert.match(h, /<td class="kp-kurzzeichen">WD<\/td>/);
  assert.match(h, /data-kz="AUS-9">.*?<td class="kp-kurzzeichen"><\/td>/s, 'ohne Feld bleibt die Zelle leer');
  assert.match(T.aussparungTabelleHtml([formZeile('AUS-1', { kurzzeichen: 'A <b>' })], {}), /<td class="kp-kurzzeichen">A &lt;b&gt;<\/td>/, 'maskiert');
  const cols = (x) => (x.match(/<th[ >]/g) ?? []).length;
  assert.equal(cols(h.slice(0, h.indexOf('</tr>'))), 14 + 0, 'Kopf: 14 Spalten (mit Material 15)');
  assert.match(T.aussparungTabelleHtml(Array.from({ length: 3 }, (_, i) => formZeile(`AUS-${i}`)), { max: 2 }), /colspan="14"/);
  // Katalog Ansicht: Schalter und Auswahl; die Eigenschaftspalette zeigt Katalog-Bools und -Auswahlen von selbst (eigenschaften.js, Feld je Typ)
  assert.equal(eig('Ansicht', 'beschriftungAmSymbol')?.typ, 'bool');
  assert.equal(eig('Ansicht', 'legende')?.typ, 'bool');
  for (const d of ['standard', 'huelle', 'aussparungen', 'einlagen', 'grundleitungen']) assert.ok(eig('Ansicht', 'darstellung').auswahl.includes(d), `darstellung ${d}`);
  assert.match(eig('Ansicht', 'beschriftungAmSymbol').beschreibung, /SIA 410\/2/);
  const ep = lies('./eigenschaften.js');
  assert.match(ep, /for \(const d of def\?\.eigenschaften \?\? \[\]\)/, 'alle Eigenschaften der Klasse laut Katalog');
  assert.match(ep, /case 'bool':\s*\n?\s*return `<input type="checkbox" data-ziel=/, 'Bool als Schalter');
  assert.match(ep, /case 'auswahl'/, 'Auswahl aus dem Katalog');
});

test('Kollisionsliste: Paar-Filter und Schalter stehen in der Palette und wirken im Browser, die Abfrage bleibt unverändert', () => {
  const p = lies('./koordination-panel.js');
  assert.match(p, /class="kf-paar"/); assert.match(p, /class="kf-ungeprueft"/);
  assert.match(p, /T\.kollisionSicht\(k\.daten, k\.filter/);
  assert.match(p, /letztePaareMerken/);
  assert.match(lies('./koordination-dialoge.js'), /letztePaareMerken\(f\.paare\)/, 'der Dialog meldet die gewählten Paare nach einem Lauf');
  const f = { ...T.KOLLISION_FILTER_VORGABE(), paar: 'hlks-vorwand', auchNichtGeprueft: true };
  assert.deepEqual(T.kollisionFilterParameter(f), {}, 'Paar und Schalter gehen nicht an die Abfrage kollisionen');
});

// ---------------------------------------------------------------------------------------------- Aussparungen

test('Aussparungsliste: Filter nach Status, Tabelle mit Koten, Kennzahlen', () => {
  const z = aussparungen.zeilen;
  assert.equal(T.aussparungFilter(z, {}).length, 3, 'ohne Angabe alle');
  assert.deepEqual(T.aussparungFilter(z, { status: ['freigegeben', 'veraltet'] }).map((x) => x.aussparung), ['AUS-2', 'AUS-3']);
  assert.deepEqual(T.aussparungFilter(z, { status: [] }), [], 'nichts angekreuzt: nichts');
  const h = T.aussparungTabelleHtml(z, { auswahl: new Set(['AUS-2']), aktiv: 'AUS-1' });
  assert.match(h, /<tr class="kp-zeile aktiv" data-kz="AUS-1">/);
  assert.match(h, /A-EG-001/);
  assert.match(h, /UK 952\.5 · OK 1047\.5/);
  assert.match(h, /Achse 1000/);
  assert.match(h, /Achse 600/, 'Kote über OK Geschoss');
  assert.match(h, /kp-sa-veraltet">veraltet/);
  assert.match(h, /<td>–<\/td><td class="kp-kurzzeichen"><\/td><td>AUS-3<\/td>/, 'ohne Nummer: Strich');
  assert.equal(T.aussparungKopf(aussparungen), '3 Aussparungen · vorgeschlagen 2, freigegeben 1 · 1 Form von Hand');
  assert.equal(T.aussparungKopf(aussparungen, 2), '2 von 3 Aussparungen · vorgeschlagen 2, freigegeben 1 · 1 Form von Hand');
  assert.match(T.aussparungTabelleHtml([], { leerHinweis: 'Noch keine Aussparungen' }), /Noch keine Aussparungen/);
  assert.match(T.aussparungDetailHtml(z[0]), /data-ref="T-1"[\s\S]*data-ref="T-3"/);
  assert.match(T.aussparungDetailHtml(z[2]), /\(UL-1\)/, 'Bauteil aus IFC-Unterlage: nur Text');
});

test('Aussparungen: Parameter von Status, Vorschlagen, Nummerieren und Abgleich', () => {
  assert.deepEqual(T.aussparungStatusParameter({ aussparungen: ['AUS-1', 'A-EG-002'], status: 'freigegeben' }), { aussparungen: ['AUS-1', 'A-EG-002'], status: 'freigegeben' });
  assert.match(T.aussparungStatusPruefen({ aussparungen: [], status: 'versandt' }), /Zuerst Aussparungen wählen/);
  assert.match(T.aussparungStatusPruefen({ aussparungen: ['AUS-1'], status: 'x' }), /Status wählen/);
  assert.equal(T.aussparungStatusPruefen({ aussparungen: ['AUS-1'], status: 'versandt' }), true);
  assert.deepEqual(T.vorschlagenParameter({ geschoss: '', leitungen: '', bauteile: '' }), {});
  assert.deepEqual(T.vorschlagenParameter({ geschoss: 'EG', leitungen: 'T-1, T-2;T-3  T-4', bauteile: 'W-EG-S UL-1:3x' }), { geschoss: 'EG', leitungen: ['T-1', 'T-2', 'T-3', 'T-4'], bauteile: ['W-EG-S', 'UL-1:3x'] });
  assert.deepEqual(T.nummerierenParameter({ geschoss: '', alle: false }), {});
  assert.deepEqual(T.nummerierenParameter({ geschoss: '1.OG', alle: true }), { geschoss: '1.OG', alle: true });
  assert.deepEqual(T.abgleichParameter(' UL-2 '), { unterlage: 'UL-2' });
  assert.match(T.abgleichPruefen(''), /IFC-Unterlage/);
  assert.equal(T.abgleichPruefen('UL-2'), true);
  assert.deepEqual(T.ifcUnterlagen([{ werte: { art: 'ifc' } }, { werte: { art: 'IFC' } }, { werte: { art: 'pdf' } }, {}]).length, 2);
});

// ---------------------------------------------------------------------------------------------- Form der Aussparung (aussparung_form)

test('Form: Umschalter je Zeile – aktiv nur bei formWaehlbar, sonst formSperre als Tooltip; «von Hand» sichtbar', () => {
  const [a4, a5, a6] = aussparungenForm.zeilen;
  const s4 = T.formSchalterHtml(a4);
  assert.match(s4, /data-form="rund" data-aus="AUS-4" aria-pressed="false"/);
  assert.doesNotMatch(s4.match(/<button[^>]*data-form="rund"[^>]*>/)[0], /disabled/, 'rund ist wählbar');
  assert.match(s4.match(/<button[^>]*data-form="rechteckig"[^>]*>/)[0], /aria-pressed="true"[\s\S]*disabled/, 'die aktuelle Form ist gewählt und nicht anklickbar');
  assert.doesNotMatch(s4, /kp-vonhand/, 'nicht von Hand');
  const s5 = T.formSchalterHtml(a5);
  assert.match(s5, /class="kp-vonhand"[^>]*>von Hand<\/span>/, 'von Hand gewählt: sichtbar gekennzeichnet');
  assert.match(s5.match(/<button[^>]*data-form="rund"[^>]*>/)[0], /aria-pressed="true"/);
  assert.doesNotMatch(s5.match(/<button[^>]*data-form="rechteckig"[^>]*>/)[0], /disabled/, 'zurück zum Rechteck ist möglich');
  const s6 = T.formSchalterHtml(a6);
  assert.equal((s6.match(/disabled/g) ?? []).length, 2, 'beide Knöpfe gesperrt');
  assert.match(s6, /kp-formschalter gesperrt" title="ohne Leitung – Mass nicht rechenbar"/, 'Grund des Kerns als Tooltip');
  assert.match(s6.match(/data-form="rund"[^>]*title="([^"]*)"/)[1], /ohne Leitung/);
  // Sammelaussparung (Fixture AUS-1) und freigegebene (AUS-2) mit ihrem Grund
  assert.match(T.formSchalterHtml(aussparungen.zeilen[0]), /Sammelaussparung \(2 Leitungen\) – bleibt rechteckig/);
  assert.match(T.formSchalterHtml(aussparungen.zeilen[1]), /Status freigegeben – zuerst aussparung_status vorgeschlagen/);
  // ältere Kerne ohne die Felder: nur der Name der Form, kein Schalter
  assert.equal(T.formSchalterHtml({ aussparung: 'AUS-9', form: 'rund' }), 'rund (Kernbohrung)');
  // die Tabelle: Schalter in der Zeile, Klick darauf ist kein Zeilenklick (eigene Klasse kp-formwahl)
  const h = T.aussparungTabelleHtml(aussparungenForm.zeilen, {});
  assert.equal((h.match(/class="kp-formschalter/g) ?? []).length, 3);
  assert.match(h, /<td class="kp-masse">215 × 95<\/td>/);
  assert.match(h, /<th>Form<\/th><th>Mass<\/th>/);
});

test('Form: Detail nennt die Sperre und die Handwahl; Kopfzeile zählt die Handwahl', () => {
  const d6 = T.aussparungDetailHtml(aussparungenForm.zeilen[2]);
  assert.match(d6, /Form nicht wählbar: ohne Leitung – Mass nicht rechenbar/);
  assert.match(T.aussparungDetailHtml(aussparungenForm.zeilen[1]), /Form <\/b>|<b>Form von Hand<\/b>/);
  assert.doesNotMatch(T.aussparungDetailHtml(aussparungenForm.zeilen[0]), /Form nicht wählbar|von Hand/);
  assert.equal(T.aussparungKopf(aussparungenForm), '3 Aussparungen · vorgeschlagen 3 · 1 Form von Hand');
});

test('Form: Parameter von aussparung_form, nur wählbare gehen an den Kern, die übrigen stehen mit ihrem Grund da', () => {
  assert.deepEqual(T.FORMEN, ['rund', 'rechteckig']);
  assert.deepEqual(T.formParameter({ aussparungen: ['AUS-4', 'A-EG-002'], form: 'rund' }), { aussparungen: ['AUS-4', 'A-EG-002'], form: 'rund' });
  assert.equal(T.formPruefen({ aussparungen: ['AUS-4'], form: 'rund' }), true);
  assert.match(T.formPruefen({ aussparungen: [], form: 'rund' }), /Zuerst Aussparungen wählen/);
  assert.match(T.formPruefen({ aussparungen: ['AUS-4'], form: 'oval' }), /rund oder rechteckig/);
  const z = T.formZiele(aussparungenForm.zeilen, ['AUS-4', 'AUS-6', 'AUS-5', 'AUS-9']);
  assert.deepEqual(z.senden, ['AUS-4', 'AUS-5']);
  assert.deepEqual(z.uebersprungen, [{ aussparung: 'AUS-6', nummer: null, grund: 'ohne Leitung – Mass nicht rechenbar' }]);
  const alle = T.formZiele(aussparungen.zeilen, ['AUS-1', 'AUS-2']);
  assert.deepEqual(alle.senden, []);
  assert.equal(alle.uebersprungen.length, 2);
  assert.deepEqual(T.formZiele(aussparungen.zeilen, ['AUS-3']).senden, ['AUS-3'], 'veraltet ist wählbar');
  assert.equal(T.FORM_KNOPF.rund, 'Als Kernbohrung');
  assert.equal(T.FORM_KNOPF.rechteckig, 'Als Rechteck');
  assert.match(T.formKeineHtml(alle, 'rund'), /Als Kernbohrung: keine der gewählten Aussparungen ist wählbar[\s\S]*Sammelaussparung \(2 Leitungen\)[\s\S]*Status freigegeben/);
});

test('Form: Vorschau (Trockenlauf) mit Hinweisen, Ablehnungen mit Fehler und Vorschlag, Ergebnis', () => {
  const ziele = T.formZiele(aussparungenForm.zeilen, ['AUS-4', 'AUS-6']);
  const vor = T.formMeldungHtml({ angenommen: true, aenderungen: [{ klasse: 'Aussparung', kennzeichen: 'AUS-4', art: 'geaendert' }], hinweise: [
    'AUS-4: rechteckig 215 × 95 → rund Ø 95, Kernbohrung (von Hand gewählt); Status bleibt veraltet.',
    'AUS-4: AUS-7 (Nachbar, Abstand 80 mm) – unter abstand_zusammenfassen 100 mm; eine Kernbohrung von Hand wird nicht automatisch zusammengefasst.',
    'AUS-8 ist schon rund (Kernbohrung von Hand) – keine Änderung.'] }, { trocken: true, form: 'rund', ziele });
  assert.match(vor, /Vorschau \(Trockenlauf – nichts wurde geändert\): Als Kernbohrung, 1 Änderung\(en\)/);
  assert.match(vor, /data-kp="form-ja">Ausführen<\/button>/);
  assert.doesNotMatch(vor, /data-kp="form-ja" disabled/);
  assert.match(vor, /data-kp="form-nein">Abbrechen<\/button>/);
  assert.match(vor, /class="kp-warn">AUS-4: AUS-7 \(Nachbar/, 'Nachbar unter abstand_zusammenfassen: Warnfarbe');
  assert.match(vor, /class="[^"]*kp-warn[^"]*">AUS-4: rechteckig 215 × 95 → rund Ø 95, Kernbohrung \(von Hand gewählt\); Status bleibt veraltet/, '«Status bleibt veraltet»');
  assert.match(vor, /AUS-8 ist schon rund/, '«ist schon …»');
  assert.match(vor, /Nicht wählbar, nicht gesendet:[\s\S]*AUS-6: ohne Leitung – Mass nicht rechenbar/);
  // nichts zu ändern: Ausführen gesperrt
  assert.match(T.formMeldungHtml({ angenommen: true, aenderungen: [], hinweise: ['AUS-4 ist schon rechteckig – keine Änderung.'] }, { trocken: true, form: 'rechteckig' }), /data-kp="form-ja" disabled/);
  // Ablehnungen des Kerns: immer Fehler und Vorschlag
  const nein = T.formMeldungHtml({ angenommen: false, fehler: 'Die Form lässt sich nur bei vorgeschlagenen und veralteten Aussparungen wählen: AUS-2 ist «freigegeben».',
    vorschlag: 'Zuerst mit aussparung_status zurück auf vorgeschlagen setzen, dann aussparung_form' }, { trocken: true, form: 'rund' });
  assert.match(nein, /<b>Die Form lässt sich nur bei vorgeschlagenen und veralteten Aussparungen wählen/);
  assert.match(nein, /Vorschlag: Zuerst mit «Aussparungsliste …» zurück auf vorgeschlagen setzen, dann «Als Kernbohrung \/ Als Rechteck …»/);
  assert.doesNotMatch(nein, /form-ja/, 'abgelehnt: kein Ausführen');
  assert.match(T.formMeldungHtml({ angenommen: false, fehler: 'Eine Sammelaussparung kann keine Kernbohrung werden.' }, { form: 'rund' }), /Der Kern nennt keinen Vorschlag/);
  for (const text of ['Eine Sammelaussparung kann keine Kernbohrung werden', 'Ohne Leitung ist das Mass nicht rechenbar', 'geht nicht mehr durch die Mitte der Aussparung (verschoben bzw. geändert)', 'Form nicht gesetzt – das Mass ist nicht rechenbar'])
    assert.match(T.formMeldungHtml({ angenommen: false, fehler: `${text}.`, vorschlag: 'x' }, { form: 'rund' }), new RegExp(text.replace(/[()]/g, '\\$&')), text);
  // Ergebnis nach dem Ausführen: erste Zeile als Überschrift, ohne Knöpfe
  const ok = T.formMeldungHtml({ angenommen: true, aenderungen: [{}], hinweise: ['AUS-4: rechteckig 215 × 95 → rund Ø 95, Kernbohrung (von Hand gewählt).'] }, { form: 'rund' });
  assert.match(ok, /^<b>AUS-4: rechteckig 215 × 95 → rund Ø 95/);
  assert.doesNotMatch(ok, /form-ja|Vorschau/);
  // WARNT: Hinweise über Nachbarn und «Status bleibt veraltet» in Warnfarbe
  assert.match(T.hinweiseListeHtml(['x', 'AUS-1: Nachbar – unter abstand_zusammenfassen 100 mm; …']), /class="kp-warn">AUS-1: Nachbar/);
  assert.equal(T.fuerMenschen('dann aussparung_form'), 'dann «Als Kernbohrung / Als Rechteck …»');
});

test('Form: Spalte Material und «Material unbekannt» nur, wenn der Kern die Felder liefert (B3c)', () => {
  assert.equal(T.hatMaterial(aussparungen.zeilen), false);
  assert.doesNotMatch(T.aussparungTabelleHtml(aussparungen.zeilen, {}), /<th>Material<\/th>|Material unbekannt/);
  const mit = [formZeile('AUS-7', { material: 'Beton', materialUnbekannt: false }), formZeile('AUS-8', { material: null, materialUnbekannt: true }), formZeile('AUS-9')];
  assert.equal(T.hatMaterial(mit), true);
  const h = T.aussparungTabelleHtml(mit, {});
  assert.match(h, /<th>Bauteil<\/th><th>Material<\/th><th>Form<\/th>/);
  assert.match(h, /<td class="kp-material">Beton<\/td>/);
  assert.match(h, /<td class="kp-material"><span class="kp-warn kp-matunbekannt"[^>]*>Material unbekannt<\/span><\/td>/);
  assert.match(h, /<td class="kp-material">–<\/td>/, 'Zeile ohne Feld: Strich');
  const kopf = T.aussparungKopf({ anzahl: 3, jeStatus: { vorgeschlagen: 3 }, zeilen: mit });
  assert.match(kopf, /1 mit unbekanntem Material/);
  assert.match(T.aussparungDetailHtml(mit[1]), /Material unbekannt/);
  assert.match(T.aussparungDetailHtml(mit[0]), /Material Beton/);
  // Spaltenzahl der «weitere»-Zeile stimmt
  const viele = Array.from({ length: 600 }, (_, i) => formZeile(`AUS-${i + 1}`, { material: 'Beton' }));
  assert.match(T.aussparungTabelleHtml(viele, { max: 500 }), /<td colspan="15" class="leise">… und 100 weitere/);
  assert.match(T.aussparungTabelleHtml(aussparungen.zeilen, { max: 2 }), /<td colspan="14" class="leise">… und 1 weitere/);
});

// ---------------------------------------------------------------------------------------------- Aussparungsplan

test('Aussparungsplan: Ansicht mit Darstellung «aussparungen», Planblatt nur mit eigener Nummer', () => {
  const p = T.planAnsichtParameter({ geschoss: 'EG', massstab: '50', gewerke: ['architektur', 'sanitaer'] });
  assert.deepEqual(p, { klasse: 'Ansicht', kennzeichen: 'AN-AUS-EG', name: 'Aussparungsplan EG', geschoss: 'EG', eigenschaften: { art: 'grundriss', darstellung: 'aussparungen', detailgrad: 'mittel', massstab: 50, gewerke: ['architektur', 'sanitaer'] } });
  assert.ok(eig('Ansicht', 'darstellung').auswahl.includes('aussparungen'), 'Katalog kennt die Darstellung');
  assert.equal(T.planAnsichtParameter({ geschoss: '1.OG', massstab: 100 }).eigenschaften.gewerke, undefined, 'ohne Gewerke keine Einschränkung');
  assert.deepEqual(T.planblattParameter({ nummer: ' 2317-101 ', titel: 'Aussparungsplan EG', format: 'A3', hochformat: false }), { nummer: '2317-101', titel: 'Aussparungsplan EG', format: 'A3' });
  assert.deepEqual(T.planblattParameter({ nummer: 'x', titel: 'y', format: 'A2', hochformat: true }), { nummer: 'x', titel: 'y', format: 'A2', hochformat: true });
  const z = (g, o = {}) => ({ geschoss: g, gewaehlt: true, blatt: false, nummer: '', titel: `Aussparungsplan ${g}`, ...o });
  assert.match(T.planPruefen([z('EG', { gewaehlt: false })]), /Mindestens ein Geschoss/);
  assert.equal(T.planPruefen([z('EG'), z('1.OG')]), true, 'ohne Planblatt keine Nummer nötig');
  assert.match(T.planPruefen([z('EG', { blatt: true })]), /Plannummer fehlt/);
  assert.match(T.planPruefen([z('EG', { blatt: true, nummer: '1' }), z('1.OG', { blatt: true, nummer: '1' })]), /steht doppelt/);
  assert.match(T.planPruefen([z('EG', { blatt: true, nummer: 'A-1' })], ['a-1']), /gibt es schon/);
  assert.match(T.planPruefen([z('EG', { blatt: true, nummer: '1', titel: ' ' })]), /Titel/);
  assert.equal(T.planPruefen([z('EG', { blatt: true, nummer: '1' }), z('1.OG', { blatt: true, nummer: '2' })]), true);
  assert.deepEqual(T.geschosseMitAussparungen(aussparungen.zeilen), ['EG', '1.OG']);
});

// ---------------------------------------------------------------------------------------------- IFC für den Architekten

test('IFC für Architekt: Knopf nur mit der Abfrage im Kern, Parameter nach den Angaben des Kerns', () => {
  assert.equal(T.ifcFaehigkeit({ abfragen: [{ name: 'bcf_export' }] }).vorhanden, false);
  assert.equal(T.ifcFaehigkeit(null).vorhanden, false);
  const f = T.ifcFaehigkeit({ abfragen: [{ name: 'aussparungen_ifc', parameter: [{ name: 'datei', typ: 'text' }, { name: 'fassung', typ: 'text' }, { name: 'geschoss', typ: 'text' }, { name: 'status', typ: 'liste' }] }] });
  assert.equal(f.vorhanden, true);
  const alle = { datei: '"C:\\T\\a.ifc"', fassung: 'IFC4', geschoss: 'EG', status: ['freigegeben'], koordinaten: 'ifc' };
  assert.deepEqual(T.ifcParameter(alle, f), { datei: 'C:\\T\\a.ifc', fassung: 'IFC4', geschoss: 'EG', status: ['freigegeben'] }, 'koordinaten kennt der Kern in dieser Liste nicht → nicht gesendet');
  const text = T.ifcFaehigkeit({ abfragen: [{ name: 'aussparungen_ifc', parameter: [{ name: 'datei' }, { name: 'status', typ: 'text' }, { name: 'koordinaten', typ: 'text' }] }] });
  assert.deepEqual(T.ifcParameter(alle, text), { datei: 'C:\\T\\a.ifc', fassung: 'IFC4', status: 'freigegeben', koordinaten: 'ifc' });
  assert.deepEqual(T.ifcParameter({ datei: 'x.ifc', fassung: 'IFC2X3', geschoss: '', status: [], koordinaten: '' }, f), { datei: 'x.ifc', fassung: 'IFC2X3' }, 'ohne Wahl entscheidet der Kern (Vorgabe)');
  assert.deepEqual(T.ifcParameter({ datei: 'x.ifc', fassung: 'IFC4', koordinaten: 'projekt' }, text), { datei: 'x.ifc', fassung: 'IFC4', koordinaten: 'projekt' }, 'eine ausdrückliche Wahl geht immer mit, auch «projekt»');
  assert.deepEqual(T.IFC_FASSUNGEN, ['IFC4', 'IFC2X3']);
  assert.match(T.IFC_FEHLT_HINWEIS, /noch nicht vorhanden/);
});

// ---------------------------------------------------------------------------------------------- BCF

test('Koordinaten (BCF und IFC): «Vorgabe» sendet nichts, eine ausdrückliche Wahl immer – auch «projekt» (Entscheid Christian 19.10.2026)', () => {
  assert.deepEqual(T.KOORDINATEN_WAHL.map((k) => k.wert), ['', 'ifc', 'projekt']);
  assert.equal(T.KOORDINATEN_WAHL[0].text, 'Vorgabe (IFC mit Projektbezug, sonst Projekt)');
  const h = T.koordinatenOptionenHtml('');
  assert.match(h, /<option value="" selected>Vorgabe \(IFC mit Projektbezug, sonst Projekt\)<\/option>/, 'Vorgabe ist vorgewählt');
  assert.match(h, /<option value="ifc">IFC /);
  assert.match(h, /<option value="projekt">Projekt /);
  assert.equal(T.koordinatenWert(''), null);
  assert.equal(T.koordinatenWert(undefined), null);
  assert.equal(T.koordinatenWert(' projekt '), 'projekt');
  assert.equal(T.koordinatenWert('IFC'), 'ifc');
  assert.equal(T.koordinatenWert('gibtsnicht'), null, 'nur die zwei Werte des Kerns');
  const bcf = (k) => T.bcfExportParameter({ datei: 'C:\\T\\k.bcf', status: T.KOLLISION_STATUS, koordinaten: k });
  assert.deepEqual([bcf(''), bcf('ifc'), bcf('projekt')].map((p) => p.koordinaten), [undefined, 'ifc', 'projekt']);
  const f = T.ifcFaehigkeit({ abfragen: [{ name: 'aussparungen_ifc', parameter: [{ name: 'datei' }, { name: 'fassung' }, { name: 'koordinaten', typ: 'text' }] }] });
  const ifc = (k) => T.ifcParameter({ datei: 'C:\\T\\a.ifc', fassung: 'IFC4', koordinaten: k }, f);
  assert.deepEqual([ifc(''), ifc('ifc'), ifc('projekt')].map((p) => p.koordinaten), [undefined, 'ifc', 'projekt']);
  assert.ok(!('koordinaten' in ifc('')));
});

test('IFC für Architekt: Exportbericht des Kerns (Zahlen, Koordinaten, Rundreise, Warnungen)', () => {
  const bericht = {
    datei: 'C:\\T\\a.ifc', fassung: 'IFC4', entitaet: 'IfcBuildingElementProxy', kennzeichnung: 'PredefinedType PROVISIONFORVOID', groesseBytes: 10240, anzahl: 2, jeArt: { aussparung: 1, kernbohrung: 1 },
    jeGeschoss: { EG: 2 }, jeStatus: { freigegeben: 1, versandt: 1 }, koordinaten: { art: 'projekt', text: 'projekt: Projektkoordinaten von KICAD' },
    rundreise: { ergebnis: 'bestanden', aussparungenGeprueft: 2, abweichungen: [] }, warnungen: ['Kein Projektbezug – Projektkoordinaten'], hinweise: ['Datei geschrieben.'],
  };
  const h = T.ifcExportHtml(bericht);
  assert.match(h, /IFC4/);
  assert.match(h, /IfcBuildingElementProxy \(PredefinedType PROVISIONFORVOID\)/);
  assert.match(h, /2 · aussparung 1, kernbohrung 1/);
  assert.match(h, /freigegeben 1, versandt 1/);
  assert.match(h, /10 kB/);
  assert.match(h, /bestanden · 2 Aussparungen geprüft/);
  assert.match(h, /Kein Projektbezug/);
  assert.match(h, /Datei geschrieben\./);
  assert.doesNotMatch(h, /Abweichungen der Rundreise/);
  assert.match(T.ifcExportHtml({ ...bericht, rundreise: { ergebnis: 'abgewichen', abweichungen: ['x fehlt'] } }), /Abweichungen der Rundreise[\s\S]*x fehlt/);
});

test('BCF: Pfade müssen vollständig sein und die richtige Endung tragen', () => {
  assert.equal(T.dateiPfadPruefen('C:\\Temp\\k.bcf'), true);
  assert.equal(T.dateiPfadPruefen('"C:/Temp/k.BCFZIP"'), true);
  assert.equal(T.dateiPfadPruefen('\\\\nas\\w\\k.bcf'), true);
  assert.equal(T.dateiPfadPruefen('/tmp/k.bcf'), true);
  assert.match(T.dateiPfadPruefen('k.bcf'), /vollständigen Pfad/);
  assert.match(T.dateiPfadPruefen(''), /vollständigen Pfad/);
  assert.match(T.dateiPfadPruefen('C:\\Temp\\k.zip'), /Endung/);
  assert.equal(T.dateiPfadPruefen('C:\\T\\a.ifc', ['.ifc']), true);
  assert.equal(T.bcfDateiname('MFH Musterweg (Beispiel, fiktiv)', new Date(2026, 9, 19)), 'Kollisionen_MFH_Musterweg_2026-10-19.bcf');
  assert.equal(T.bcfDateiname('', new Date(2026, 0, 5)), 'Kollisionen_Projekt_2026-01-05.bcf');
});

test('BCF-Export: Parameter nur mit Filter, Vorschau der Zahl, Ergebnis des Kerns', () => {
  const alle = T.KOLLISION_STATUS;
  assert.deepEqual(T.bcfExportParameter({ datei: ' C:\\T\\k.bcf ', status: alle, art: '', autor: '', koordinaten: '', kollisionen: [] }), { datei: 'C:\\T\\k.bcf' }, '«Vorgabe» sendet nichts: der Kern wählt ifc (mit Projektbezug) bzw. projekt');
  assert.deepEqual(T.bcfExportParameter({ datei: 'C:\\T\\k.bcf', status: alle, koordinaten: 'projekt' }), { datei: 'C:\\T\\k.bcf', koordinaten: 'projekt' }, 'eine ausdrückliche Wahl geht immer mit, auch «projekt»');
  assert.deepEqual(T.bcfExportParameter({ datei: 'C:\\T\\k.bcf', status: alle, koordinaten: 'ifc' }), { datei: 'C:\\T\\k.bcf', koordinaten: 'ifc' });
  assert.deepEqual(T.bcfExportParameter({ datei: 'C:\\T\\k.bcf', status: alle }), { datei: 'C:\\T\\k.bcf' }, 'ohne Angabe nichts');
  assert.deepEqual(T.bcfExportParameter({ datei: 'C:\\T\\k.bcf', status: ['neu', 'bestaetigt'], art: 'hart', autor: 'x@y.ch', koordinaten: 'ifc', kollisionen: ['KOL-1'] }),
    { datei: 'C:\\T\\k.bcf', kollisionen: ['KOL-1'], status: ['neu', 'bestaetigt'], art: 'hart', autor: 'x@y.ch', koordinaten: 'ifc' });
  assert.equal(T.bcfExportVorschau(kollisionen, 0), '3 Kollision(en) werden exportiert (hart 2, weich 1).');
  assert.equal(T.bcfExportVorschau(null, 2), '2 gewählte Kollision(en) werden exportiert.');
  assert.match(T.bcfExportVorschau({ zeilen: [] }, 0), /nichts zu exportieren/);
  const h = T.bcfExportHtml({ datei: 'C:\\T\\k.bcf', bcfVersion: '3.0', groesseBytes: 20480, topics: 3, hart: 2, weich: 1, koordinaten: 'projekt: Projektkoordinaten', schemapruefung: 'bestanden', hinweise: ['KOL-3: keine Lage'] });
  assert.match(h, /C:\\T\\k\.bcf/);
  assert.match(h, /3 \(hart 2, weich 1\)/);
  assert.match(h, /20 kB/);
  assert.match(h, /bestanden/);
  assert.match(h, /KOL-3: keine Lage/);
});

const bcfLesen = {
  datei: 'k.bcf', bcfVersion: '3.0', projekt: { id: 'P', name: 'Fremdprojekt', diesesProjekt: false }, schemapruefung: 'bestanden', anzahlTopics: 3, zugeordnet: 2, unbekannt: 1,
  topics: [
    { guid: 'g1', titel: 'T-1 ↔ W-EG-S', status: 'Closed', kollision: 'KOL-1', statusKicad: 'neu', statusNachImport: 'erledigt', zugewiesen: 'arch@x.ch',
      kommentare: [{ text: 'ok', neu: true }, { text: 'alt', neu: false }] },
    { guid: 'g2', titel: 'T-1 ↔ T-3', status: 'Open', kollision: 'KOL-2', statusKicad: 'neu', statusNachImport: 'neu', kommentare: [] },
    { guid: 'g3', titel: 'Fremdes Topic', status: 'Open', kollision: null, kommentare: [{ text: 'x', neu: null }] },
  ],
  hinweise: ['Schema ok'],
};

test('BCF-Import: Vorschau aus bcf_lesen – Zuordnung, Status nach dem Import, neue Kommentare, unbekannte Topics', () => {
  const h = T.bcfLesenHtml(bcfLesen);
  assert.match(h, /BCF 3\.0 · 3 Topic\(s\) · 2 zugeordnet · 1 unbekannt · 1 mit Änderung · Schemaprüfung: bestanden/);
  assert.match(h, /anderes Projekt \(Fremdprojekt\)/);
  assert.match(h, /neu → <b>erledigt<\/b>/);
  assert.match(h, /<td class="zahl">1<\/td><td>arch@x\.ch<\/td>/, 'ein neuer Kommentar');
  assert.match(h, /class="kp-unbekannt"><td>Fremdes Topic<\/td><td><span class="leise">unbekannt/);
  assert.match(h, /Schema ok/);
  assert.equal(T.bcfImportAenderungen(bcfLesen), 1, 'nur KOL-1 ändert etwas');
  assert.equal(T.bcfImportAenderungen({ topics: [{ kollision: 'KOL-2', statusKicad: 'neu', statusNachImport: 'unverändert (Status «x» unbekannt)', kommentare: [] }] }), 0);
  assert.match(T.bcfLesenHtml({ topics: [] }), /keine Topics/);
  assert.equal(T.bcfLesenHtml(null), '');
});

// ---------------------------------------------------------------------------------------------- Verdrahtung

test('Verdrahtung: Register «Koordination» zwischen Ändern und Ansicht, Gruppen, Aktionen, Symbole, Import', () => {
  const mb = lies('./menueband.js');
  assert.match(mb, /\['aendern', 'Ändern'\], \['koordination', 'Koordination'\], \['ansicht', 'Ansicht'\]/);
  assert.match(mb, /\['einfuegen', 'Einfügen'\], \['beschriften', 'Beschriften'\], \['aendern', 'Ändern'\]/, 'Reihenfolge Einfügen › Beschriften › Ändern bleibt (einfuegen.browser.mjs, werkzeuge.browser.mjs)');
  const folge = /koordination: \[([^\]]*)\]/.exec(mb)[1].match(/'([^']+)'/g).map((x) => x.slice(1, -1));
  assert.deepEqual(folge, ['Vorgaben', 'Typenliste', 'Kollisionen', 'Aussparungen', 'Koordinationsplan', 'BCF']);   // Koordinationsplan seit Modul 7 Teil B3
  assert.match(lies('./aktionen.js'), /import '\.\/aktionen-koordination\.js';/);

  const q = lies('./aktionen-koordination.js');
  const gruppen = [...q.matchAll(/register: REGISTER, gruppe: '([^']+)'/g)].map((m) => m[1]);
  assert.deepEqual([...new Set(gruppen)], folge, 'jede Gruppe hat Aktionen, in der Reihenfolge des Menübands');
  assert.match(q, /const REGISTER = 'koordination'/);
  const ids = [...q.matchAll(/id: '(koordination\.[a-z-]+)'/g)].map((m) => m[1]);
  assert.deepEqual(ids, ['koordination.vorgaben', 'koordination.typenliste', 'koordination.pruefen', 'koordination.kollisionsliste', 'koordination.aus-vorschlagen', 'koordination.aus-nummerieren',
    'koordination.aus-liste', 'koordination.aus-abgleich', 'koordination.aus-plan', 'koordination.aus-ifc', 'koordination.plan', 'koordination.boxen', 'koordination.bcf-export', 'koordination.bcf-import']);
  const symbole = [...q.matchAll(/symbol: '([^']+)'/g)].map((m) => m[1]);
  assert.equal(symbole.length, ids.length);
  const unbekannt = sym.symbol('gibtsnicht');
  for (const s of symbole) assert.notEqual(sym.symbol(s), unbekannt, `Symbol «${s}» fehlt in symbole.js`);
  assert.equal(new Set(symbole).size, symbole.length, 'jedes Symbol nur einmal');
  // die Knöpfe des Panels rufen nur registrierte Aktionen
  const panel = lies('./koordination-panel.js');
  for (const m of panel.matchAll(/data-kp="aktion:([a-z.-]+)"/g)) assert.ok(ids.includes(m[1]), m[1]);
});

test('Verdrahtung: nur Befehle und Abfragen des Kerns, keine Fachlogik im Browser', () => {
  const d = lies('./koordination-dialoge.js') + lies('./koordination-panel.js');
  for (const b of ['vorgaben_setzen', 'kollision_pruefen', 'kollision_status', 'aussparungen_vorschlagen', 'aussparungen_nummerieren', 'aussparung_status', 'aussparungen_abgleichen', 'aussparung_form',
    'bcf_import', 'element_anlegen', 'planblatt_anlegen', 'ansicht_platzieren', 'typmaterial_setzen']) assert.ok(d.includes(`'${b}'`), `Befehl ${b}`);
  for (const a of ['vorgaben', 'kollisionen', 'aussparungsliste', 'aussparungen_ifc', 'bcf_export', 'bcf_lesen', 'typmaterial']) assert.ok(d.includes(`'${a}'`), `Abfrage ${a}`);
  // der Kern kennt die Befehle und Abfragen (soweit schon gebaut)
  const kern = [kernDatei('Kollision/Typenliste.cs'), kernDatei('Kollision/KollisionBefehle.cs'), kernDatei('Kollision/Kollisionsliste.cs'), kernDatei('Kollision/Kollisionspruefung.cs'), kernDatei('Aussparungen/AussparungBefehle.cs'),
    kernDatei('Aussparungen/AussparungPflege.cs'), kernDatei('Aussparungen/AussparungAbgleich.cs'), kernDatei('Aussparungen/AussparungVorschlag.cs'), kernDatei('Kollision/Vorgaben.cs'), kernDatei('Kollision/BcfBefehle.cs')].filter(Boolean).join('\n');
  if (kern) for (const n of ['vorgaben_setzen', 'kollision_pruefen', 'kollision_status', 'aussparungen_vorschlagen', 'aussparungen_nummerieren', 'aussparung_status', 'aussparungen_abgleichen', 'aussparung_form', 'bcf_import', 'bcf_export', 'bcf_lesen', 'aussparungsliste', 'typmaterial_setzen'])
    assert.ok(kern.includes(`"${n}"`), `${n} im Kern`);
  // die Abfrage typmaterial steht nicht in den Dateien der Kollisions-Befehle, sondern im MCP (WerkzeugeKollision.cs): dort als Name des Werkzeugs
  const mcp = existsSync(path.resolve(hier, '../../../../Hlks.Mcp/WerkzeugeKollision.cs')) ? readFileSync(path.resolve(hier, '../../../../Hlks.Mcp/WerkzeugeKollision.cs'), 'utf8') : null;
  if (mcp) assert.ok(mcp.includes('Name = "typmaterial"'), 'typmaterial im MCP');
  // keine Zahl als Vorgabewert im Browser-Code der Dialoge
  assert.doesNotMatch(lies('./koordination-dialoge.js'), /(spiel_aussparung|grenze_kernbohrung|abstand_zusammenfassen|abstand_leitungen|toleranz_hart)\W{1,6}\d/);
  // beide Dialoge mit Koordinaten benutzen dieselbe Auswahl; die Palette ändert die Form nur über aussparung_form
  const dlg = lies('./koordination-dialoge.js');
  assert.equal((dlg.match(/T\.koordinatenOptionenHtml\(/g) ?? []).length, 2, 'IFC und BCF');
  assert.match(lies('./koordination-panel.js'), /sende\('aussparung_form'/);
  assert.doesNotMatch(dlg + lies('./koordination-panel.js'), /grenze_kernbohrung/, 'die Dialoge und die Palette kennen die Grenze nicht mehr (nur koordination-text.js für den Hinweis)');
});

test('Design: der CSS-Block der Koordination hat keine festen Farben, nur Variablen', () => {
  const css = lies('../../css/oberflaeche.css');
  const block = css.slice(css.indexOf('/* ---- Koordination (Modul 3b, C1)'));
  assert.ok(block.length > 500, 'Block gefunden');
  assert.doesNotMatch(block, /#[0-9a-fA-F]{3,8}\b/, 'keine Hex-Farben');
  assert.doesNotMatch(block, /\brgba?\(|\bhsla?\(/, 'keine rgb/hsl-Farben');
  for (const k of ['.koord-panel', '.kp-tab', '.kp-art-hart', '.kp-hinweise', '.kp-dlg']) assert.ok(block.includes(k), k);
});

// ---------------------------------------------------------------------------------------------- gegen den echten Kern (nur lesend und Trockenläufe)

test('Echter Kern (HLKS_TEST_SERVER): Antworten von vorgaben, kollisionen, aussparungsliste passen zu den Bausteinen; Quelle ist Pflicht', { skip: !process.env.HLKS_TEST_SERVER }, async () => {
  const s = process.env.HLKS_TEST_SERVER.replace(/\/$/, '');
  const frage = async (n, p = {}) => (await (await fetch(`${s}/api/abfrage/${n}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(p) })).json());
  const befehl = async (n, p = {}) => (await (await fetch(`${s}/api/befehl/${n}?trockenlauf=true`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(p) })).json());
  const v = await frage('vorgaben');
  assert.ok(Array.isArray(v.fehlend) && v.werte && Array.isArray(v.abstandWeichSeiten));
  const f = T.vorgabenFormular(v);
  assert.doesNotThrow(() => T.vorgabenHtml(v, f));
  for (const x of v.fehlend) assert.ok(T.VORGABEN.some((y) => y.name === x.name), `Vorgabe ${x.name} kennt der Dialog`);
  assert.ok(Array.isArray(v.nichtMehrVerwendet) && v.nichtMehrVerwendet.some((x) => x.name === 'grenze_kernbohrung'), 'der Kern führt grenze_kernbohrung als «nicht mehr verwendet»');
  assert.ok(!v.fehlend.some((x) => x.name === 'grenze_kernbohrung'), 'und nicht mehr unter «fehlend»');
  assert.equal(f.alt.grenze_kernbohrung.gesetzt, T.nichtMehrGesetzt(v, 'grenze_kernbohrung'));
  // Quelle ist Pflicht: der Kern lehnt ohne Quelle ab, die Oberfläche sendet es gar nicht erst
  const ohne = await befehl('vorgaben_setzen', { werte: { spiel_aussparung: { wert: 20 } } });
  assert.equal(ohne.angenommen, false);
  const mit = JSON.parse(JSON.stringify(f)); mit.felder.spiel_aussparung = { wert: '20', quelle: FIKTIV };
  const par = T.vorgabenParameter(mit, f);
  assert.ok(par.werte.spiel_aussparung.quelle);
  assert.equal((await befehl('vorgaben_setzen', par)).angenommen, true, 'mit Quelle (Trockenlauf) angenommen');
  const k = await frage('kollisionen', T.kollisionFilterParameter(T.KOLLISION_FILTER_VORGABE()) ?? {});
  assert.ok(Array.isArray(k.zeilen));
  assert.doesNotThrow(() => T.kollisionTabelleHtml(k, {}));
  assert.doesNotThrow(() => T.kollisionKopf(k));
  for (const z of k.zeilen.slice(0, 20)) assert.doesNotThrow(() => T.kollisionDetailHtml(z));
  // Nachtrag C1c: Paare des letzten Laufs, Paar je Zeile, Sicht der Palette
  for (const z of k.zeilen.slice(0, 50)) assert.ok(typeof z.paar === 'string' && T.PAARE.some((p) => p.id === z.paar), `${z.kollision}: Paar ${z.paar} kennt die Oberfläche`);
  if (k.kollisionslauf) {
    const lp = T.laufPaare(k.kollisionslauf);
    assert.ok(Array.isArray(lp) && lp.length > 0 && lp.every((p) => T.PAARE.some((x) => x.id === p)), `kollisionslauf nennt die Paare des letzten Laufs: ${k.kollisionslauf}`);
    const sicht = T.kollisionSicht(k, T.KOLLISION_FILTER_VORGABE());
    assert.ok(sicht.zeilen.every((z) => lp.includes(z.paar)), 'die Sicht zeigt nur Paare des letzten Laufs');
    assert.equal(sicht.zeilen.length + sicht.ausgeblendet, k.zeilen.length, 'nichts geht verloren: gezeigt + ausgeblendet = Antwort');
    assert.equal(T.kollisionSicht(k, { ...T.KOLLISION_FILTER_VORGABE(), auchNichtGeprueft: true }).zeilen.length, k.zeilen.length);
    assert.doesNotThrow(() => T.kollisionTabelleHtml(sicht, {}));
  }
  const tm = await frage('typmaterial');
  for (const f of ['typen', 'unbekannt', 'ausTypname', 'kategorien', 'ohneQuelle', 'doppelt', 'hinweise']) assert.ok(Array.isArray(tm[f]), `typmaterial.${f}`);
  assert.ok(tm.zusammenfassung && 'unbekannteTypen' in tm.zusammenfassung && 'jeHerkunft' in tm.zusammenfassung);
  assert.deepEqual(T.typenKategorien(tm).map((x) => x.kategorie), eig('Typmaterial', 'kategorie').auswahl, 'Kategorien des Kerns = Katalog');
  for (const r of [...tm.unbekannt, ...tm.ausTypname]) for (const feld of ['schluessel', 'typnamen', 'bauteile', 'aussparungen', 'durchgaenge']) assert.ok(feld in r, `Typ ${r.schluessel}: Feld ${feld}`);
  for (const t of tm.typen) for (const feld of ['element', 'typname', 'kategorie', 'wirkung', 'quelle', 'bauteile', 'aussparungen', 'durchgaenge']) assert.ok(feld in t, `Eintrag ${t.element}: Feld ${feld}`);
  const f0 = T.typenFormular();
  for (const reiter of ['unbekannt', 'bestaetigen', 'liste']) assert.doesNotThrow(() => T.typenInhaltHtml(tm, { ...f0, reiter }), `Reiter ${reiter}`);
  assert.doesNotThrow(() => T.typenKopfHtml(tm)); assert.doesNotThrow(() => T.typenWarnungenHtml(tm)); assert.doesNotThrow(() => T.typenReiterHtml(tm, f0));
  if (tm.unbekannt.length) {   // Trockenlauf mit Quelle: angenommen; ohne Quelle: abgelehnt – die Oberfläche sendet es gar nicht erst
    const r0 = tm.unbekannt[0], kat = T.typenKategorien(tm)[0].kategorie;
    assert.equal((await befehl('typmaterial_setzen', { typen: [{ typname: r0.schluessel, kategorie: kat }] })).angenommen, false, 'ohne Quelle lehnt der Kern ab');
    const fo = { ...f0, unbekannt: { [r0.schluessel]: kat }, quelle: FIKTIV };
    const pa = T.typenParameter(T.typenAuswertung(fo, tm));
    assert.deepEqual(pa, { typen: [{ typname: r0.schluessel, kategorie: kat }], quelle: FIKTIV });
    const rt = await befehl('typmaterial_setzen', pa);
    assert.equal(rt.angenommen, true, 'mit Quelle (Trockenlauf) angenommen');
    assert.doesNotThrow(() => T.typenVorschauHtml(rt, {}));
    assert.equal((await frage('typmaterial')).typen.length, tm.typen.length, 'der Trockenlauf hat nichts geändert');
  }
  const a = await frage('aussparungsliste');
  assert.ok(Array.isArray(a.zeilen));
  assert.doesNotThrow(() => T.aussparungTabelleHtml(a.zeilen, {}));
  for (const z of a.zeilen.slice(0, 20)) assert.ok('formWaehlbar' in z && 'formVonHand' in z && 'formSperre' in z, `${z.aussparung}: Felder der Form (Nachtrag B3b)`);
  if (a.zeilen.length) {   // Trockenlauf: nur wählbare gehen an den Kern; er antwortet mit Hinweisen oder einer Ablehnung mit Fehler
    const z = T.formZiele(a.zeilen, a.zeilen.map((x) => x.aussparung));
    if (z.senden.length) { const r = await befehl('aussparung_form', T.formParameter({ aussparungen: z.senden, form: 'rechteckig' })); assert.ok(r.angenommen === true || typeof r.fehler === 'string'); assert.doesNotThrow(() => T.formMeldungHtml(r, { trocken: true, form: 'rechteckig', ziele: z })); }
  }
  const pl = await (await fetch(`${s}/api/befehl/kollision_pruefen?trockenlauf=true`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(T.pruefenParameter({ paare: T.PAARE_VORGABE, unterlagen: [], alleUnterlagen: [], geschoss: '', weich: true })) })).json();
  assert.ok(pl.angenommen === true || typeof pl.fehler === 'string');
  assert.doesNotThrow(() => T.laufHtml(pl, { trocken: true }));
});
