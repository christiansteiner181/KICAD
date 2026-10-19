// Tests der Listenansichten «kollisionen» und «aussparungsliste» (Modul 3b, Teil B4a) – listenformat.js (rein) und liste.js (mit kleinem
// Ersatz für DOM und fetch, ohne Browser). Die Fixtures haben das Format der Abfragen des Kerns (Kollisionsliste.Lesen,
// Aussparungsliste.Abfrage); der letzte Test prüft, dass jeder Spaltenschlüssel im Kern vorkommt. Alle Daten FIKTIV.
// Start: node --test js/ansichten/liste-3b.test.mjs   (aus src/Hlks.Web/wwwroot)

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { readFileSync } from 'node:fs';

const fmt = await import('./listenformat.js');

const hier = path.dirname(fileURLToPath(import.meta.url));
const wwwroot = path.resolve(hier, '../..');
const repo = path.resolve(hier, '../../../../..');
const lies = (rel) => readFileSync(path.join(wwwroot, rel), 'utf8');

// ---------------------------------------------------------------------------------------------------------- Fixtures im Format des Kerns

const kollisionen = {
  ableitung: 'Kollisionsliste', projekt: 'Test 3b', modellStand: { pruefsumme: 'k1', pakete: 9 }, erzeugt: '2026-10-19 10:00',
  filter: { status: null, art: 'hart', typ: null, geschoss: null, element: null },
  anzahl: 3, kollisionslauf: '2026-10-19 09:55 · Lauf 2 · 120 Paare',
  zusammenfassung: {
    jeStatus: { bestaetigt: 1, neu: 2 }, jeArt: { hart: 2, weich: 1 }, jeTyp: { durchdringung: 1, kontakt: 2 },
    jeArtTyp: { 'hart durchdringung': 1, 'hart kontakt': 1, 'weich kontakt': 1 }, jeGeschoss: { EG: 3 },
  },
  zeilen: [
    {
      kollision: 'KOL-10', art: 'hart', typ: 'durchdringung', status: 'neu', mass_mm: 12.5, geschoss: 'EG',
      beteiligte: 'RO-1 (Rohr) ↔ W-1 (Wand)', bauteil: 'W-1 (Wand)', lage_mm: [3000, 0, 1000], kommentar: null, pruefung: '2026-10-19 09:55 · Lauf 1 · 120 Paare',
      kennung: 'a1b2', beteiligteListe: [{ kennung: 'x', kennzeichen: 'RO-1', klasse: 'Rohr', text: 'RO-1 (Rohr)' }],
    },
    {
      kollision: 'KOL-2', art: 'weich', typ: 'kontakt', status: 'bestaetigt', mass_mm: 30, geschoss: 'EG',
      beteiligte: 'RO-1 (Rohr) ↔ KA-1 (Kanal)', bauteil: null, lage_mm: [2500.5, 100, 1500], kommentar: 'Rohr verschieben', pruefung: null,
      kennung: 'c3d4', beteiligteListe: [],
    },
    {
      kollision: 'KOL-3', art: 'hart', typ: 'kontakt', status: 'neu', mass_mm: null, geschoss: 'EG',
      beteiligte: 'KA-1 (Kanal) ↔ IfcBeam 1abc (IFC-1)', bauteil: null, lage_mm: [1, 2, 3], kommentar: null, pruefung: null, kennung: 'e5f6', beteiligteListe: [],
    },
  ],
};

const aussparungen = {
  ableitung: 'Aussparungsliste', projekt: 'Test 3b', modellStand: { pruefsumme: 'a1', pakete: 11 }, erzeugt: '2026-10-19 10:05',
  geschoss: 'EG', einheit: 'mm; Koten über ±0.00 bzw. über OK Geschoss (…Geschoss); rund: Kote der Achse',
  anzahl: 2, jeStatus: { ausgefuehrt: 1, vorgeschlagen: 1 }, jeArt: { aussparung: 1, kernbohrung: 1 },
  zeilen: [
    {
      aussparung: 'AUS-2', nummer: 'A-EG-001', geschoss: 'EG', bauteil: 'W-1', bauteilArt: 'Wand', unterlage: null, form: 'rechteckig', masse: '440 × 240',
      d: null, b: 440, h: 240, tiefe: 200, koteUk: 1880, koteOk: 2120, koteAchse: null, koteUkGeschoss: 1880, koteOkGeschoss: 2120, koteAchseGeschoss: null,
      art: 'aussparung', status: 'ausgefuehrt', leitungen: ['KA-1'], mitte: [2000, 0, 2000],
    },
    {
      aussparung: 'AUS-1', nummer: 'A-EG-002', geschoss: 'EG', bauteil: 'W-1', bauteilArt: 'Wand', unterlage: null, form: 'rund', masse: 'Ø 95',
      d: 95, b: null, h: null, tiefe: 200, koteUk: null, koteOk: null, koteAchse: 1000, koteUkGeschoss: null, koteOkGeschoss: null, koteAchseGeschoss: 1000,
      art: 'kernbohrung', status: 'vorgeschlagen', leitungen: ['RO-1', 'RO-2'], mitte: [3000, 0, 1000],
    },
  ],
};

// ---------------------------------------------------------------------------------------------------------- Aufbereitung

test('Kollisionsliste: Titel, Klick-Schlüssel «kollision», Spalten in Reihenfolge, leere Spalten und Einzelheiten verborgen', () => {
  const a = fmt.aufbereiten('kollisionen', kollisionen);
  assert.equal(a.titel, 'Kollisionsliste');
  assert.equal(a.projekt, 'Test 3b');
  assert.deepEqual(a.stand, { pruefsumme: 'k1', pakete: 9 });
  assert.equal(a.tabellen.length, 1);
  const t = a.tabellen[0];
  assert.equal(t.schluessel, 'kollision');
  assert.deepEqual(t.spalten.map((s) => s.titel), ['Kollision', 'Art', 'Typ', 'Status', 'Mass mm', 'Geschoss', 'Beteiligte', 'Gequertes Bauteil', 'Lage x, y, z mm', 'Kommentar', 'Prüflauf']);
  assert.deepEqual(t.spalten.map((s) => s.art), ['text', 'text', 'text', 'text', 'zahl', 'text', 'text', 'text', 'text', 'text', 'text']);
  assert.ok(!t.spalten.some((s) => ['kennung', 'beteiligteListe'].includes(s.schluessel)));
  assert.equal(t.zeilen.length, 3);
});

test('Kollisionsliste: Zusammenfassung mit Zahlen je Status, Art und Typ, letztem Prüflauf und Filter', () => {
  const z = fmt.aufbereiten('kollisionen', kollisionen).zusammenfassung;
  assert.equal(z[0], '3 Kollisionen');
  assert.equal(z[1], 'Status: bestätigt 1 · neu 2');
  assert.equal(z[2], 'Art: hart 2 · weich 1');
  assert.equal(z[3], 'Typ: durchdringung 1 · kontakt 2');
  assert.equal(z[4], 'Letzter Prüflauf: 2026-10-19 09:55 · Lauf 2 · 120 Paare');
  assert.equal(z[5], 'Filter: art hart');
  assert.equal(z.length, 6);
});

test('Kollisionsliste: Spalte ohne jeden Wert entfällt (Kommentar, Gequertes Bauteil), eine einzige Kollision heisst «Kollision»', () => {
  const eine = structuredClone(kollisionen);
  eine.zeilen = [{ ...eine.zeilen[2] }];
  eine.anzahl = 1;
  eine.kollisionslauf = null;
  eine.filter = { status: null, art: null, typ: null, geschoss: null, element: null };
  const a = fmt.aufbereiten('kollisionen', eine);
  const titel = a.tabellen[0].spalten.map((s) => s.titel);
  assert.ok(!titel.includes('Kommentar') && !titel.includes('Gequertes Bauteil') && !titel.includes('Prüflauf') && !titel.includes('Mass mm'));
  assert.ok(titel.includes('Kollision') && titel.includes('Beteiligte'));
  assert.equal(a.zusammenfassung[0], '1 Kollision');
  assert.ok(!a.zusammenfassung.some((x) => x.startsWith('Filter') || x.startsWith('Letzter')));
});

test('Kollisionsliste: Werte – Lage mit Komma, Mass mit Dezimalkomma, Status ohne Umlautersatz, Sortieren nach Mass', () => {
  const t = fmt.aufbereiten('kollisionen', kollisionen).tabellen[0];
  assert.equal(fmt.formatWert('lage_mm', [3000, 0, 1000]), '3000, 0, 1000');
  assert.equal(fmt.formatWert('lage_mm', [2500.5, 100, 1500]), '2500,5, 100, 1500');
  assert.equal(fmt.formatWert('mass_mm', 12.5), '12,5');
  assert.equal(fmt.formatWert('mass_mm', null), '');
  assert.equal(fmt.formatWert('status', 'bestaetigt'), 'bestätigt');
  assert.equal(fmt.formatWert('status', 'neu'), 'neu');
  assert.equal(fmt.formatWert('art', 'hart'), 'hart');
  // natürlich sortiert: KOL-2 vor KOL-3 vor KOL-10; nach Mass absteigend, leer ans Ende
  assert.deepEqual(fmt.sortiere(t.zeilen, 'kollision').map((z) => z.kollision), ['KOL-2', 'KOL-3', 'KOL-10']);
  assert.deepEqual(fmt.sortiere(t.zeilen, 'mass_mm', true).map((z) => z.kollision), ['KOL-2', 'KOL-10', 'KOL-3']);
});

test('Zusatz B4d-UI: Aussparungsliste mit Spalte «Kurzzeichen» (hinter der Nummer); ohne das Feld (älterer Kern) entfällt sie wie jede leere Spalte', () => {
  const mit = { ...aussparungen, zeilen: aussparungen.zeilen.map((z, i) => ({ ...z, kurzzeichen: i === 0 ? 'S DD' : 'WD' })) };
  const t = fmt.aufbereiten('aussparungsliste', mit).tabellen[0];
  const titel = t.spalten.map((s) => s.titel);
  assert.deepEqual(titel.slice(0, 3), ['Nummer', 'Kurzzeichen', 'Aussparung']);
  assert.deepEqual(t.zeilen.map((z) => z.kurzzeichen), ['S DD', 'WD'], 'die Werte kommen unverändert vom Kern');
  assert.ok(!fmt.aufbereiten('aussparungsliste', aussparungen).tabellen[0].spalten.some((s) => s.schluessel === 'kurzzeichen'), 'ältere Kerne: keine Spalte');
  assert.match(readFileSync(path.join(repo, 'src', 'Hlks.Kern', 'Aussparungen', 'AussparungAbgleich.cs'), 'utf8'), /\["kurzzeichen"\]/, 'der Kern liefert das Feld');
});

test('Aussparungsliste: Spalten in Reihenfolge, b/h/d verborgen, Spalte ohne Wert (Unterlage) entfällt, Schlüssel «aussparung»', () => {
  const a = fmt.aufbereiten('aussparungsliste', aussparungen);
  assert.equal(a.titel, 'Aussparungsliste');
  const t = a.tabellen[0];
  assert.equal(t.schluessel, 'aussparung');
  assert.deepEqual(t.spalten.map((s) => s.titel), [
    'Nummer', 'Aussparung', 'Geschoss', 'Status', 'Art', 'Form', 'Mass mm', 'Tiefe mm',
    'UK ü. ±0.00 mm', 'OK ü. ±0.00 mm', 'Achse ü. ±0.00 mm', 'UK ü. OK Geschoss mm', 'OK ü. OK Geschoss mm', 'Achse ü. OK Geschoss mm',
    'Bauteil', 'Bauteilart', 'Leitungen', 'Mitte x, y, z mm',
  ]);
  assert.equal(t.spalten.find((s) => s.schluessel === 'tiefe').art, 'zahl');
  assert.equal(t.spalten.find((s) => s.schluessel === 'koteUk').art, 'zahl');
  assert.ok(!t.spalten.some((s) => ['d', 'b', 'h', 'unterlage'].includes(s.schluessel)));
  // «nummer» entfällt nicht: sie weicht vom Kennzeichen ab
  assert.ok(t.spalten.some((s) => s.schluessel === 'nummer'));
});

test('Aussparungsliste: nur runde Aussparungen → keine Spalten für UK/OK; Werte der Zeilen', () => {
  const rund = structuredClone(aussparungen);
  rund.zeilen = [rund.zeilen[1]];
  rund.anzahl = 1;
  const t = fmt.aufbereiten('aussparungsliste', rund).tabellen[0];
  const schluessel = t.spalten.map((s) => s.schluessel);
  assert.ok(schluessel.includes('koteAchse') && schluessel.includes('koteAchseGeschoss'));
  assert.ok(!schluessel.includes('koteUk') && !schluessel.includes('koteOkGeschoss'));
  const z = t.zeilen[0];
  assert.equal(fmt.formatWert('masse', z.masse), 'Ø 95');
  assert.equal(fmt.formatWert('leitungen', z.leitungen), 'RO-1, RO-2');
  assert.equal(fmt.formatWert('mitte', z.mitte), '3000, 0, 1000');
  assert.equal(fmt.formatWert('koteAchse', z.koteAchse), '1000');
});

test('Aussparungsliste: Zusammenfassung mit Geschoss, Zahlen je Status und Art, Einheiten', () => {
  const z = fmt.aufbereiten('aussparungsliste', aussparungen).zusammenfassung;
  assert.equal(z[0], '2 Aussparungen im Geschoss EG');
  assert.equal(z[1], 'Status: ausgeführt 1 · vorgeschlagen 1');
  assert.equal(z[2], 'Art: aussparung 1 · kernbohrung 1');
  assert.match(z[3], /^Einheiten: mm; Koten über ±0\.00/);
  const alle = structuredClone(aussparungen);
  alle.geschoss = null; alle.anzahl = 1;
  assert.equal(fmt.aufbereiten('aussparungsliste', alle).zusammenfassung[0], '1 Aussparung');
});

test('Aussparungsliste: Sortieren – Nummer natürlich (A-EG-2 vor A-EG-10), Kote numerisch, leere am Ende', () => {
  const zeilen = [{ nummer: 'A-EG-10', koteUk: 500 }, { nummer: 'A-EG-2', koteUk: 1880 }, { nummer: 'A-EG-1', koteUk: null }];
  assert.deepEqual(fmt.sortiere(zeilen, 'nummer').map((z) => z.nummer), ['A-EG-1', 'A-EG-2', 'A-EG-10']);
  assert.deepEqual(fmt.sortiere(zeilen, 'koteUk').map((z) => z.nummer), ['A-EG-10', 'A-EG-2', 'A-EG-1']);
  assert.deepEqual(fmt.sortiere(zeilen, 'koteUk', true).map((z) => z.nummer), ['A-EG-2', 'A-EG-10', 'A-EG-1']);
});

test('Leere und fehlende Antworten werfen nicht', () => {
  const leer = fmt.aufbereiten('kollisionen', { ableitung: 'Kollisionsliste', anzahl: 0, zeilen: [], zusammenfassung: { jeStatus: {}, jeArt: {}, jeTyp: {} }, hinweis: 'Noch keine Kollisionen – Prüflauf mit kollision_pruefen (zuerst als Trockenlauf).' });
  assert.equal(leer.tabellen[0].zeilen.length, 0);
  assert.deepEqual(leer.zusammenfassung, ['0 Kollisionen', 'Noch keine Kollisionen – Prüflauf mit kollision_pruefen (zuerst als Trockenlauf).']);
  assert.equal(fmt.aufbereiten('kollisionen', null).tabellen[0].zeilen.length, 0);
  assert.equal(fmt.aufbereiten('aussparungsliste', {}).tabellen[0].zeilen.length, 0);
  assert.equal(fmt.aufbereiten('aussparungsliste', { anzahl: 0, zeilen: [], jeStatus: {}, jeArt: {} }).zusammenfassung[0], '0 Aussparungen');
});

test('Bestehende Listen unverändert: Spalten ohne Wert bleiben dort, wo leereSpaltenAus nicht gilt', () => {
  const a = fmt.aufbereiten('raumliste', { ableitung: 'Raumliste', zeilen: [{ raum: 'R1', name: null, geschoss: null }] });
  assert.deepEqual(a.tabellen[0].spalten.map((s) => s.schluessel), ['raum', 'name', 'geschoss']);
  assert.equal(fmt.formatWert('x', 'bestaetigt'), 'bestätigt');   // nur die zwei Katalogwerte ohne Umlaut werden ersetzt
  assert.equal(fmt.formatWert('x', 'beton'), 'beton');
  assert.equal(fmt.formatWert('x', 'constructor'), 'constructor');
});

// ---------------------------------------------------------------------------------------------------------- liste.js mit Ersatz-DOM

/** Kleiner Ersatz für DOM-Knoten: genau das, was liste.js braucht. */
class Knoten {
  constructor(tag) { this.tag = tag; this.kinder = []; this.dataset = {}; this.attribute = {}; this.klassen = new Set(); this._text = ''; this.scrollTop = 0; }
  set className(v) { this.klassen = new Set(String(v).split(/\s+/).filter(Boolean)); }
  get className() { return [...this.klassen].join(' '); }
  get classList() {
    const k = this.klassen;
    return { add: (c) => k.add(c), contains: (c) => k.has(c), toggle: (c, an) => { if (an) k.add(c); else k.delete(c); } };
  }
  set textContent(v) { this._text = String(v); this.kinder = []; }
  get textContent() { return this._text + this.kinder.map((k) => k.textContent).join(''); }
  set innerHTML(html) {
    this.kinder = [];
    // genau das Gerüst aus liste.js: <div class="li-wurzel"><div class="li-kopf"></div><div class="li-inhalt"></div></div>
    assert.match(html, /^<div class="li-wurzel"><div class="li-kopf"><\/div><div class="li-inhalt"><\/div><\/div>$/);
    const w = new Knoten('div'); w.className = 'li-wurzel';
    for (const c of ['li-kopf', 'li-inhalt']) { const k = new Knoten('div'); k.className = c; w.kinder.push(k); }
    this.kinder.push(w);
  }
  append(...teile) { for (const t of teile) this.kinder.push(typeof t === 'string' ? Object.assign(new Knoten('#text'), { _text: t }) : t); }
  replaceChildren(...teile) { this.kinder = []; this.append(...teile); }
  setAttribute(n, v) { this.attribute[n] = v; }
  alle() { return this.kinder.flatMap((k) => [k, ...k.alle()]); }
  querySelector(sel) { return this.querySelectorAll(sel)[0] ?? null; }
  querySelectorAll(sel) {
    const m = /^([a-z]*)\.?([\w-]*)$/.exec(sel);
    return this.alle().filter((k) => (!m[1] || k.tag === m[1]) && (!m[2] || k.klassen.has(m[2])));
  }
}

const fakeDocument = { createElement: (tag) => new Knoten(tag), createTextNode: (t) => Object.assign(new Knoten('#text'), { _text: t }) };

/** Ruft liste.js mit einer Ansicht auf; fetch liefert die gegebene Antwort. Liefert { inhalt, kopf, aufrufe, instanz, zustandsmodul }. */
async function listeZeigen(ansicht, antwort) {
  const aufrufe = [];
  globalThis.document = fakeDocument;
  globalThis.fetch = async (url, opt) => {
    aufrufe.push({ url, body: opt?.body ? JSON.parse(opt.body) : null });
    return { ok: true, status: 200, headers: { get: () => 'application/json' }, json: async () => antwort, text: async () => JSON.stringify(antwort) };
  };
  const zs = await import('../zustand.js');
  const liste = (await import('./liste.js')).default;
  const behaelter = new Knoten('div');
  const instanz = liste.erstellen(behaelter, ansicht);
  for (let i = 0; i < 20 && aufrufe.length === 0; i++) await new Promise((r) => setTimeout(r, 5));
  await new Promise((r) => setTimeout(r, 20));
  const wurzel = behaelter.kinder[0];
  return { behaelter, kopf: wurzel.kinder[0], inhalt: wurzel.kinder[1], aufrufe, instanz, zs };
}

function elementeEintragen(zs, elemente) {
  zs.zustand.elemente.clear(); zs.zustand.nachKennzeichen.clear();
  for (const e of elemente) { zs.zustand.elemente.set(e.id, e); zs.zustand.nachKennzeichen.set(e.kennzeichen.toLowerCase(), e.id); }
}

const zeilenVon = (inhalt) => inhalt.querySelectorAll('tr').filter((tr) => tr.klassen.has('klickbar'));

test('liste.js: Kollisionsliste – fragt «kollisionen» ab, zeigt eine Zeile je Kollision, Klick wählt das Element (Kennzeichen der ersten Spalte)', async () => {
  const { inhalt, kopf, aufrufe, instanz, zs } = await listeZeigen({ werte: { liste: 'kollisionen' } }, kollisionen);
  try {
    elementeEintragen(zs, [{ id: 'id-k10', kennzeichen: 'KOL-10', klasse: 'Kollision' }, { id: 'id-k2', kennzeichen: 'KOL-2', klasse: 'Kollision' }, { id: 'id-k3', kennzeichen: 'KOL-3', klasse: 'Kollision' }]);
    assert.equal(aufrufe[0].url, '/api/abfrage/kollisionen');
    assert.deepEqual(aufrufe[0].body, {});   // Ansicht ohne Geschoss: keine Einschränkung
    assert.match(kopf.textContent, /Kollisionsliste · Test 3b/);
    assert.match(kopf.textContent, /3 Kollisionen/);
    assert.match(kopf.textContent, /Status: bestätigt 1 · neu 2/);
    const zeilen = zeilenVon(inhalt);
    assert.deepEqual(zeilen.map((tr) => tr.dataset.kennzeichen), ['KOL-10', 'KOL-2', 'KOL-3']);
    assert.match(zeilen[0].textContent, /RO-1 \(Rohr\) ↔ W-1 \(Wand\)/);
    assert.match(zeilen[1].textContent, /bestätigt/);
    zeilen[1].onclick();
    assert.equal(zs.zustand.auswahl, 'id-k2');
    zeilen[0].onclick();
    assert.equal(zs.zustand.auswahl, 'id-k10');
    // Markierung der gewählten Zeile
    instanz.auswahlGeaendert('id-k3');
    assert.deepEqual(zeilenVon(inhalt).filter((tr) => tr.klassen.has('gewaehlt')).map((tr) => tr.dataset.kennzeichen), ['KOL-3']);
  } finally { instanz.zerstoeren(); }
});

test('liste.js: Aussparungsliste – Geschoss der Ansicht geht als Parameter mit, Klick wählt die Aussparung, Spaltenkopf sortiert', async () => {
  const { inhalt, aufrufe, instanz, zs } = await listeZeigen({ geschoss: 'EG', werte: { liste: 'aussparungsliste' } }, aussparungen);
  try {
    elementeEintragen(zs, [{ id: 'id-a1', kennzeichen: 'AUS-1', klasse: 'Aussparung' }, { id: 'id-a2', kennzeichen: 'AUS-2', klasse: 'Aussparung' }]);
    assert.equal(aufrufe[0].url, '/api/abfrage/aussparungsliste');
    assert.deepEqual(aufrufe[0].body, { geschoss: 'EG' });
    let zeilen = zeilenVon(inhalt);
    assert.deepEqual(zeilen.map((tr) => tr.dataset.kennzeichen), ['AUS-2', 'AUS-1']);   // Reihenfolge des Kerns: nach Nummer
    zeilen[1].onclick();
    assert.equal(zs.zustand.auswahl, 'id-a1');
    // Sortieren nach Tiefe-Spalte ist möglich, ebenso nach Nummer absteigend
    const kopfzeile = inhalt.querySelector('thead').kinder[0];
    const nummer = kopfzeile.kinder.find((th) => th.textContent.startsWith('Nummer'));
    nummer.onclick(); nummer.onclick();   // zweiter Klick: absteigend
    const nummern = inhalt.querySelectorAll('tr').filter((tr) => tr.klassen.has('klickbar')).map((tr) => tr.dataset.kennzeichen);
    assert.deepEqual(nummern, ['AUS-1', 'AUS-2']);
    assert.match(inhalt.querySelector('tbody').textContent, /Ø 95/);
    assert.match(inhalt.querySelector('tbody').textContent, /RO-1, RO-2/);
  } finally { instanz.zerstoeren(); }
});

test('liste.js: Fehler der Abfrage erscheinen im Kasten, ohne Absturz', async () => {
  const aufrufe = [];
  globalThis.document = fakeDocument;
  globalThis.fetch = async (url) => { aufrufe.push(url); return { ok: false, status: 400, headers: { get: () => 'application/json' }, json: async () => ({ fehler: 'Kein Projekt offen.', vorschlag: 'Projekt öffnen.' }) }; };
  const liste = (await import('./liste.js')).default;
  const b = new Knoten('div');
  const instanz = liste.erstellen(b, { werte: { liste: 'kollisionen' } });
  await new Promise((r) => setTimeout(r, 40));
  try {
    const inhalt = b.kinder[0].kinder[1];
    assert.match(inhalt.textContent, /Liste konnte nicht erstellt werden\. Kein Projekt offen\./);
    assert.match(inhalt.textContent, /Projekt öffnen\./);
  } finally { instanz.zerstoeren(); }
});

// ---------------------------------------------------------------------------------------------------------- Verträge mit Kern und Katalog

test('liste.js: kennt beide Listennamen, die bisherigen Zeilen für Namen und Geschossfilter bleiben (ansichten.test.mjs, huelle.test.mjs)', () => {
  const q = lies('js/ansichten/liste.js');
  assert.match(q, /NAMEN_MODUL3B = \['kollisionen', 'aussparungsliste'\]/);
  assert.match(q, /NAMEN_MODUL3 = \['materialliste'\]/);
  assert.match(q, /NAMEN = \['raumliste', 'strangliste', 'mengen', 'raumgrenzen', 'huellbauteile'\]/);
  assert.match(q, /\['raumliste', 'mengen', 'raumgrenzen', 'huellbauteile', 'materialliste'\]\.includes\(n\)/);
});

test('Katalog: Ansicht «liste» kennt kollisionen und aussparungsliste, «darstellung» kennt aussparungen', () => {
  const klassen = JSON.parse(readFileSync(path.join(repo, 'kataloge', 'klassen.json'), 'utf8'));
  const ansicht = (Array.isArray(klassen) ? klassen : klassen.klassen).find((k) => k.name === 'Ansicht');
  const auswahl = (n) => ansicht.eigenschaften.find((e) => e.name === n).auswahl;
  assert.ok(auswahl('liste').includes('kollisionen') && auswahl('liste').includes('aussparungsliste'));
  assert.ok(auswahl('darstellung').includes('aussparungen'));
});

test('Kern: jeder Spaltenschlüssel der beiden Listen steht in der Abfrage des Kerns', () => {
  const kern = (rel) => readFileSync(path.join(repo, 'src', 'Hlks.Kern', rel), 'utf8');
  const kollisionsQuelle = kern('Kollision/Kollisionsliste.cs');
  const aussparungsQuelle = kern('Aussparungen/AussparungAbgleich.cs');
  const hatSchluessel = (quelle, k) => quelle.includes(`["${k}"]`);
  // Spaltenschlüssel aus den Fixtures (= Vorgaben) gegen den Kern
  for (const k of Object.keys(kollisionen.zeilen[0])) assert.ok(hatSchluessel(kollisionsQuelle, k), `Kollisionsliste.cs kennt «${k}» nicht`);
  for (const k of Object.keys(aussparungen.zeilen[0])) assert.ok(hatSchluessel(aussparungsQuelle, k), `AussparungAbgleich.cs kennt «${k}» nicht`);
  // und jede Vorgabespalte kommt in den Fixtures vor (sonst wäre sie nie sichtbar)
  const sichtbar = (name, daten) => fmt.aufbereiten(name, daten).tabellen[0].spalten.map((s) => s.schluessel);
  for (const k of ['kollision', 'art', 'typ', 'status', 'mass_mm', 'geschoss', 'beteiligte', 'bauteil', 'lage_mm', 'kommentar', 'pruefung'])
    assert.ok(sichtbar('kollisionen', kollisionen).includes(k), k);
  for (const k of ['nummer', 'aussparung', 'geschoss', 'status', 'art', 'form', 'masse', 'tiefe', 'koteUk', 'koteOk', 'koteAchse', 'koteUkGeschoss', 'koteOkGeschoss', 'koteAchseGeschoss', 'bauteil', 'bauteilArt', 'leitungen', 'mitte'])
    assert.ok(sichtbar('aussparungsliste', aussparungen).includes(k), k);
});
