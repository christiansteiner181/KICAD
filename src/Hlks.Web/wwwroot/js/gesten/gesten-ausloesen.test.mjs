// Tests zu Modul G «Gesten am Arbeitsplatz», Teil Oberfläche (hlks-modul), ohne Browser und ohne Kamera: Einstellung und Vorgaben, Liste der Gesten, Zustände der Statusleiste,
// Stellschrauben (generisch), Handskelett, Punktfolge, Auslösen der Schritt-Gesten (G2, G3, G6) mit einer Attrappe von gesten.js, Dialogtexte, Kürzel ohne Kollision,
// Einbindung ohne Last (gesten.js nur per dynamischem import). gesten.js, gesten-logik.js und gesten-worker.js baut hlks-kern; hier gibt es sie nur als Attrappe.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/gesten/gesten-ausloesen.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

import * as T from './gesten-text.js';
import * as L from './gesten-ausloesen-logik.js';
import * as D from './gesten-dialog-text.js';
import { schrittAusfuehren, ausloesenEinrichten } from './gesten-ausloesen.js';
import { steuerungErzeugen } from './gesten-steuerung.js';
import * as A from '../oberflaeche/anordnung.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const js = path.join(hier, '..');
const lies = (rel) => readFileSync(path.join(js, rel), 'utf8');

// ---------------------------------------------------------------------------------------------- Attrappe von gesten.js

/** Attrappe mit genau der Schnittstelle aus «Schnittstellen im Bau»; ereignis() lässt gesten.js etwas melden. */
function attrappe({ startenWirft = null, kamerasListe = [{ id: 'cam1', name: 'Eingebaute Kamera' }, { id: 'cam2', name: 'Externe <b>Kamera</b>' }] } = {}) {
  const hoerer = new Set();
  const m = {
    aufrufe: [], laeuft: false, test: false, aufnahme: false,
    async starten(opt) { m.aufrufe.push(['starten', opt]); if (startenWirft) throw startenWirft; m.laeuft = true; },
    stoppen() { m.aufrufe.push(['stoppen']); m.laeuft = false; },
    einstellen(teil) { m.aufrufe.push(['einstellen', teil]); },
    kameras: async () => kamerasListe,
    abonnieren(fn) { hoerer.add(fn); return () => hoerer.delete(fn); },
    testmodus(an) { m.test = an; m.aufrufe.push(['testmodus', an]); },
    aufnahmeStarten() { m.aufnahme = true; m.aufrufe.push(['aufnahmeStarten']); },
    async aufnahmeBeenden() { m.aufnahme = false; return { version: 1, hand: 'links', bilder: [{ t: 0, punkte: Array.from({ length: 21 }, (_, i) => [i / 21, 0.5]), welt: [[0, 0, 0]], haendigkeit: 'Left', bild: 'DATA' }] }; },
    ereignis(e) { for (const fn of [...hoerer]) fn(e); },
    zahlHoerer: () => hoerer.size,
  };
  return m;
}

/** Steuerung mit Attrappe, Speicher im Arbeitsspeicher und gesammelten Meldungen. */
function bauen({ gespeichert = null, modul = attrappe(), ladenWirft = null, erlaubt = true, stellschrauben = null } = {}) {
  const log = { laden: 0, geschrieben: [], meldungen: [] };
  const s = steuerungErzeugen({
    laden: async () => { log.laden++; if (ladenWirft) throw ladenWirft; return modul; },
    stellschrauben: async () => stellschrauben,
    lesen: async () => gespeichert,
    schreiben: async (w) => { log.geschrieben.push(JSON.parse(JSON.stringify(w))); },
    melden: (t, a) => log.meldungen.push([t, a]),
    erlaubt: () => erlaubt,
  });
  return { s, modul, log };
}

/** Umgebung der Auslösung mit Attrappen für Dialog, Ansicht, Register und Meldung. */
function umgebung({ dialog = null, ansicht = { seiten: false, rollbar: false }, aktionOk = true, reiterTitel = '3D Lüftung', seite = { text: 'Seite 2 von 3' }, rollt = true } = {}) {
  const u = {
    aufrufe: [], meldungen: [],
    kontext: () => ({ dialog, ansicht: dialog ? null : ansicht }),
    seite: (schritt) => { u.aufrufe.push(['seite', schritt]); return seite; },
    rollen: (schritt) => { u.aufrufe.push(['rollen', schritt]); return rollt; },
    aktion: async (id) => {
      u.aufrufe.push(['aktion', id]);
      const titel = { 'ansicht.reiter-vor': 'Nächster Reiter', 'ansicht.reiter-zurueck': 'Voriger Reiter', 'ansicht.zoom-alles': 'Zoom alles' }[id] ?? id;
      return { ok: aktionOk, ergebnis: id.startsWith('ansicht.reiter-') ? { titel: reiterTitel } : undefined, titel };
    },
    meldung: (t) => u.meldungen.push(t),
  };
  return u;
}

const schritt = (geste, richtung) => ({ art: 'schritt', geste, richtung });

// ---------------------------------------------------------------------------------------------- Einstellung

test('Einstellung: Vorgaben – Gesten aus, linke Hand, Handskelett an, keine Stellschrauben, Belegung Reiter und Zoom alles', () => {
  const e = T.einstellungNormal(null);
  assert.deepEqual(e, { an: false, hand: 'links', kameraId: null, skelett: true, stellschrauben: {}, belegung: { G2: { links: 'ansicht.reiter-zurueck', rechts: 'ansicht.reiter-vor' }, G6: 'ansicht.zoom-alles' } });
  assert.deepEqual(T.einstellungNormal({}), e);
  assert.deepEqual(T.einstellungNormal('Unsinn'), e);
  assert.deepEqual(T.einstellungNormal([1]), e);
});

test('Einstellung: Rundlauf – eine gültige Einstellung bleibt, Unbrauchbares wird zur Vorgabe, der Eingabewert bleibt unverändert', () => {
  const roh = { an: true, hand: 'rechts', kameraId: 'abc', skelett: false, stellschrauben: { anstoss_weg: 0.05, 'Ungültig': 1, text: 'x', unendlich: Infinity }, belegung: { G2: { links: '', rechts: 'ansicht.zoom-alle' }, G6: 'ansicht.reiter' } };
  const kopie = structuredClone(roh);
  const e = T.einstellungNormal(roh);
  assert.deepEqual(roh, kopie, 'Eingabe unverändert');
  assert.deepEqual(e, { an: true, hand: 'rechts', kameraId: 'abc', skelett: false, stellschrauben: { anstoss_weg: 0.05 }, belegung: { G2: { links: '', rechts: 'ansicht.zoom-alle' }, G6: 'ansicht.reiter' } });
  assert.deepEqual(T.einstellungNormal(JSON.parse(JSON.stringify(e))), e, 'Normalisieren ist stabil');
  const k = T.einstellungNormal({ an: 'ja', hand: 'oben', kameraId: 5, skelett: 'nein', belegung: { G2: 3, G6: 7 } });
  assert.deepEqual(k, T.einstellungNormal(null), 'falsche Typen ergeben die Vorgabe');
});

test('Belegung: nur Aktionen für Ansicht und Navigation; unbekannte, leere oder gefährliche Ids ergeben keine Wirkung', () => {
  const e = T.einstellungNormal(null);
  assert.equal(T.belegungAktion(e, 'G2', 'rechts'), 'ansicht.reiter-vor');
  assert.equal(T.belegungAktion(e, 'G2', 'links'), 'ansicht.reiter-zurueck');
  assert.equal(T.belegungAktion(e, 'G6'), 'ansicht.zoom-alles');
  assert.equal(T.belegungAktion(e, 'G2', 'hoch'), null);
  assert.equal(T.belegungAktion(e, 'G9'), null);
  const boes = T.einstellungNormal({ belegung: { G2: { rechts: 'aendern.loeschen' }, G6: 'datei.schliessen' } });
  assert.equal(T.belegungAktion(boes, 'G2', 'rechts'), null, 'Löschen ist nicht belegbar');
  assert.equal(T.belegungAktion(boes, 'G6'), null, 'Projekt schliessen ist nicht belegbar');
  assert.equal(T.belegungAktion(T.belegungSetzen(e, 'G6', '', ''), 'G6'), null, 'leer = keine Wirkung');
  assert.equal(T.belegungAktion(T.belegungSetzen(e, 'G6', '', 'ansicht.zoom-alle'), 'G6'), 'ansicht.zoom-alle');
  assert.equal(T.belegungAktion(T.belegungSetzen(e, 'G2', 'links', 'ansicht.reiter-vor'), 'G2', 'links'), 'ansicht.reiter-vor');
  assert.ok(T.BELEGBAR.every((id) => id.startsWith('ansicht.')), 'alles aus dem Register Ansicht');
  assert.ok(!T.BELEGBAR.some((id) => /loeschen|aendern|anlegen|speichern|schliessen/.test(id)));
});

test('Auf Vorgabe zurück: alles ausser dem Grundschalter', () => {
  const e = T.einstellungNormal({ an: true, hand: 'rechts', kameraId: 'x', skelett: false, stellschrauben: { a: 1 }, belegung: { G6: '' } });
  assert.deepEqual(T.aufVorgabe(e), { ...T.einstellungNormal(null), an: true });
  assert.deepEqual(T.aufVorgabe({ an: false, hand: 'rechts' }), T.einstellungNormal(null));
});

// ---------------------------------------------------------------------------------------------- Liste, Status

test('Liste der Gesten: G2 bis G7 mit Nummer, Geste, Wirkung und Art; G2 und G6 zeigen die Belegung', () => {
  const titel = { 'ansicht.reiter-vor': 'Nächster Reiter', 'ansicht.reiter-zurueck': 'Voriger Reiter', 'ansicht.zoom-alles': 'Zoom​ alles' };
  const z = T.gestenZeilen(null, (id) => titel[id]);
  assert.deepEqual(z.map((x) => x.nr), ['G2', 'G3', 'G4', 'G5', 'G6', 'G7']);
  assert.deepEqual(z.map((x) => x.art), ['Schritt', 'Schritt', 'fliessend', 'fliessend', 'Schritt', 'fliessend']);
  assert.ok(z.every((x) => x.name && x.wirkung));
  assert.match(z[0].wirkung, /nach rechts «Nächster Reiter».*nach links «Voriger Reiter»/);
  assert.match(z[4].wirkung, /«Zoom alles»/, 'weiche Trennzeichen entfallen');
  const anders = T.gestenZeilen({ belegung: { G6: '' } }, () => undefined);
  assert.match(anders[4].wirkung, /keine Wirkung/);
  assert.ok(!z.some((x) => x.nr === 'G1'), 'G1 entfällt');
});

test('Statusleiste: vier Zustände aus, bereit, Hand erkannt, Geste aktiv mit Namen – dazu «startet»; Fehler im Hinweis', () => {
  const an = { an: true, laeuft: true };
  assert.equal(T.statusAnzeige({}).stufe, 'aus');
  assert.equal(T.statusAnzeige({ an: false, laeuft: false, zustand: 'aus' }).text, 'Gesten aus');
  const bereit = T.statusAnzeige({ ...an, zustand: 'bereit' });
  assert.deepEqual([bereit.stufe, bereit.text], ['bereit', 'Gesten bereit']);
  assert.match(bereit.titel, /keine Hand/);
  const hand = T.statusAnzeige({ ...an, zustand: 'hand' });
  assert.deepEqual([hand.stufe, hand.text], ['hand', 'Hand erkannt']);
  for (const g of ['drehen', 'rollen', 'zoomen', 'verschieben']) {
    const a = T.statusAnzeige({ ...an, zustand: 'aktiv', geste: g });
    assert.deepEqual([a.stufe, a.text], ['aktiv', `Geste: ${g}`]);
  }
  assert.equal(T.statusAnzeige({ ...an, zustand: 'aktiv', geste: null }).text, 'Geste: Geste');
  assert.equal(T.statusAnzeige({ startet: true, an: true }).stufe, 'startet');
  const fehler = T.statusAnzeige({ an: false, laeuft: false, zustand: 'aus', fehler: 'Kamera fehlt.' });
  assert.equal(fehler.stufe, 'aus');
  assert.match(fehler.titel, /Zuletzt: Kamera fehlt\./);
  assert.match(fehler.titel, /Rechtsklick/, 'Bedienhinweis');
  assert.equal(T.statusAnzeige({ an: true, laeuft: false, zustand: 'bereit' }).stufe, 'aus', 'eingeschaltet, aber nicht in Betrieb: aus');
  assert.match(T.statusAnzeige({ ...an, zustand: 'hand', test: true }).titel, /Testfeld/);
});

test('Fehlertexte der Kamera sind deutsch und führen zur Lösung', () => {
  assert.match(T.fehlerText({ name: 'NotAllowedError' }), /nicht erlaubt/);
  assert.match(T.fehlerText({ name: 'NotFoundError' }), /keine passende Kamera/);
  assert.match(T.fehlerText({ name: 'NotReadableError' }), /anderen Programm/);
  assert.equal(T.fehlerText(new Error('Die Kamera fehlt.')), 'Die Kamera fehlt.');
  assert.equal(T.fehlerText(new Error('')), 'Die Gesten konnten nicht gestartet werden.');
  assert.equal(T.fehlerText('Text'), 'Text');
});

// ---------------------------------------------------------------------------------------------- Stellschrauben

test('Stellschrauben: generisch aus STELLSCHRAUBEN_VORGABE – Vorgabe, Bereich, Einheit, Erklärung in mehreren Schreibweisen', () => {
  const vorgabe = {
    anstoss_weg: { vorgabe: 0.045, min: 0.02, max: 0.2, schritt: 0.005, einheit: 'm', titel: 'Anstoss: Weg', erklaerung: 'So weit muss die Hand sich bewegen.' },
    anstoss_tempo_ms: { wert: 0.35, bereich: [0.2, 3], einheit: 'm/s', beschreibung: 'Tempo des Anstosses.' },
    totbereich: 0.015,
    sperre_ms: { vorgabe: 300 },
    kaputt: { min: 1 },
    text: 'x',
  };
  const z = T.stellschraubenZeilen(vorgabe, { anstoss_weg: 0.06, totbereich: 5 });
  assert.deepEqual(z.map((x) => x.name), ['anstoss_weg', 'anstoss_tempo_ms', 'totbereich', 'sperre_ms'], 'Einträge ohne Zahl entfallen');
  const w = z[0];
  assert.deepEqual([w.titel, w.vorgabe, w.wert, w.min, w.max, w.schritt, w.einheit, w.geaendert], ['Anstoss: Weg', 0.045, 0.06, 0.02, 0.2, 0.005, 'm', true]);
  assert.equal(w.erklaerung, 'So weit muss die Hand sich bewegen.');
  assert.deepEqual([z[1].min, z[1].max, z[1].erklaerung, z[1].geaendert], [0.2, 3, 'Tempo des Anstosses.', false]);
  assert.deepEqual([z[2].wert, z[2].geaendert], [5, true], 'ohne Bereich wird nicht begrenzt');
  assert.equal(z[3].titel, 'sperre_ms', 'ohne Titel gilt der Name');
  assert.deepEqual(T.stellschraubenZeilen(null, {}), []);
  assert.deepEqual(T.stellschraubenZeilen(vorgabe, { anstoss_weg: 99 })[0].wert, 0.2, 'gespeicherter Wert wird auf den Bereich begrenzt');
});

test('Stellschraube setzen: begrenzt auf den Bereich, Vorgabe entfernt den Eintrag, Ungültiges ändert nichts, Komma geht', () => {
  const zeile = T.stellschraubenZeilen({ a: { vorgabe: 1, min: 0, max: 2 } })[0];
  const e0 = T.einstellungNormal(null);
  assert.deepEqual(T.stellschraubeSetzen(e0, zeile, 1.5).stellschrauben, { a: 1.5 });
  assert.deepEqual(T.stellschraubeSetzen(e0, zeile, '1,25').stellschrauben, { a: 1.25 });
  assert.deepEqual(T.stellschraubeSetzen(e0, zeile, 9).stellschrauben, { a: 2 });
  assert.deepEqual(T.stellschraubeSetzen(e0, zeile, -9).stellschrauben, { a: 0 });
  assert.deepEqual(T.stellschraubeSetzen({ stellschrauben: { a: 1.5 } }, zeile, 1).stellschrauben, {}, 'Vorgabe entfernt den Eintrag');
  assert.deepEqual(T.stellschraubeSetzen(e0, zeile, 'abc').stellschrauben, {});
  assert.deepEqual(T.stellschraubeSetzen(e0, zeile, '').stellschrauben, {});
  assert.deepEqual(T.stellschraubeSetzen(e0, null, 1).stellschrauben, {});
  assert.equal(T.wertText(0.045, 'm/s'), '0,045 m/s');
  assert.equal(T.wertText(300), '300');
});

// ---------------------------------------------------------------------------------------------- Handskelett

test('Handskelett: 21 Punkte ergeben 21 Linien im Feld; ohne Hand oder mit weniger Punkten keine; Punkte ausserhalb werden an den Rand gesetzt', () => {
  const punkte = Array.from({ length: 21 }, (_, i) => [0.2 + i * 0.03, 0.9 - i * 0.04]);
  const l = T.skelettLinien(punkte, 160, 120);
  assert.equal(l.length, 21);
  assert.equal(T.HAND_VERBINDUNGEN.length, 21);
  for (const [x1, y1, x2, y2] of l) for (const v of [x1, x2]) assert.ok(v >= 0 && v <= 160 && y1 >= 0 && y1 <= 120 && y2 >= 0 && y2 <= 120);
  const [a, b] = T.HAND_VERBINDUNGEN[0];
  assert.deepEqual(l[0], [punkte[a][0] * 160, punkte[a][1] * 120, punkte[b][0] * 160, punkte[b][1] * 120]);
  assert.equal(T.skelettLinien(null, 160, 120), null);
  assert.equal(T.skelettLinien(punkte.slice(0, 20), 160, 120), null);
  assert.equal(T.skelettLinien([...punkte.slice(0, 20), [NaN, 0]], 160, 120), null);
  const raus = T.skelettLinien(punkte.map(() => [3, -2]), 160, 120);
  assert.deepEqual(raus[0], [160, 0, 160, 0]);
  const alle = new Set(T.HAND_VERBINDUNGEN.flat());
  assert.equal(alle.size, 21, 'jeder Punkt hängt an einer Linie');
});

// ---------------------------------------------------------------------------------------------- Punktfolge

test('Punktfolge: nur Punkte werden Datei – ein mitgegebenes Bild fällt weg; leere Aufnahme ergibt keine Datei', async () => {
  const m = attrappe();
  const r = await m.aufnahmeBeenden();
  assert.equal(r.bilder[0].bild, 'DATA', 'die Attrappe gibt absichtlich ein Bildfeld mit');
  const d = T.aufnahmeDatei(r, new Date(2026, 9, 26, 14, 5, 9));
  assert.equal(d.name, 'gestenfolge-20261026-140509.json');
  assert.equal(d.bilder, 1);
  const inhalt = JSON.parse(d.text);
  assert.deepEqual(Object.keys(inhalt), ['version', 'hand', 'bilder']);
  assert.deepEqual(Object.keys(inhalt.bilder[0]), ['t', 'punkte', 'welt', 'haendigkeit']);
  assert.ok(!d.text.includes('DATA'), 'kein Bild in der Datei');
  assert.equal(inhalt.bilder[0].punkte.length, 21);
  assert.equal(T.aufnahmeDatei({ bilder: [] }), null);
  assert.equal(T.aufnahmeDatei(null), null);
  assert.equal(T.aufnahmeDatei({ bilder: [null, 5] }), null, 'nur Einträge, die Objekte sind, zählen');
});

// ---------------------------------------------------------------------------------------------- Entscheidung der Schritt-Gesten

test('Schritt-Gesten ohne Dialog: G2 löst die belegte Aktion aus, rechts nächster, links voriger Reiter; G6 Zoom alles', () => {
  const e = T.einstellungNormal(null);
  const kontext = { dialog: null, ansicht: { seiten: false, rollbar: false } };
  assert.deepEqual(L.schrittPlan(schritt('G2', 'rechts'), kontext, e), { art: 'aktion', id: 'ansicht.reiter-vor', geste: 'G2' });
  assert.deepEqual(L.schrittPlan(schritt('G2', 'links'), kontext, e), { art: 'aktion', id: 'ansicht.reiter-zurueck', geste: 'G2' });
  assert.deepEqual(L.schrittPlan(schritt('G6', null), kontext, e), { art: 'aktion', id: 'ansicht.zoom-alles', geste: 'G6' });
  assert.equal(L.schrittPlan(schritt('G2', 'hoch'), kontext, e).art, 'nichts');
  assert.equal(L.schrittPlan(schritt('G2', null), kontext, e).art, 'nichts');
});

test('Schritt-Gesten: eine geänderte Belegung gilt; eine leere oder nicht belegbare gibt keine Wirkung', () => {
  const kontext = { dialog: null, ansicht: null };
  const e = T.belegungSetzen(T.belegungSetzen(null, 'G6', '', 'ansicht.zoom-alle'), 'G2', 'rechts', '');
  assert.equal(L.schrittPlan(schritt('G6'), kontext, e).id, 'ansicht.zoom-alle');
  assert.deepEqual(L.schrittPlan(schritt('G2', 'rechts'), kontext, e), { art: 'nichts', grund: 'unbelegt' });
  const boes = { belegung: { G6: 'aendern.loeschen' } };
  assert.deepEqual(L.schrittPlan(schritt('G6'), kontext, boes), { art: 'nichts', grund: 'unbelegt' });
});

test('Schritt-Gesten im Dialog: G2 blättert nur Seiten, G3 rollt nur, G6 nichts; hinter einem Dialog wechselt nie ein Reiter', () => {
  const e = T.einstellungNormal(null);
  const mitSeiten = { dialog: { seiten: true, rollbar: true }, ansicht: null };
  const ohne = { dialog: { seiten: false, rollbar: false }, ansicht: null };
  assert.deepEqual(L.schrittPlan(schritt('G2', 'rechts'), mitSeiten, e), { art: 'seite', schritt: 1 });
  assert.deepEqual(L.schrittPlan(schritt('G2', 'links'), mitSeiten, e), { art: 'seite', schritt: -1 });
  assert.deepEqual(L.schrittPlan(schritt('G2', 'rechts'), ohne, e), { art: 'nichts', grund: 'dialog' });
  assert.deepEqual(L.schrittPlan(schritt('G3', 'runter'), mitSeiten, e), { art: 'rollen', schritt: 1 });
  assert.deepEqual(L.schrittPlan(schritt('G3', 'hoch'), mitSeiten, e), { art: 'rollen', schritt: -1 });
  assert.deepEqual(L.schrittPlan(schritt('G3', 'runter'), ohne, e), { art: 'nichts', grund: 'dialog' });
  assert.deepEqual(L.schrittPlan(schritt('G6'), mitSeiten, e), { art: 'nichts', grund: 'dialog' });
});

test('Schritt-Gesten im Ansichtsfenster: G3 rollt nur in der Liste, in Plan, Schema und 3D nichts (kein Geschosswechsel); G2 blättert Seiten einer Liste', () => {
  const e = T.einstellungNormal(null);
  assert.deepEqual(L.schrittPlan(schritt('G3', 'runter'), { dialog: null, ansicht: { seiten: false, rollbar: true } }, e), { art: 'rollen', schritt: 1 });
  assert.deepEqual(L.schrittPlan(schritt('G3', 'runter'), { dialog: null, ansicht: { seiten: false, rollbar: false } }, e), { art: 'nichts', grund: 'ansicht' });
  assert.deepEqual(L.schrittPlan(schritt('G3', 'hoch'), { dialog: null, ansicht: null }, e), { art: 'nichts', grund: 'ansicht' });
  assert.deepEqual(L.schrittPlan(schritt('G2', 'rechts'), { dialog: null, ansicht: { seiten: true, rollbar: true } }, e), { art: 'seite', schritt: 1 });
  assert.deepEqual(L.ROLLBARE_ARTEN, ['liste']);
  for (const art of ['grundriss', 'schnitt', 'ansicht', '3d', 'schema', 'planblatt', 'familie', null]) assert.equal(L.ansichtRollbar(art), false, String(art));
  assert.equal(L.ansichtRollbar('liste'), true);
});

test('Testfeld offen oder kein Schritt-Ereignis: nichts wird ausgelöst', () => {
  const e = T.einstellungNormal(null);
  const k = { dialog: null, ansicht: { seiten: true, rollbar: true } };
  for (const g of [schritt('G2', 'rechts'), schritt('G3', 'runter'), schritt('G6')]) assert.deepEqual(L.schrittPlan(g, k, e, { test: true }), { art: 'nichts', grund: 'testfeld' });
  assert.equal(L.schrittPlan({ art: 'zustand', zustand: 'hand' }, k, e).art, 'nichts');
  assert.equal(L.schrittPlan(null, k, e).art, 'nichts');
  assert.equal(L.schrittPlan(schritt('G9', 'links'), k, e).art, 'nichts');
});

test('Rollen: eine Bildschirmseite, begrenzt auf Anfang und Ende; Meldungstexte', () => {
  assert.deepEqual(L.rollenZiel({ scrollTop: 0, clientHeight: 500, scrollHeight: 2000 }, 1), { top: 450, bewegt: true });
  assert.deepEqual(L.rollenZiel({ scrollTop: 450, clientHeight: 500, scrollHeight: 2000 }, -1), { top: 0, bewegt: true });
  assert.deepEqual(L.rollenZiel({ scrollTop: 1500, clientHeight: 500, scrollHeight: 2000 }, 1), { top: 1500, bewegt: false });
  assert.deepEqual(L.rollenZiel({ scrollTop: 0, clientHeight: 500, scrollHeight: 2000 }, -1), { top: 0, bewegt: false });
  assert.deepEqual(L.rollenZiel({ scrollTop: 1200, clientHeight: 500, scrollHeight: 2000 }, 1), { top: 1500, bewegt: true }, 'letzte Seite kürzer');
  assert.equal(L.meldungText({ art: 'rollen', schritt: 1 }, { bewegt: true }), null, 'Rollen ist sichtbar: keine Meldung');
  assert.equal(L.meldungText({ art: 'rollen', schritt: 1 }, { bewegt: false }), 'Ende erreicht');
  assert.equal(L.meldungText({ art: 'rollen', schritt: -1 }, { bewegt: false }), 'Anfang erreicht');
  assert.equal(L.meldungText({ art: 'seite', schritt: 1 }, {}), 'Seite vor');
  assert.equal(L.meldungText({ art: 'seite', schritt: -1 }, { text: 'Seite 1 von 3' }), 'Seite 1 von 3');
  assert.equal(L.meldungText({ art: 'aktion', id: 'x' }, { reiter: '3D Lüftung' }), 'Reiter: 3D Lüftung');
  assert.equal(L.meldungText({ art: 'aktion', id: 'x' }, { aktionTitel: 'Zoom​ alles' }), 'Zoom alles');
  assert.equal(L.meldungText({ art: 'nichts' }, {}), null);
});

/** Kleine Attrappe eines DOM-Elements zum Rollen und Blättern. */
function el(opt = {}) {
  const e = { scrollTop: 0, scrollHeight: 0, clientHeight: 0, overflowY: 'visible', kinder: [], klicks: 0, attribute: {}, ...opt };
  e.querySelectorAll = () => e.kinder.flatMap((k) => [k, ...k.querySelectorAll('*')]);
  e.querySelector = (s) => e.suche?.[s] ?? null;
  e.getAttribute = (n) => e.attribute[n];
  e.click = () => { e.klicks++; };
  e.scrollTo = ({ top }) => { e.scrollTop = top; };
  return e;
}
const stil = (x) => ({ overflowY: x.overflowY });

test('Rollfläche finden: das grösste rollbare Element, nur mit overflow auto/scroll und wirklich höherem Inhalt', () => {
  const klein = el({ scrollHeight: 400, clientHeight: 100, overflowY: 'auto' });
  const gross = el({ scrollHeight: 3000, clientHeight: 400, overflowY: 'scroll' });
  const versteckt = el({ scrollHeight: 3000, clientHeight: 400, overflowY: 'hidden' });
  const passt = el({ scrollHeight: 400, clientHeight: 400, overflowY: 'auto' });
  const wurzel = el({ scrollHeight: 400, clientHeight: 400, overflowY: 'visible', kinder: [klein, versteckt, passt, gross] });
  assert.equal(L.rollbarFinden(wurzel, stil), gross);
  assert.equal(L.rollbarFinden(el({ kinder: [versteckt, passt] }), stil), null);
  assert.equal(L.rollbarFinden(null, stil), null);
  assert.equal(L.rollbarFinden(el({ scrollHeight: 900, clientHeight: 300, overflowY: 'auto' }), stil)?.clientHeight, 300, 'die Wurzel selbst zählt');
});

test('Seite rollen: smooth scrollTo, false am Ende', () => {
  const f = el({ scrollHeight: 2000, clientHeight: 500, overflowY: 'auto' });
  assert.equal(L.seiteRollen(f, 1), true);
  assert.equal(f.scrollTop, 450);
  f.scrollTop = 1500;
  assert.equal(L.seiteRollen(f, 1), false);
  assert.equal(L.seiteRollen(null, 1), false);
});

test('Seiten finden und blättern: Knöpfe mit data-seite und Reiterliste; am Anfang und Ende keine Seite', () => {
  const zurueck = el(), vor = el();
  const gruppe = el({ suche: { '[data-seite="zurueck"]': zurueck, '[data-seite="vor"]': vor, '[data-seite-text]': { textContent: ' Seite 2 von 3 ' } } });
  const dialog = el({ suche: { '[data-seiten]': gruppe } });
  const f = L.seitenFinden(dialog);
  assert.equal(f.art, 'knoepfe');
  assert.deepEqual(L.seiteBlaettern(f, 1), { text: 'Seite 2 von 3' });
  assert.equal(vor.klicks, 1);
  assert.deepEqual(L.seiteBlaettern(f, -1), { text: 'Seite 2 von 3' });
  assert.equal(zurueck.klicks, 1);
  const letzte = el({ disabled: true });
  assert.equal(L.seiteBlaettern({ art: 'knoepfe', zurueck, vor: letzte, text: '' }, 1), null);
  assert.equal(L.seiteBlaettern({ art: 'knoepfe', zurueck, vor, text: '' }, 1).text, 'Seite vor');

  const tabs = [0, 1, 2].map((i) => el({ attribute: { 'aria-selected': i === 1 ? 'true' : 'false' } }));
  const liste = el({ kinder: tabs });
  liste.querySelectorAll = () => tabs;
  const tabDialog = el({ suche: { '[role="tablist"]': liste } });
  const t = L.seitenFinden(tabDialog);
  assert.deepEqual([t.art, t.aktuell], ['tabs', 1]);
  assert.deepEqual(L.seiteBlaettern(t, 1), { text: 'Seite 3 von 3' });
  assert.equal(tabs[2].klicks, 1);
  assert.deepEqual(L.seiteBlaettern(t, -1), { text: 'Seite 1 von 3' });
  assert.equal(L.seiteBlaettern({ ...t, aktuell: 2 }, 1), null, 'am Ende');
  assert.equal(L.seiteBlaettern({ ...t, aktuell: 0 }, -1), null, 'am Anfang');
  assert.equal(L.seitenFinden(el()), null);
  assert.equal(L.seitenFinden(null), null);
});

// ---------------------------------------------------------------------------------------------- Auslösen mit Attrappe

test('Auslösen: G2 wechselt den Reiter über das Register und meldet «Reiter: 3D Lüftung»', async () => {
  const { s } = bauen();
  const u = umgebung();
  const r = await schrittAusfuehren(schritt('G2', 'rechts'), s, u);
  assert.deepEqual(u.aufrufe, [['aktion', 'ansicht.reiter-vor']]);
  assert.deepEqual(u.meldungen, ['Reiter: 3D Lüftung']);
  assert.equal(r.plan.art, 'aktion');
  await schrittAusfuehren(schritt('G2', 'links'), s, u);
  assert.deepEqual(u.aufrufe.at(-1), ['aktion', 'ansicht.reiter-zurueck']);
});

test('Auslösen: G6 löst Zoom alles aus und meldet den Titel der Aktion; eine nicht verfügbare Aktion meldet nichts doppelt', async () => {
  const { s } = bauen();
  const u = umgebung();
  await schrittAusfuehren(schritt('G6'), s, u);
  assert.deepEqual(u.aufrufe, [['aktion', 'ansicht.zoom-alles']]);
  assert.deepEqual(u.meldungen, ['Zoom alles']);
  const gesperrt = umgebung({ aktionOk: false });
  const r = await schrittAusfuehren(schritt('G6'), s, gesperrt);
  assert.deepEqual(gesperrt.meldungen, [], 'der Hinweis kommt vom Register');
  assert.equal(r.text, null);
});

test('Auslösen: im Dialog blättert G2 die Seiten und G3 rollt; ohne Seiten passiert hinter dem Dialog nichts', async () => {
  const { s } = bauen();
  const mit = umgebung({ dialog: { seiten: true, rollbar: true } });
  await schrittAusfuehren(schritt('G2', 'rechts'), s, mit);
  await schrittAusfuehren(schritt('G3', 'runter'), s, mit);
  assert.deepEqual(mit.aufrufe, [['seite', 1], ['rollen', 1]]);
  assert.deepEqual(mit.meldungen, ['Seite 2 von 3']);
  const ohne = umgebung({ dialog: { seiten: false, rollbar: false } });
  for (const g of [schritt('G2', 'rechts'), schritt('G3', 'hoch'), schritt('G6')]) await schrittAusfuehren(g, s, ohne);
  assert.deepEqual(ohne.aufrufe, []);
  assert.deepEqual(ohne.meldungen, []);
  const ende = umgebung({ dialog: { seiten: true, rollbar: true }, rollt: false });
  await schrittAusfuehren(schritt('G3', 'runter'), s, ende);
  assert.deepEqual(ende.meldungen, ['Ende erreicht']);
  const keineSeite = umgebung({ dialog: { seiten: true, rollbar: true }, seite: null });
  await schrittAusfuehren(schritt('G2', 'links'), s, keineSeite);
  assert.deepEqual(keineSeite.meldungen, [], 'am Ende der Seiten bleibt es still');
});

test('Auslösen: in Plan, Schema und 3D rollt G3 nichts und es gibt keinen Geschosswechsel', async () => {
  const { s } = bauen();
  const u = umgebung({ ansicht: { seiten: false, rollbar: false } });
  await schrittAusfuehren(schritt('G3', 'runter'), s, u);
  await schrittAusfuehren(schritt('G3', 'hoch'), s, u);
  assert.deepEqual(u.aufrufe, []);
  const liste = umgebung({ ansicht: { seiten: false, rollbar: true } });
  await schrittAusfuehren(schritt('G3', 'runter'), s, liste);
  assert.deepEqual(liste.aufrufe, [['rollen', 1]]);
});

test('Auslösen: bei offenem Testfeld löst nichts aus; die Steuerung leitet nur Schritt-Ereignisse an das Auslösen', async () => {
  const { s, modul } = bauen();
  await s.schalten(true);
  const u = umgebung();
  const abmelden = ausloesenEinrichten(s, u);
  modul.ereignis(schritt('G2', 'rechts'));
  await new Promise((r) => setTimeout(r, 5));
  assert.deepEqual(u.aufrufe, [['aktion', 'ansicht.reiter-vor']]);
  s.testmodus(true);
  modul.ereignis(schritt('G2', 'rechts'));
  modul.ereignis(schritt('G6'));
  modul.ereignis({ art: 'zustand', zustand: 'hand' });
  modul.ereignis({ art: 'punkte', punkte: null });
  await new Promise((r) => setTimeout(r, 5));
  assert.equal(u.aufrufe.length, 1, 'im Testfeld nichts ausgelöst');
  s.testmodus(false);
  modul.ereignis(schritt('G6'));
  await new Promise((r) => setTimeout(r, 5));
  assert.equal(u.aufrufe.length, 2);
  abmelden();
  modul.ereignis(schritt('G6'));
  await new Promise((r) => setTimeout(r, 5));
  assert.equal(u.aufrufe.length, 2, 'abgemeldet');
});

test('Auslösen wirft nie: ein Fehler in der Umgebung wird gemeldet, nicht geworfen', async () => {
  const { s } = bauen();
  const u = umgebung();
  u.aktion = async () => { throw new Error('kaputt'); };
  const alt = console.error;
  console.error = () => {};
  try { const r = await schrittAusfuehren(schritt('G6'), s, u); assert.equal(r.text, null); } finally { console.error = alt; }
});

// ---------------------------------------------------------------------------------------------- Steuerung ohne Last

test('Ohne Grundschalter keine Last: gesten.js wird nicht geladen, die Kamera nicht gestartet', async () => {
  const { s, modul, log } = bauen({ gespeichert: null });
  assert.equal(await s.einrichten(), false);
  assert.equal(log.laden, 0, 'kein import');
  assert.deepEqual(modul.aufrufe, []);
  assert.deepEqual(s.zustand(), { an: false, laeuft: false, startet: false, zustand: 'aus', geste: null, fehler: null, test: false, aufnahme: false });
  assert.equal(s.an(), false);
  assert.equal(T.statusAnzeige(s.zustand()).stufe, 'aus');
});

test('Eingeschaltet gespeichert: beim Start werden die Einstellungen an gesten.js gegeben', async () => {
  const { s, modul, log } = bauen({ gespeichert: { an: true, hand: 'rechts', kameraId: 'cam2', stellschrauben: { x: 2 } } });
  assert.equal(await s.einrichten(), true);
  assert.equal(log.laden, 1);
  assert.deepEqual(modul.aufrufe[0], ['starten', { hand: 'rechts', kameraId: 'cam2', stellschrauben: { x: 2 } }]);
  assert.equal(s.zustand().zustand, 'bereit');
  assert.equal(T.statusAnzeige(s.zustand()).stufe, 'bereit');
  assert.equal(s.an(), true);
});

test('Nicht erlaubt (eigenes Fenster einer Ansicht): nichts wird gelesen oder gestartet', async () => {
  const { s, log } = bauen({ gespeichert: { an: true }, erlaubt: false });
  assert.equal(await s.einrichten(), false);
  assert.equal(await s.schalten(true), false);
  assert.equal(log.laden, 0);
});

test('Fehlt gesten.js, meldet die Steuerung es sauber: Zustand aus mit Hinweis, Grundschalter zurück, keine Ausnahme', async () => {
  const alt = console.error;
  console.error = () => {};
  try {
    const { s, log } = bauen({ gespeichert: { an: true }, ladenWirft: new TypeError('Failed to fetch dynamically imported module') });
    assert.equal(await s.einrichten(), false, 'der Start von KICAD bricht nicht ab');
    const z = s.zustand();
    assert.deepEqual([z.an, z.laeuft, z.zustand], [false, false, 'aus']);
    assert.equal(z.fehler, T.NICHT_EINGEBAUT);
    assert.equal(T.statusAnzeige(z).stufe, 'aus');
    assert.match(T.statusAnzeige(z).titel, /nicht vorhanden/);
    assert.equal(log.meldungen.length, 1);
    assert.equal(log.meldungen[0][1], 'warnung');
    assert.equal(log.geschrieben.at(-1) ?? null, null, 'beim Einrichten wird nichts geschrieben');
    assert.equal(await s.schalten(true), false);
    assert.equal(s.zustand().an, false);
    assert.equal(log.geschrieben.at(-1)?.an ?? false, false, 'nie «an» gespeichert, wenn es nicht läuft');
    const { s: s2 } = bauen({ modul: {} });   // Modul ohne starten()
    assert.equal(await s2.schalten(true), false);
    assert.equal(s2.zustand().fehler, T.NICHT_EINGEBAUT);
  } finally { console.error = alt; }
});

test('Wirft starten() (Kamera verweigert oder fehlt), steht die Meldung in der Statusleiste und der Schalter geht zurück', async () => {
  const wirft = Object.assign(new Error('x'), { name: 'NotAllowedError' });
  const { s, modul, log } = bauen({ modul: attrappe({ startenWirft: wirft }) });
  assert.equal(await s.schalten(true), false);
  const z = s.zustand();
  assert.match(z.fehler, /nicht erlaubt/);
  assert.equal(z.an, false);
  assert.equal(modul.zahlHoerer(), 0, 'abgemeldet');
  assert.match(T.statusAnzeige(z).titel, /Zuletzt: Der Browser hat den Zugriff/);
  assert.match(log.meldungen.at(-1)[0], /Kamera/);
  const { s: s2 } = bauen({ modul: attrappe({ startenWirft: new Error('Es ist keine Kamera angeschlossen.') }) });
  await s2.schalten(true);
  assert.equal(s2.zustand().fehler, 'Es ist keine Kamera angeschlossen.', 'deutsche Meldung von gesten.js bleibt');
});

test('Ein- und Ausschalten: erst beim Einschalten wird geladen und gespeichert; Ausschalten gibt die Kamera frei', async () => {
  const { s, modul, log } = bauen();
  await s.einrichten();
  assert.equal(log.laden, 0);
  const ereignisse = [];
  s.abonnieren((e) => ereignisse.push(e.art));
  assert.equal(await s.schalten(), true, 'ohne Angabe umschalten');
  assert.equal(log.laden, 1);
  assert.equal(s.laeuft(), true);
  assert.deepEqual(log.geschrieben.at(-1).an, true);
  assert.ok(ereignisse.includes('schalter'));
  modul.ereignis({ art: 'zustand', zustand: 'hand', geste: null });
  assert.equal(T.statusAnzeige(s.zustand()).stufe, 'hand');
  modul.ereignis({ art: 'zustand', zustand: 'aktiv', geste: 'drehen' });
  assert.equal(T.statusAnzeige(s.zustand()).text, 'Geste: drehen');
  modul.ereignis({ art: 'zustand', zustand: 'bereit', geste: null });
  assert.equal(T.statusAnzeige(s.zustand()).stufe, 'bereit');
  assert.equal(await s.schalten(), false);
  assert.deepEqual(modul.aufrufe.at(-1), ['stoppen']);
  assert.equal(s.laeuft(), false);
  assert.equal(log.geschrieben.at(-1).an, false);
  assert.equal(T.statusAnzeige(s.zustand()).stufe, 'aus');
  assert.equal(modul.zahlHoerer(), 0);
  await s.schalten(true);
  assert.equal(log.laden, 1, 'das Modul wird nur einmal geladen');
});

test('Punkte und Schritte der Gestenlogik erreichen die Oberfläche; meldet gesten.js «aus» mit Fehler (Kamera verloren), geht der Schalter zurück', async () => {
  const { s, modul, log } = bauen();
  await s.schalten(true);
  const gesehen = [];
  s.abonnieren((e) => gesehen.push(e));
  const pkt = Array.from({ length: 21 }, () => [0.5, 0.5]);
  modul.ereignis({ art: 'punkte', punkte: pkt });
  modul.ereignis(schritt('G2', 'links'));
  assert.deepEqual(gesehen.map((e) => e.art), ['punkte', 'schritt']);
  const nach = gesehen.length;
  modul.ereignis({ art: 'zustand', zustand: 'aus', fehler: new Error('Die Kamera wurde getrennt.') });
  assert.equal(s.zustand().laeuft, false);
  assert.equal(s.zustand().fehler, 'Die Kamera wurde getrennt.');
  assert.equal(log.geschrieben.at(-1).an, false);
  assert.equal(log.meldungen.at(-1)[1], 'warnung');
  assert.ok(gesehen.slice(nach).some((e) => e.art === 'punkte' && e.punkte === null), 'das Handskelett wird geleert');
});

test('Einstellungen ändern: speichern und nur Geändertes an gesten.js geben; im Betrieb wird die Hand gewechselt', async () => {
  const { s, modul, log } = bauen({ gespeichert: { an: true } });
  await s.einrichten();
  await s.einstellungSetzen({ hand: 'rechts' });
  assert.deepEqual(modul.aufrufe.at(-1), ['einstellen', { hand: 'rechts' }]);
  await s.einstellungSetzen({ skelett: false });
  assert.equal(modul.aufrufe.filter((a) => a[0] === 'einstellen').length, 1, 'das Skelett betrifft gesten.js nicht');
  await s.einstellungSetzen({ stellschrauben: { anstoss_weg: 0.05 } });
  assert.deepEqual(modul.aufrufe.at(-1), ['einstellen', { stellschrauben: { anstoss_weg: 0.05 } }]);
  await s.einstellungSetzen({ kameraId: 'cam2' });
  assert.deepEqual(modul.aufrufe.at(-1), ['einstellen', { kameraId: 'cam2' }]);
  const e = log.geschrieben.at(-1);
  assert.deepEqual([e.an, e.hand, e.kameraId, e.skelett, e.stellschrauben], [true, 'rechts', 'cam2', false, { anstoss_weg: 0.05 }]);
  await s.einstellungSetzen({ an: false });
  assert.equal(s.an(), true, 'der Grundschalter ändert sich nur über schalten()');
  const g = s.einstellung();
  g.stellschrauben.z = 1;
  assert.equal(s.einstellung().stellschrauben.z, undefined, 'einstellung() gibt eine Kopie');
});

test('Einstellungen ändern bei ausgeschalteten Gesten: nur speichern, nichts laden', async () => {
  const { s, log } = bauen();
  await s.einrichten();
  await s.einstellungSetzen({ hand: 'rechts' });
  assert.equal(log.laden, 0);
  assert.equal(log.geschrieben.at(-1).hand, 'rechts');
  assert.equal(log.geschrieben.at(-1).an, false);
});

test('Speichern schlägt fehl (OneDrive weg): Meldung statt Absturz', async () => {
  const log = { meldungen: [] };
  const s = steuerungErzeugen({ laden: async () => attrappe(), schreiben: async () => { throw new Error('Platte voll'); }, melden: (t, a) => log.meldungen.push([t, a]) });
  await s.einstellungSetzen({ hand: 'rechts' });
  assert.match(log.meldungen[0][0], /nicht gespeichert werden: Platte voll/);
  assert.equal(s.einstellung().hand, 'rechts', 'die Wahl gilt für diese Sitzung');
});

test('Kameras und Stellschrauben für den Dialog: ohne Modul leer, mit Modul die Liste; Stellschrauben aus der Gestenlogik', async () => {
  const { s } = bauen({ stellschrauben: { a: { vorgabe: 1 } } });
  assert.deepEqual((await s.kameras()).map((k) => k.id), ['cam1', 'cam2']);
  assert.deepEqual(await s.stellschraubenVorgabe(), { a: { vorgabe: 1 } });
  const alt = console.error; console.error = () => {};
  try {
    const { s: ohne } = bauen({ ladenWirft: new Error('weg') });
    assert.deepEqual(await ohne.kameras(), []);
    assert.equal(await ohne.stellschraubenVorgabe(), null);
  } finally { console.error = alt; }
});

test('Testmodus wird an gesten.js weitergegeben, auch beim späteren Start; die Steuerung merkt ihn für das Auslösen', async () => {
  const { s, modul } = bauen();
  s.testmodus(true);
  assert.equal(s.istTest(), true);
  assert.deepEqual(modul.aufrufe, [], 'ohne laufende Gesten kein Aufruf');
  await s.schalten(true);
  assert.deepEqual(modul.aufrufe.filter((a) => a[0] === 'testmodus'), [['testmodus', true]]);
  s.testmodus(false);
  assert.deepEqual(modul.aufrufe.at(-1), ['testmodus', false]);
  assert.equal(s.zustand().test, false);
});

test('Aufnahme: nur bei laufenden Gesten; das Ergebnis enthält nur Punkte; Stoppen beendet eine laufende Aufnahme', async () => {
  const { s, modul } = bauen();
  assert.throws(() => s.aufnahmeStarten(), /eingeschaltet/);
  await s.schalten(true);
  s.aufnahmeStarten();
  assert.equal(s.zustand().aufnahme, true);
  const r = await s.aufnahmeBeenden();
  assert.equal(s.zustand().aufnahme, false);
  assert.ok(!T.aufnahmeDatei(r).text.includes('DATA'));
  assert.equal(await s.aufnahmeBeenden(), null);
  s.aufnahmeStarten();
  await s.schalten(false);
  assert.equal(modul.aufnahme, false);
});

// ---------------------------------------------------------------------------------------------- Dialogtexte

test('Dialog: alle Abschnitte, Nr/Geste/Wirkung/Art, Einstellungen, Empfindlichkeit, Testfeld, Aufnahme', () => {
  const belegbar = [{ id: 'ansicht.reiter-vor', titel: 'Nächster​ Reiter' }, { id: 'ansicht.zoom-alles', titel: 'Zoom alles' }];
  const schrauben = T.stellschraubenZeilen({ anstoss_weg: { vorgabe: 0.045, min: 0.02, max: 0.2, schritt: 0.005, einheit: 'm', titel: 'Anstoss: Weg', erklaerung: 'So weit.' } }, { anstoss_weg: 0.06 });
  const html = D.dialogHtml({
    zustand: { an: true, laeuft: true, zustand: 'hand' }, einst: T.einstellungNormal({ kameraId: 'cam2' }), zeilen: T.gestenZeilen(null, () => 'Titel'),
    kameras: [{ id: 'cam1', name: 'Eingebaute Kamera' }, { id: 'cam2', name: 'Externe' }], belegbar, schrauben, protokoll: ['Hand erkannt'],
  });
  for (const teil of ['gd-kopf', 'gd-liste', 'gd-einst', 'gd-empfindlichkeit', 'gd-test', 'gd-aufnahme', 'gd-an', 'gd-hand', 'gd-kamera', 'gd-skelett', 'gd-vorgabe', 'gd-aufnahme-start', 'gd-aufnahme-ende']) assert.ok(html.includes(teil), teil);
  for (const nr of ['G2', 'G3', 'G4', 'G5', 'G6', 'G7']) assert.ok(html.includes(`<kbd>${nr}</kbd>`), nr);
  assert.match(html, /<th>Nr<\/th><th>Geste<\/th><th>Wirkung<\/th><th>Art<\/th>/);
  assert.match(html, /<input type="checkbox" class="gd-an"[^>]*checked>/);
  assert.match(html, /class="gd-status hand"[^>]*>Hand erkannt/);
  assert.match(html, /<option value="cam2" selected>Externe<\/option>/);
  assert.equal([...html.matchAll(/class="gd-beleg"/g)].length, 3, 'G2 nach rechts und links, G6');
  assert.match(html, /data-nr="G2" data-richtung="rechts"[^>]*>.*?<option value="ansicht.reiter-vor" selected>Nächster Reiter<\/option>/s);
  assert.match(html, /<option value="">keine Wirkung<\/option>/);
  assert.match(html, /class="gd-schraube" data-name="anstoss_weg" value="0.06" min="0.02" max="0.2" step="0.005"/);
  assert.match(html, /Bereich 0,02 m bis 0,2 m<br>Vorgabe 0,045 m/);
  assert.match(html, /gd-schraube-zeile geaendert/);
  assert.match(html, /<li>Hand erkannt<\/li>/);
  assert.ok(html.includes(T.DATENSCHUTZ.replace(/&/g, '&amp;')), 'Datenschutz-Satz steht im Dialog');
  assert.ok(html.includes('data-kein-enter="1"'), 'Enter in einem Zahlenfeld schliesst den Dialog nicht');
});

test('Dialog: Text aus Daten wird maskiert (Kameraname, Titel, Erklärung), eine unbekannte gewählte Kamera bleibt wählbar', () => {
  const html = D.dialogHtml({
    einst: T.einstellungNormal({ kameraId: 'weg"><script>' }), zeilen: [{ nr: 'G2', name: '<img src=x onerror=1>', wirkung: '"&"', art: 'Schritt' }],
    kameras: [{ id: 'k"1', name: 'Kamera <b>fett</b>' }], belegbar: [{ id: 'a.b', titel: '<i>x</i>' }],
    schrauben: T.stellschraubenZeilen({ s: { vorgabe: 1, titel: '<u>T</u>', erklaerung: '<script>alert(1)</script>' } }), protokoll: ['<script>x</script>'],
  });
  assert.ok(!/<script|<img|<b>fett|<i>x|<u>T/.test(html), 'kein fremdes Markup');
  assert.match(html, /Gewählte Kamera \(nicht gefunden\)/);
  assert.match(html, /&lt;b&gt;fett&lt;\/b&gt;/);
});

test('Dialog: ohne Gestenlogik steht ein Hinweis statt der Empfindlichkeit; das Testfeld sagt, wenn die Gesten aus sind', () => {
  assert.match(D.empfindlichkeitHtml(null), /sobald die Gestenerkennung in dieser Fassung vorhanden ist/);
  assert.match(D.empfindlichkeitHtml([]), /sobald/);
  assert.match(D.testfeldHtml([], false), /Die Gesten sind aus/);
  assert.match(D.testfeldHtml([], true), /Noch keine Geste erkannt/);
  assert.match(D.aufnahmeHtml({ laeuft: false }), /gd-aufnahme-start" disabled/);
  assert.match(D.aufnahmeHtml({ laeuft: true }), /gd-aufnahme-start"><|gd-aufnahme-start">/);
  assert.match(D.aufnahmeHtml({ laeuft: true, aufnahme: true }), /gd-aufnahme-ende">/);
  assert.match(D.kopfHtml({ an: false, fehler: 'Kamera fehlt.' }), /Gesten aus/);
  assert.match(D.kopfHtml({ an: false, fehler: 'Kamera fehlt.' }), /<p class="leise gd-klein gd-fehler">Kamera fehlt\./);
  assert.match(D.kopfHtml({ an: true, laeuft: true, zustand: 'bereit' }), /gd-fehler" hidden/);
});

test('Testfeld: Zeilen zu erkannten Gesten sagen, was ausgelöst würde, ohne es zu tun', () => {
  const e = T.einstellungNormal(null);
  const titel = (id) => ({ 'ansicht.reiter-vor': 'Nächster Reiter', 'ansicht.zoom-alles': 'Zoom alles' }[id]);
  assert.equal(T.ereignisZeile({ art: 'zustand', zustand: 'hand' }, e, titel), 'Hand erkannt');
  assert.equal(T.ereignisZeile({ art: 'zustand', zustand: 'aktiv', geste: 'rollen' }, e, titel), 'Geste: rollen');
  assert.match(T.ereignisZeile({ art: 'schritt', geste: 'G2', richtung: 'rechts' }, e, titel), /G2 Wisch nach rechts: würde «Nächster Reiter».* auslösen/);
  assert.match(T.ereignisZeile({ art: 'schritt', geste: 'G3', richtung: 'runter' }, e, titel), /G3 Wisch runter: würde eine Bildschirmseite rollen/);
  assert.match(T.ereignisZeile({ art: 'schritt', geste: 'G6' }, e, titel), /G6 Pistolengeste: würde «Zoom alles» auslösen/);
  assert.match(T.ereignisZeile({ art: 'schritt', geste: 'G6' }, { belegung: { G6: '' } }, titel), /würde nichts auslösen/);
  assert.equal(T.ereignisZeile({ art: 'punkte', punkte: null }, e, titel), null);
  assert.equal(T.ereignisZeile(null, e, titel), null);
});

// ---------------------------------------------------------------------------------------------- Kürzel, Einbindung, Datenschutz

/** Alle Kürzel der Oberfläche aus dem Quelltext (wie der Kürzeltest in ki-palette.test.mjs, dazu die Tabellen [id, Titel, Symbol, Kürzel] in aktionen.js und aktionen-leitung.js). */
function alleKuerzel() {
  const alle = [];
  const gehe = (ordner) => {
    for (const n of readdirSync(ordner)) {
      const p = path.join(ordner, n);
      if (statSync(p).isDirectory()) { if (n !== 'lib') gehe(p); continue; }
      if (!n.endsWith('.js')) continue;
      const q = readFileSync(p, 'utf8');
      for (const m of q.matchAll(/kuerzel(?:Alt)?:\s*(\[[^\]]*\]|'[^']*')/g)) for (const k of m[1].matchAll(/'([A-Za-z0-9+]+)'/g)) alle.push([path.relative(js, p), k[1]]);
      for (const m of q.matchAll(/\['[a-z.]+', '[^']+', '[a-zA-Z]+', '([A-Z0-9]{2})'/g)) alle.push([n, m[1]]);
    }
  };
  gehe(js);
  return alle;
}

test('Kürzel GE, RV und RZ sind frei: jedes kommt genau einmal vor, kein Kürzel der Oberfläche ist doppelt, alle zweistelligen sind frei von Vorsilben-Konflikten', () => {
  const alle = alleKuerzel();
  const zaehle = (k) => alle.filter(([, x]) => x.toUpperCase() === k).length;
  for (const k of ['GE', 'RV', 'RZ']) assert.equal(zaehle(k), 1, `${k} kommt ${zaehle(k)}× vor`);
  const zwei = alle.map(([, k]) => k).filter((k) => /^[A-Za-z0-9]{2}$/.test(k)).map((k) => k.toUpperCase());
  const doppelt = zwei.filter((k, i) => zwei.indexOf(k) !== i);
  assert.deepEqual([...new Set(doppelt)], [], 'doppelt vergebene Kürzel');
  assert.ok(zwei.includes('KS') && zwei.includes('KI'), 'der Bestand ist gefunden');
  const lang = alle.map(([, k]) => k).filter((k) => /^[A-Za-z0-9]{3,}$/.test(k) && !['Esc', 'Entf'].includes(k));   // Esc und Entf sind Sondertasten, keine Buchstabenfolgen
  assert.deepEqual(lang, [], 'ein längeres Kürzel würde ein zweistelliges verdecken');
  const quelle = lies('oberflaeche/aktionen-ansicht.js');
  assert.match(quelle, /id: 'ansicht\.gesten', titel: 'Gesten', symbol: 'gesten', kuerzel: 'GE', register: 'ansicht', gruppe: 'Fenster'/);
  assert.match(quelle, /id: 'ansicht\.reiter-vor', titel: 'Nächster Reiter', symbol: 'reiter', kuerzel: 'RV', register: 'ansicht', gruppe: 'Fenster'/);
  assert.match(quelle, /id: 'ansicht\.reiter-zurueck', titel: 'Voriger Reiter', symbol: 'reiter', kuerzel: 'RZ', register: 'ansicht', gruppe: 'Fenster'/);
  assert.match(quelle, /gedrueckt: \(\) => gesten\.an\(\)/, 'Grundschalter ist ein gedrückter Knopf');
  assert.match(quelle, /fenster\.reiterWechseln\(1\)/);
  assert.match(quelle, /fenster\.reiterWechseln\(-1\)/);
});

test('Symbol «gesten» gibt es im Strich- und im Revit-Satz; der Dialog steht in Verwalten › Hilfe neben «Tastaturkürzel»', () => {
  assert.match(lies('oberflaeche/symbole.js'), /^ {2}gesten: '/m);
  assert.match(lies('oberflaeche/symbole-revit.js'), /^s\('gesten',/m);
  const projekt = lies('oberflaeche/aktionen-projekt.js');
  assert.match(projekt, /id: 'verwalten\.gesten', titel: 'Gesten', symbol: 'gesten', kuerzel: null, register: 'verwalten', gruppe: 'Hilfe'/);
  assert.match(projekt, /import\('\.\.\/gesten\/gesten-dialog\.js'\)/, 'der Dialog wird erst beim Öffnen geladen');
  assert.ok(projekt.indexOf("id: 'verwalten.kuerzel'") < projekt.indexOf("id: 'verwalten.gesten'"));
});

test('Einbindung ohne Last: gesten.js, gesten-logik.js und gesten-worker.js nur per dynamischem import() – nirgends ein statischer import', () => {
  const treffer = [];
  const gehe = (ordner) => {
    for (const n of readdirSync(ordner)) {
      const p = path.join(ordner, n);
      if (statSync(p).isDirectory()) { if (n !== 'lib') gehe(p); continue; }
      if (!n.endsWith('.js') || n === 'gesten.js' || n === 'gesten-logik.js' || n === 'gesten-worker.js') continue;   // diese baut hlks-kern; ihr Inneres ist nicht Teil dieser Prüfung
      const q = readFileSync(p, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|\s)\/\/.*$/gm, '$1');
      for (const m of q.matchAll(/^\s*(?:import|export)\s[^;]*?from\s+['"][^'"]*gesten(?:-logik|-worker)?\.js['"]/gm)) treffer.push(`${path.relative(js, p)}: ${m[0].trim()}`);
      for (const m of q.matchAll(/^\s*import\s+['"][^'"]*gesten(?:-logik|-worker)?\.js['"]/gm)) treffer.push(`${path.relative(js, p)}: ${m[0].trim()}`);
    }
  };
  gehe(js);
  assert.deepEqual(treffer, []);
  const st = lies('gesten/gesten-steuerung.js');
  assert.match(st, /laden: \(\) => import\('\.\/gesten\.js'\)/);
  assert.match(st, /import\('\.\/gesten-logik\.js'\)/);
  assert.match(lies('gesten/gesten-status.js'), /import\('\.\/gesten-dialog\.js'\)/);
});

test('Statusleiste, Menüband, Meldungen und Einbindung: Symbol, Ereignis zum Nachführen, ersetzbare Meldung, Stilregeln', () => {
  const sl = lies('oberflaeche/statusleiste.js');
  assert.match(sl, /<button type="button" class="sl-gesten aus"><\/button>/);
  assert.match(sl, /gestenEinrichten\(w\.querySelector\('\.sl-gesten'\)\)/);
  assert.match(lies('oberflaeche/menueband.js'), /window\.addEventListener\('hlks-aktionszustand', spaeter\)/);
  assert.match(lies('gesten/gesten-oberflaeche.js'), /new CustomEvent\('hlks-aktionszustand'\)/);
  const dlg = lies('oberflaeche/dialoge.js');
  assert.match(dlg, /opt\.ersetze/);
  assert.match(dlg, /opt\.dauer > 0/);
  assert.match(lies('gesten/gesten-umgebung.js'), /meldung\(text, 'info', \{ ersetze: 'geste', dauer: 1600 \}\)/);
  assert.match(lies('../index.html'), /css\/gesten\.css/);
  const css = lies('../css/gesten.css');
  for (const k of ['.sl-gesten.aus', '.sl-gesten.bereit', '.sl-gesten.hand', '.sl-gesten.aktiv', '#gesten-skelett', '--meldungen-unten', '.gd-liste']) assert.ok(css.includes(k), k);
  assert.match(css, /#gesten-skelett \{[^}]*pointer-events: none/);
});

test('Datenschutz: kein Modul der Oberfläche liest, zeichnet, speichert oder sendet ein Kamerabild; die Datei der Punktfolge ist die einzige Datei und enthält nur Punkte', () => {
  const dateien = readdirSync(hier).filter((n) => n.endsWith('.js') && !['gesten.js', 'gesten-logik.js', 'gesten-worker.js'].includes(n));
  assert.ok(dateien.length >= 9, dateien.join(', '));
  for (const n of dateien) {
    const q = readFileSync(path.join(hier, n), 'utf8').replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|\s)\/\/.*$/gm, '$1');
    assert.doesNotMatch(q, /getUserMedia|drawImage|createImageBitmap|ImageBitmap|toDataURL|toBlob|<video|new FormData|sendBeacon|localStorage|sessionStorage|indexedDB|WebSocket/, n);
    assert.doesNotMatch(q, /\bfetch\(/, `${n}: kein eigener Abruf`);
  }
  const dialog = readFileSync(path.join(hier, 'gesten-dialog.js'), 'utf8');
  assert.equal([...dialog.matchAll(/new Blob\(/g)].length, 1);
  assert.match(dialog, /new Blob\(\[text\], \{ type: 'application\/json' \}\)/);
  assert.match(dialog, /T\.aufnahmeDatei\(r\)/, 'die Datei entsteht nur über aufnahmeDatei (nur Punkte)');
  const skelett = readFileSync(path.join(hier, 'gesten-skelett.js'), 'utf8');
  assert.match(skelett, /ohne Kamerabild/);
  // die Einstellung auf dem Server nimmt nichts ausser Einstellungen an (GestenEinstellung.cs, Web-Test)
  assert.match(readFileSync(path.join(hier, '..', '..', '..', 'GestenEinstellung.cs'), 'utf8'), /Unbekannte Felder werden abgelehnt/);
});

// ---------------------------------------------------------------------------------------------- Reiter wechseln

test('Reiter wechseln: nächster und vorheriger Reiter der aktiven Gruppe, am Ende rundherum; ohne zweiten Reiter null', () => {
  const aus = (...n) => n.reduce((a, x) => A.hinzufuegen(a, x), A.leer());
  assert.equal(A.reiterNachbar(A.leer(), 1), null);
  assert.equal(A.reiterNachbar(aus('a'), 1), null);
  assert.equal(A.reiterZahl(aus('a', 'b')), 2);
  const a = aus('a', 'b', 'c');   // c ist aktiv
  assert.equal(A.reiterNachbar(a, 1), 'a', 'nach dem letzten kommt der erste');
  assert.equal(A.reiterNachbar(a, -1), 'b');
  const b = A.aktivieren(a, 'a');
  assert.equal(A.reiterNachbar(b, -1), 'c', 'vor dem ersten kommt der letzte');
  assert.equal(A.reiterNachbar(b, 1), 'b');
  assert.equal(A.reiterNachbar(A.aktivieren(a, 'b'), 1), 'c');
  // zwei Gruppen: nur die aktive zählt
  const zwei = A.verschieben(aus('a', 'b', 'c'), 'c', { gruppe: A.aktiveGruppe(aus('a', 'b', 'c')).id, ort: 'rechts' });
  assert.equal(A.reiterZahl(zwei), A.aktiveGruppe(zwei).ansichten.length);
  const q = lies('fenster.js');
  assert.match(q, /export function reiterWechseln\(schritt = 1\)/);
  assert.match(q, /export const reiterAnzahl = \(\) => A\.reiterZahl\(anordnung\)/);
});

// ---------------------------------------------------------------------------------------------- Anleitung

test('Anleitung: Kapitel «Gesten am Arbeitsplatz» im Funktionsinventar – jede Funktion mit Ort, Schritten, Erwartung, Prüfpunkten und Bildvorgabe; Ids eindeutig', () => {
  const inventar = JSON.parse(readFileSync(path.join(hier, '..', '..', '..', '..', '..', 'berichte', 'anleitung', 'inventar.json'), 'utf8'));
  const k = inventar.kapitel.find((x) => x.id === 'mg');
  assert.ok(k, 'Kapitel mg fehlt');
  assert.equal(k.titel, 'Gesten am Arbeitsplatz');
  assert.ok(k.einleitung.length > 100);
  assert.ok(k.funktionen.length >= 8, `nur ${k.funktionen.length} Funktionen`);
  const ids = inventar.kapitel.flatMap((x) => x.funktionen.map((f) => f.id));
  assert.equal(new Set(ids).size, ids.length, 'Ids im ganzen Inventar eindeutig');
  for (const f of k.funktionen) {
    assert.match(f.id, /^mg-/, f.id);
    assert.ok(f.titel, f.id);
    assert.deepEqual(Object.keys(f.wo), ['register', 'gruppe', 'knopf', 'kuerzel', 'sonst'], f.id);
    assert.ok(f.schritte.length >= 1 && f.schritte.every((s) => s.length > 5), f.id);
    assert.ok(f.erwartung.length > 20, f.id);
    assert.ok(f.pruefpunkte.length >= 1, f.id);
    assert.ok(f.bild?.projekt && f.bild?.ansicht && f.bild?.zustand, `${f.id}: Bildvorgabe`);
    assert.ok(Array.isArray(f.quelle) && f.quelle.length >= 1, f.id);
  }
  const text = JSON.stringify(k);
  for (const stichwort of ['Grundschalter', 'GE', 'RV', 'RZ', 'Statusleiste', 'Handskelett', 'Testfeld', 'Punktfolge', 'Belegung', 'Empfindlichkeit', 'G2', 'G3', 'G6', 'Kamera']) assert.ok(text.includes(stichwort), stichwort);
  assert.ok(k.funktionen.some((f) => f.wo.kuerzel === 'GE'));
  assert.ok(k.funktionen.some((f) => f.hauptfunktion), 'mindestens eine Hauptfunktion');
});

test('Dokumentation: OBERFLAECHE.md hat den Abschnitt «Modul G: Gesten am Arbeitsplatz»', () => {
  const md = readFileSync(path.join(hier, '..', '..', '..', 'OBERFLAECHE.md'), 'utf8');
  assert.match(md, /^## Modul G: Gesten am Arbeitsplatz/m);
  const teil = md.slice(md.indexOf('## Modul G: Gesten am Arbeitsplatz'));
  for (const s of ['Wo', 'Bedienung', 'Erwartung', 'Prüfen', 'GE', 'RV', 'RZ', 'data-seiten', 'Testfeld', 'Handskelett', 'gesten.js']) assert.ok(teil.includes(s), s);
});
