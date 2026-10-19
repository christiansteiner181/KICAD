// Tests der Texte, Prüfungen und Parameter für Register Architektur › Decke/Boden/Dach und Register Hülle (Modul 1c, Auftrag C1a) ohne Browser:
// Decke erzeugen, Material (Quelle Pflicht), Aufbau-Editor (R/U aus dem Kern, fehlende Kennwerte), Zuweisen, Fensterkennwerte, Übergang, Hülle prüfen,
// Wärmebrücken, Listen, Projektbrowser-Knoten, 3D-Nachladen, Verdrahtung (Menüband, Symbole, Aktionen).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/huelle.test.mjs
// Die Beispieldaten haben die Form der Antworten des Kerns (HuelleAbfragen, AufbauNachfuehrung, DeckeBefehle); Zahlen sind als «Testwert» gedacht –
// der Kern setzt keine Kennwerte, die Oberfläche auch nicht. Ist HLKS_TEST_SERVER gesetzt (Server mit offenem Projekt), prüft ein zusätzlicher Test
// die Bausteine gegen die echten (nur lesenden) Antworten des Kerns.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { register } from 'node:module';
import { pathToFileURL, fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const threeUrl = pathToFileURL(path.resolve(hier, '../../lib/three/three.module.js')).href;
register('data:text/javascript,' + encodeURIComponent(
  `export async function resolve(s, c, n) { if (s === 'three') return { url: ${JSON.stringify(threeUrl)}, shortCircuit: true }; return n(s, c); }`));

const T = await import('./huelle-text.js');
const fmt = await import('../ansichten/listenformat.js');
const sym = await import('./symbole.js');
const k3 = await import('../ansichten/koerper3d.js');

const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const kernDatei = (rel) => { const p = path.resolve(hier, '../../../../Hlks.Kern', rel); return existsSync(p) ? readFileSync(p, 'utf8') : null; };
const katalog = JSON.parse(lies('../../../../../kataloge/klassen.json'));
const klasse = (n) => (Array.isArray(katalog) ? katalog : katalog.klassen).find((k) => k.name === n);

// ---------------------------------------------------------------------------------------------- Beispieldaten (Form wie im Kern)

const KONTEXT = 'AUF-1: kein U-Wert, es fehlt: Schicht 2: λ von MAT-2 «Testdaemmung» fehlt (material_aendern mit werte.lambda und Quelle). Rsi/Rse für «horizontal» fehlen (uebergang_setzen mit Quelle).';
const materialien = {
  projekt: [
    { kennzeichen: 'MAT-1', name: 'Testbeton', kategorie: 'Beton', werte: { lambda: { wert: 2, einheit: 'W/(m·K)', quelle: 'Testwert C1a', herkunft: 'mensch' } }, fehlt: [], verwendetIn: ['AUF-1'] },
    { kennzeichen: 'MAT-2', name: 'Testdaemmung', kategorie: 'Dämmstoff', werte: {}, fehlt: ['lambda'], verwendetIn: ['AUF-1'] },
    { kennzeichen: 'MAT-3', name: 'Testputz', kategorie: null, werte: { lambda: { wert: 0.7, einheit: 'W/(m·K)', quelle: null, herkunft: 'mensch' } }, fehlt: ['Quelle von lambda'], verwendetIn: [] },
  ],
  bibliothek: { datei: 'x/materialien.json', anzahl: 0, eintraege: [], hinweis: 'Die Bürobibliothek ist leer. Kennwerte kommen aus dem IFC, aus Datenblättern oder von Christian, je mit Quelle (material_anlegen).' },
};
const aufbauRechnen = {
  aufbau: 'AUF-1', name: 'Test-AW', art: 'wand', waermestrom: 'horizontal',
  schichten: [{ nr: 1, material: 'MAT-1', name: 'Testbeton', dicke_mm: 200, lambda: 2, lambdaQuelle: 'Testwert C1a', r: 0.1 }, { nr: 2, material: 'MAT-2', name: 'Testdaemmung', dicke_mm: 100, lambda: null, lambdaQuelle: null, r: null }],
  rsi: null, rse: null, r: null, u: null, uAusdruecklich: false, uQuelle: null,
  fehlt: ['Schicht 2: λ von MAT-2 «Testdaemmung» fehlt (material_aendern mit werte.lambda und Quelle).', 'Rsi/Rse für «horizontal» fehlen (uebergang_setzen mit Quelle).'],
};
const aufbauElement = { kennzeichen: 'AUF-1', klasse: 'Aufbau', name: 'Test-AW', werte: { art: 'wand', schichten: [{ material: 'id-1', dicke: 200 }, { material: 'id-2', dicke: 100 }] } };
const matKz = (id) => ({ 'id-1': 'MAT-1', 'id-2': 'MAT-2' }[id]);
const laufNeu = {
  angenommen: true, befehl: 'aufbau_anlegen', trockenlauf: true,
  aenderungen: [{ art: 'angelegt', kennzeichen: 'AUF-2', klasse: 'Aufbau' }, { art: 'geaendert', kennzeichen: 'AUF-2', klasse: 'Aufbau', detail: 'rWert: – → 3.075 m²·K/W; uWert: – → 0.3081664098613251 W/(m²·K)' }],
  hinweise: ['R/U neu gerechnet: AUF-2'],
};
const laufOhneU = { angenommen: true, trockenlauf: true, aenderungen: [{ art: 'angelegt', kennzeichen: 'AUF-1', klasse: 'Aufbau' }], hinweise: [KONTEXT] };

// ---------------------------------------------------------------------------------------------- Zahlen und Texte

test('Zahlen: Komma und Punkt, Ungültiges ergibt null; Anzeige rundet nur', () => {
  assert.equal(T.zahlLesen('0,13'), 0.13);
  assert.equal(T.zahlLesen(' 2.5 '), 2.5);
  assert.equal(T.zahlLesen('1e-3'), 0.001);
  for (const s of ['', '  ', 'abc', '1,2,3', '12 mm', null, undefined]) assert.equal(T.zahlLesen(s), null, String(s));
  assert.equal(T.zahlAnzeige(0.3081664098613251), '0.308');
  assert.equal(T.zahlAnzeige(2.6), '2.6');
  assert.equal(T.zahlAnzeige(null), '–');
  assert.equal(T.zahlFeld(0.13), '0.13');
  assert.equal(T.zahlFeld(null), '');
});

test('Texte des Kerns für Menschen: Rat an MCP wird zum Namen des Dialogs, der Inhalt bleibt', () => {
  const t = T.hinweisFuerMenschen('Schicht 2: λ von MAT-2 «Testdaemmung» fehlt (material_aendern mit werte.lambda und Quelle).');
  assert.match(t, /λ von MAT-2 «Testdaemmung» fehlt/);
  assert.match(t, /Dialog «Material …»/);
  assert.doesNotMatch(t, /material_aendern|werte\.lambda/);
  assert.match(T.hinweisFuerMenschen('Rsi/Rse für «horizontal» fehlen (uebergang_setzen mit Quelle).'), /Dialog «Übergang …»/);
  assert.equal(T.hinweisFuerMenschen('Mit element_loeschen entfernen.'), 'Mit «Löschen …» entfernen.');
  assert.equal(T.hinweisFuerMenschen('unbekannt_aber_ohne_knopf bleibt'), 'unbekannt_aber_ohne_knopf bleibt');
});

test('Fehlende Kennwerte aus dem Hinweis des Kerns: ein Satz je Fehlendes, mit dem Text des Kerns', () => {
  const s = T.fehltAusHinweisen(['Etwas anderes', KONTEXT]);
  assert.equal(s.length, 2);
  assert.match(s[0], /^Schicht 2: λ von MAT-2 «Testdaemmung» fehlt/);
  assert.match(s[1], /^Rsi\/Rse für «horizontal» fehlen/);
  assert.deepEqual(T.fehltAusHinweisen(['R/U neu gerechnet: AUF-2']), []);
  assert.deepEqual(T.fehltAusHinweisen(null), []);
  const drei = T.fehltAusHinweisen(['AUF-3: kein U-Wert, es fehlt: Schicht 1: Dicke fehlt oder ist nicht grösser als 0. Schicht 2: Material nicht gefunden (Id x). Rsi oder Rse für «aufwaerts» sind unvollständig (uebergang_setzen).']);
  assert.equal(drei.length, 3);
});

test('R und U aus dem Trockenlauf: nur, was der Kern im Detail nennt; «–» = fällt weg', () => {
  assert.deepEqual(T.rechnungAusLauf(laufNeu), { r: 3.075, u: 0.3081664098613251 });
  assert.deepEqual(T.rechnungAusLauf(laufOhneU), {});
  const aendern = { aenderungen: [{ klasse: 'Aufbau', kennzeichen: 'AUF-1', art: 'geaendert', detail: 'schichten: [{"material":"x","dicke":200}] → [{"material":"x","dicke":250}]' },
    { klasse: 'Aufbau', kennzeichen: 'AUF-1', art: 'geaendert', detail: 'rWert: 2.6 m²·K/W → 2.625 m²·K/W; uWert: 0.36101083032490977 W/(m²·K) → –' }] };
  assert.deepEqual(T.rechnungAusLauf(aendern), { r: 2.625, u: null });
  assert.deepEqual(T.rechnungAusLauf({ aenderungen: [{ klasse: 'Aufbau', art: 'geaendert', detail: '' }] }), {});
  assert.deepEqual(T.rechnungAusLauf(null), {});
});

test('Fehlertext einer Ablehnung mit Vorschlag; Lauf-HTML maskiert', () => {
  assert.equal(T.ablehnung({ fehler: 'Quelle fehlt.', vorschlag: 'Quelle nennen.' }), 'Quelle fehlt. – Vorschlag: Quelle nennen.');
  assert.equal(T.ablehnung(null), 'Abgelehnt.');
  const h = T.laufHtml({ angenommen: false, fehler: 'a <b> & c', vorschlag: 'v' });
  assert.match(h, /fehlerkasten/);
  assert.doesNotMatch(h, /<b> &/);
  assert.match(T.laufHtml({ angenommen: true, hinweise: ['R/U neu gerechnet: AUF-2'] }), /<li>R\/U neu gerechnet: AUF-2<\/li>/);
});

// ---------------------------------------------------------------------------------------------- Decke, Boden, Dach

test('Decke erzeugen: Dicke ist Pflicht, es gibt keine Vorgabe', () => {
  const ok = { geschoss: 'EG', art: 'decke', dicke: '200', aufbau: '' };
  assert.equal(T.deckePruefen(ok), true);
  for (const dicke of ['', '  ', 'x', '0', '-5']) assert.notEqual(T.deckePruefen({ ...ok, dicke }), true, `Dicke «${dicke}»`);
  assert.match(T.deckePruefen({ ...ok, dicke: '' }), /keine Vorgabe/);
  assert.match(T.deckePruefen({ ...ok, geschoss: '' }), /Geschoss/);
  assert.match(T.deckePruefen({ ...ok, art: 'balkon' }), /Decke, Boden oder Dach/);
});

test('Decke erzeugen: Parameter – Aufbau nur, wenn gewählt', () => {
  assert.deepEqual(T.deckeParameter({ geschoss: 'EG', art: 'boden', dicke: '250,5', aufbau: '' }), { geschoss: 'EG', art: 'boden', dicke: 250.5 });
  assert.deepEqual(T.deckeParameter({ geschoss: ' EG ', art: 'dach', dicke: '200', aufbau: 'AUF-3' }), { geschoss: 'EG', art: 'dach', dicke: 200, aufbau: 'AUF-3' });
  assert.deepEqual(Object.keys(T.DECKE_ARTEN), ['decke', 'boden', 'dach']);
  const cs = kernDatei('Architektur/DeckeBefehle.cs');
  if (cs) assert.match(cs, /Arten = \["decke", "boden", "dach"\]/);
});

test('Decke erzeugen: Geschossauswahl von oben nach unten, Vorgabe = aktive Ansicht, sonst EG (Kote 0)', () => {
  const gs = [{ kennzeichen: 'UG', name: 'Untergeschoss', werte: { kote: -2700 } }, { kennzeichen: 'EG', name: 'Erdgeschoss', werte: { kote: 0 } }, { kennzeichen: '1.OG', name: null, werte: { kote: 2900 } }];
  assert.deepEqual(T.geschossOptionen(gs).map((o) => o.wert), ['1.OG', 'EG', 'UG']);
  assert.equal(T.geschossOptionen(gs)[1].text, 'EG – Erdgeschoss');
  assert.equal(T.geschossVorgabe(gs, '1.OG'), '1.OG');
  assert.equal(T.geschossVorgabe(gs, 'gibts-nicht'), 'EG');
  assert.equal(T.geschossVorgabe([{ kennzeichen: 'A', werte: { kote: 100 } }, { kennzeichen: 'B', werte: { kote: 50 } }]), 'B');
  assert.equal(T.geschossVorgabe([]), '');
});

test('Decke erzeugen: Aufbauten passend zur Art; Ergebnis hebt Fläche und Höhe hervor, Hinweise des Kerns folgen', () => {
  const aufbauten = [{ kennzeichen: 'AUF-2', werte: { art: 'decke' } }, { kennzeichen: 'AUF-1', werte: { art: 'wand' } }, { kennzeichen: 'AUF-10', werte: { art: 'decke' } }];
  assert.deepEqual(T.aufbautenFuerDecke(aufbauten, 'decke').map((a) => a.kennzeichen), ['AUF-2', 'AUF-10']);
  assert.deepEqual(T.aufbautenFuerDecke(aufbauten, 'dach'), []);
  const r = { angenommen: true, trockenlauf: true, aenderungen: [{ art: 'angelegt', kennzeichen: 'D-1', klasse: 'Decke' }], hinweise: [
    'Umriss aus 4 Wänden und 5 Räumen von EG: Wandbänder mit Eckverlängerung und Räume vereinigt, Löcher gefüllt.',
    'D-1 (decke) in 1.OG: Fläche 127.82 m², Oberkante 2900 mm (Kote von 1.OG, dem Geschoss darüber), Dicke 200 mm, Unterkante 2700 mm'] };
  const h = T.deckeErgebnisHtml(r);
  assert.match(h, /Vorschau \(Trockenlauf – nichts wurde geändert\)/);
  assert.match(h, /<b>D-1 \(decke\) in 1\.OG: Fläche 127\.82 m², Oberkante 2900 mm/);
  assert.match(h, /Hinweise des Kerns/);
  assert.match(h, /Umriss aus 4 Wänden/);
  assert.match(T.deckeErgebnisHtml({ ...r, trockenlauf: false }), /<h4>Erzeugt<\/h4>/);
  assert.match(T.deckeErgebnisHtml({ angenommen: false, fehler: 'Dicke fehlt', vorschlag: 'v' }), /fehlerkasten/);
});

// ---------------------------------------------------------------------------------------------- Material

test('Katalog: Kennwerte, Einheiten, Aufbau-Arten, Wärmeströme und Fensterfelder stimmen mit klassen.json überein', () => {
  const mat = klasse('Material');
  for (const k of T.KENNWERTE) {
    const e = mat.eigenschaften.find((x) => x.name === k.name);
    assert.ok(e, `Material.${k.name} fehlt im Katalog`);
    assert.equal(e.einheit ?? '', k.einheit, `Einheit ${k.name}`);
  }
  const auf = klasse('Aufbau');
  assert.deepEqual(T.AUFBAU_ARTEN.map(([w]) => w), auf.eigenschaften.find((x) => x.name === 'art').auswahl);
  assert.deepEqual(T.WAERMESTROM.map(([w]) => w).filter(Boolean), auf.eigenschaften.find((x) => x.name === 'waermestrom').auswahl);
  for (const f of T.FENSTER_FELDER) {
    const e = klasse(f.fuer).eigenschaften.find((x) => x.name === f.name);
    assert.ok(e, `${f.fuer}.${f.name} fehlt im Katalog`);
    assert.equal(e.einheit ?? '', f.einheit, `Einheit ${f.name}`);
  }
  assert.deepEqual(T.SONNENSCHUTZ.map(([w]) => w).filter(Boolean), klasse('Fenster').eigenschaften.find((x) => x.name === 'sonnenschutz').auswahl);
  const kern = kernDatei('Huelle/AufbauBefehle.cs');
  if (kern) assert.match(kern, new RegExp(`Gruppen = \\[${T.UEBERGANG_GRUPPEN.map((g) => `"${g.name}"`).join(', ')}\\]`));
  assert.ok(klasse('Waermebruecke') && klasse('Projektinformation').eigenschaften.some((x) => x.name === 'uebergang'));
});

test('Keine Vorgabewerte: leere Formulare für Material, Aufbau und Übergang', () => {
  const m = T.materialLeer();
  assert.ok(T.KENNWERTE.every((k) => m.werte[k.name].wert === '' && m.werte[k.name].quelle === ''));
  const a = T.aufbauLeer();
  assert.deepEqual(a.schichten, [{ material: '', dicke: '' }]);
  assert.equal(a.uWert, '');
  const u = T.uebergangFormular(null);
  assert.ok(T.UEBERGANG_GRUPPEN.every((g) => u[g.name].rsi === '' && u[g.name].rse === '' && u[g.name].quelle === ''));
  assert.ok(T.FENSTER_FELDER.every((f) => !('vorgabe' in f)));
  // der Quelltext der Dialoge setzt nirgends einen Zahlenwert in ein Feld (value="0.…" o. ä.)
  assert.doesNotMatch(lies('./aktionen-huelle.js'), /value="\d/);
});

test('Material: jeder Kennwert braucht eine Quelle – ohne Quelle kein Speichern', () => {
  const f = T.materialLeer();
  f.name = 'Testbeton';
  f.werte.lambda = { wert: '2,0', quelle: '' };
  const pr = T.materialPruefen(f);
  assert.equal(pr.fehler.length, 1);
  assert.match(pr.fehler[0], /^λ: Quelle fehlt\. Kennwerte nur mit Quelle \(Norm mit Ausgabe, Datenblatt\)\./);
  f.werte.lambda.quelle = 'Testwert C1a';
  assert.deepEqual(T.materialPruefen(f).fehler, []);
  f.werte.rohdichte = { wert: '', quelle: 'Datenblatt' };
  assert.match(T.materialPruefen(f).fehler[0], /ρ: Quelle angegeben, aber kein Wert/);
  f.werte.rohdichte = { wert: 'viel', quelle: 'x' };
  assert.match(T.materialPruefen(f).fehler[0], /ρ: «viel» ist keine Zahl/);
  f.werte.rohdichte = { wert: '0', quelle: 'x' };
  assert.match(T.materialPruefen(f).fehler[0], /ρ: muss grösser als 0 sein/);
  f.werte.rohdichte = { wert: '', quelle: '' };
  f.werte.emission = { wert: '1,5', quelle: 'x' };
  assert.match(T.materialPruefen(f).fehler[0], /ε: muss zwischen 0 und 1 liegen/);
  assert.match(T.materialPruefen({ ...T.materialLeer(), name: '  ' }).fehler[0], /Namen/);
});

test('Material: Warnung, wenn der Wert sich ändert, die Quelle aber dieselbe bleibt', () => {
  const bisher = T.materialFormular(materialien.projekt[0]);
  const f = T.materialFormular(materialien.projekt[0]);
  assert.deepEqual(T.materialPruefen(f, bisher), { fehler: [], warnungen: [] });
  f.werte.lambda.wert = '2.1';
  const pr = T.materialPruefen(f, bisher);
  assert.deepEqual(pr.fehler, []);
  assert.match(pr.warnungen[0], /λ: Wert geändert, Quelle unverändert/);
  f.werte.lambda.quelle = 'Testwert C1a, neu';
  assert.deepEqual(T.materialPruefen(f, bisher).warnungen, []);
});

test('Material: Parameter anlegen (nur Kennwerte mit Wert) und ändern (nur Geändertes, Leeren entfernt)', () => {
  const f = T.materialLeer();
  f.name = ' Testbeton '; f.kategorie = 'Beton';
  f.werte.lambda = { wert: '2', quelle: ' Testwert C1a ' };
  assert.deepEqual(T.materialAnlegenParameter(f), { name: 'Testbeton', kategorie: 'Beton', werte: { lambda: { wert: 2, quelle: 'Testwert C1a' } } });
  assert.deepEqual(T.materialAnlegenParameter({ ...T.materialLeer(), name: 'Nur Name' }), { name: 'Nur Name' });

  const bisher = T.materialFormular(materialien.projekt[0]);
  assert.equal(bisher.kennzeichen, 'MAT-1');
  assert.equal(bisher.werte.lambda.wert, '2');
  const g = T.materialFormular(materialien.projekt[0]);
  assert.equal(T.materialAendernParameter(g, bisher), null);
  g.werte.lambda = { wert: '2.2', quelle: 'Testwert C1a, neu' };
  g.werte.rohdichte = { wert: '2400', quelle: 'Testwert C1a' };
  g.name = 'Testbeton B';
  assert.deepEqual(T.materialAendernParameter(g, bisher), { material: 'MAT-1', name: 'Testbeton B', werte: { lambda: { wert: 2.2, quelle: 'Testwert C1a, neu' }, rohdichte: { wert: 2400, quelle: 'Testwert C1a' } } });
  const h = T.materialFormular(materialien.projekt[0]);
  h.werte.lambda = { wert: '', quelle: '' };
  assert.deepEqual(T.materialAendernParameter(h, bisher), { material: 'MAT-1', werte: { lambda: null } });
});

test('Material: Liste mit λ in Rot, wenn es fehlt oder die Quelle fehlt; Bibliothek leer zeigt den Hinweis des Kerns', () => {
  const h = T.materialListeHtml(materialien, 'MAT-1');
  assert.match(h, /<tr data-kz="MAT-1" class="gewaehlt">/);
  assert.match(h, /<td class="zahl rot"[^>]*>λ fehlt<\/td>/);
  assert.match(h, /λ fehlt \(Quelle\)/);
  assert.match(h, /<td class="zahl"[^>]*>2<\/td>/);
  assert.match(T.materialListeHtml({ projekt: [] }), /Noch kein Material/);
  assert.match(T.bibliothekHtml(materialien.bibliothek), /Die Bürobibliothek ist leer/);
  const bib = { eintraege: [{ name: 'Testdämmung <b>', kategorie: 'Dämmstoff', werte: { lambda: { wert: 0.04, quelle: 'Testwert' } } }] };
  const b = T.bibliothekHtml(bib);
  assert.match(b, /data-name="Testdämmung &lt;b&gt;"/);
  assert.match(b, /λ 0\.04/);
  assert.match(T.bibliothekHtml({ fehler: 'kaputt', vorschlag: 'prüfen' }), /kaputt/);
});

// ---------------------------------------------------------------------------------------------- Aufbau

const gueltigerAufbau = () => ({ ...T.aufbauLeer(), name: 'Test-AW', art: 'wand', schichten: [{ material: 'MAT-1', dicke: '200' }, { material: 'MAT-2', dicke: '100' }] });

test('Aufbau: Formular aus dem Element; ausdrücklicher U-Wert kommt mit Quelle', () => {
  const f = T.aufbauFormular(aufbauElement, aufbauRechnen, matKz);
  assert.equal(f.kennzeichen, 'AUF-1');
  assert.deepEqual(f.schichten, [{ material: 'MAT-1', dicke: '200' }, { material: 'MAT-2', dicke: '100' }]);
  assert.equal(f.uWert, '');
  const g = T.aufbauFormular(aufbauElement, { ...aufbauRechnen, u: 0.3, uAusdruecklich: true, uQuelle: 'Datenblatt Testwert' }, matKz);
  assert.equal(g.uWert, '0.3'); assert.equal(g.uQuelle, 'Datenblatt Testwert');
  assert.deepEqual(T.aufbauFormular(null).schichten, [{ material: '', dicke: '' }]);
});

test('Aufbau: Prüfung – Name, Schichten, Dicke; U ausdrücklich nur mit Quelle', () => {
  assert.deepEqual(T.aufbauPruefen(gueltigerAufbau()).fehler, []);
  assert.match(T.aufbauPruefen({ ...gueltigerAufbau(), name: '' }).fehler[0], /Namen/);
  assert.match(T.aufbauPruefen(T.aufbauLeer()).fehler.join(' '), /Mindestens eine Schicht/);
  const f = gueltigerAufbau();
  f.schichten[1].dicke = '';
  assert.match(T.aufbauPruefen(f).fehler[0], /Schicht 2: Dicke in mm angeben/);
  f.schichten[1] = { material: '', dicke: '50' };
  assert.match(T.aufbauPruefen(f).fehler[0], /Schicht 2: Material wählen/);
  f.schichten[1] = { material: 'MAT-2', dicke: '-3' };
  assert.match(T.aufbauPruefen(f).fehler[0], /grösser als 0/);
  const u = { ...gueltigerAufbau(), uWert: '0,3' };
  assert.match(T.aufbauPruefen(u).fehler[0], /U-Wert: Quelle fehlt\. Kennwerte nur mit Quelle/);
  u.uQuelle = 'Datenblatt Testwert';
  assert.deepEqual(T.aufbauPruefen(u).fehler, []);
  assert.match(T.aufbauPruefen({ ...gueltigerAufbau(), uQuelle: 'x' }).fehler[0], /kein U-Wert/);
  assert.match(T.aufbauPruefen({ ...gueltigerAufbau(), uWert: 'abc', uQuelle: 'x' }).fehler[0], /Zahl grösser als 0/);
});

test('Aufbau: Parameter anlegen und ändern', () => {
  const a = gueltigerAufbau();
  assert.deepEqual(T.aufbauAnlegenParameter(a), { name: 'Test-AW', art: 'wand', schichten: [{ material: 'MAT-1', dicke: 200 }, { material: 'MAT-2', dicke: 100 }] });
  assert.deepEqual(T.aufbauAnlegenParameter({ ...a, waermestrom: 'abwaerts', uWert: '0,3', uQuelle: 'Datenblatt Testwert', quelle: 'Plan 1:50' }),
    { name: 'Test-AW', art: 'wand', schichten: [{ material: 'MAT-1', dicke: 200 }, { material: 'MAT-2', dicke: 100 }], waermestrom: 'abwaerts', uWert: { wert: 0.3, quelle: 'Datenblatt Testwert' }, quelle: 'Plan 1:50' });
  // leere Schichtzeilen zählen nicht
  assert.equal(T.aufbauAnlegenParameter({ ...a, schichten: [...a.schichten, { material: '', dicke: '' }] }).schichten.length, 2);

  const bisher = T.aufbauFormular(aufbauElement, aufbauRechnen, matKz);
  assert.equal(T.aufbauAendernParameter(structuredClone(bisher), bisher), null);
  assert.equal(T.aufbauGeaendert(structuredClone(bisher), bisher), false);
  const neu = structuredClone(bisher);
  neu.schichten[0].dicke = '250'; neu.name = 'Test-AW neu'; neu.waermestrom = 'horizontal';
  assert.equal(T.aufbauGeaendert(neu, bisher), true);
  assert.deepEqual(T.aufbauAendernParameter(neu, bisher), { aufbau: 'AUF-1', name: 'Test-AW neu', waermestrom: 'horizontal', schichten: [{ material: 'MAT-1', dicke: 250 }, { material: 'MAT-2', dicke: 100 }] });
  neu.waermestrom = bisher.waermestrom = 'horizontal';
  const mitU = structuredClone(bisher); mitU.uWert = '0.3'; mitU.uQuelle = 'Datenblatt Testwert';
  assert.deepEqual(T.aufbauAendernParameter(mitU, bisher), { aufbau: 'AUF-1', uWert: { wert: 0.3, quelle: 'Datenblatt Testwert' } });
  const auto = structuredClone(bisher); auto.waermestrom = '';
  assert.equal(T.aufbauAendernParameter(auto, bisher).waermestrom, 'auto');
});

test('Aufbau: einen ausdrücklichen U-Wert entfernt das Leeren des Feldes (element_aendern uWert null)', () => {
  const bisher = { ...gueltigerAufbau(), kennzeichen: 'AUF-1', uWert: '0.3', uQuelle: 'Datenblatt Testwert' };
  assert.equal(T.uWertEntfernen({ ...bisher, uWert: '', uQuelle: '' }, bisher), true);
  assert.equal(T.uWertEntfernen(bisher, bisher), false);
  assert.equal(T.uWertEntfernen({ ...bisher, uWert: '' }, { ...bisher, uWert: '' }), false);
  assert.equal(T.uWertEntfernen(bisher, null), false);
});

test('Aufbau: Schichten verschieben, einfügen, löschen (innen → aussen)', () => {
  const l = [{ material: 'A', dicke: '1' }, { material: 'B', dicke: '2' }, { material: 'C', dicke: '3' }];
  assert.deepEqual(T.schichtenAendern(l, 'runter', 0).map((s) => s.material), ['B', 'A', 'C']);
  assert.deepEqual(T.schichtenAendern(l, 'hoch', 2).map((s) => s.material), ['A', 'C', 'B']);
  assert.deepEqual(T.schichtenAendern(l, 'hoch', 0).map((s) => s.material), ['A', 'B', 'C']);
  assert.deepEqual(T.schichtenAendern(l, 'runter', 2).map((s) => s.material), ['A', 'B', 'C']);
  assert.deepEqual(T.schichtenAendern(l, 'weg', 1).map((s) => s.material), ['A', 'C']);
  assert.deepEqual(T.schichtenAendern(l, 'davor', 1).map((s) => s.material), ['A', '', 'B', 'C']);
  assert.deepEqual(T.schichtenAendern(l, 'ende', 0).map((s) => s.material), ['A', 'B', 'C', '']);
  assert.deepEqual(T.schichtenAendern([{ material: 'A', dicke: '1' }], 'weg', 0), [{ material: '', dicke: '' }]);
  assert.equal(l.length, 3, 'Eingabe bleibt unverändert');
});

test('Aufbau: je Schicht λ und R vom Kern; fehlendes λ in Rot; R nur für unveränderte Zeilen', () => {
  const f = T.aufbauFormular(aufbauElement, aufbauRechnen, matKz);
  const inf = T.schichtInfo(f, materialien, aufbauRechnen);
  assert.equal(inf[0].lambda, 2); assert.equal(inf[0].r, 0.1); assert.equal(inf[0].lambdaFehlt, '');
  assert.equal(inf[1].lambda, null); assert.equal(inf[1].lambdaFehlt, 'λ fehlt'); assert.equal(inf[1].r, null);
  f.schichten[0].dicke = '250';
  assert.equal(T.schichtInfo(f, materialien, aufbauRechnen)[0].r, null, 'R nach Änderung der Dicke rechnet der Kern erst nach dem Speichern');
  f.schichten.push({ material: 'MAT-3', dicke: '15' });
  assert.equal(T.schichtInfo(f, materialien, null)[2].lambdaFehlt, 'Quelle von λ fehlt');
  assert.equal(T.schichtInfo({ schichten: [{ material: 'MAT-99', dicke: '1' }] }, materialien)[0].lambdaFehlt, '');
  assert.match(T.schichtLambdaHtml(inf[1]), /class="rot"[^>]*>λ fehlt/);
  assert.match(T.schichtLambdaHtml(inf[0]), /title="Testwert C1a">2</);
});

test('Aufbau: Summen – gespeicherter Stand, Trockenlauf, fehlende Kennwerte rot mit dem Text des Kerns', () => {
  // gespeichert, U fehlt: R und U fehlen, der Kern nennt beide Gründe
  let s = T.summen(null, aufbauRechnen);
  assert.equal(s.r, null); assert.equal(s.u, null);
  assert.equal(s.fehlt.length, 2);
  let h = T.summenHtml(s);
  assert.equal((h.match(/<span class="huelle-wert rot">fehlt<\/span>/g) ?? []).length, 2, "R und U fehlen, in Rot");
  assert.match(h, /<ul class="huelle-fehlt rot">/);
  assert.match(h, /λ von MAT-2 «Testdaemmung» fehlt/);
  assert.match(h, /Dialog «Material …»/);

  // neuer Aufbau, vollständig: Zahlen aus dem Detail des Trockenlaufs
  s = T.summen(laufNeu, null);
  assert.equal(s.r, 3.075); assert.ok(Math.abs(s.u - 0.3081664) < 1e-6); assert.deepEqual(s.fehlt, []);
  h = T.summenHtml(s);
  assert.match(h, /<b>3\.075<\/b>/); assert.match(h, /<b>0\.308<\/b>/); assert.doesNotMatch(h, /huelle-fehlt/);

  // neuer Aufbau, U nicht rechenbar: die Sätze stehen im Hinweis des Kerns
  s = T.summen(laufOhneU, null);
  assert.equal(s.u, null); assert.equal(s.fehlt.length, 2); assert.match(s.fehlt[0], /^Schicht 2: λ von MAT-2/);

  // geänderter Aufbau: was der Lauf nicht nennt, bleibt vom gespeicherten Stand
  const gut = { ...aufbauRechnen, r: 2.6, u: 0.361011, fehlt: [], rsi: 0.13, rse: 0.04, uQuelle: 'U = 1/(Rsi + R + Rse) …' };
  s = T.summen({ angenommen: true, hinweise: [], aenderungen: [{ klasse: 'Aufbau', detail: 'rWert: 2.6 m²·K/W → 2.625 m²·K/W; uWert: 0.36101083032490977 W/(m²·K) → 0.35778175313059035 W/(m²·K)' }] }, gut);
  assert.equal(s.r, 2.625); assert.ok(Math.abs(s.u - 0.357781) < 1e-5);
  s = T.summen({ angenommen: true, hinweise: [], aenderungen: [{ klasse: 'Aufbau', detail: 'uWert: 0.36 W/(m²·K) → 0.3 W/(m²·K)' }] }, gut);
  assert.equal(s.r, 2.6); assert.equal(s.u, 0.3);
  assert.match(T.summenHtml({ ...s, ausdruecklich: true }), /ausdrücklich, geht vor/);
});

test('Aufbau: Schichtzeile als HTML – Eingabefelder, Auswahl des Materials, Knöpfe sperren am Rand', () => {
  const opt = T.materialOptionen(materialien);
  assert.deepEqual(opt[0], { wert: 'MAT-1', text: 'MAT-1 – Testbeton' });
  const z0 = T.schichtZeileHtml(0, { material: 'MAT-1', dicke: '200' }, { r: 0.1, lambda: 2, lambdaFehlt: '' }, opt, 2);
  assert.match(z0, /<option value="MAT-1" selected>/);
  assert.match(z0, /value="200"/);
  assert.match(z0, /class="chip s-hoch"[^>]*disabled/);
  assert.doesNotMatch(z0, /class="chip s-runter"[^>]*disabled/);
  const z1 = T.schichtZeileHtml(1, { material: 'MAT-77', dicke: '' }, null, opt, 2);
  assert.match(z1, /MAT-77 \(nicht gefunden\)/);
  assert.match(z1, /class="chip s-runter"[^>]*disabled/);
});

// ---------------------------------------------------------------------------------------------- Zuweisen

test('Zuweisen: Bauteile mit Aufbau-Fähigkeit, Auswahl vorbelegt, Suche, Parameter', () => {
  const els = [
    { kennzeichen: 'W-EG-N', klasse: 'Wand', geschoss: 'EG', bezuege: { aufbau: 'AUF-1' } }, { kennzeichen: 'W-EG-S', klasse: 'Wand', geschoss: 'EG' },
    { kennzeichen: 'D-1', klasse: 'Decke', geschoss: '1.OG' }, { kennzeichen: 'F-1', klasse: 'Fenster', geschoss: 'EG' }, { kennzeichen: 'R-1', klasse: 'Raum' },
    { kennzeichen: 'T-1', klasse: 'Tuer', geschoss: 'EG' }, { kennzeichen: 'DA-1', klasse: 'Dach', geschoss: '1.OG' },
  ];
  const z = T.zuweisenZeilen(els, ['w-eg-s']);
  assert.deepEqual(z.map((x) => x.kennzeichen), ['DA-1', 'D-1', 'T-1', 'W-EG-N', 'W-EG-S'], 'nach Geschoss, Klasse, Kennzeichen – ohne Fenster und Räume');
  assert.ok(!z.some((x) => ['F-1', 'R-1'].includes(x.kennzeichen)));
  assert.equal(z.find((x) => x.kennzeichen === 'W-EG-S').gewaehlt, true);
  assert.equal(z.find((x) => x.kennzeichen === 'W-EG-N').aufbau, 'AUF-1');
  assert.deepEqual(T.zuweisenZeilen(els, [], 'auf-1').map((x) => x.kennzeichen), ['W-EG-N']);
  assert.match(T.zuweisenTabelleHtml(z), /<input type="checkbox" class="z-wahl" value="W-EG-S" checked/);
  assert.match(T.zuweisenTabelleHtml([]), /Keine Wände/);
  assert.equal(T.zuweisenPruefen(['W-1'], 'AUF-1'), true);
  assert.match(T.zuweisenPruefen([], 'AUF-1'), /mindestens ein Bauteil/);
  assert.match(T.zuweisenPruefen(['W-1'], ''), /Aufbau wählen/);
  assert.deepEqual(T.zuweisenParameter(['W-1', 'W-2'], ' AUF-1 '), { elemente: ['W-1', 'W-2'], aufbau: 'AUF-1' });
  assert.deepEqual(T.AUFBAU_KLASSEN, ['Wand', 'Decke', 'Dach', 'Tuer']);
});

// ---------------------------------------------------------------------------------------------- Fenster

test('Fenster: Auswahl nach Klasse; jeder Zahlenwert braucht die Quelle; Bereiche 0 … 1', () => {
  const a = T.fensterAuswahl([{ klasse: 'Fenster', kennzeichen: 'F-1' }, { klasse: 'Tuer', kennzeichen: 'T-1' }, { klasse: 'Wand', kennzeichen: 'W-1' }]);
  assert.equal(a.Fenster.length, 1); assert.equal(a.Tuer.length, 1); assert.equal(a.andere.length, 1);
  const f = { werte: { ug: '1,1', uf: '', g: '0.5' }, sonnenschutz: '', quelle: '' };
  assert.match(T.fensterPruefen(f).fehler[0], /^Quelle fehlt\. Kennwerte nur mit Quelle/);
  f.quelle = 'Datenblatt Testwert';
  assert.deepEqual(T.fensterPruefen(f).fehler, []);
  assert.match(T.fensterPruefen({ ...f, werte: { g: '1.5' } }).fehler[0], /g Gesamtenergiedurchlass.*zwischen 0 und 1/);
  assert.match(T.fensterPruefen({ ...f, werte: { ug: 'x' } }).fehler[0], /keine Zahl/);
  assert.match(T.fensterPruefen({ ...f, werte: { ug: '-1' } }).fehler[0], /nicht negativ/);
  assert.match(T.fensterPruefen({ werte: {}, sonnenschutz: '', quelle: '' }).fehler[0], /Mindestens einen Kennwert/);
  assert.deepEqual(T.fensterPruefen({ werte: {}, sonnenschutz: 'aussen', quelle: '' }).fehler, [], 'Sonnenschutz braucht keine Quelle');
  assert.deepEqual(T.fensterPruefen({ werte: { ud: '1.5' }, quelle: 'Test' }, 'Tuer').fehler, []);
  assert.deepEqual(T.fensterPruefen({ werte: { ug: '1.1' }, quelle: '' }, 'Tuer').fehler.length, 1, 'Ug gilt nicht für Türen');
});

test('Fenster: Parameter – nur Ausgefülltes; Quelle nur mit Zahlenwerten; Türen nur ud', () => {
  assert.deepEqual(T.fensterParameter(['F-1', 'F-2'], { werte: { ug: '1,1', uf: '1.6', psiG: '', g: '0.5', rahmenanteil: '0.3', fc: '', uw: '' }, sonnenschutz: 'aussen', quelle: ' Datenblatt Testwert ' }),
    { elemente: ['F-1', 'F-2'], ug: 1.1, uf: 1.6, g: 0.5, rahmenanteil: 0.3, sonnenschutz: 'aussen', quelle: 'Datenblatt Testwert' });
  assert.deepEqual(T.fensterParameter(['F-1'], { werte: {}, sonnenschutz: 'innen', quelle: 'unnötig' }), { elemente: ['F-1'], sonnenschutz: 'innen' });
  assert.deepEqual(T.fensterParameter(['T-1'], { werte: { ud: '1.8', ug: '1' }, sonnenschutz: 'aussen', quelle: 'Test' }, 'Tuer'), { elemente: ['T-1'], ud: 1.8, quelle: 'Test' });
  const zeilen = T.uwZeilen([{ kennzeichen: 'F-1', klasse: 'Fenster', werte: { uw: 1.2345 } }, { kennzeichen: 'F-2', klasse: 'Fenster', werte: {} }, { kennzeichen: 'T-1', klasse: 'Tuer', werte: { ud: 1.8 } }]);
  assert.deepEqual(zeilen.map((z) => z.text), ['Uw 1.235 W/(m²·K)', 'Uw fehlt', 'Ud 1.8 W/(m²·K)']);
  assert.match(T.UW_TIPP, /Verglasung dem Fenster ähnlich/);
  const tab = T.aenderungenDetailHtml([{ kennzeichen: 'F-1', detail: 'uw: – → 1.3 W/(m²·K)' }, { kennzeichen: 'F-2' }]);
  assert.match(tab, /F-1<\/td><td>uw: – → 1\.3/); assert.doesNotMatch(tab, /F-2/);
  assert.equal(T.aenderungenDetailHtml([{ kennzeichen: 'x' }]), '');
});

// ---------------------------------------------------------------------------------------------- Übergang

test('Übergang: Rsi und Rse je Gruppe mit Quelle (Pflicht); nichts erfunden', () => {
  assert.match(T.UEBERGANG_SATZ, /Ohne Rsi\/Rse rechnet KICAD nur R, keinen U-Wert\./);
  const leer = T.uebergangFormular(null);
  assert.deepEqual(T.uebergangPruefen(leer).fehler, ['Nichts geändert.']);
  const f = structuredClone(leer);
  f.horizontal = { rsi: '0,13', rse: '', quelle: 'Testwert C1a' };
  assert.match(T.uebergangPruefen(f).fehler[0], /Wärmestrom horizontal: Rsi und Rse angeben/);
  f.horizontal = { rsi: '0,13', rse: '0,04', quelle: '' };
  assert.match(T.uebergangPruefen(f).fehler[0], /Quelle fehlt\. Norm mit Ausgabe und Tabelle angeben/);
  f.horizontal.quelle = 'Testwert C1a';
  const pr = T.uebergangPruefen(f);
  assert.deepEqual(pr.fehler, []); assert.deepEqual(pr.geaendert, ['horizontal']);
  f.horizontal.rse = 'x';
  assert.match(T.uebergangPruefen(f).fehler[0], /müssen Zahlen sein/);
});

test('Übergang: Formular aus der Projektinformation; ein Aufruf je Quelle', () => {
  const info = { werte: { uebergang: { horizontal: { rsi: 0.13, rse: 0.04, quelle: 'Testwert C1a' }, erdreich: { rsi: 0.17, rse: 0, quelle: 'Testwert C1a' } } } };
  const bisher = T.uebergangFormular(info);
  assert.deepEqual(bisher.horizontal, { rsi: '0.13', rse: '0.04', quelle: 'Testwert C1a' });
  assert.deepEqual(bisher.aufwaerts, { rsi: '', rse: '', quelle: '' });
  assert.equal(bisher.erdreich.rse, '0');
  const f = structuredClone(bisher);
  assert.deepEqual(T.uebergangPruefen(f, bisher).fehler, ['Nichts geändert.']);
  f.horizontal.rsi = '0.14'; f.horizontal.quelle = 'Testwert A';
  f.aufwaerts = { rsi: '0.1', rse: '0.04', quelle: 'Testwert B' };
  f.abwaerts = { rsi: '0.17', rse: '0.04', quelle: 'Testwert B' };
  const pr = T.uebergangPruefen(f, bisher);
  assert.deepEqual(pr.geaendert, ['horizontal', 'aufwaerts', 'abwaerts']);
  const aufrufe = T.uebergangAufrufe(f, pr.geaendert);
  assert.equal(aufrufe.length, 2);
  assert.deepEqual(aufrufe[0].parameter, { quelle: 'Testwert A', horizontal: { rsi: 0.14, rse: 0.04 } });
  assert.deepEqual(aufrufe[1].parameter, { quelle: 'Testwert B', aufwaerts: { rsi: 0.1, rse: 0.04 }, abwaerts: { rsi: 0.17, rse: 0.04 } });
});

// ---------------------------------------------------------------------------------------------- Hülle prüfen, Wärmebrücken

const pruefDaten = {
  zusammenfassung: 'Offen: 3 Bauteile ohne Aufbau, 1 Aufbauten ohne U-Wert, 1 Fenster und 0 Türen ohne Kennwerte, 1 Materialien ohne λ, 1 Gruppen Rsi/Rse fehlen, 0 Aufbauten der falschen Art.',
  bauteileOhneAufbau: { anzahl: 3, nachKlasse: { Wand: 2, Decke: 1 }, liste: ['D-1', 'W-EG-N', 'W-EG-S'], gekuerzt: false },
  aufbautenOhneU: { anzahl: 1, liste: [{ aufbau: 'AUF-1', name: 'Test-AW', art: 'wand', rWert: null, fehlt: ['Schicht 2: λ von MAT-2 «Testdaemmung» fehlt (material_aendern mit werte.lambda und Quelle).'] }], gekuerzt: false },
  uebergangFehlt: { anzahl: 1, liste: [{ gruppe: 'horizontal', grund: 'nicht gesetzt', vorschlag: 'uebergang_setzen mit rsi, rse und Quelle' }], gekuerzt: false },
  fensterOhneKennwerte: { anzahl: 1, liste: [{ fenster: 'F-1', uw: null, fehlt: ['ug fehlt.', 'g fehlt (fenster_kennwerte).'] }], gekuerzt: false },
  tuerenOhneKennwerte: { anzahl: 0, liste: [], gekuerzt: false },
  materialienOhneLambda: { anzahl: 1, liste: [{ material: 'MAT-2', name: 'Testdaemmung', fehlt: 'lambda' }], gekuerzt: false },
  aufbauUnpassendeArt: { anzahl: 0, liste: [], gekuerzt: false },
};

test('Hülle prüfen: Abschnitte nur mit Inhalt, jede Zeile mit Sprung zum Element', () => {
  const a = T.pruefAbschnitte(pruefDaten);
  assert.deepEqual(a.map((x) => x.titel), ['Bauteile ohne Aufbau', 'Aufbauten ohne U-Wert', 'Rsi/Rse fehlen', 'Fenster ohne Kennwerte', 'Materialien ohne λ']);
  assert.deepEqual(a[0].zeilen.map((z) => z.kz), ['D-1', 'W-EG-N', 'W-EG-S']);
  assert.equal(a[0].zusatz, '2 Wand, 1 Decke');
  assert.equal(a[1].zeilen[0].kz, 'AUF-1');
  assert.match(a[1].zeilen[0].text, /Test-AW – Schicht 2: λ von MAT-2 «Testdaemmung» fehlt \(Dialog «Material …»\)/);
  assert.equal(a[2].zeilen[0].kz, null, 'Übergang gehört keinem Element');
  assert.match(a[3].zeilen[0].text, /ug fehlt\. g fehlt \(Dialog «Kennwerte …»\)/);
  assert.equal(a[4].zeilen[0].kz, 'MAT-2');
  const h = T.pruefHtml(pruefDaten);
  assert.match(h, /Offen: 3 Bauteile ohne Aufbau/);
  assert.equal((h.match(/class="chip p-sprung"/g) ?? []).length, 3);
  assert.match(h, /<tr class="p-sprung" data-kz="AUF-1"/);
  assert.match(h, /<td class="rot">Test-AW/);
  assert.doesNotMatch(h, /<h4>Türen ohne Kennwerte/);
});

test('Hülle prüfen: vollständige Hülle zeigt nur die Zusammenfassung; leere Antwort bricht nicht', () => {
  const leer = { zusammenfassung: 'Hülle vollständig: 2 Aufbauten, alle Bauteile mit Aufbau, alle U-Werte vorhanden.', bauteileOhneAufbau: { anzahl: 0, liste: [] }, aufbautenOhneU: { anzahl: 0, liste: [] } };
  assert.deepEqual(T.pruefAbschnitte(leer), []);
  assert.match(T.pruefHtml(leer), /class="huelle-ok"/);
  assert.deepEqual(T.pruefAbschnitte(null), []);
  assert.match(T.pruefHtml(null), /Keine Antwort/);
  assert.equal(T.pruefAbschnitte({ ...leer, aufbautenOhneU: { anzahl: 250, liste: [], gekuerzt: true } })[0].gekuerzt, true);
});

test('Wärmebrücken: Parameter, Zählung der Vorschläge, ψ-Satz', () => {
  assert.deepEqual(T.waermebrueckenParameter(''), {});
  assert.deepEqual(T.waermebrueckenParameter(' EG '), { geschoss: 'EG' });
  assert.deepEqual(T.waermebrueckenZaehlen([{ klasse: 'Waermebruecke', art: 'angelegt' }, { klasse: 'Waermebruecke', art: 'angelegt' }, { klasse: 'Waermebruecke', art: 'geaendert' }, { klasse: 'Wand', art: 'angelegt' }]),
    { neu: 2, geaendert: 1, geloescht: 0 });
  assert.match(T.WB_PSI_SATZ, /ψ fehlt/);
  const cs = kernDatei('Huelle/RaumgrenzenBefehle.cs');
  if (cs) assert.match(cs, /ψ fehlt: Der Kern hat keinen Wärmebrückenkatalog als Quelle/);
});

// ---------------------------------------------------------------------------------------------- Listen

const raumgrenzenAntwort = {
  ableitung: 'Raumgrenzen', anzahl: 3, anzahlRaeume: 2, hinweise: ['Test-Hinweis'],
  summenJeRaum: [{ raum: '1.01', flaeche_m2: 12.5, jeAngrenzung: { aussen: {} } }, { raum: '1.02', flaeche_m2: 9, jeAngrenzung: {} }],
  zeilen: [{ raum: '1.01', nachbar: 'aussen', angrenzung: 'aussen', art: 'wand', bauteil: 'W-1', aufbau: null, flaeche_m2: 10.12, azimut: 180, neigung: 90, u: null, ua_WK: null },
    { raum: '1.01', nachbar: '1.02', angrenzung: 'beheizt', art: 'boden', bauteil: 'D-1', aufbau: 'AUF-2', flaeche_m2: 12.5, azimut: null, neigung: 180, u: 0.3, ua_WK: 3.75 },
    { raum: '1.02', nachbar: 'aussen', angrenzung: 'aussen', art: 'dach', bauteil: null, aufbau: null, flaeche_m2: 9, azimut: null, neigung: 0, u: null, ua_WK: null }],
};
const huellbauteileAntwort = {
  ableitung: 'Hüllbauteile', anzahl: 1, hinweise: [],
  zeilen: [{ bauteil: 'W-1', klasse: 'Wand', geschoss: 'EG', aufbau: 'AUF-1', u: 0.36, flaecheInnen_m2: 10.12, flaecheAussen_m2: 11, azimut: 180, neigung: 90, angrenzung: 'aussen', angrenzungen: { aussen: 10.12 }, raeume: ['1.01'], hinweis: null }],
};

test('Listen: die Listenansicht wird nur gewählt, wenn sie alle Zeilen der Ableitung zeigt (sonst Tabelle im Dialog)', () => {
  assert.equal(T.ansichtZeigtZeilen('huellbauteile', huellbauteileAntwort, fmt.aufbereiten), true);
  // Befund C1a: listenformat.js nimmt bei raumgrenzen die erste Objektliste (summenJeRaum) statt der Zeilen
  const echt = T.ansichtZeigtZeilen('raumgrenzen', raumgrenzenAntwort, fmt.aufbereiten);
  assert.equal(typeof echt, 'boolean');
  assert.equal(echt, fmt.aufbereiten('raumgrenzen', raumgrenzenAntwort).tabellen[0].zeilen.length === 3 && fmt.aufbereiten('raumgrenzen', raumgrenzenAntwort).tabellen[0].spalten.some((s) => s.schluessel === 'art'));
  // Logik mit Ersatzfunktionen
  const zeigt = (zeilen, spalten) => () => ({ tabellen: [{ zeilen, spalten: spalten.map((schluessel) => ({ schluessel })) }] });
  assert.equal(T.ansichtZeigtZeilen('raumgrenzen', raumgrenzenAntwort, zeigt(raumgrenzenAntwort.zeilen, ['raum', 'art'])), true);
  assert.equal(T.ansichtZeigtZeilen('raumgrenzen', raumgrenzenAntwort, zeigt(raumgrenzenAntwort.summenJeRaum, ['raum'])), false);
  assert.equal(T.ansichtZeigtZeilen('raumgrenzen', raumgrenzenAntwort, () => { throw new Error('x'); }), false);
  assert.equal(T.ansichtZeigtZeilen('raumgrenzen', null, fmt.aufbereiten), false);
  assert.deepEqual(Object.keys(T.LISTEN), ['raumgrenzen', 'huellbauteile']);
  // der Katalog kennt beide Werte der Ansicht art liste
  const liste = klasse('Ansicht').eigenschaften.find((x) => x.name === 'liste').auswahl;
  for (const n of Object.keys(T.LISTEN)) assert.ok(liste.includes(n), n);
});

test('Listen: Ersatztabelle zeigt alle Zeilen mit Kern-Hinweisen; Klick-Schlüssel ist Bauteil bzw. Raum', () => {
  const h = T.ableitungTabelleHtml('raumgrenzen', raumgrenzenAntwort);
  assert.match(h, /3 Raumgrenzen<\/b> in 2 Räumen/);
  assert.match(h, /Test-Hinweis/);
  assert.equal((h.match(/<tr data-kz=/g) ?? []).length, 3);
  assert.match(h, /<tr data-kz="W-1"><td>1\.01<\/td><td>aussen<\/td>/);
  assert.match(h, /<tr data-kz="1\.02"/, 'ohne Bauteil: der Raum');
  assert.match(h, /<td class="zahl">10\.12<\/td>/);
  const b = T.ableitungTabelleHtml('huellbauteile', huellbauteileAntwort);
  assert.match(b, /1 Hüllbauteile/);
  assert.match(b, /Fläche innen m²/);
  assert.match(T.ableitungTabelleHtml('raumgrenzen', { zeilen: [] }), /Keine Einträge/);
  assert.match(T.ableitungTabelleHtml('raumgrenzen', null), /Keine Antwort/);
});

// ---------------------------------------------------------------------------------------------- Projektbrowser, 3D, Verdrahtung

test('Projektbrowser: Zahlen für den Knoten «Hülle»', () => {
  assert.deepEqual(T.huelleZahlen([{ klasse: 'Material' }, { klasse: 'Material' }, { klasse: 'Aufbau' }, { klasse: 'Waermebruecke' }, { klasse: 'Wand' }]), { Material: 2, Aufbau: 1, Waermebruecke: 1 });
  const q = lies('./projektbrowser.js') + lies('./projektbrowser-aeste.js');   // G1: Zeichnen/Bedienen und Äste getrennt
  for (const s of ["'w:huelle'", "'Hülle'", "'Materialien'", "'Aufbauten'", "'Wärmebrücken'", "'huelle.material'", "'huelle.aufbau'", "'huelle.zuweisen'", 'Zuweisen …'])
    assert.ok(q.includes(s), s);
  assert.match(lies('./projektbrowser-gliederung.js'), /quelle: 'huelle' \},\s*\{ quelle: 'systeme' \}/);
});

test('3D: Dach und Geschoss laden alle Körper neu (Befund B6)', () => {
  const klassen = { a: 'Dach', b: 'Geschoss', c: 'Rohr', d: 'Raum' };
  const von = (id) => klassen[id] ?? null;
  assert.equal(k3.brauchtAlleKoerper(['a'], von), true);
  assert.equal(k3.brauchtAlleKoerper(['b'], von), true);
  assert.equal(k3.brauchtAlleKoerper(['c', 'd'], von), false);
  for (const n of ['Wand', 'Tuer', 'Fenster', 'Decke']) assert.ok(k3.WIRKT_AUF_NACHBARN_KLASSEN.has(n), n);
});

test('Verdrahtung: Register Hülle nach Architektur, Gruppen, Symbole, Aktionen, Import', () => {
  const mb = lies('./menueband.js');
  assert.match(mb, /\['architektur', 'Architektur'\], \['huelle', 'Hülle'\], \['gebaeudetechnik'/);
  const folge = /huelle: \[([^\]]*)\]/.exec(mb)[1].match(/'([^']+)'/g).map((x) => x.slice(1, -1));
  assert.deepEqual(folge, ['Aufbauten', 'Materialien', 'Fenster', 'Übergang', 'Prüfen', 'Listen']);
  assert.match(mb, /architektur: \['Bauteile', 'Decke, Boden, Dach', 'Raum', 'Raster'\]/);
  assert.match(lies('./aktionen.js'), /import '\.\/aktionen-huelle\.js';/);

  const q = lies('./aktionen-huelle.js');
  const gruppen = [...q.matchAll(/register: 'huelle', gruppe: '([^']+)'/g)].map((m) => m[1]);
  assert.deepEqual([...new Set(gruppen)], folge, 'jede Gruppe des Registers hat Aktionen');
  assert.match(q, /register: 'architektur', gruppe: 'Decke, Boden, Dach'/);
  const symbole = new Set([...q.matchAll(/symbol: '([^']+)'/g)].map((m) => m[1]).concat(Object.values(T.DECKE_ARTEN).map((d) => d.symbol)));
  const unbekannt = sym.symbol('gibtsnicht');
  for (const s of symbole) assert.notEqual(sym.symbol(s), unbekannt, `Symbol «${s}» fehlt in symbole.js`);
  for (const k of ['Material', 'Aufbau', 'Waermebruecke']) assert.notEqual(sym.symbolFuerKlasse(k), 'familie', k);
  // Aktionen laufen nur über Kern-Befehle und -Abfragen
  for (const b of ['decke_erzeugen', 'material_anlegen', 'material_aendern', 'material_uebernehmen', 'aufbau_anlegen', 'aufbau_aendern', 'aufbau_zuweisen', 'fenster_kennwerte', 'uebergang_setzen', 'waermebruecken_vorschlagen'])
    assert.ok(q.includes(`'${b}'`), b);
  for (const a of ['aufbau_rechnen', 'materialien', 'huelle_pruefen']) assert.ok(q.includes(`'${a}'`), a);
});

// ---------------------------------------------------------------------------------------------- gegen den echten Kern (nur lesend)

test('Echter Kern (HLKS_TEST_SERVER): Antworten von materialien, huelle_pruefen, raumgrenzen, huellbauteile passen zu den Bausteinen', { skip: !process.env.HLKS_TEST_SERVER }, async () => {
  const s = process.env.HLKS_TEST_SERVER.replace(/\/$/, '');
  const frage = async (n, p = {}) => (await (await fetch(`${s}/api/abfrage/${n}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(p) })).json());
  const mat = await frage('materialien');
  assert.ok(Array.isArray(mat.projekt) && mat.bibliothek);
  assert.doesNotThrow(() => T.materialListeHtml(mat));
  assert.doesNotThrow(() => T.bibliothekHtml(mat.bibliothek));
  const p = await frage('huelle_pruefen');
  assert.ok(typeof p.zusammenfassung === 'string');
  assert.doesNotThrow(() => T.pruefHtml(p));
  for (const n of ['raumgrenzen', 'huellbauteile']) {
    const d = await frage(n);
    assert.ok(Array.isArray(d.zeilen), n);
    assert.doesNotThrow(() => T.ableitungTabelleHtml(n, d));
    assert.equal(typeof T.ansichtZeigtZeilen(n, d, fmt.aufbereiten), 'boolean');
  }
  for (const m of mat.projekt) {
    const f = T.materialFormular(m);
    assert.equal(f.kennzeichen, m.kennzeichen);
    assert.deepEqual(T.materialPruefen(f).fehler, [], `${m.kennzeichen} aus dem Kern muss ohne Fehler durch die Prüfung gehen`);
  }
});

// ---------------------------------------------------------------------------------------------- Listenansicht: Raumgrenzen und Hüllbauteile (listenformat.js)

const rgKern = {
  ableitung: 'Raumgrenzen', projekt: 'MFH', modellStand: { pruefsumme: 'abc', pakete: 12 }, erzeugt: '2026-10-18 12:00', anzahl: 3, anzahlRaeume: 2,
  hinweise: ['Ohne U-Wert (Aufbau ohne uWert, kein Aufbau, Fenster ohne uw): 2 Raumgrenzen', 'Flächen in Innenmassen'],
  summenJeRaum: raumgrenzenAntwort.summenJeRaum,
  summen: { jeAngrenzung: { aussen: { flaeche_m2: 19.12, ua_WK: 0, ohneU_m2: 19.12 }, beheizt: { flaeche_m2: 12.5, ua_WK: 3.75, ohneU_m2: 0 } }, jeArt: { wand: 10.12 } },
  zeilen: raumgrenzenAntwort.zeilen.map((z) => ({ ...z, laenge_m: 3, hoehe_m: 2.9, uQuelle: 'U = 1/(…)' })),
};

test('Listenansicht Raumgrenzen: eine Zeile je Raumgrenze mit deutschen Spalten, Klick wählt den Raum', () => {
  const a = fmt.aufbereiten('raumgrenzen', rgKern);
  assert.equal(a.titel, 'Raumgrenzen');
  const t = a.tabellen[0];
  assert.equal(t.zeilen.length, 3, 'nicht summenJeRaum (2 Zeilen)');
  assert.equal(t.schluessel, 'raum');
  assert.deepEqual(t.spalten.map((s) => s.titel), ['Raum', 'Art', 'Nachbar', 'Angrenzung', 'Bauteil', 'Aufbau', 'Fläche m²', 'Azimut °', 'Neigung °', 'U W/(m²·K)', 'U·A W/K']);
  assert.ok(!t.spalten.some((s) => ['laenge_m', 'hoehe_m', 'uQuelle'].includes(s.schluessel)), 'Hilfsfelder verborgen');
  assert.ok(t.spalten.filter((s) => s.art === 'zahl').map((s) => s.schluessel).includes('ua_WK'));
});

test('Listenansicht Raumgrenzen: Hinweise des Kerns über der Tabelle, Summen je Angrenzung als Fusszeile', () => {
  const a = fmt.aufbereiten('raumgrenzen', rgKern);
  assert.equal(a.zusammenfassung[0], '3 Raumgrenzen in 2 Räumen');
  assert.deepEqual(a.zusammenfassung.slice(1), rgKern.hinweise);
  const fuss = a.tabellen[0].fuss;
  assert.equal(fuss.length, 2);
  assert.deepEqual(fuss[0], { raum: 'Summe aussen (ohne U: 19,12 m²)', flaeche_m2: 19.12, ua_WK: 0 });
  assert.deepEqual(fuss[1], { raum: 'Summe beheizt', flaeche_m2: 12.5, ua_WK: 3.75 });
  assert.equal(fmt.formatWert('flaeche_m2', fuss[1].flaeche_m2), '12,50');
  assert.equal(fmt.formatWert('u', 0.3621), '0,362');
  assert.equal(fmt.aufbereiten('raumgrenzen', { ...rgKern, anzahl: 1, anzahlRaeume: 1, hinweise: [] }).zusammenfassung[0], '1 Raumgrenze in 1 Raum');
  assert.equal(fmt.aufbereiten('raumgrenzen', { ableitung: 'Raumgrenzen', anzahl: 0, zeilen: [] }).tabellen[0].fuss, undefined, 'ohne Zeilen keine Fusszeile');
  assert.equal(fmt.aufbereiten('raumliste', { zeilen: [{ raum: 'a' }] }).tabellen[0].fuss, undefined, 'andere Listen unverändert');
});

test('Listenansicht Hüllbauteile: Spalten, Fusszeile mit Summen je Angrenzung, Klick wählt das Bauteil', () => {
  const d = { ...huellbauteileAntwort, summen: { aussen: { flaecheInnen_m2: 10.12, flaecheAussen_m2: 11, bauteile: 1 }, erdreich: { flaecheInnen_m2: 20, flaecheAussen_m2: 22, bauteile: 2 } },
    hinweise: ['Fläche innen = Summe der Raumgrenzen (Innenmass)'], zeilen: huellbauteileAntwort.zeilen.map((z) => ({ ...z, uQuelle: 'AUF-1.uWert' })) };
  const a = fmt.aufbereiten('huellbauteile', d);
  assert.equal(a.titel, 'Hüllbauteile');
  assert.deepEqual(a.zusammenfassung, ['1 Hüllbauteil', 'Fläche innen = Summe der Raumgrenzen (Innenmass)']);
  const t = a.tabellen[0];
  assert.equal(t.schluessel, 'bauteil');
  assert.deepEqual(t.spalten.slice(0, 7).map((s) => s.titel), ['Bauteil', 'Klasse', 'Geschoss', 'Aufbau', 'U W/(m²·K)', 'Fläche innen m²', 'Fläche aussen m²']);
  assert.ok(t.spalten.some((s) => s.titel === 'Orientierung (Azimut °)') && t.spalten.some((s) => s.titel === 'Angrenzung'));
  assert.ok(!t.spalten.some((s) => ['uQuelle', 'angrenzungen'].includes(s.schluessel)));
  assert.deepEqual(t.fuss, [
    { bauteil: 'Summe aussen (1 Bauteil)', flaecheInnen_m2: 10.12, flaecheAussen_m2: 11 },
    { bauteil: 'Summe erdreich (2 Bauteile)', flaecheInnen_m2: 20, flaecheAussen_m2: 22 }]);
});

test('Listenansicht: die Aktion «Raumgrenzen» nimmt jetzt die Listenansicht, liste.js kennt beide Namen und zeichnet Fusszeilen', () => {
  assert.equal(T.ansichtZeigtZeilen('raumgrenzen', rgKern, fmt.aufbereiten), true);
  assert.equal(T.ansichtZeigtZeilen('huellbauteile', huellbauteileAntwort, fmt.aufbereiten), true);
  const l = lies('../ansichten/liste.js');
  assert.match(l, /NAMEN = \['raumliste', 'strangliste', 'mengen', 'raumgrenzen', 'huellbauteile'\]/);
  assert.match(l, /t\.fuss/); assert.match(l, /li-fuss/);
  assert.match(lies('../../css/oberflaeche.css'), /\.li-tabelle tfoot td/);
});

// ---------------------------------------------------------------------------------------------- Eigenschaftenpalette: Hinweise und «ohne Quelle»

test('Palette: «ohne Quelle» nur bei Kennwerten ohne Quellenbeleg; gerechnete Werte und andere Eigenschaften nie', () => {
  const mit = 'mensch, T, 2026-10-18 12:07, Testwert C1a';
  const mitKomma = 'mensch, T, 2026-10-18 12:07, Datenblatt Hersteller, 2024';
  const ohne = 'mensch, T, 2026-10-18 12:07';
  assert.equal(T.ohneQuelle('Fenster', 'ug', ohne), true);
  assert.equal(T.ohneQuelle('Fenster', 'ug', mit), false);
  assert.equal(T.ohneQuelle('Fenster', 'ug', mitKomma), false);
  assert.equal(T.ohneQuelle('Material', 'lambda', ohne), true);
  assert.equal(T.ohneQuelle('Tuer', 'ud', ohne), true);
  assert.equal(T.ohneQuelle('Aufbau', 'uWert', ohne), true);
  assert.equal(T.ohneQuelle('Fenster', 'uw', 'berechnung, T, 2026-10-18 12:07, Uw = (Ag·Ug …)'), false, 'gerechnet');
  assert.equal(T.ohneQuelle('Fenster', 'uw', 'berechnung, T, 2026-10-18 12:07'), false, 'gerechnet, auch ohne Quellenzeile');
  assert.equal(T.ohneQuelle('Fenster', 'breite', ohne), false, 'kein Kennwert mit Quellenpflicht');
  assert.equal(T.ohneQuelle('Wand', 'ug', ohne), false);
  assert.equal(T.ohneQuelle('Fenster', 'ug', 'unbekanntes Format'), false, 'Herkunft unbekannt: keine Marke');
  assert.equal(T.ohneQuelle('Fenster', 'ug', undefined), false);
  assert.match(T.ohneQuelleMarke('Fenster', 'uf', ohne), /<span class="ep-ohnequelle" title="[^"]*">ohne Quelle<\/span>/);
  assert.equal(T.ohneQuelleMarke('Fenster', 'uf', mit), '');
});

test('Palette: die Liste der Kennwerte mit Quellenpflicht entspricht der Nachführung im Kern', () => {
  const cs = kernDatei('Huelle/AufbauNachfuehrung.cs');
  if (!cs) return;
  const liste = (n) => [...new RegExp(`${n} = \\[([^\\]]*)\\]`).exec(cs)[1].matchAll(/"([^"]+)"/g)].map((m) => m[1]);
  assert.deepEqual(T.QUELLE_PFLICHT.Material, liste('MaterialKennwerte'));
  assert.deepEqual(T.QUELLE_PFLICHT.Fenster, liste('FensterKennwerte'));
  assert.match(cs, /IstArt\(e, "Tuer"\)\) PruefeQuellen\(e, \["ud"\]/);
  assert.match(cs, /PruefeQuellen\(e, \["uWert"\]/);
  for (const [k, namen] of Object.entries(T.QUELLE_PFLICHT)) for (const n of namen) assert.ok(klasse(k).eigenschaften.some((e) => e.name === n), `${k}.${n} im Katalog`);
});

test('Palette: Hinweise des Kerns zu einem angenommenen Befehl – ohne «Keine Änderung», ohne Doppelte, mit Namen der Knöpfe', () => {
  const kern = ['Kennwerte ohne Quelle: F-2 ug, uf. Kennwerte brauchen eine Quelle; in der Rechnung gelten λ, Ug, Uf, ψg, Rahmenanteil, Rsi und Rse ohne Quelle als fehlend (material_aendern, fenster_kennwerte, uebergang_setzen verlangen sie).',
    'F-2: kein Uw, es fehlt: Ug hat keine Quelle und gilt nicht (fenster_kennwerte mit ug und Quelle).'];
  const h = T.paletteHinweise([...kern, '', 'Keine Änderung.', kern[0]]);
  assert.equal(h.length, 2);
  assert.match(h[0], /^Kennwerte ohne Quelle: F-2 ug, uf\./);
  assert.match(h[0], /«Material …»/);
  assert.doesNotMatch(h[1], /fenster_kennwerte/);
  assert.deepEqual(T.paletteHinweise(null), []);
  assert.deepEqual(T.paletteHinweise(['Keine Änderung.']), []);
  const html = T.paletteHinweiseHtml(['A <b>', 'B']);
  assert.match(html, /class="ep-hinweise" role="status"/);
  assert.match(html, /Hinweise des Kerns/);
  assert.match(html, /<li>A &lt;b&gt;<\/li>/);
  assert.match(T.paletteHinweiseHtml(['A']), /<b>Hinweis des Kerns<\/b>/);
  assert.equal(T.paletteHinweiseHtml([]), '');
});

test('Palette: Verdrahtung in eigenschaften.js und CSS im bestehenden Stil', () => {
  const q = lies('./eigenschaften.js');
  assert.match(q, /import \{ ohneQuelleMarke, paletteHinweise, paletteHinweiseHtml \} from '\.\/huelle-text\.js'/);
  assert.match(q, /paletteHinweise\(r\.hinweise\)/);
  assert.match(q, /ohneQuelleMarke\(el\.klasse, d\.name, werte\[d\.name\]\.herkunft\)/);
  assert.match(q, /hinweise = null/);
  const css = lies('../../css/oberflaeche.css');
  assert.match(css, /\.ep-hinweise \{[^}]*var\(--ob-warnung-flaeche\)/);
  assert.match(css, /\.ep-ohnequelle \{/);
  assert.match(q, /ep-marke/);
  assert.match(css, /\.ep-marke \{ grid-column: 1 \/ -1/);
});
