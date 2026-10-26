// Tests des Registers «XR» im Menüband (Christian, 26.10.2026: «wir nehmen alles was VR/XR ist in einen eigenen Menue Tab im CAD») ohne Browser: Register vorhanden und an der richtigen Stelle,
// Gruppen, jeder Eintrag nur einmal, alte Orte leer, Kürzel eindeutig, Symbole in Strich und Revit-Fassung, Kontextmenüs unverändert, Doku und Inventar nachgeführt.
// Die Aktionen laden DOM-Module; deshalb liest der Test die Quelltexte (wie die übrigen Verdrahtungstests) und prüft die Zeilen der Aktionen, nicht ihre Ausführung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/xr-register.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import * as strich from './symbole.js';
import * as wz from '../werkzeuge/symbole.js';
import * as rv from './symbole-revit.js';
import * as K from './kuerzel-liste.js';

const hier = path.dirname(fileURLToPath(import.meta.url));
const js = path.join(hier, '..');
const lies = (p) => readFileSync(path.join(hier, p), 'utf8');

/** Alle Quelldateien unter js/ (ohne Tests, ohne Bibliotheken). */
function quellen(ordner = js, acc = []) {
  for (const n of readdirSync(ordner)) {
    const p = path.join(ordner, n);
    if (statSync(p).isDirectory()) { if (n !== 'lib') quellen(p, acc); } else if (/\.js$/.test(n)) acc.push(p);
  }
  return acc;
}
const DATEIEN = quellen().map((p) => [p, readFileSync(p, 'utf8')]);

/** Aktionen, die in einer Zeile mit id, titel und (meist) register stehen. */
const feld = (z, name) => z.match(new RegExp(`\\b${name}: '([^']*)'`))?.[1] ?? null;
const AKTIONEN = [];
for (const [p, t] of DATEIEN) {
  for (const z of t.split('\n')) {
    const id = z.match(/\bid: '([^']+)', titel: '([^']+)'/);
    if (!id || z.trimStart().startsWith('//')) continue;
    AKTIONEN.push({ datei: path.basename(p), id: id[1], titel: id[2], symbol: feld(z, 'symbol'), register: feld(z, 'register'), gruppe: feld(z, 'gruppe'), kuerzel: feld(z, 'kuerzel'), zeile: z });
  }
}
const imRegister = (r) => AKTIONEN.filter((a) => a.register === r);

const ERWARTET = {
  Brille: ['ansicht.questVerbinden', 'xr.appInstallieren', 'xr.autostart', 'ansicht.brilleZeigen'],
  Zeigen: ['ansicht.vrBegehen', 'ansicht.vrSitzungsraum', 'ansicht.vrRaumZeigen', 'xr.anBrille'],
  Koordination: ['koordination.problemMarker'],
  Labor: ['ansicht.xrLabor'],
};
const ALLE_IDS = Object.values(ERWARTET).flat();

// ---------------------------------------------------------------------------------------------- Register

test('Register XR: im Menüband zwischen Ansicht und Verwalten, mit den vier Gruppen in der Reihenfolge Brille · Zeigen · Koordination · Labor', () => {
  const m = lies('./menueband.js');
  assert.match(m, /\['koordination', 'Koordination'\], \['ansicht', 'Ansicht'\], \['xr', 'XR'\], \['verwalten', 'Verwalten'\]\]/);
  assert.match(m, /xr: \['Brille', 'Zeigen', 'Koordination', 'Labor'\]/);
  assert.deepEqual(Object.keys(ERWARTET), ['Brille', 'Zeigen', 'Koordination', 'Labor']);
});

test('Kürzeldialog: das Register XR heisst dort wie im Menüband und steht zwischen Ansicht und Verwalten', () => {
  assert.equal(K.REGISTER_NAMEN.xr, 'XR');
  const folge = Object.keys(K.REGISTER_NAMEN);
  assert.equal(folge.indexOf('xr'), folge.indexOf('ansicht') + 1);
  assert.equal(folge.indexOf('verwalten'), folge.indexOf('xr') + 1);
  assert.equal(K.registerName({ register: 'xr' }), 'XR');
});

test('Alle VR/XR-Aktionen stehen im Register XR, in der erwarteten Gruppe, und es sind genau die erwarteten', () => {
  const xr = imRegister('xr');
  assert.deepEqual(xr.map((a) => a.id).sort(), [...ALLE_IDS].sort());
  for (const [gruppe, ids] of Object.entries(ERWARTET)) {
    for (const id of ids) assert.equal(xr.find((a) => a.id === id)?.gruppe, gruppe, `${id} gehört in die Gruppe ${gruppe}`);
  }
  for (const a of xr) assert.ok(a.symbol && a.titel, `${a.id}: Symbol und Titel`);
});

test('Jede Funktion nur einmal: Ids und Titel im Register XR einmalig, keine Id doppelt im ganzen Quelltext, kein VR/XR-Titel in einem anderen Register', () => {
  const xr = imRegister('xr');
  assert.equal(new Set(xr.map((a) => a.id)).size, xr.length);
  assert.equal(new Set(xr.map((a) => a.titel)).size, xr.length, 'Titel doppelt');
  for (const id of ALLE_IDS) assert.equal(AKTIONEN.filter((a) => a.id === id).length, 1, `${id} mehrfach definiert`);
  const fremd = AKTIONEN.filter((a) => a.register && a.register !== 'xr' && /quest|brille|\bVR\b|\bXR\b|\bAR\b|sitzungsraum|problem-?marker|autostart/i.test(a.titel));
  assert.deepEqual(fremd.map((a) => `${a.register}/${a.id}`), [], 'VR/XR-Eintrag ausserhalb des Registers XR');
});

test('Alte Orte leer: nichts mehr in Ansicht › Fenster oder Koordination › Kollisionen, was zu VR/XR gehört', () => {
  const ansichtFenster = AKTIONEN.filter((a) => a.register === 'ansicht' && a.gruppe === 'Fenster');
  assert.ok(ansichtFenster.length > 0, 'die Gruppe Fenster besteht weiter (Fenster, Paletten …)');
  for (const id of ALLE_IDS) assert.ok(!ansichtFenster.some((a) => a.id === id), `${id} noch in Ansicht › Fenster`);
  for (const a of AKTIONEN.filter((x) => x.register === 'koordination')) assert.notEqual(a.id, 'koordination.problemMarker');
  for (const a of AKTIONEN.filter((x) => x.register === 'ansicht' || x.register === 'koordination')) assert.ok(!ALLE_IDS.includes(a.id), a.id);
  const quest = lies('./aktionen-quest.js');
  assert.doesNotMatch(quest, /register: 'ansicht'/);
  assert.doesNotMatch(lies('./problemmarker-panel.js'), /register: 'koordination'/);
});

// ---------------------------------------------------------------------------------------------- Kürzel

test('Kürzel eindeutig: kein zweistelliges Kürzel kommt zweimal vor; die neuen beginnen mit X, sind gross geschrieben und gehören nur dem Register XR', () => {
  const alle = [];
  for (const [p, t] of DATEIEN) {
    for (const m of t.matchAll(/\bkuerzel: '([^']+)'/g)) alle.push([m[1], path.basename(p)]);
    for (const m of t.matchAll(/\bkuerzelAlt: \[([^\]]*)\]/g)) for (const k of m[1].matchAll(/'([^']+)'/g)) alle.push([k[1], path.basename(p)]);
  }
  const zwei = alle.filter(([k]) => /^[A-Za-z0-9]{2}$/.test(k)).map(([k, d]) => [k.toUpperCase(), d]);
  assert.ok(zwei.length >= 20);
  const doppelt = zwei.filter(([k], i) => zwei.findIndex(([k2]) => k2 === k) !== i).map(([k]) => k);
  assert.deepEqual(doppelt, [], 'doppelte Kürzel');
  const xr = imRegister('xr');
  for (const a of xr) assert.match(a.kuerzel ?? '', /^X[A-Z]$/, `${a.id}: Kürzel`);
  assert.equal(new Set(xr.map((a) => a.kuerzel)).size, xr.length);
  const mitX = AKTIONEN.filter((a) => a.register !== 'xr' && /^X/i.test(a.kuerzel ?? ''));
  assert.deepEqual(mitX, [], 'X-Kürzel gehören dem Register XR');
});

test('Bestehende Kürzel bleiben: die Kürzel der übrigen Register sind unverändert (Stichprobe)', () => {
  const kz = (id) => AKTIONEN.find((a) => a.id === id)?.kuerzel;
  assert.equal(kz('arch.wand'), 'WA');
  assert.equal(kz('arch.raum'), 'RM');
  assert.equal(kz('start.zeigen'), 'Strg+D');
});

// ---------------------------------------------------------------------------------------------- Symbole

test('Symbole: jedes Symbol des Registers XR gibt es im Strichsatz und in der Revit-Fassung (32 und 16, verschieden gezeichnet)', () => {
  const strichNamen = [...strich.SYMBOLNAMEN_STRICH, ...wz.SYMBOLNAMEN];
  for (const a of imRegister('xr')) {
    assert.ok(strichNamen.includes(a.symbol), `${a.id}: Strichsymbol «${a.symbol}» fehlt`);
    assert.ok(rv.hatRevitFassung(a.symbol), `${a.id}: Revit-Fassung «${a.symbol}» fehlt`);
    assert.ok(rv.revitInhalt(a.symbol, false) && rv.revitInhalt(a.symbol, true));
    assert.notEqual(rv.revitInhalt(a.symbol, false), rv.revitInhalt(a.symbol, true), `${a.symbol}: 16 gleich 32`);
    assert.ok(rv.ZUORDNUNG.some((z) => z[0] === a.symbol), `${a.symbol}: Zeile in der Zuordnungstabelle fehlt`);
  }
});

test('Neue Symbole: vier neue Strichsymbole, alle im Zeichenfeld 32 und ohne feste Farben', () => {
  for (const n of ['appInstallieren', 'autostart', 'anBrille', 'raumAr']) {
    const svg = strich.symbol(n, 32);
    assert.match(svg, new RegExp(`data-sym="${n}"`));
    assert.ok(!/stroke-dasharray="3 3"/.test(svg), `${n}: unbekannt (neutrales Quadrat)`);
    for (const z of (svg.match(/ d="([^"]*)"/g) ?? []).join(' ').match(/-?\d*\.?\d+/g) ?? []) assert.ok(Math.abs(Number(z)) <= 32.01, `${n}: Zahl ${z}`);
  }
  assert.equal(new Set(imRegister('xr').map((a) => a.symbol)).size >= 9, true, 'jede Aktion hat ihr eigenes Symbol (bis auf wenige Wiederholungen)');
});

// ---------------------------------------------------------------------------------------------- Kontextmenüs und Aktionen

test('Kontextmenü Raum im Grundriss: die drei VR-Einträge sind unverändert und rufen dieselben Aktionen', () => {
  const g = readFileSync(path.join(js, 'ansichten/grundriss.js'), 'utf8');
  assert.match(g, /titel: 'Raum in AR', aktion: \(\) => ausfuehren\('ansicht\.vrRaumZeigen', \{ raeume: \[raum\.kennzeichen\] \}\)/);
  assert.match(g, /titel: 'Raum begehen in VR', aktion: \(\) => ausfuehren\('ansicht\.vrBegehen', \{ raeume: \[raum\.kennzeichen\] \}\)/);
  assert.match(g, /titel: 'Modell am Sitzungstisch', aktion: \(\) => ausfuehren\('ansicht\.vrSitzungsraum', \{ raeume: \[raum\.kennzeichen\] \}\)/);
});

test('Raumaktionen: Raum aus dem Kontextmenü-Parameter oder, im Menüband, aus der Auswahl; «An die Brille senden» braucht nur ein offenes Projekt', () => {
  const t = lies('./aktionen-vr-auftrag.js');
  assert.match(t, /const kz = p\?\.raeume \? p\.raeume\[0\] : zustand\.auswahl;/);
  assert.match(t, /import \{ element, zustand \} from '\.\.\/zustand\.js'/);
  assert.match(t, /id: 'xr\.anBrille'[^\n]*\n\s*aktiv: \(\) => !!zustand\.status\?\.offen/);
  assert.match(t, /ausfuehren: \(\) => anBrilleSenden\(\)/);
  assert.match(lies('./aktionen.js'), /import '\.\/aktionen-vr-auftrag\.js'/);
});

test('Autostart und App installieren: Autostart ist ein Schalter auf quest.autostart (Vorgabe ein), App installieren öffnet den Dialog «Quest verbinden»', () => {
  const t = lies('./aktionen-quest.js');
  assert.match(t, /id: 'xr\.autostart'[\s\S]*gedrueckt: \(\) => autostartAn\(benutzerWert\(AUTOSTART_SCHLUESSEL, true\)\)/);
  assert.match(t, /benutzerSetzen\(AUTOSTART_SCHLUESSEL, an\)/);
  assert.match(t, /id: 'xr\.appInstallieren'[^\n]*\n\s*ausfuehren: \(\) => questDialog\(\)/);
  assert.equal(lies('./quest-text.js').match(/AUTOSTART_SCHLUESSEL = '([^']+)'/)[1], 'quest.autostart');
});

// ---------------------------------------------------------------------------------------------- Doku und Inventar

test('OBERFLAECHE.md: Menüband-Tabelle mit Register XR, Abschnitt «Register XR», alle Wege der Module auf «XR › …» umgeschrieben', () => {
  const t = readFileSync(path.join(hier, '../../../OBERFLAECHE.md'), 'utf8');
  assert.match(t, /\| XR \(seit 26\.10\.2026, zwischen Ansicht und Verwalten\) \|/);
  assert.match(t, /## Register XR: alles zu Brille, Quest App und VR\/AR in einem Register/);
  for (const [id] of [['ansicht.questVerbinden'], ['xr.appInstallieren'], ['xr.autostart'], ['ansicht.brilleZeigen'], ['ansicht.vrBegehen'], ['ansicht.vrSitzungsraum'], ['ansicht.vrRaumZeigen'], ['xr.anBrille'], ['koordination.problemMarker'], ['ansicht.xrLabor']]) {
    assert.ok(t.includes('`' + id + '`'), `${id} fehlt im Abschnitt Register XR`);
  }
  for (const alt of [/Ansicht › (Gruppe )?Fenster › «(Quest verbinden|Brille zeigen|XR-Labor)/, /Ansicht › Fenster › «XR-Labor/, /Koordination › Gruppe Kollisionen › «Problem-Marker/, /Register \*\*Ansicht › Gruppe Fenster\*\*, Aktion `ansicht\.xrLabor`/]) {
    assert.doesNotMatch(t, alt, `alter Weg noch in der Doku: ${alt}`);
  }
  assert.match(t, /Register \*\*XR › Gruppe Brille › «Quest verbinden …»\*\*/);
  assert.match(t, /Register \*\*XR › Labor › «XR-Labor …»\*\*/);
});

test('Inventar der Anleitung: gültiges JSON, Wege der XR-Einträge zeigen auf das Register XR, keine alten Wege', () => {
  const roh = readFileSync(path.join(hier, '../../../../../berichte/anleitung/inventar.json'), 'utf8');
  const inv = JSON.parse(roh);
  const wo = [];
  (function lauf(o) {
    if (Array.isArray(o)) o.forEach(lauf);
    else if (o && typeof o === 'object') { if (o.id && o.wo) wo.push(o); Object.values(o).forEach(lauf); }
  })(inv);
  const knoepfe = ['Brille zeigen', 'XR-Labor …', 'Problem-Marker'];
  const xr = wo.filter((e) => knoepfe.includes(e.wo.knopf) || /^Quest verbinden …/.test(e.wo.knopf));
  assert.ok(xr.length >= 12, `XR-Einträge: ${xr.length}`);
  for (const e of xr) {
    assert.equal(e.wo.register, 'XR', e.id);
    assert.ok(['Brille', 'Labor', 'Koordination'].includes(e.wo.gruppe), `${e.id}: Gruppe ${e.wo.gruppe}`);
  }
  assert.equal(xr.find((e) => e.wo.knopf === 'Problem-Marker').wo.gruppe, 'Koordination');
  assert.equal(xr.find((e) => e.wo.knopf === 'XR-Labor …').wo.gruppe, 'Labor');
  assert.doesNotMatch(roh, /Ansicht › (Gruppe )?Fenster(\)| ›) ?(«(Quest verbinden|Brille zeigen|XR-Labor)|die Quest verbinden|im Teil «Quest App»)/);
  assert.doesNotMatch(roh, /Koordination › Gruppe Kollisionen › «Problem-Marker/);
  assert.doesNotMatch(roh, /«XR-Labor …» steht im Register Ansicht/);
});
