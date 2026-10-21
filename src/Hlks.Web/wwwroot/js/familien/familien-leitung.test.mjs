// Tests der Seite «Leitungsserie» im Familieneditor (Modul 3, Auftrag C3) ohne Browser: Minimalvorlage für Rohr und Kanal, Definition → Formular, Formular → Definition
// (die ops für familie_aendern), Validierung, Kontrolltabelle aus der Abfrage leitungsserie und die Verdrahtung. Aufruf:
//   node --test src/Hlks.Web/wwwroot/js/familien/familien-leitung.test.mjs
// Die Testserien sind die fiktiven Familien des Hauptlaufs (tests/daten/familien/test_*.familie.json): alle Werte FIKTIV.
// Mit HLKS_TEST_SERVER (Server mit offenem Projekt) prüft ein zusätzlicher Test gegen den echten Kern (nur Trockenläufe), dass er die Vorlagen und alle ops annimmt
// und Fehler mit verständlicher Meldung ablehnt.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const T = await import('./familien-text.js');
const L = await import('./editor-leitung.js');

const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const famDatei = (n) => JSON.parse(lies(`../../../../../tests/daten/familien/${n}.familie.json`));
const rohr = famDatei('test_rohrserie');
const kanal = famDatei('test_kanalserie');
const abwasser = famDatei('test_abwasserserie');
const klon = (x) => JSON.parse(JSON.stringify(x));

// Familien des Projekts (Antwort der Abfrage familien, gekürzt)
const familien = [
  { kennzeichen: 'FAM-2', name: 'Testbogen (fiktiv)', id: 'fam-test-bogen', kategorie: 'Bogen', gueltig: true },
  { kennzeichen: 'FAM-3', name: 'Test-T-Stück egal (fiktiv)', id: 'fam-test-tstueck', kategorie: 'TStueck', gueltig: true },
  { kennzeichen: 'FAM-4', name: 'Testreduktion (fiktiv)', id: 'fam-test-reduktion', kategorie: 'Reduktion', gueltig: true },
  { kennzeichen: 'FAM-5', name: 'Zweiter Bogen', id: 'fam-zweiter-bogen', kategorie: 'Bogen', gueltig: false },
  { kennzeichen: 'FAM-6', name: 'Heizkörper', id: 'fam-hk', kategorie: 'Heizkoerper', gueltig: true },
];
const klassen = [
  { name: 'Leitung', basis: null }, { name: 'Rohr', basis: 'Leitung' }, { name: 'Kanal', basis: 'Leitung' }, { name: 'Heizkoerper', basis: null },
  { name: 'Waermeleitung', basis: 'Rohr' },
];

/** Wendet die ops der Oberfläche an wie der Kern (parameter_setzen ersetzt oder hängt an, leitung_setzen ersetzt den Block). */
function anwenden(def, ops) {
  const d = klon(def);
  for (const op of ops) {
    if (op.op === 'parameter_setzen') { const i = d.parameter.findIndex((p) => p.name === op.parameter.name); if (i >= 0) d.parameter[i] = op.parameter; else d.parameter.push(op.parameter); }
    else if (op.op === 'leitung_setzen') d.leitung = op.leitung;
    else throw new Error(`op ${op.op} unerwartet`);
  }
  return d;
}
const feld = (def, pfad, o = {}) => T.leitungFelder(def, { art: T.leitungsArt(def.kategorie, klassen), familien, ...o }).find((f) => f.pfad === pfad);

// ================================================================================================ Minimalvorlage

test('Vorlage Rohr: Block «leitung» rund mit dn und da als Typparameter ohne Werte, ein Typ «Standard» ohne Werte', () => {
  const d = T.familieEntwurfMinimal('Rohr', ' Mein Rohr ');
  assert.equal(d.name, 'Mein Rohr'); assert.equal(d.kategorie, 'Rohr');
  assert.deepEqual(d.leitung, { querschnitt: 'rund', groesse: { dn: 'dn' }, aussen: 'da' });
  assert.deepEqual(d.parameter.map((p) => [p.name, p.art, p.datentyp]), [['dn', 'typ', 'dn'], ['da', 'typ', 'laenge']]);
  assert.equal(d.parameter[0].ziel, 'dn', 'wie die Testserie: die Grösse setzt das Rohr-Element');
  for (const p of d.parameter) { assert.equal(p.wert, undefined, 'kein Vorgabewert: Werte kommen aus der Unterlage'); assert.equal(p.formel, undefined); assert.equal(p.gruppe, 'abmessung'); }
  assert.deepEqual(d.typen, [{ name: 'Standard', werte: {} }]);
  assert.deepEqual(d.koerper, []); assert.deepEqual(d.anschluesse, []);
  assert.equal(d.leitung.innen, undefined); assert.equal(d.leitung.formteile, undefined, 'keine erfundenen Formteil-Familien');
});

test('Vorlage Kanal: rund (Ø d) oder rechteckig (b × h); die Form steht in der Auswahl, die erste Form der Kategorie ist die Vorgabe', () => {
  const rund = T.familieEntwurfMinimal('Kanal', 'K1');
  assert.deepEqual(rund.leitung, { querschnitt: 'rund', groesse: { d: 'd' }, aussen: 'da' });
  assert.deepEqual(rund.parameter.map((p) => p.name), ['d', 'da']);
  assert.equal(rund.parameter[0].datentyp, 'laenge', 'Kanalmasse sind Längen in mm');
  assert.equal(rund.parameter[0].ziel, undefined, 'kein erfundenes Ziel');
  const eck = T.familieEntwurfMinimal('Kanal', 'K2', { form: 'kanal-rechteck' });
  assert.deepEqual(eck.leitung, { querschnitt: 'rechteck', groesse: { b: 'b', h: 'h' }, aussen: 'b' }, 'bei rechteckig nicht genutzt, aber Pflicht (wie die Testserie)');
  assert.deepEqual(eck.parameter.map((p) => p.name), ['b', 'h']);
  // eine Form, die zur Kategorie nicht passt, fällt auf die erste der Kategorie zurück (Rohr ist immer rund)
  assert.deepEqual(T.familieEntwurfMinimal('Rohr', 'R', { form: 'kanal-rechteck' }).leitung.querschnitt, 'rund');
  assert.deepEqual(T.leitungFormen('rohr').map(([f]) => f), ['rohr']);
  assert.deepEqual(T.leitungFormen('kanal').map(([f]) => f), ['kanal-rund', 'kanal-rechteck']);
  assert.deepEqual(T.leitungFormen('leitung').map(([f]) => f), ['rohr', 'kanal-rund', 'kanal-rechteck']);
  assert.deepEqual(T.leitungFormen(null), []);
});

test('Art der Leitungsserie: Rohr, Kanal, abgeleitete Klassen über basis; andere Kategorien bleiben beim bisherigen Entwurf', () => {
  assert.equal(T.leitungsArt('Rohr'), 'rohr'); assert.equal(T.leitungsArt('Kanal'), 'kanal'); assert.equal(T.leitungsArt('Leitung'), 'leitung');
  assert.equal(T.leitungsArt('Waermeleitung', klassen), 'rohr');
  assert.equal(T.leitungsArt('Waermeleitung'), null, 'ohne Katalog ist eine unbekannte Klasse keine Leitung');
  assert.equal(T.leitungsArt('Heizkoerper', klassen), null);
  assert.equal(T.leitungsArt(undefined, klassen), null);
  const d = T.familieEntwurfMinimal('Waermeleitung', 'W', { klassen });
  assert.equal(d.leitung.querschnitt, 'rund'); assert.deepEqual(d.leitung.groesse, { dn: 'dn' });
  // keine Leitung: unverändert, kein Block
  const hk = T.familieEntwurfMinimal('Heizkoerper', 'H', { klassen });
  assert.deepEqual(hk, { format: 1, name: 'H', kategorie: 'Heizkoerper', parameter: [], typen: [{ name: 'Standard', werte: {} }], koerper: [], anschluesse: [] });
  assert.equal('leitung' in hk, false);
});

// ================================================================================================ Definition → Formular

test('Definition → Formular: Testrohrserie mit allen Feldern, Werten und Gruppen', () => {
  const f = T.leitungFelder(rohr, { art: 'rohr', familien });
  assert.deepEqual(f.map((x) => x.pfad), ['querschnitt', 'einsatz', 'groesse.dn', 'aussen', 'innen', 'wand', 'kuerzel',
    'formteile.bogen.familie', 'formteile.bogen.typ', 'formteile.tstueck.familie', 'formteile.tstueck.typ', 'formteile.reduktion.familie', 'formteile.reduktion.typ', 'formteile.reinigungsoeffnung.familie', 'formteile.reinigungsoeffnung.typ',
    'regeln.winkel', 'regeln.biegeradius', 'regeln.abzweig', 'regeln.abzweigwinkel']);
  const w = (p) => feld(rohr, p);
  assert.equal(w('querschnitt').wert, 'rund');
  assert.deepEqual(w('querschnitt').optionen, [['rund', 'rund']], 'ein Rohr ist immer rund');
  assert.equal(w('groesse.dn').wert, 'dn');
  assert.deepEqual(w('groesse.dn').optionen, [['dn', 'dn'], ['da', 'da'], ['s', 's'], ['di', 'di']], 'alle Typparameter mit Zahl (auch mit Formel – der Kern entscheidet)');
  assert.equal(w('aussen').text, 'da'); assert.equal(w('innen').text, 'di'); assert.equal(w('wand').text, 's');
  assert.equal(w('formteile.bogen.familie').wert, 'fam-test-bogen');
  assert.equal(w('formteile.reinigungsoeffnung.familie').wert, '');
  assert.equal(w('regeln.winkel').text, '15, 30, 45, 90');
  assert.equal(w('regeln.abzweig').wert, 'tstueck');
  assert.equal(w('regeln.abzweigwinkel').text, '');
  assert.equal(w('regeln.biegeradius').text, '');
  assert.deepEqual([...new Set(f.map((x) => x.gruppe))], ['Querschnitt und Grösse', 'Masse (Formeln aus den Parametern)', 'Beschriftung', 'Formteile (Familien des Projekts)', 'Regeln']);
  assert.equal(w('kuerzel').text, '', 'die Testrohrserie hat kein Kürzel');
});

// ================================================================================================ Kürzel (Nachtrag B9)

test('Kürzel (Werkstoff): Feld «Kürzel (Werkstoff)» mit Hinweis, nur bei Rohren; Wert aus dem Block; Eingabe → leitung.kuerzel, leer entfernt es', () => {
  const k = feld(rohr, 'kuerzel');
  assert.equal(k.label, 'Kürzel (Werkstoff)');
  assert.equal(k.typ, 'text');
  assert.equal(k.gruppe, 'Beschriftung');
  assert.equal(k.hinweis, 'für die Beschriftung, z. B. CNS, PE-S, PEX');
  assert.equal(k.text, '');
  // Wert aus der Definition (FIKTIVES Kürzel)
  const mit = klon(rohr); mit.leitung.kuerzel = 'XY';
  assert.equal(feld(mit, 'kuerzel').text, 'XY');
  // Eingabe: Leerzeichen am Rand fallen weg, der übrige Block bleibt
  let r = T.leitungAenderung(rohr, 'rohr', 'kuerzel', 'text', '  XY-1 ');
  assert.deepEqual(r.ops, [{ op: 'leitung_setzen', leitung: { ...rohr.leitung, kuerzel: 'XY-1' } }]);
  // leer entfernt das Feld
  r = T.leitungAenderung(mit, 'rohr', 'kuerzel', 'text', '   ');
  assert.equal('kuerzel' in r.ops[0].leitung, false);
  assert.deepEqual(r.ops[0].leitung, rohr.leitung);
  // Länge und Zeichen prüft der Kern: die Seite sendet weiter (hier 13 Zeichen)
  assert.equal(T.leitungAenderung(rohr, 'rohr', 'kuerzel', 'text', '1234567890123').ops[0].leitung.kuerzel, '1234567890123');
  // Kanäle: Kürzel wirkt nur bei Rohren, das Feld entfällt
  assert.equal(feld(kanal, 'kuerzel'), undefined);
  // Sprung aus der Prüfliste
  assert.deepEqual(T.stelleZiel('leitung.kuerzel'), { tab: 'leitung', name: '', feld: 'kuerzel' });
  // HTML: Textfeld mit Wert, Platzhalter und Hinweis
  const html = L.formularHtml(mit, { art: 'rohr', familien });
  assert.match(html, /<h4 class="fe-abschnitt">Beschriftung<\/h4>/);
  assert.match(html, /data-pfad="kuerzel" data-ftyp="text"><input type="text"[^>]*value="XY"/);
  assert.match(html, /Kürzel \(Werkstoff\)/);
  assert.match(html, /<span class="leise fe-feldhinweis">für die Beschriftung, z\. B\. CNS, PE-S, PEX<\/span>/);
  // das Seitenelement liefert die op wie die übrigen Felder
  const el = { value: 'PEX', closest: () => ({ dataset: { pfad: 'kuerzel', ftyp: 'text' } }) };
  assert.equal(L.leitungTab.aenderung(el, { def: rohr, klassen: () => klassen }).ops[0].leitung.kuerzel, 'PEX');
});

test('Grössenparameter: nur Typparameter mit Zahl (DN, Länge, Zahl) – keine Exemplar-, Text- oder Ja/Nein-Parameter', () => {
  const d = klon(rohr);
  d.parameter.push({ name: 'ex', art: 'exemplar', datentyp: 'laenge' }, { name: 'txt', art: 'typ', datentyp: 'text' }, { name: 'jn', art: 'typ', datentyp: 'ja_nein' }, { name: 'z', art: 'typ', datentyp: 'zahl' });
  assert.deepEqual(T.groessenParameter(d), ['dn', 'da', 's', 'di', 'z']);
});

test('Definition → Formular: Kanalserie rechteckig mit b und h, beide Querschnitte wählbar; Abwasserserie mit Abzweigwinkeln', () => {
  const f = T.leitungFelder(kanal, { art: 'kanal', familien });
  assert.deepEqual(f.filter((x) => x.pfad.startsWith('groesse.')).map((x) => [x.pfad, x.wert]), [['groesse.b', 'b'], ['groesse.h', 'h']]);
  assert.deepEqual(f[0].optionen.map(([w]) => w), ['rund', 'rechteck']);
  assert.equal(f.find((x) => x.pfad === 'regeln.winkel').text, '45, 90');
  const a = T.leitungFelder(abwasser, { art: 'rohr', familien });
  assert.equal(a.find((x) => x.pfad === 'regeln.winkel').text, '15, 30, 45, 88.5');
  assert.equal(a.find((x) => x.pfad === 'regeln.abzweigwinkel').text, '45, 88.5');
  // «frei» steht als Wort
  const frei = klon(rohr); frei.leitung.regeln.winkel = 'frei';
  assert.equal(feld(frei, 'regeln.winkel').text, 'frei');
  // ohne Winkelangabe: leer
  const ohne = klon(rohr); delete ohne.leitung.regeln.winkel;
  assert.equal(feld(ohne, 'regeln.winkel').text, '');
  // der Grössenschlüssel der Basisklasse folgt dem Block (Kern: d, sonst dn)
  const bl = klon(kanal); bl.leitung = { querschnitt: 'rund', groesse: { d: 'b' }, aussen: 'b' };
  assert.deepEqual(T.groesseSchluessel(bl.leitung, 'leitung'), ['d']);
  assert.deepEqual(T.groesseSchluessel({ querschnitt: 'rund', groesse: { dn: 'dn' } }, 'leitung'), ['dn']);
});

test('Auswahl der Formteil-Familien: nur die Kategorie der Art; was der Block nennt und im Projekt fehlt, bleibt sichtbar', () => {
  const b = feld(rohr, 'formteile.bogen.familie');
  assert.deepEqual(b.optionen, [['fam-test-bogen', 'FAM-2 · Testbogen (fiktiv)'], ['fam-zweiter-bogen', 'FAM-5 · Zweiter Bogen (ungültig)']].sort((x, y) => x[1].localeCompare(y[1], 'de')));
  assert.ok(!b.optionen.some(([w]) => w === 'fam-hk'), 'Heizkörper ist kein Bogen');
  assert.equal(b.hinweis, undefined);
  // fehlende Familie im Projekt
  const d = klon(rohr); d.leitung.formteile.bogen.familie = 'fam-weg';
  const fb = feld(d, 'formteile.bogen.familie');
  assert.ok(fb.optionen.some(([w, t]) => w === 'fam-weg' && /nicht im Projekt/.test(t)));
  // keine Familie der Kategorie → Hinweis mit Weg
  const r = feld(rohr, 'formteile.reinigungsoeffnung.familie', { familien: [] });
  assert.match(r.hinweis, /keine Familie der Kategorie Reinigungsoeffnung/);
  // Typregel nur mit gewählter Familie
  assert.equal(feld(rohr, 'formteile.bogen.typ').deaktiviert, false);
  assert.equal(feld(rohr, 'formteile.reinigungsoeffnung.typ').deaktiviert, true);
  assert.deepEqual(T.formteilOptionen([], 'Bogen', ''), []);
});

// ================================================================================================ Formular → Definition

test('Formular → Definition: Bogenwinkel als Liste, «frei» oder leer', () => {
  const a = (text) => T.leitungAenderung(rohr, 'rohr', 'regeln.winkel', 'winkel', text);
  assert.deepEqual(a('15, 45, 90').ops, [{ op: 'leitung_setzen', leitung: { ...rohr.leitung, regeln: { winkel: [15, 45, 90], abzweig: 'tstueck' } } }]);
  assert.deepEqual(a('45 90°').ops[0].leitung.regeln.winkel, [45, 90]);
  assert.deepEqual(a('88.5;45').ops[0].leitung.regeln.winkel, [88.5, 45]);
  assert.equal(a(' Frei ').ops[0].leitung.regeln.winkel, 'frei');
  assert.equal('winkel' in a('').ops[0].leitung.regeln, false, 'leer = nicht angegeben');
  // das Original bleibt unverändert
  assert.deepEqual(rohr.leitung.regeln.winkel, [15, 30, 45, 90]);
});

test('Formular → Definition: Abzweigwinkel, Biegeradius und Abzweig; leere Regeln entfallen ganz', () => {
  const d = klon(rohr);
  let r = T.leitungAenderung(d, 'rohr', 'regeln.abzweigwinkel', 'liste', '45, 88.5');
  assert.deepEqual(r.ops[0].leitung.regeln.abzweigwinkel, [45, 88.5]);
  r = T.leitungAenderung(d, 'rohr', 'regeln.biegeradius', 'zahlopt', '2 * da');
  assert.equal(r.ops[0].leitung.regeln.biegeradius, '2 * da', 'Formeln bleiben Text');
  r = T.leitungAenderung(d, 'rohr', 'regeln.biegeradius', 'zahlopt', '60');
  assert.equal(r.ops[0].leitung.regeln.biegeradius, 60);
  r = T.leitungAenderung(d, 'rohr', 'regeln.abzweig', 'auswahl', '');
  assert.equal('abzweig' in r.ops[0].leitung.regeln, false);
  const nur = { ...klon(rohr), leitung: { querschnitt: 'rund', groesse: { dn: 'dn' }, aussen: 'da', regeln: { abzweig: 'tstueck' } } };
  r = T.leitungAenderung(nur, 'rohr', 'regeln.abzweig', 'auswahl', '');
  assert.equal('regeln' in r.ops[0].leitung, false, 'der leere Block regeln entfällt');
});

test('Formular → Definition: Masse als Zahl oder Formel; aussen ist Pflicht, innen und wand sind optional', () => {
  let r = T.leitungAenderung(rohr, 'rohr', 'aussen', 'zahl', ' dn + 5 ');
  assert.equal(r.ops[0].leitung.aussen, 'dn + 5');
  r = T.leitungAenderung(rohr, 'rohr', 'aussen', 'zahl', '25,5');
  assert.equal(r.ops[0].leitung.aussen, 25.5, 'Dezimalkomma wie in den anderen Feldern');
  r = T.leitungAenderung(rohr, 'rohr', 'aussen', 'zahl', '= da');
  assert.equal(r.ops[0].leitung.aussen, 'da');
  assert.match(T.leitungAenderung(rohr, 'rohr', 'aussen', 'zahl', '  ').fehler, /Wert fehlt/);
  r = T.leitungAenderung(rohr, 'rohr', 'innen', 'zahlopt', '');
  assert.equal('innen' in r.ops[0].leitung, false);
  assert.equal(r.ops[0].leitung.wand, 's');
  r = T.leitungAenderung(rohr, 'rohr', 'wand', 'zahlopt', '3');
  assert.equal(r.ops[0].leitung.wand, 3);
});

test('Formular → Definition: Formteil-Familie setzen, wechseln und entfernen (mit Typregel); Typregel setzen und löschen', () => {
  let r = T.leitungAenderung(rohr, 'rohr', 'formteile.reinigungsoeffnung.familie', 'auswahl', 'fam-reinigung');
  assert.deepEqual(r.ops[0].leitung.formteile.reinigungsoeffnung, { familie: 'fam-reinigung' });
  r = T.leitungAenderung(rohr, 'rohr', 'formteile.bogen.familie', 'auswahl', 'fam-zweiter-bogen');
  assert.deepEqual(r.ops[0].leitung.formteile.bogen, { familie: 'fam-zweiter-bogen' });
  const mit = klon(rohr); mit.leitung.formteile.bogen = { familie: 'fam-test-bogen', typ: 'if(dn = 15, "DN 15", "DN 20")' };
  r = T.leitungAenderung(mit, 'rohr', 'formteile.bogen.familie', 'auswahl', '');
  assert.equal('bogen' in r.ops[0].leitung.formteile, false, 'ohne Familie entfällt die ganze Art samt Typregel');
  assert.ok(r.ops[0].leitung.formteile.tstueck);
  r = T.leitungAenderung(mit, 'rohr', 'formteile.bogen.typ', 'typregel', '= if(dn = 15, "DN 15", "DN 20")');
  assert.equal(r.ops[0].leitung.formteile.bogen.typ, 'if(dn = 15, "DN 15", "DN 20")');
  r = T.leitungAenderung(mit, 'rohr', 'formteile.bogen.typ', 'typregel', '');
  assert.deepEqual(r.ops[0].leitung.formteile.bogen, { familie: 'fam-test-bogen' });
  // alle Formteile entfernt: «formteile» entfällt (der Block bleibt gültig, die Serie nennt dann keine Formteile)
  const nurEins = { ...klon(rohr), leitung: { ...klon(rohr.leitung), formteile: { bogen: { familie: 'x' } } } };
  assert.equal('formteile' in T.leitungAenderung(nurEins, 'rohr', 'formteile.bogen.familie', 'auswahl', '').ops[0].leitung, false);
});

test('Formular → Definition: Grössenparameter wählen', () => {
  const r = T.leitungAenderung(rohr, 'rohr', 'groesse.dn', 'auswahl', 'da');
  assert.deepEqual(r.ops[0].leitung.groesse, { dn: 'da' });
  assert.equal(r.ops.length, 1);
});

test('Querschnitt wechseln (Kanal rund → rechteckig): Grösse b und h, fehlende Typparameter kommen ohne Werte dazu – in einem Paket', () => {
  const rund = T.familieEntwurfMinimal('Kanal', 'K');
  const r = T.leitungAenderung(rund, 'kanal', 'querschnitt', 'auswahl', 'rechteck');
  assert.deepEqual(r.ops.map((o) => o.op), ['parameter_setzen', 'parameter_setzen', 'leitung_setzen']);
  assert.deepEqual(r.ops.slice(0, 2).map((o) => o.parameter.name), ['b', 'h']);
  for (const o of r.ops.slice(0, 2)) { assert.equal(o.parameter.art, 'typ'); assert.equal(o.parameter.wert, undefined); }
  assert.deepEqual(r.ops[2].leitung, { querschnitt: 'rechteck', groesse: { b: 'b', h: 'h' }, aussen: 'da' });
  // und zurück: die Parameter b und h bleiben, d wird neu angelegt
  const eck = anwenden(rund, r.ops);
  const zurueck = T.leitungAenderung(eck, 'kanal', 'querschnitt', 'auswahl', 'rund');
  assert.deepEqual(zurueck.ops.map((o) => o.op), ['leitung_setzen'], 'd gibt es schon in der Vorlage');
  assert.deepEqual(zurueck.ops[0].leitung.groesse, { d: 'd' });
  // gibt es den Parameter schon (Testserie «b», «h»), wird er verwendet
  const k2 = T.leitungAenderung(kanal, 'kanal', 'querschnitt', 'auswahl', 'rund');
  assert.deepEqual(k2.ops.map((o) => o.op), ['parameter_setzen', 'leitung_setzen']);
  assert.equal(k2.ops[0].parameter.name, 'd');
  // Konflikt: ein Parameter gleichen Namens, der keine Grösse sein kann
  const kaputt = klon(rund); kaputt.parameter.push({ name: 'b', art: 'exemplar', datentyp: 'text' });
  assert.match(T.leitungAenderung(kaputt, 'kanal', 'querschnitt', 'auswahl', 'rechteck').fehler, /«b» gibt es schon.*kein Typparameter mit Zahl/);
  assert.match(T.leitungAenderung(rund, 'kanal', 'querschnitt', 'auswahl', 'oval').fehler, /gibt es nicht/);
});

// ================================================================================================ Validierung

test('Validierung im Formular: Eingaben, die keine Zahl sind, werden vor dem Kern abgefangen – mit Hinweis, wie es richtig geht', () => {
  assert.match(T.leitungAenderung(rohr, 'rohr', 'regeln.winkel', 'winkel', '15, abc').fehler, /«abc» ist keine Zahl.*Liste in Grad.*frei/);
  assert.match(T.leitungAenderung(rohr, 'rohr', 'regeln.abzweigwinkel', 'liste', '45; 9x').fehler, /«9x» ist keine Zahl/);
  assert.match(T.leitungAenderung(rohr, 'rohr', 'aussen', 'zahl', '').fehler, /Wert fehlt/);
  assert.deepEqual(T.zahlenListeLesen('15, 30;45  90°'), { liste: [15, 30, 45, 90] });
  assert.deepEqual(T.zahlenListeLesen(''), { liste: [] });
  assert.equal(T.zahlenListeText([15, 88.5, 90]), '15, 88.5, 90');
  assert.equal(T.zahlenListeText(undefined), '');
  // Bereiche (0 < w ≤ 180, doppelte Winkel, Parameter gibt es) prüft der Kern – der Editor sendet sie weiter
  assert.deepEqual(T.leitungAenderung(rohr, 'rohr', 'regeln.winkel', 'winkel', '200').ops[0].leitung.regeln.winkel, [200]);
});

test('Sprung aus der Prüfliste: Kern-Stellen der Leitungsserie zeigen auf die Seite «Leitungsserie»', () => {
  assert.deepEqual(T.stelleZiel('leitung.groesse.dn'), { tab: 'leitung', name: '', feld: 'groesse.dn' });
  assert.deepEqual(T.stelleZiel('leitung.formteile.bogen.typ'), { tab: 'leitung', name: '', feld: 'formteile.bogen.typ' });
  assert.deepEqual(T.stelleZiel('leitung.regeln.winkel'), { tab: 'leitung', name: '', feld: 'regeln.winkel' });
  assert.deepEqual(T.stelleZiel('leitung'), { tab: 'leitung', name: '', feld: '' });
  assert.equal(T.stelleZiel('leitungen'), null);
  const z = T.pruefZeilen({ definitionsfehler: [{ stelle: 'leitung.aussen', meldung: 'aussen fehlt', vorschlag: 'Formel angeben' }], typen: [] });
  assert.deepEqual(z[0].ziel, { tab: 'leitung', name: '', feld: 'aussen' });
});

// ================================================================================================ HTML der Seite

test('Seite «Leitungsserie»: jedes Feld trägt Pfad und Typ, Auswahlfelder zeigen den Wert, Fokus-Schlüssel sind eindeutig', () => {
  const html = L.formularHtml(rohr, { art: 'rohr', familien });
  assert.match(html, /<h4 class="fe-abschnitt">Querschnitt und Grösse<\/h4>/);
  assert.match(html, /<h4 class="fe-abschnitt">Formteile \(Familien des Projekts\)<\/h4>/);
  assert.match(html, /<div class="fe-feld" data-pfad="querschnitt" data-ftyp="auswahl"><select[^>]*><option value="rund" selected>rund<\/option><\/select>/);
  assert.match(html, /data-pfad="groesse\.dn" data-ftyp="auswahl"><select[^>]*><option value="">\(wählen\)<\/option><option value="dn" selected>dn<\/option>/);
  assert.match(html, /data-pfad="aussen" data-ftyp="zahl"><input type="text"[^>]*value="da"/);
  assert.match(html, /data-pfad="formteile\.bogen\.familie"[^>]*><select[^>]*><option value="">\(keine\)<\/option><option value="fam-test-bogen" selected>FAM-2 · Testbogen \(fiktiv\)<\/option>/);
  assert.match(html, /data-pfad="regeln\.winkel" data-ftyp="winkel"><input type="text"[^>]*value="15, 30, 45, 90"/);
  assert.match(html, /data-pfad="formteile\.reinigungsoeffnung\.typ" data-ftyp="typregel"><input type="text"[^>]* disabled/, 'Typregel ohne Familie gesperrt');
  const fks = [...html.matchAll(/data-fk="([^"]+)"/g)].map((m) => m[1]);
  assert.equal(new Set(fks).size, fks.length, 'Fokus-Schlüssel eindeutig');
  // Eingaben werden maskiert
  const d = klon(rohr); d.leitung.aussen = 'a"<b>';
  assert.match(L.formularHtml(d, { art: 'rohr', familien }), /value="a&quot;&lt;b&gt;"/);
});

test('Seite «Leitungsserie» als Tab: nur für Serien, ohne Körper und Anschlüsse; die Seite zeichnet sich aus ctx', () => {
  assert.equal(L.leitungTab.nurSerie, true);
  const html = L.leitungTab.zeichne({ def: rohr, familien: () => familien, klassen: () => klassen, serie: () => null });
  assert.match(html, /Leitungsserie Rohr/);
  assert.match(html, /Kontrolltabelle wird geladen/);
  // aenderung() liest das Feld aus dem Element und liefert die ops
  const el = { value: '45, 90', closest: () => ({ dataset: { pfad: 'regeln.winkel', ftyp: 'winkel' } }) };
  const r = L.leitungTab.aenderung(el, { def: rohr, klassen: () => klassen });
  assert.deepEqual(r.ops[0].leitung.regeln.winkel, [45, 90]);
  assert.match(L.leitungTab.aenderung({ value: 'x', closest: () => ({ dataset: { pfad: 'regeln.winkel', ftyp: 'winkel' } }) }, { def: rohr }).fehler, /keine Zahl/);
  assert.equal(L.leitungTab.aenderung({ value: '', closest: () => null }, { def: rohr }), null);
});

// ================================================================================================ Kontrolltabelle

/** Gekürzte Antwort der Abfrage leitungsserie (Form wie Leitungsserie.Abfrage): Testrohrserie ohne Reinigungsöffnung, Reduktion DN 32 → 15 fehlt. */
const serieAntwort = {
  familie: 'FAM-1', name: 'Testrohrserie (fiktiv)', id: 'fam-test-rohrserie', kategorie: 'Rohr', querschnitt: 'rund', winkel: [15, 30, 45, 90], abzweigwinkel: [90],
  groessen: [
    { typ: 'DN 15', gueltig: true, dn: 15, aussen: 20, innen: 16, wand: 2, formteile: { bogen: { verfuegbar: true, familie: 'FAM-2', typ: 'DN 15' }, tstueck: { verfuegbar: true, familie: 'FAM-3', typ: 'DN 15' },
      reinigungsoeffnung: { verfuegbar: false, grund: 'Die Serie FAM-1 nennt keine Formteil-Familie für Reinigungsöffnung.', vorschlag: 'leitung.formteile.reinigungsoeffnung ergänzen' }, reduktion: [] } },
    { typ: 'DN 32', gueltig: true, dn: 32, aussen: 37, innen: 31, wand: 3, formteile: { bogen: { verfuegbar: true, familie: 'FAM-2', typ: 'DN 32' }, tstueck: { verfuegbar: true, familie: 'FAM-3', typ: 'DN 32' },
      reinigungsoeffnung: { verfuegbar: false, grund: 'Die Serie FAM-1 nennt keine Formteil-Familie für Reinigungsöffnung.', vorschlag: 'leitung.formteile.reinigungsoeffnung ergänzen' },
      reduktion: [{ verfuegbar: false, auf: 'DN 15', grund: 'Kein Typ der Formteil-Familie FAM-4 passt zu Reduktion a = DN 32, b = DN 15.', vorschlag: 'Typ ergänzen' }, { verfuegbar: true, auf: 'DN 25', familie: 'FAM-4', typ: 'DN 32/25' }] } },
    { typ: 'Kaputt', gueltig: false, fehler: 'Parameter «da» hat keinen Wert' },
  ],
};

test('Kontrolltabelle: je Grösse aussen, innen, wand und Formteile ja/nein; Reduktion «n von m»; der Grund steht im Tooltip und in der Liste darunter', () => {
  const z = T.serieZellen(serieAntwort.groessen[1]);
  assert.deepEqual([z.bogen.zustand, z.tstueck.zustand, z.reinigungsoeffnung.zustand, z.reduktion.zustand], ['ja', 'ja', 'nein', 'teils']);
  assert.equal(z.reduktion.text, '1 von 2');
  assert.match(z.reduktion.titel, /auf DN 15: Kein Typ der Formteil-Familie FAM-4/);
  assert.match(z.reinigungsoeffnung.titel, /nennt keine Formteil-Familie.*ergänzen/);
  assert.match(z.bogen.titel, /FAM-2 · DN 32/);
  const klein = T.serieZellen(serieAntwort.groessen[0]);
  assert.deepEqual([klein.reduktion.zustand, klein.reduktion.text], ['keine', '–'], 'die kleinste Grösse braucht keine Reduktion');
  assert.equal(T.groesseText({ dn: 25 }), 'DN 25'); assert.equal(T.groesseText({ d: 125 }), 'Ø 125'); assert.equal(T.groesseText({ b: 400, h: 200 }), '400 × 200'); assert.equal(T.groesseText({}), '–');

  const html = L.kontrolleHtml(serieAntwort);
  assert.match(html, /<tr data-typ="DN 15"><td>DN 15<\/td><td>DN 15<\/td><td class="fe-zahl">20<\/td><td class="fe-zahl">16<\/td><td class="fe-zahl">2<\/td>/);
  assert.match(html, /<td class="fe-kz fe-ja" data-art="bogen" title="FAM-2 · DN 15">ja<\/td>/);
  assert.match(html, /<tr data-typ="DN 32">[\s\S]*<td class="fe-kz fe-teils" data-art="reduktion"[^>]*>1 von 2<\/td>/);
  assert.match(html, /<td class="fe-kz fe-nein" data-art="reinigungsoeffnung"/);
  assert.match(html, /<tr class="fe-ungueltig" data-typ="Kaputt">.*nicht auswertbar: Parameter «da» hat keinen Wert/);
  // Gründe: gleiche Gründe zusammengefasst, mit den betroffenen Grössen
  const g = T.serieGruende(serieAntwort);
  assert.equal(g.length, 2);
  const re = g.find((x) => x.art === 'reinigungsoeffnung');
  assert.deepEqual(re.groessen, ['DN 15', 'DN 32']); assert.equal(re.alle, true);
  const rd = g.find((x) => x.art === 'reduktion');
  assert.equal(rd.label, 'Reduktion auf DN 15'); assert.deepEqual(rd.groessen, ['DN 32']); assert.equal(rd.alle, false); assert.equal(rd.mehrere, false);
  assert.deepEqual(g.map((x) => x.art), ['reduktion', 'reinigungsoeffnung'], 'Reihenfolge: Bogen, T-Stück, Reduktion, Reinigungsöffnung');
  assert.match(html, /<strong>Reinigungsöffnung<\/strong> – alle Grössen: Die Serie FAM-1 nennt keine Formteil-Familie/);
  assert.match(html, /<strong>Reduktion auf DN 15<\/strong> – DN 32: Kein Typ/);
  // verschiedene Texte derselben Art und Zielgrösse (der Grund nennt die Grösse): zusammengefasst, der erste steht als Beispiel
  const viele = klon(serieAntwort);
  viele.groessen.push({ typ: 'DN 40', gueltig: true, dn: 40, formteile: { bogen: { verfuegbar: true }, tstueck: { verfuegbar: true }, reinigungsoeffnung: { verfuegbar: true },
    reduktion: [{ verfuegbar: false, auf: 'DN 15', grund: 'Kein Typ passt zu Reduktion a = DN 40, b = DN 15.', vorschlag: 'Typ ergänzen' }] } });
  const gv = T.serieGruende(viele).find((x) => x.art === 'reduktion');
  assert.deepEqual(gv.groessen, ['DN 32', 'DN 40']); assert.equal(gv.mehrere, true); assert.match(gv.grund, /a = DN 32/);
  assert.match(L.kontrolleHtml(viele), /<strong>Reduktion auf DN 15<\/strong> – DN 32, DN 40: z\. B\. Kein Typ/);
  assert.match(html, /Nicht verfügbar \(2\)/);
});

test('Kontrolltabelle: alles verfügbar, Serie ohne Grösse, Fehler des Kerns, noch nicht geladen', () => {
  const ok = { kategorie: 'Rohr', querschnitt: 'rund', winkel: 'frei', abzweigwinkel: [90], groessen: [{ typ: 'DN 15', gueltig: true, dn: 15, aussen: 20, innen: null, wand: null,
    formteile: { bogen: { verfuegbar: true, familie: 'F', typ: 'T' }, tstueck: { verfuegbar: true, familie: 'F', typ: 'T' }, reinigungsoeffnung: { verfuegbar: true, familie: 'F', typ: 'T' }, reduktion: [] } }] };
  const h = L.kontrolleHtml(ok);
  assert.match(h, /Alle Formteile sind für alle Grössen verfügbar/);
  assert.match(h, /<td class="fe-zahl">–<\/td>/, 'fehlende Masse als Strich');
  assert.match(h, /Bogenwinkel frei/);
  assert.match(L.kontrolleHtml({ ...ok, groessen: [] }), /noch keine Grösse/);
  assert.match(L.kontrolleHtml({ fehler: 'Familie FAM-1: 1 Fehler – (1) leitung.aussen: aussen fehlt', vorschlag: 'Formel angeben' }), /Der Kern kann die Serie noch nicht auswerten: .*aussen fehlt.*Formel angeben/);
  assert.match(L.kontrolleHtml(null), /wird geladen/);
  // weitere Abzweigwinkel (B7) erscheinen in den Gründen
  const aw = klon(ok); aw.groessen[0].formteile.tstueckJeWinkel = [{ winkel: 45, verfuegbar: false, grund: 'Kein T-Stück mit 45°', vorschlag: 'Typ ergänzen' }, { winkel: 90, verfuegbar: true }];
  assert.match(L.kontrolleHtml(aw), /T-Stück \(Abzweig 45°\)<\/strong> – alle Grössen: Kein T-Stück mit 45°/);
});

// ================================================================================================ Verdrahtung

test('Verdrahtung: Editor kennt die Seite, blendet Körper und Anschlüsse bei Serien aus, speichert nur über familie_aendern; Liste anlegen kennt die Materialliste', () => {
  const editor = lies('editor.js');
  assert.match(editor, /import \{ leitungTab \} from '\.\/editor-leitung\.js'/);
  assert.match(editor, /const TABS = \[parameterTab, leitungTab, typenTab, koerperTab, anschlussTab, sinnbildTab/);
  assert.match(editor, /koerperTab\.nichtSerie = true;\s*anschlussTab\.nichtSerie = true;/);
  assert.match(editor, /abfrage\('leitungsserie', \{ familie: kz \}\)/);
  assert.ok(editor.includes("'familie_aendern'"));
  assert.ok(!lies('editor-leitung.js').includes('element_aendern'));
  assert.ok(!/fetch\(/.test(lies('editor-leitung.js')), 'keine Logik und keine Netzzugriffe in der Seite');
  assert.match(lies('familien-text.js'), /op: 'leitung_setzen'/);
  const nf = lies('aktionen-familien.js');
  assert.match(nf, /familieEntwurfMinimal\(f\.kategorie, f\.name, \{ klassen, form: f\.form \}\)/);
  assert.match(nf, /class="nf-form"/);
  const kern = path.resolve(hier, '../../../../Hlks.Kern/Familien/FamilienBefehle.cs');
  assert.match(readFileSync(kern, 'utf8'), /case "leitung_setzen":/, 'der Kern kennt die op');
  assert.match(lies('../oberflaeche/aktionen-ansicht.js'), /materialliste: \['Materialliste', 'MATERIALLISTE'\]/);
  const css = lies('../../css/familien.css');
  assert.match(css, /\.fe-wurzel\.fe-serie \.fe-rechts/);
  const ohneVar = css.replace(/\/\*[\s\S]*?\*\//g, '').replace(/:root(\[[^\]]*\])?\s*\{[^}]*\}/g, '');
  assert.deepEqual(ohneVar.match(/#[0-9a-fA-F]{3,8}\b/g) ?? [], [], 'feste Farben nur in den Variablen');
});

// ================================================================================================ Gegen den echten Kern (nur Trockenläufe)

test('Echter Kern (HLKS_TEST_SERVER): Vorlagen und alle ops der Seite nimmt der Kern an; Fehler kommen verständlich zurück', { skip: !process.env.HLKS_TEST_SERVER }, async () => {
  const s = process.env.HLKS_TEST_SERVER.replace(/\/$/, '');
  const post = async (art, name, body) => (await fetch(`${s}/api/${art}/${name}${art === 'befehl' ? '?trockenlauf=true' : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) })).json();
  const entwurf = (def) => { const d = klon(def); delete d.id; d.name = `Probe C3 ${Math.random().toString(36).slice(2, 7)}`; return post('befehl', 'familie_entwurf', { definition: d }); };
  const kat = (await post('abfrage', 'katalog', {})).klassen;
  // Vorlagen: jede familienfähige Kategorie lässt sich anlegen (Rohr, Kanal rund und rechteckig, die Basisklasse Leitung in allen Formen)
  for (const k of T.familienKategorien(kat)) {
    const formen = T.leitungFormen(T.leitungsArt(k, kat));
    for (const [form] of formen.length ? formen : [[undefined]]) {
      const r = await post('befehl', 'familie_entwurf', { definition: T.familieEntwurfMinimal(k, `Probe ${k} ${form ?? ''}`, { klassen: kat, form }) });
      assert.equal(r.angenommen, true, `${k} ${form ?? ''}: ${r.fehler} ${r.vorschlag ?? ''}`);
    }
  }
  // Ausgangslage und die ops der Seite auf den Testserien
  for (const [def, art] of [[rohr, 'rohr'], [kanal, 'kanal'], [abwasser, 'rohr']]) {
    assert.equal((await entwurf(def)).angenommen, true, def.name);
    const aend = (pfad, typ, text) => T.leitungAenderung(def, art, pfad, typ, text);
    for (const [pfad, typ, text] of [['regeln.winkel', 'winkel', '15, 45, 90'], ['regeln.winkel', 'winkel', 'frei'], ['regeln.winkel', 'winkel', ''], ['regeln.abzweigwinkel', 'liste', '45, 88.5'],
      ['regeln.biegeradius', 'zahlopt', '2 * ' + (art === 'kanal' ? 'b' : 'da')], ['innen', 'zahlopt', ''], ['wand', 'zahlopt', ''], ['regeln.abzweig', 'auswahl', ''],
      ['formteile.bogen.familie', 'auswahl', 'fam-andere'], ['formteile.reinigungsoeffnung.familie', 'auswahl', 'fam-reinigung']]) {
      const r = aend(pfad, typ, text);
      assert.ok(r.ops, `${pfad}: ${r.fehler}`);
      const k = await entwurf(anwenden(def, r.ops));
      assert.equal(k.angenommen, true, `${def.name} ${pfad} = «${text}»: ${k.fehler}`);
    }
  }
  // Querschnitt wechseln: Kanal rund ↔ rechteckig; die Parameter kommen im selben Paket
  const k1 = T.familieEntwurfMinimal('Kanal', 'K');
  const wechsel = T.leitungAenderung(k1, 'kanal', 'querschnitt', 'auswahl', 'rechteck');
  assert.equal((await entwurf(anwenden(k1, wechsel.ops))).angenommen, true);
  // Fehler: der Kern lehnt ab, die Meldung nennt die Stelle und einen Weg; die Seite springt dorthin
  const bad = async (pfad, typ, text, muster) => {
    const r = T.leitungAenderung(rohr, 'rohr', pfad, typ, text);
    const k = await entwurf(anwenden(rohr, r.ops));
    assert.equal(k.angenommen, false, `${pfad} = «${text}» sollte abgelehnt werden`);
    const f = T.kernFehler(k);
    assert.match(f.text, muster);
    return f;
  };
  let f = await bad('regeln.winkel', 'winkel', '200', /Winkel 200°.*0 < w ≤ 180/);
  assert.equal(f.eintraege[0].stelle, 'leitung.regeln.winkel'); assert.ok(f.vorschlag, 'Vorschlag des Kerns');
  assert.deepEqual(T.stelleZiel(f.eintraege[0].stelle), { tab: 'leitung', name: '', feld: 'regeln.winkel' });
  f = await bad('regeln.abzweigwinkel', 'liste', '120', /Abzweigwinkel 120°/);
  await bad('aussen', 'zahl', 'gibtsnicht', /gibtsnicht|unbekannter Name/);
  await bad('groesse.dn', 'auswahl', 'gibtsnicht', /gibtsnicht/);
  f = await bad('formteile.bogen.typ', 'typregel', 'if(xx = 1, "a", "b")', /unbekannter Name «xx»/);
  assert.equal(f.eintraege[0].stelle, 'leitung.formteile.bogen.typ');
  await bad('regeln.winkel', 'winkel', '30, 30', /doppelt/);
  // ein Rohr kann nicht rechteckig sein: die Seite bietet es gar nicht an, der Kern lehnt es trotzdem ab
  const r = await entwurf({ ...klon(rohr), leitung: { ...klon(rohr.leitung), querschnitt: 'rechteck' } });
  assert.equal(r.angenommen, false); assert.match(T.kernFehler(r).text, /Rohre sind rund/);
  // ohne den Block lehnt der Kern ab – deshalb bekommt die Vorlage ihn
  const ohne = klon(rohr); delete ohne.leitung;
  const o = await entwurf(ohne);
  assert.equal(o.angenommen, false); assert.match(T.kernFehler(o).text, /braucht den Block «leitung»/);
});
