// Tests der Oberfläche für die freie Grösse (Kanalserien) und die Abwasser-Serie (Nachtrag Modul 3, entwurf/freie-groesse-kanal.md und entwurf/abwasser-serie.md)
// ohne Browser: Zahlenfelder lesen, leichte Serie aus der Abfrage familie, Anzeige «frei» bzw. «Normgrösse», Parameter «groesse» der Befehle, Optionsleiste, Palette,
// Familieneditor (Einsatz, Grössenart, Block frei, Vorlage der Formteile, Kontrolle auf Abruf mit Probe) und Texte des Kerns (Vorschau, Ablehnung, Befunde P29/P30).
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/werkzeuge/frei-abwasser.test.mjs
// Die Testserien sind die fiktiven Familien des Kerns (tests/daten/familien/test_kanalfrei, test_abwasser_d_serie): alle Werte FIKTIV.
// Mit HLKS_TEST_SERVER (Server mit offenem Beispielprojekt) prüft ein zusätzlicher Test gegen den echten Kern (nur Trockenläufe und Abfragen), dass er die Parameter der
// Oberfläche annimmt, die leichte Serie dem Kern entspricht und Grenzen sowie Schritt mit Vorschlag ablehnt.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const hier = path.dirname(fileURLToPath(import.meta.url));
const F = await import('./leitung-frei.js');
const L = await import('./leitung-logik.js');
const P = await import('./leitung-palette.js');
const O = await import('./optionen.js');
const D = await import('./leitung-daten.js');
const T = await import('../familien/familien-text.js');
const E = await import('../familien/editor-leitung.js');

const lies = (rel) => readFileSync(path.resolve(hier, rel), 'utf8');
const famDatei = (n) => JSON.parse(lies(`../../../../../tests/daten/familien/${n}.familie.json`));
const klon = (x) => JSON.parse(JSON.stringify(x));

/** Antwort der Abfrage familie (gekürzt): Definition und aufgelöste Werte je Typ (Vorgaben der Parameter + Werte des Typs). */
function familieAntwort(def, kz) {
  const vorgaben = Object.fromEntries((def.parameter ?? []).filter((p) => p.wert !== undefined).map((p) => [p.name, p.wert]));
  return { kennzeichen: kz, name: def.name, id: def.id, kategorie: def.kategorie, definition: def, typen: def.typen.map((t) => ({ name: t.name, gueltig: true, werte: { ...vorgaben, ...t.werte } })) };
}
const kanalDef = famDatei('test_kanalfrei');
const abwasserDef = famDatei('test_abwasser_d_serie');
const kanalFrei = F.serieLeichtAus(familieAntwort(kanalDef, 'FAM-4'));
const abwasserD = F.serieLeichtAus(familieAntwort(abwasserDef, 'FAM-8'));
const rohrDn = F.serieLeichtAus(familieAntwort(famDatei('test_rohrserie'), 'FAM-12'));

// ================================================================================================ Zahlenfelder

test('Eingabe: Breite, Höhe, Durchmesser mit Komma oder Punkt, grösser als 0; Grenzen und Schritt prüft der Kern', () => {
  assert.deepEqual(F.masseLesen('430'), { wert: 430 });
  assert.deepEqual(F.masseLesen(' 430,5 '), { wert: 430.5 });
  assert.deepEqual(F.masseLesen('430 mm'), { wert: 430 });
  assert.deepEqual(F.masseLesen(''), { leer: true });
  assert.deepEqual(F.masseLesen(null), { leer: true });
  assert.match(F.masseLesen('abc', 'Breite').fehler, /Breite: «abc» ist keine Zahl/);
  assert.match(F.masseLesen('0', 'Höhe').fehler, /Höhe muss grösser als 0 sein/);
  assert.match(F.masseLesen('-5').fehler, /keine Zahl/);
  assert.match(F.masseLesen('1e3').fehler, /keine Zahl/, 'keine Exponentenschreibweise');
  // ausserhalb der Grenzen / nicht im Schritt ist hier erlaubt – das lehnt der Kern ab (Meldung statt stiller Rundung)
  assert.deepEqual(F.masseLesen('3000'), { wert: 3000 });
  assert.deepEqual(F.masseLesen('430.5'), { wert: 430.5 });
});

test('Eingabe der Palette: Breite und Höhe zusammen, beide nötig; Durchmesser bei rundem Kanal', () => {
  assert.deepEqual(F.freiEingabe(kanalFrei, { b: '430', h: '270' }), { groesse: { b: 430, h: 270 } });
  assert.match(F.freiEingabe(kanalFrei, { b: '430', h: '' }).fehler, /Höhe fehlt/);
  assert.match(F.freiEingabe(kanalFrei, { b: 'x', h: '270' }).fehler, /Breite: «x» ist keine Zahl/);
  const rund = klon(kanalFrei); rund.groessenparameter = { d: 'd' };
  assert.deepEqual(F.freiEingabe(rund, { d: '250' }), { groesse: { d: 250 } });
  assert.match(F.freiEingabe(rund, { d: '' }).fehler, /Durchmesser fehlt/);
});

// ================================================================================================ Leichte Serie (Abfrage familie)

test('Leichte Serie: Raster, Block frei (Vorlage, Grenzen, Schritt) und Text wie der Kern, ohne die Abfrage leitungsserie', () => {
  assert.equal(kanalFrei.leicht, true);
  assert.equal(F.istFrei(kanalFrei), true);
  assert.equal(F.istRechteck(kanalFrei), true);
  assert.deepEqual(F.groessenSchluessel(kanalFrei), ['b', 'h']);
  assert.equal(kanalFrei.frei.vorlage, '400x200');
  assert.deepEqual(kanalFrei.frei.grenzen, { b: { min: 145, max: 2500 }, h: { min: 145, max: 2500 } });
  assert.equal(kanalFrei.frei.schritt, 1);
  assert.equal(kanalFrei.frei.text, 'frei b × h (145 … 2500 mm, Schritt 1 mm)', 'derselbe Text wie Leitungsserie.FreiText');
  assert.deepEqual(kanalFrei.groessen.map((g) => [g.typ, g.b, g.h, g.gueltig]), [['500x300', 500, 300, true], ['400x200', 400, 200, true], ['300x200', 300, 200, true], ['200x200', 200, 200, true]]);
  assert.equal(kanalFrei.winkelRegel, 'liste');
  assert.deepEqual(L.erlaubteWinkel(kanalFrei), [45, 90], 'der Winkelfang findet die Winkel der Serie auch in der leichten Serie');
  assert.equal(kanalFrei.einsatz, null);
  assert.equal(kanalFrei.nachAussendurchmesser, false);
  assert.equal(kanalFrei.probe, null, 'die Probe holt der Familieneditor auf Knopfdruck');
  // ohne Block frei: keine freie Serie
  assert.equal(F.istFrei(rohrDn), false);
  assert.equal(rohrDn.frei, null);
});

test('Leichte Serie: Grenzen nur nach unten oder ohne Grenzen, Schritt 5, Text wie der Kern', () => {
  const o = klon(kanalDef); o.leitung.frei = { vorlage: '400x200', grenzen: { b: { min: 145 }, h: { min: 145 } }, schritt: 5 };
  assert.equal(F.serieLeichtAus(familieAntwort(o, 'F')).frei.text, 'frei b × h (ab 145 mm, Schritt 5 mm)');
  o.leitung.frei = { vorlage: '400x200', grenzen: { b: { min: 145, max: 2500 }, h: { max: 1000 } } };
  assert.equal(F.serieLeichtAus(familieAntwort(o, 'F')).frei.text, 'frei b × h (b 145 … 2500 mm, h bis 1000 mm, Schritt 1 mm)');
  o.leitung.frei = { vorlage: '400x200' };
  assert.equal(F.serieLeichtAus(familieAntwort(o, 'F')).frei.text, 'frei b × h (jede Grösse > 0, Schritt 1 mm)');
});

test('Leichte Serie: Typ ohne Zahl für die Grösse ist ungültig und fehlt in den Vorschlägen', () => {
  const a = familieAntwort(kanalDef, 'FAM-4');
  a.typen.push({ name: 'leer', gueltig: true, werte: { s: 1 } });
  const s = F.serieLeichtAus(a);
  assert.equal(s.groessen.find((g) => g.typ === 'leer').gueltig, false);
  assert.ok(!F.rasterVorschlag(s).some((r) => r.typ === 'leer'));
});

test('Abwasser-Serie nach Aussendurchmesser: Einsatz, Grösse d, «d 110» statt «DN 100»', () => {
  assert.equal(abwasserD.einsatz, 'abwasser');
  assert.equal(abwasserD.nachAussendurchmesser, true);
  assert.deepEqual(F.groessenSchluessel(abwasserD), ['d']);
  assert.equal(F.istAbwasser(abwasserD), true);
  assert.equal(F.istAbwasser(rohrDn), false);
  assert.equal(F.nachAussendurchmesser(rohrDn), false);
  assert.deepEqual(abwasserD.groessen.map((g) => [g.typ, g.d, g.dn ?? null]),[['d 32', 32, null], ['d 56', 56, null], ['d 75', 75, null], ['d 110', 110, null]], 'die DN des Herstellers steht nicht in der Grösse');
  assert.equal(F.groesseText({ d: 110 }, abwasserD), 'd 110');
  assert.equal(F.groesseText({ d: 250 }, kanalFrei), 'Ø 250', 'Kanal rund: Durchmesser');
  assert.equal(F.groesseText({ dn: 100 }, rohrDn), 'DN 100');
  assert.equal(F.groesseText({ b: 430, h: 270 }), '430 × 270');
  assert.equal(F.groesseText(null), '–');
});

// ================================================================================================ Anzeige frei / Normgrösse

test('Anzeige: eine Rastergrösse bleibt ein normaler Typ, jede andere Grösse wird als «frei» angezeigt', () => {
  assert.deepEqual(F.groesseAnzeige(kanalFrei, { breite: 400, hoehe: 200, familientyp: '400x200' }), { text: '400 × 200', frei: false, typ: '400x200' });
  assert.deepEqual(F.groesseAnzeige(kanalFrei, { breite: 500, hoehe: 300, familientyp: '400x200' }), { text: '500 × 300', frei: false, typ: '500x300' }, 'Typ nach der Grösse, nicht nach familientyp');
  assert.deepEqual(F.groesseAnzeige(kanalFrei, { breite: 430, hoehe: 270, familientyp: '400x200' }), { text: '430 × 270', frei: true, typ: null });
  assert.deepEqual(F.groesseAnzeige(kanalFrei, { breite: 400, hoehe: 201, familientyp: '400x200' }), { text: '400 × 201', frei: true, typ: null }, 'schon 1 mm daneben ist frei');
  assert.deepEqual(F.groesseAnzeige(kanalFrei, { familientyp: '400x200' }), { text: '400x200', frei: false, typ: '400x200' }, 'ohne Grösse am Element: der Typ');
  assert.deepEqual(F.rasterTyp(kanalFrei, { b: 300, h: 200 }), '300x200');
  assert.equal(F.rasterTyp(kanalFrei, { b: 300, h: 201 }), null);
});

test('Vorschlagsliste: die Rastertypen nach Grösse sortiert; Optionen der Grössenwahl je Serienart', () => {
  assert.deepEqual(F.rasterVorschlag(kanalFrei).map((r) => [r.typ, r.text]), [['200x200', '200 × 200'], ['300x200', '300 × 200'], ['400x200', '400 × 200'], ['500x300', '500 × 300']]);
  assert.deepEqual(F.groessenOptionen(kanalFrei).map(([, t]) => t), ['200 × 200', '300 × 200', '400 × 200', '500 × 300']);
  assert.deepEqual(F.groessenOptionen(abwasserD), [['d 32', 'd 32'], ['d 56', 'd 56'], ['d 75', 'd 75'], ['d 110', 'd 110']]);
  const anders = klon(abwasserD); anders.groessen[3].typ = 'PE 110';
  assert.deepEqual(F.groessenOptionen(anders)[3], ['PE 110', 'd 110 (PE 110)'], 'der Typname folgt, wenn er anders heisst');
  assert.deepEqual(F.groessenOptionen(rohrDn).map(([t]) => t), rohrDn.groessen.map((g) => g.typ), 'DN-Serien unverändert');
  assert.deepEqual(F.groessenOptionen(null), []);
});

test('Vorbelegung: die Zahlenfelder zeigen die Vorlage der Serie', () => {
  assert.deepEqual(F.vorbelegung(kanalFrei), { freiB: 400, freiH: 200 });
  const rund = klon(kanalFrei); rund.groessenparameter = { d: 'd' }; rund.groessen = [{ typ: 'Ø 250', gueltig: true, d: 250 }]; rund.frei.vorlage = 'Ø 250';
  assert.deepEqual(F.vorbelegung(rund), { freiD: 250 });
  assert.equal(F.vorbelegung(rohrDn), null);
  const ohneVorlage = klon(kanalFrei); ohneVorlage.frei.vorlage = 'gibt es nicht';
  assert.equal(F.vorbelegung(ohneVorlage), null);
});

// ================================================================================================ Parameter der Befehle

const optFrei = { serie: 'FAM-4', groesse: '', freiB: 430, freiH: 270, freiD: null, hoehe: 300, system: 'ZU', strang: 'ZU-1' };

test('Parameter groesse: bei einer freien Serie { b, h } aus den Zahlenfeldern, sonst der Typname', () => {
  assert.deepEqual(F.groesseAusOptionen(kanalFrei, optFrei), { b: 430, h: 270 });
  assert.equal(F.groesseAusOptionen(kanalFrei, { ...optFrei, freiH: null }), null);
  assert.equal(F.groesseAusOptionen(kanalFrei, { ...optFrei, freiB: 0 }), null);
  assert.equal(F.groesseAusOptionen(rohrDn, { groesse: 'DN 20' }), 'DN 20');
  assert.equal(F.groesseAusOptionen(rohrDn, { groesse: '' }), null);
  assert.equal(F.groesseAusOptionen(null, { groesse: 'DN 20' }), 'DN 20', 'Serie noch nicht geladen: Typname');
  assert.equal(F.groesseAusOptionen(abwasserD, { groesse: 'd 110' }), 'd 110');
});

test('leitung_zeichnen: freie Serie sendet { b, h }, auch wenn es eine Rastergrösse ist (der Kern nimmt dann den Typ); Abwasser-Serie den Typ «d 110»', () => {
  const punkte = [[0, 0, 300], [2000, 0, 300]];
  const p = L.leitungParameter({ opt: optFrei, punkte, serie: kanalFrei }).parameter;
  assert.deepEqual(p.groesse, { b: 430, h: 270 });
  assert.equal(p.serie, 'FAM-4'); assert.equal(p.system, 'ZU'); assert.equal(p.strang, 'ZU-1');
  assert.deepEqual(L.leitungParameter({ opt: { ...optFrei, freiB: 400, freiH: 200 }, punkte, serie: kanalFrei }).parameter.groesse, { b: 400, h: 200 });
  assert.equal(L.leitungParameter({ opt: { serie: 'FAM-8', groesse: 'd 110', system: 'SW' }, punkte, serie: abwasserD }).parameter.groesse, 'd 110');
  assert.equal(L.leitungParameter({ opt: { serie: 'FAM-12', groesse: 'DN 20', system: 'KW' }, punkte }).parameter.groesse, 'DN 20', 'ohne Serienangabe wie bisher');
  assert.throws(() => L.leitungParameter({ opt: { ...optFrei, freiH: null }, punkte, serie: kanalFrei }), /Höhe der Leitung fehlt/);
});

test('leitung_abzweigen: freie Serie sendet { b, h }; ohne ausgefüllte Felder übernimmt der Kern die Grösse der Leitung', () => {
  const a = L.abzweigParameter({ opt: { ...optFrei, freiB: 250, freiH: 200 }, leitung: 'KA-1', punkt: [100, 0, 300], punkte: [[100, 500, 300]], serie: kanalFrei });
  assert.deepEqual(a.groesse, { b: 250, h: 200 });
  const b = L.abzweigParameter({ opt: { serie: 'FAM-4', groesse: '', freiB: null, freiH: null }, leitung: 'KA-1', punkt: [100, 0, 300], richtung: [0, 1, 0], serie: kanalFrei });
  assert.equal('groesse' in b, false);
  assert.equal(L.abzweigParameter({ opt: { serie: 'FAM-12', groesse: 'DN 15' }, leitung: 'RO-1', punkt: [0, 0, 0], richtung: [0, 1, 0] }).groesse, 'DN 15');
});

test('Pflichtangaben: freie Serie verlangt Breite und Höhe statt der Typwahl', () => {
  const serien = [{ kz: 'FAM-4' }];
  assert.equal(L.pflichtFehlt(optFrei, { serien, serie: kanalFrei }), null);
  assert.equal(L.pflichtFehlt({ ...optFrei, freiB: null }, { serien, serie: kanalFrei }).feld, 'freiB');
  assert.equal(L.pflichtFehlt({ ...optFrei, freiH: null }, { serien, serie: kanalFrei }).feld, 'freiH');
  assert.match(L.pflichtFehlt({ ...optFrei, freiH: null }, { serien, serie: kanalFrei }).vorschlag, /frei b × h \(145 … 2500 mm, Schritt 1 mm\)/, 'Grenzen und Schritt stehen im Vorschlag');
  assert.equal(L.pflichtFehlt({ ...optFrei, groesse: '' }, { serien, serie: kanalFrei }), null, 'die Normgrösse ist nur ein Vorschlag');
  assert.equal(L.pflichtFehlt({ serie: 'FAM-12', groesse: '', hoehe: 300 }, { serien, serie: rohrDn }).feld, 'groesse', 'andere Serien wie bisher');
  assert.equal(L.pflichtFehlt({ ...optFrei, hoehe: null }, { serien, serie: kanalFrei }).feld, 'hoehe');
});

test('Gummiband: bei einer freien Serie die gewünschte Breite, sonst die Aussenbreite der Grösse', () => {
  assert.equal(L.bandBreite(kanalFrei, optFrei), 430);
  assert.equal(L.bandBreite(kanalFrei, { ...optFrei, freiB: null }), null);
  assert.equal(L.bandBreite({ ...rohrDn, groessen: [{ typ: 'DN 20', gueltig: true, aussen: 25 }] }, { groesse: 'DN 20' }), 25);
  const rund = klon(kanalFrei); rund.groessenparameter = { d: 'd' };
  assert.equal(L.bandBreite(rund, { freiD: 250 }), 250);
});

test('Abzweig übernimmt die Grösse der gewählten Leitung in die Zahlenfelder', () => {
  assert.deepEqual(F.abzweigGroesse({ breite: 430, hoehe: 270, familientyp: '400x200' }), [['freiB', 430], ['freiH', 270], ['freiD', null]]);
  assert.deepEqual(F.abzweigGroesse({ durchmesser: 110, dn: 100 }), [['freiB', null], ['freiH', null], ['freiD', 110]]);
  assert.deepEqual(F.abzweigGroesse({}), [['freiB', null], ['freiH', null], ['freiD', null]]);
});

// ================================================================================================ Texte des Kerns

test('Vorschau: Geraden und Formteile einer freien Grösse tragen die Marke «frei» (Hinweise des Kerns, 21.10.2026)', () => {
  const r = { hinweise: [
    'leitung_zeichnen: 2 Gerade(n), zusammen 3270 mm, und 1 Formteil(e) – Serie FAM-4 «Testkanal frei rechteckig (fiktiv)».',
    'Gerade KA-1 430 × 270 mm: 1635 mm von [14000, 3000, 300] bis [15635, 3000, 300], frei (Vorlage «400x200»)',
    'Bogen BO-1 Typ «400x200», 90° bei [16000, 3000, 300], Einbaulängen a 365 mm, b 365 mm, frei 430 × 270',
    'Gerade KA-2 430 × 270 mm: 1635 mm von [16000, 3365, 300] bis [16000, 5000, 300], frei (Vorlage «400x200»)',
  ] };
  const v = L.vorschauAus(r);
  assert.equal(v.geraden.length, 2);
  assert.equal(v.geraden[0].groesse, '430 × 270 mm');
  assert.equal(v.geraden[0].frei, true);
  assert.equal(v.formteile[0].frei, true);
  assert.equal(v.formteile[0].winkel, 90);
  assert.equal(L.vorschauZeile(v), '2 Geraden, 3270 mm · 1 Bogen 90°');
  assert.deepEqual(L.vorschauListe(v), ['Gerade KA-1 · 430 × 270 mm · 1635 mm · frei', 'Gerade KA-2 · 430 × 270 mm · 1635 mm · frei', 'Bogen BO-1 · Typ «400x200» · 90° · frei (Exemplarwerte)']);
  // Rastergrösse: keine Marke
  const n = L.vorschauAus({ hinweise: ['Gerade KA-3 500 × 300 mm: 1600 mm von [0, 0, 0] bis [1600, 0, 0]', 'Bogen BO-2 Typ «500x300», 90° bei [1, 2, 3], Einbaulängen a 400 mm, b 400 mm'] });
  assert.equal('frei' in n.geraden[0], false);
  assert.deepEqual(L.vorschauListe(n)[0], 'Gerade KA-3 · 500 × 300 mm · 1600 mm');
  // Abwasser-Serie: Grösse «d 110»
  const a = L.vorschauAus({ hinweise: ['Gerade RO-1 d 110: 2000 mm von [14000, 8000, 300] bis [16000, 8000, 300]'] });
  assert.equal(a.geraden[0].groesse, 'd 110');
  assert.deepEqual(L.vorschauListe(a), ['Gerade RO-1 · d 110 · 2000 mm']);
});

test('Ablehnung: Grösse ausserhalb der Grenzen bzw. nicht im Schritt mit dem Vorschlag des Kerns, «Vorschlag:» nur einmal', () => {
  const a = L.ablehnung({ angenommen: false, fehler: 'Die Grösse 3000 × 270 mm liegt ausserhalb der Grenzen der Serie FAM-4: b = 3000 mm, erlaubt 145 … 2500 mm.',
    vorschlag: 'Vorschlag: 2500 × 270 (innerhalb der Grenzen, im Schritt) oder die Normgrösse 500 × 300 – frei b × h (145 … 2500 mm, Schritt 1 mm).' });
  assert.match(a.text, /ausserhalb der Grenzen/);
  assert.equal(a.vorschlag, '2500 × 270 (innerhalb der Grenzen, im Schritt) oder die Normgrösse 500 × 300 – frei b × h (145 … 2500 mm, Schritt 1 mm).');
  assert.equal(L.ablehnung({ fehler: 'x', vorschlag: 'Den Verlauf so legen' }).vorschlag, 'Den Verlauf so legen', 'andere Vorschläge unverändert');
  assert.equal(L.ablehnung({ fehler: 'x', vorschlag: 'Vorschlag:  ' }).vorschlag, null);
});

test('Befunde nach einem Befehl: P29 (auch als Hinweis) und P30 (auch ohne Medium) erscheinen, P01 nicht', () => {
  const t = L.neueBefundeTexte({ neueBefunde: [
    { code: 'P29', schwere: 'hinweis', element: 'KA-1', text: 'Kanal 430.5 × 270 liegt nicht im Schritt der Serie' },
    { code: 'P30', schwere: 'fehler', element: 'RO-1', text: 'Abwasser-Serie FAM-8 für Heizung Vorlauf' },
    { code: 'P30', schwere: 'hinweis', element: 'RO-2', text: 'Abwasser-Serie ohne Medium' },
    { code: 'P01', schwere: 'hinweis', element: 'RO-1', text: 'Anschluss offen' },
  ] });
  assert.equal(t.length, 3);
  assert.match(t[0], /^P29 KA-1: /); assert.match(t[1], /^P30 RO-1: /); assert.match(t[2], /^P30 RO-2: /);
});

test('Befunde eines Elements aus der Prüfung des Kerns: nur P29 und P30 dieser Leitung', () => {
  const b = [
    { code: 'P29', schwere: 'fehler', element: 'KA-1', text: 'ausserhalb', vorschlag: 'Grösse wählen' },
    { code: 'P30', schwere: 'fehler', element: 'ka-1', text: 'Abwasser-Serie' },
    { code: 'P29', schwere: 'fehler', element: 'KA-2', text: 'andere Leitung' },
    { code: 'P01', schwere: 'hinweis', element: 'KA-1', text: 'offen' },
  ];
  assert.deepEqual(F.leitungsBefunde(b, 'KA-1').map((x) => x.code), ['P29', 'P30']);
  assert.deepEqual(F.leitungsBefunde(null, 'KA-1'), []);
});

test('Hinweistexte: Grenzen und Schritt bzw. Aussendurchmesser und Beschriftung', () => {
  assert.match(F.freiHinweis(kanalFrei), /^frei b × h \(145 … 2500 mm, Schritt 1 mm\) – die Normgrössen sind Vorschläge; der Kern prüft Grenzen und Schritt\.$/);
  assert.equal(F.freiHinweis(rohrDn), '');
  assert.match(F.abwasserHinweis(abwasserD, 'PE 110'), /Grösse nach Aussendurchmesser, Beschriftung «PE 110»; nur für Schmutz- und Regenwasser/);
  assert.match(F.abwasserHinweis(abwasserD), /Aussendurchmesser; nur für/);
  assert.equal(F.abwasserHinweis(rohrDn), '');
  assert.equal(F.beschriftungVon({ groessen: [{ typ: 'd 110', beschriftung: 'PE 110' }] }, 'd 110'), 'PE 110');
  assert.equal(F.beschriftungVon(abwasserD, 'd 110'), null, 'die leichte Serie hat keine Beschriftung – die gehört dem Kern');
});

// ================================================================================================ Optionsleiste

test('Optionsleiste: Zahlenfelder und Hinweis erscheinen nur bei einer freien Serie; Titel und Optionen der Grösse folgen der Serie', () => {
  D.serieEinsetzen('FAM-F', kanalFrei); D.serieEinsetzen('FAM-A', abwasserD); D.serieEinsetzen('FAM-R', { ...rohrDn, groessen: [{ typ: 'DN 20', gueltig: true, dn: 20 }] });
  const feld = (wid, id) => O.feldDef(wid, id);
  O.setze('kanal', 'serie', 'FAM-F');
  assert.equal(feld('kanal', 'freiB').sichtbar(), true);
  assert.equal(feld('kanal', 'freiH').sichtbar(), true);
  assert.equal(feld('kanal', 'freiD').sichtbar(), false, 'rechteckig: kein Durchmesser');
  assert.equal(feld('kanal', 'freiHinweis').sichtbar(), true);
  assert.equal(feld('kanal', 'abwasserHinweis').sichtbar(), false);
  assert.equal(feld('kanal', 'freiB').pflicht, true);
  assert.equal(feld('kanal', 'groesse').titel, 'Normgrösse');
  assert.equal(feld('kanal', 'groesse').leer, 'Vorschlag …');
  assert.deepEqual(feld('kanal', 'groesse').werte.map(([, t]) => t), ['200 × 200', '300 × 200', '400 × 200', '500 × 300']);
  assert.match(feld('kanal', 'freiHinweis').text, /145 … 2500 mm, Schritt 1 mm/);
  // Normgrösse gewählt: Zahlenfelder
  O.setze('kanal', 'groesse', '500x300');
  feld('kanal', 'groesse').beiAenderung();
  assert.equal(O.wert('kanal', 'freiB'), 500); assert.equal(O.wert('kanal', 'freiH'), 300);
  // Vorbelegung mit der Vorlage
  O.freiVorbelegen('kanal', kanalFrei);
  assert.equal(O.wert('kanal', 'freiB'), 400); assert.equal(O.wert('kanal', 'freiH'), 200);
  O.setze('kanal', 'freiB', 430);
  O.freiVorbelegen('kanal', kanalFrei, true);
  assert.equal(O.wert('kanal', 'freiB'), 430, 'beim Start bleibt, was schon drinsteht');
  O.setze('kanal', 'freiH', null);
  O.freiVorbelegen('kanal', kanalFrei, true);
  assert.equal(O.wert('kanal', 'freiH'), 200, 'leere Felder werden gefüllt');
  O.freiVorbelegen('kanal', rohrDn);
  assert.equal(O.wert('kanal', 'freiB'), null, 'andere Serie: die Felder werden geleert');

  // Abwasser-Serie: Grösse «d 110», Hinweis, keine Zahlenfelder
  O.setze('rohr', 'serie', 'FAM-A');
  assert.equal(feld('rohr', 'abwasserHinweis').sichtbar(), true);
  assert.equal(feld('rohr', 'freiB').sichtbar(), false);
  assert.equal(feld('rohr', 'freiHinweis').sichtbar(), false);
  assert.equal(feld('rohr', 'groesse').titel, 'Grösse');
  assert.deepEqual(feld('rohr', 'groesse').werte.map(([, t]) => t), ['d 32', 'd 56', 'd 75', 'd 110']);
  assert.match(feld('rohr', 'abwasserHinweis').text, /Abwasser-Serie: Grösse nach Aussendurchmesser/);
  // DN-Serie wie bisher
  O.setze('rohr', 'serie', 'FAM-R');
  assert.equal(feld('rohr', 'abwasserHinweis').sichtbar(), false);
  assert.deepEqual(feld('rohr', 'groesse').werte, [['DN 20', 'DN 20']]);
});

test('Optionsleiste: Hinweise erscheinen als HTML mit eigener Klasse, die Zahlenfelder als Eingabefelder mit Pflicht-Markierung', async () => {
  const quelle = lies('./optionsleiste.js');
  assert.match(quelle, /class="ol-hinweis\$\{f\.klasse \? ' ' \+ esc\(f\.klasse\) : ''\}"/);
  assert.match(lies('../../css/oberflaeche.css'), /#optionsleiste \.ol-hinweis\.ol-info/);
});

// ================================================================================================ Palette

const H = { zeile: (label, feld, o = {}) => `[${label}|${feld}${o.klasse ? '|' + o.klasse : ''}]`, gruppe: (name, zeilen) => `<${name}>${zeilen.join('')}</${name}>`, nurLesen: (t) => `«${t}»` };
const kanalEl = (werte) => ({ klasse: 'Kanal', kennzeichen: 'KA-1', bezuege: { familie: 'FAM-4' }, werte });
const lesenKanal = (w) => ({ eigenschaften: { familientyp: { wert: '400x200' }, breite: { wert: w.breite }, hoehe: { wert: w.hoehe } } });

test('Palette: freie Grösse zeigt Breite und Höhe als Zahlenfelder, «frei» und die Normgrössen als Vorschlag', () => {
  const w = { breite: 430, hoehe: 270, familientyp: '400x200' };
  const h = P.leitungGruppe({ el: kanalEl(w), l: lesenKanal(w), f: { kennzeichen: 'FAM-4', name: 'Testkanal' }, serie: kanalFrei, serien: [{ kz: 'FAM-4', name: 'Testkanal' }], zug: null }, H);
  assert.match(h, /«430 × 270 \(frei\)»/);
  assert.match(h, /data-ziel="lt:b"[^>]*value="430"/);
  assert.match(h, /data-ziel="lt:h"[^>]*value="270"/);
  assert.ok(!h.includes('data-ziel="lt:groesse"'), 'keine Typwahl mit der Vorlage als Pseudo-Grösse');
  assert.match(h, /data-ziel="lt:raster"/);
  assert.match(h, /<option value="" selected>frei \(keine Normgrösse\)<\/option>/);
  for (const t of ['200 × 200', '300 × 200', '400 × 200', '500 × 300']) assert.ok(h.includes(`>${t}</option>`), t);
  assert.match(h, /frei b × h \(145 … 2500 mm, Schritt 1 mm\)/, 'Grenzen und Schritt als Hinweis');
  assert.match(h, /data-ziel="eig:gefaelle"/, 'Gefälle, Dämmung usw. bleiben');
});

test('Palette: Rastergrösse einer freien Serie ist ein normaler Typ (Normgrösse gewählt, nicht «frei»)', () => {
  const w = { breite: 400, hoehe: 200, familientyp: '400x200' };
  const h = P.leitungGruppe({ el: kanalEl(w), l: lesenKanal(w), f: { kennzeichen: 'FAM-4', name: 'Testkanal' }, serie: kanalFrei, serien: [], zug: null }, H);
  assert.match(h, /«400 × 200 \(Normgrösse 400x200\)»/);
  assert.ok(!/\(frei\)/.test(h));
  assert.match(h, /<option value="400x200" selected>400 × 200<\/option>/);
  assert.match(h, /data-ziel="lt:b"[^>]*value="400"/);
});

test('Palette: Befunde P29 und P30 des Kerns erscheinen in der Gruppe', () => {
  const w = { breite: 3000, hoehe: 270, familientyp: '400x200' };
  const h = P.leitungGruppe({ el: kanalEl(w), l: lesenKanal(w), f: { kennzeichen: 'FAM-4', name: 'x' }, serie: kanalFrei, serien: [], zug: null,
    befunde: [{ code: 'P29', schwere: 'fehler', text: 'Kanal 3000 × 270 liegt ausserhalb der Grenzen', vorschlag: 'Grösse innerhalb der Grenzen wählen' }] }, H);
  assert.match(h, /\[Befund P29\|<span class="ep-text ep-befund ep-befund-fehler">Kanal 3000 × 270 liegt ausserhalb der Grenzen <span class="leise">→ Grösse innerhalb der Grenzen wählen<\/span><\/span>\|ep-befundzeile\]/);
});

const rohrAEl = { klasse: 'Rohr', kennzeichen: 'RO-1', bezuege: { familie: 'FAM-8' }, werte: { familientyp: 'd 110', durchmesser: 110, dn: 100 } };
const lesenRohrA = { eigenschaften: { familientyp: { wert: 'd 110' }, durchmesser: { wert: 110 }, dn: { wert: 100 } } };
const abwasserVoll = { ...abwasserD, leicht: undefined, groessen: abwasserD.groessen.map((g) => ({ ...g, aussen: g.d, beschriftung: `PE ${g.d}` })) };

test('Palette: Abwasser-Serie zeigt «d 110», Aussendurchmesser, DN nur als Angabe des Herstellers, Beschriftung des Kerns und Einsatz', () => {
  const h = P.leitungGruppe({ el: rohrAEl, l: lesenRohrA, f: { kennzeichen: 'FAM-8', name: 'PE' }, serie: abwasserVoll, serien: [], zug: null }, H);
  assert.match(h, /<option value="d 110" selected>d 110<\/option>/);
  assert.ok(!h.includes('DN 100</option>'), 'keine DN-Auswahl');
  assert.match(h, /\[Einsatz\|«Abwasser»/);
  assert.match(h, /\[Aussendurchmesser\|«110 mm»\]/);
  assert.match(h, /\[DN \(Hersteller\)\|«DN 100»\|klein\]/);
  assert.match(h, /\[Beschriftung\|«PE 110»\]/);
  // d 32 ohne DN: keine Herstellerzeile
  const e32 = { ...rohrAEl, werte: { familientyp: 'd 32', durchmesser: 32 } };
  const h32 = P.leitungGruppe({ el: e32, l: { eigenschaften: { familientyp: { wert: 'd 32' }, durchmesser: { wert: 32 } } }, f: { kennzeichen: 'FAM-8', name: 'PE' }, serie: abwasserVoll, serien: [], zug: null }, H);
  assert.ok(!h32.includes('DN (Hersteller)'));
  assert.match(h32, /\[Beschriftung\|«PE 32»\]/);
});

test('Palette: DN-Serie bleibt unverändert (keine Zeilen für Einsatz, Aussendurchmesser, Normgrösse)', () => {
  const rohr = { klasse: 'Rohr', kennzeichen: 'RO-2', bezuege: { familie: 'FAM-12' }, werte: { familientyp: 'DN 20' } };
  const serie = { ...rohrDn, groessen: [{ typ: 'DN 20', gueltig: true, dn: 20, aussen: 25 }] };
  const h = P.leitungGruppe({ el: rohr, l: { eigenschaften: { familientyp: { wert: 'DN 20' } } }, f: { kennzeichen: 'FAM-12', name: 'x' }, serie, serien: [], zug: null }, H);
  assert.match(h, /data-ziel="lt:groesse"/);
  for (const nein of ['Einsatz', 'Aussendurchmesser', 'Normgrösse', 'lt:b', 'Befund']) assert.ok(!h.includes(nein), nein);
});

test('Palette: welche Eigenschaften die Gruppe «Leitung» übernimmt', () => {
  assert.deepEqual(P.leitungEigenschaften(rohrDn), P.LEITUNG_EIGENSCHAFTEN);
  assert.deepEqual(P.leitungEigenschaften(null), P.LEITUNG_EIGENSCHAFTEN);
  assert.deepEqual(P.leitungEigenschaften(kanalFrei), [...P.LEITUNG_EIGENSCHAFTEN, 'breite', 'hoehe', 'durchmesser']);
  assert.deepEqual(P.leitungEigenschaften(abwasserD), [...P.LEITUNG_EIGENSCHAFTEN, 'durchmesser']);
  const nichtFrei = klon(kanalFrei); nichtFrei.frei = null;
  assert.deepEqual(P.leitungEigenschaften(nichtFrei), P.LEITUNG_EIGENSCHAFTEN, 'Kanal ohne freie Grösse: breite und hoehe bleiben unter «Eigenschaften»');
});

// ================================================================================================ Familieneditor

const familien = [
  { kennzeichen: 'FAM-1', name: 'Kanalbogen frei', id: 'fam-test-kanalbogen-frei', kategorie: 'Bogen', gueltig: true, typen: ['400x200', '500x300'] },
  { kennzeichen: 'FAM-2', name: 'Kanal-T frei', id: 'fam-test-kanal-t-frei', kategorie: 'TStueck', gueltig: true, typen: ['400x200'] },
];
const klassen = [{ name: 'Leitung', basis: null }, { name: 'Rohr', basis: 'Leitung' }, { name: 'Kanal', basis: 'Leitung' }];
const feld = (def, pfad, art = T.leitungsArt(def.kategorie, klassen)) => T.leitungFelder(def, { art, familien }).find((f) => f.pfad === pfad);

test('Editor: Block frei wird angezeigt (Vorlage, Grenzen je Seite, Schritt) und nur bei Kanalserien', () => {
  const f = T.leitungFelder(kanalDef, { art: 'kanal', familien });
  const pfade = f.map((x) => x.pfad);
  assert.deepEqual(pfade.filter((p) => p.startsWith('frei.')), ['frei.vorlage', 'frei.grenzen.b.min', 'frei.grenzen.b.max', 'frei.grenzen.h.min', 'frei.grenzen.h.max', 'frei.schritt']);
  assert.equal(feld(kanalDef, 'frei.vorlage').wert, '400x200');
  assert.deepEqual(feld(kanalDef, 'frei.vorlage').optionen, [['500x300', '500x300'], ['400x200', '400x200'], ['300x200', '300x200'], ['200x200', '200x200']], 'die Typen der Serie');
  assert.equal(feld(kanalDef, 'frei.grenzen.b.min').text, '145');
  assert.equal(feld(kanalDef, 'frei.grenzen.h.max').text, '2500');
  assert.equal(feld(kanalDef, 'frei.schritt').text, '1');
  assert.equal(feld(kanalDef, 'frei.schritt').deaktiviert, false);
  assert.equal(f.find((x) => x.pfad === 'frei.vorlage').gruppe, 'Freie Grösse (nach Mass gefertigt)');
  // Kanal ohne freie Grösse: Vorlage leer, Felder gesperrt
  const ohne = klon(kanalDef); delete ohne.leitung.frei;
  assert.equal(feld(ohne, 'frei.vorlage').wert, '');
  assert.equal(feld(ohne, 'frei.grenzen.b.min').deaktiviert, true);
  assert.equal(feld(ohne, 'frei.schritt').deaktiviert, true);
  // Rohr: nie eine freie Grösse
  assert.deepEqual(T.leitungFelder(famDatei('test_rohrserie'), { art: 'rohr', familien }).filter((x) => x.pfad.startsWith('frei')), []);
  // rund: Grenzen für d
  const rund = klon(kanalDef); rund.leitung = { querschnitt: 'rund', groesse: { d: 'b' }, aussen: 'b', frei: { vorlage: '400x200', grenzen: { d: { min: 100 } } } };
  assert.deepEqual(T.leitungFelder(rund, { art: 'kanal', familien }).map((x) => x.pfad).filter((p) => p.startsWith('frei.')), ['frei.vorlage', 'frei.grenzen.d.min', 'frei.grenzen.d.max', 'frei.schritt']);
});

test('Editor: Block frei bearbeiten – Vorlage setzen und entfernen, Grenzen, Schritt, Fehler bei falschen Eingaben', () => {
  const ohne = klon(kanalDef); delete ohne.leitung.frei;
  const setzen = T.leitungAenderung(ohne, 'kanal', 'frei.vorlage', 'auswahl', '400x200');
  assert.deepEqual(setzen.ops[0].leitung.frei, { vorlage: '400x200' });
  const entfernen = T.leitungAenderung(kanalDef, 'kanal', 'frei.vorlage', 'auswahl', '');
  assert.equal('frei' in entfernen.ops[0].leitung, false, 'ohne Vorlage entfällt der ganze Block samt Grenzen und Schritt');
  const min = T.leitungAenderung(kanalDef, 'kanal', 'frei.grenzen.b.min', 'masse', '150');
  assert.deepEqual(min.ops[0].leitung.frei.grenzen.b, { min: 150, max: 2500 });
  const min0 = T.leitungAenderung(kanalDef, 'kanal', 'frei.grenzen.b.min', 'masse', '');
  assert.deepEqual(min0.ops[0].leitung.frei.grenzen.b, { max: 2500 }, 'leer = keine Grenze');
  const ohneHMin = { ...kanalDef, leitung: T.leitungAenderung(kanalDef, 'kanal', 'frei.grenzen.h.min', 'masse', '').ops[0].leitung };
  const beide = T.leitungAenderung(ohneHMin, 'kanal', 'frei.grenzen.h.max', 'masse', '');
  assert.equal('h' in (beide.ops[0].leitung.frei.grenzen ?? {}), false, 'ohne beide Grenzen entfällt der Schlüssel');
  assert.match(T.leitungAenderung(kanalDef, 'kanal', 'frei.grenzen.b.max', 'masse', '2 500,5').fehler, /keine Grenze/);
  assert.match(T.leitungAenderung(kanalDef, 'kanal', 'frei.grenzen.b.min', 'masse', 'viel').fehler, /keine Grenze/);
  assert.match(T.leitungAenderung(kanalDef, 'kanal', 'frei.grenzen.b.min', 'masse', '-3').fehler, /keine Grenze/);
  assert.equal(T.leitungAenderung(kanalDef, 'kanal', 'frei.grenzen.b.max', 'masse', '3000 mm').ops[0].leitung.frei.grenzen.b.max, 3000);
  assert.equal(T.leitungAenderung(kanalDef, 'kanal', 'frei.schritt', 'ganzzahl', '5').ops[0].leitung.frei.schritt, 5);
  assert.equal('schritt' in T.leitungAenderung(kanalDef, 'kanal', 'frei.schritt', 'ganzzahl', '').ops[0].leitung.frei, false, 'leer = Vorgabe 1 des Kerns');
  assert.match(T.leitungAenderung(kanalDef, 'kanal', 'frei.schritt', 'ganzzahl', '2.5').fehler, /kein Schritt/);
  assert.match(T.leitungAenderung(kanalDef, 'kanal', 'frei.schritt', 'ganzzahl', '0').fehler, /kein Schritt/);
  // die Quellen der Grenzen bleiben unberührt
  const mitQuelle = klon(kanalDef); mitQuelle.leitung.frei.quellen = [{ unterlage: 'U1', seite: 19 }];
  assert.deepEqual(T.leitungAenderung(mitQuelle, 'kanal', 'frei.schritt', 'ganzzahl', '5').ops[0].leitung.frei.quellen, [{ unterlage: 'U1', seite: 19 }]);
  assert.match(feld(mitQuelle, 'frei.vorlage').hinweis, /Quellen der Grenzen: 1/);
  assert.match(feld(kanalDef, 'frei.vorlage').hinweis, /ohne Quelle/);
  assert.equal(T.leitungAenderung(kanalDef, 'kanal', 'frei.schritt', 'ganzzahl', '5').ops.length, 1, 'ein Paket: nur leitung_setzen');
});

test('Editor: Vorlage der Formteile (formteile.<art>.vorlage) mit den Typen der Formteil-Familie', () => {
  const f = T.leitungFelder(kanalDef, { art: 'kanal', familien }).filter((x) => x.pfad.endsWith('.vorlage') && x.pfad.startsWith('formteile.'));
  assert.deepEqual(f.map((x) => x.pfad), ['formteile.bogen.vorlage', 'formteile.tstueck.vorlage', 'formteile.reduktion.vorlage', 'formteile.reinigungsoeffnung.vorlage']);
  const bogen = feld(kanalDef, 'formteile.bogen.vorlage');
  assert.equal(bogen.wert, ''); assert.deepEqual(bogen.optionen, [['400x200', '400x200'], ['500x300', '500x300']]);
  assert.equal(bogen.deaktiviert, false);
  assert.equal(feld(kanalDef, 'formteile.reduktion.vorlage').wert, '400x200/300x200');
  assert.equal(feld(kanalDef, 'formteile.reduktion.vorlage').optionen.at(-1)[1], '400x200/300x200 (Familie nicht im Projekt)');
  assert.equal(feld(kanalDef, 'formteile.reinigungsoeffnung.vorlage').deaktiviert, true, 'ohne Familie nichts zu wählen');
  const gesetzt = T.leitungAenderung(kanalDef, 'kanal', 'formteile.bogen.vorlage', 'auswahl', '500x300');
  assert.equal(gesetzt.ops[0].leitung.formteile.bogen.vorlage, '500x300');
  assert.equal(gesetzt.ops[0].leitung.formteile.bogen.familie, 'fam-test-kanalbogen-frei');
  const weg = T.leitungAenderung(kanalDef, 'kanal', 'formteile.reduktion.vorlage', 'auswahl', '');
  assert.equal('vorlage' in weg.ops[0].leitung.formteile.reduktion, false);
  assert.equal(weg.ops[0].leitung.formteile.reduktion.familie, 'fam-test-kanalreduktion-frei');
  // ohne freie Grösse und ohne Vorlage: das Feld gibt es nicht
  const ohne = klon(kanalDef); delete ohne.leitung.frei; delete ohne.leitung.formteile.reduktion.vorlage;
  assert.deepEqual(T.leitungFelder(ohne, { art: 'kanal', familien }).filter((x) => x.pfad.endsWith('.vorlage') && x.pfad.startsWith('formteile.')), []);
  // wird die Familie einer Art entfernt, entfällt die Art samt Vorlage
  assert.equal('reduktion' in T.leitungAenderung(kanalDef, 'kanal', 'formteile.reduktion.familie', 'auswahl', '').ops[0].leitung.formteile, false);
});

test('Editor: Einsatz «Abwasser» anzeigen und setzen (nur Rohre); Grössenart d bzw. DN', () => {
  const rohr = famDatei('test_rohrserie');
  const einsatz = feld(rohr, 'einsatz', 'rohr');
  assert.equal(einsatz.wert, ''); assert.deepEqual(einsatz.optionen, [['abwasser', 'Abwasser (Schmutz-, Regenwasser)']]);
  assert.equal(feld(rohr, 'groessenart', 'rohr'), undefined, 'ohne Einsatz Abwasser keine Wahl der Grössenart');
  assert.equal(feld(kanalDef, 'einsatz', 'kanal'), undefined, 'Kanäle haben keinen Einsatz');
  const set = T.leitungAenderung(rohr, 'rohr', 'einsatz', 'auswahl', 'abwasser');
  assert.equal(set.ops[0].leitung.einsatz, 'abwasser');
  assert.equal('einsatz' in T.leitungAenderung(abwasserDef, 'rohr', 'einsatz', 'auswahl', '').ops[0].leitung, false);
  // Abwasser-Serie nach d: Feld Einsatz, Grössenart d, Grösse als Aussendurchmesser
  assert.equal(feld(abwasserDef, 'einsatz', 'rohr').wert, 'abwasser');
  assert.equal(feld(abwasserDef, 'groessenart', 'rohr').wert, 'd');
  assert.deepEqual(T.groesseSchluessel(abwasserDef.leitung, 'rohr'), ['d']);
  assert.equal(feld(abwasserDef, 'groesse.d', 'rohr').label, 'Grösse Aussendurchmesser d');
  assert.equal(feld(abwasserDef, 'groesse.d', 'rohr').wert, 'd');
  assert.equal(feld(abwasserDef, 'groesse.dn', 'rohr'), undefined);
  // DN-Serie mit Einsatz Abwasser: Grössenart dn
  const dnAbwasser = klon(rohr); dnAbwasser.leitung.einsatz = 'abwasser';
  assert.equal(feld(dnAbwasser, 'groessenart', 'rohr').wert, 'dn');
  // Grössenart wechseln: neuer Block mit Schlüssel d, Typparameter wird genommen oder (ohne Werte) angelegt
  const nachD = T.leitungAenderung(dnAbwasser, 'rohr', 'groessenart', 'auswahl', 'd');
  assert.deepEqual(nachD.ops[0], { op: 'parameter_setzen', parameter: T.groessenParameterNeu('d') });
  assert.deepEqual(nachD.ops[1].leitung.groesse, { d: 'd' });
  assert.equal(nachD.ops[1].leitung.einsatz, 'abwasser');
  const nachDn = T.leitungAenderung(abwasserDef, 'rohr', 'groessenart', 'auswahl', 'dn');
  assert.deepEqual(nachDn.ops.map((o) => o.op), ['parameter_setzen', 'leitung_setzen'], 'dn gibt es hier nicht als Parameter: neuer Typparameter ohne Werte');
  assert.deepEqual(nachDn.ops[1].leitung.groesse, { dn: 'dn' });
  const vorhanden = T.leitungAenderung(dnAbwasser, 'rohr', 'groessenart', 'auswahl', 'dn');
  assert.deepEqual(vorhanden.ops.map((o) => o.op), ['leitung_setzen'], 'vorhandener Typparameter dn');
  assert.match(T.leitungAenderung(dnAbwasser, 'rohr', 'groessenart', 'auswahl', 'x').fehler, /Grössenart «x» gibt es nicht/);
  // Kontrolltabelle: «d 110»
  assert.equal(T.groesseText({ d: 110 }, { nachAussendurchmesser: true }), 'd 110');
  assert.equal(T.groesseText({ d: 110 }), 'Ø 110');
});

test('Editor: Rohrserie mit Grösse d und Einsatz werden bei der Minimalvorlage nicht verändert', () => {
  const d = T.familieEntwurfMinimal('Rohr', 'R');
  assert.deepEqual(d.leitung, { querschnitt: 'rund', groesse: { dn: 'dn' }, aussen: 'da' });
  assert.deepEqual(T.groesseSchluessel({ querschnitt: 'rund', groesse: { dn: 'dn' } }, 'rohr'), ['dn']);
  assert.deepEqual(T.groesseSchluessel({ querschnitt: 'rund', groesse: { d: 'd' } }, 'kanal'), ['d']);
  assert.deepEqual(T.groesseSchluessel({ querschnitt: 'rechteck' }, 'kanal'), ['b', 'h']);
});

const serieKontrolle = {
  familie: 'FAM-4', name: 'Testkanal frei', kategorie: 'Kanal', querschnitt: 'rechteck', winkel: [45, 90], winkelRegel: 'liste', abzweigwinkel: [90], einsatz: null,
  frei: { vorlage: '400x200', grenzen: { b: { min: 145, max: 2500 }, h: { min: 145, max: 2500 } }, schritt: 1, text: 'frei b × h (145 … 2500 mm, Schritt 1 mm)' },
  groessen: [{ typ: '400x200', gueltig: true, b: 400, h: 200, aussen: 402, innen: null, wand: 1,
    formteile: { bogen: { verfuegbar: true, familie: 'FAM-1', typ: '400x200' }, tstueck: { verfuegbar: true, familie: 'FAM-2', typ: '400x200' }, reinigungsoeffnung: { verfuegbar: false, grund: 'keine Familie', vorschlag: 'ergänzen' }, reduktion: [] } }],
  probe: { groesse: { b: 1323, h: 1323 }, text: '1323 × 1323', gueltig: true, typ: '400x200', frei: true, aussen: 1325, innen: null, wand: 1,
    formteile: { bogen: { verfuegbar: true, familie: 'FAM-1', typ: '400x200', frei: true, parameter: { breite: 1323, hoehe: 1323 } }, tstueck: { verfuegbar: true, familie: 'FAM-2', typ: '400x200', frei: true, parameter: { b_a: 1323 } },
      reinigungsoeffnung: { verfuegbar: false, grund: 'keine Familie' }, reduktion: [{ verfuegbar: true, familie: 'FAM-3', typ: '400x200/300x200', frei: true, parameter: { breite1: 1323 }, auf: '500x300' }] } },
};

test('Editor: Kontrolle einer freien Serie kommt nie ungefragt – Knopf, Laufanzeige mit Abbrechen, Ergebnis mit Probe', () => {
  // Bereit: Knopf statt «wird geladen»
  const bereit = E.kontrolleHtml(null, { aufAbruf: true, lauf: { zustand: 'bereit', sekunden: 0 } });
  assert.match(bereit, /data-aktion="kontrolle-laden"/);
  assert.match(bereit, /Kontrolltabelle und Probe berechnen/);
  assert.ok(!bereit.includes('wird geladen'));
  assert.match(bereit, /nur auf Knopfdruck/);
  // Läuft: Laufzeit und Abbrechen
  const laeuft = E.kontrolleHtml(null, { aufAbruf: true, lauf: { zustand: 'laedt', sekunden: 12 } });
  assert.match(laeuft, /fe-spinner/);
  assert.match(laeuft, /<b class="fe-kontrolle-zeit">12 s<\/b>/);
  assert.match(laeuft, /data-aktion="kontrolle-abbrechen"/);
  assert.ok(!laeuft.includes('data-aktion="kontrolle-laden"'));
  assert.match(laeuft, /mehrere Minuten/);
  // Abgebrochen: noch einmal
  const ab = E.kontrolleHtml(null, { aufAbruf: true, lauf: { zustand: 'abgebrochen' } });
  assert.match(ab, /Abgebrochen/);
  assert.match(ab, /Noch einmal berechnen/);
  // Ergebnis: Tabelle, Probe, Knopf «Neu berechnen»; veraltet nach Änderungen
  const fertig = E.kontrolleHtml(serieKontrolle, { aufAbruf: true, lauf: { zustand: 'fertig' }, veraltet: false });
  assert.match(fertig, /<table[^>]*fe-kontrolle/);
  assert.match(fertig, /Neu berechnen/);
  assert.ok(!fertig.includes('Stand vor der letzten Änderung'));
  assert.match(E.kontrolleHtml(serieKontrolle, { aufAbruf: true, veraltet: true }), /Stand vor der letzten Änderung der Definition/);
  assert.match(fertig, /frei b × h \(145 … 2500 mm, Schritt 1 mm\)/, 'Kopfzeile nennt die freie Grösse');
  // Probe
  assert.match(fertig, /Probe mit einer freien Grösse/);
  assert.match(fertig, /<tr class="fe-probe" data-typ="400x200"><td>1323 × 1323<\/td><td>400x200 <span class="leise">\(frei\)<\/span><\/td>/);
  assert.match(fertig, /data-art="bogen" title="FAM-1 · 400x200 · Exemplarwerte: breite = 1323, hoehe = 1323"/);
  assert.match(fertig, /data-art="reduktion" title="auf 500x300: 400x200\/300x200 \(Exemplarwerte: breite1 = 1323\)"/);
  assert.match(fertig, /fe-nein" data-art="reinigungsoeffnung"/);
  // Probe ungültig
  const ungueltig = E.kontrolleHtml({ ...serieKontrolle, probe: { groesse: { b: 9, h: 9 }, text: '9 × 9', gueltig: false, grund: 'liegt ausserhalb der Grenzen' } }, { aufAbruf: true });
  assert.match(ungueltig, /Die Probegrösse 9 × 9 nimmt die Serie nicht: liegt ausserhalb der Grenzen/);
  // Fehler des Kerns mit Knopf
  assert.match(E.kontrolleHtml({ fehler: 'zu lange', vorschlag: 'x' }, { aufAbruf: true }), /data-aktion="kontrolle-laden"/);
  // Serien ohne freie Grösse: wie bisher, ohne Knopf und ohne Probe
  const alt = E.kontrolleHtml({ ...serieKontrolle, frei: null, probe: null });
  assert.ok(!alt.includes('kontrolle-laden') && !alt.includes('Probe'));
  assert.match(E.kontrolleHtml(null), /wird geladen/);
});

test('Editor: Kontrolltabelle einer Abwasser-Serie zeigt Beschriftung, DN des Herstellers und «d 110»', () => {
  const serie = { familie: 'FAM-8', kategorie: 'Rohr', querschnitt: 'rund', einsatz: 'abwasser', nachAussendurchmesser: true, winkel: [45, 88.5], winkelRegel: 'liste', abzweigwinkel: [45, 88.5], frei: null, probe: null,
    groessen: [
      { typ: 'd 32', gueltig: true, d: 32, aussen: 32, innen: 26, wand: 3, beschriftung: 'PE 32', dnHersteller: null, formteile: { bogen: { verfuegbar: true }, tstueck: { verfuegbar: true }, reinigungsoeffnung: { verfuegbar: true }, reduktion: [] } },
      { typ: 'd 110', gueltig: true, d: 110, aussen: 110, innen: 102, wand: 4, beschriftung: 'PE 110', dnHersteller: 100, formteile: { bogen: { verfuegbar: true }, tstueck: { verfuegbar: true }, reinigungsoeffnung: { verfuegbar: true }, reduktion: [] } },
    ] };
  const h = E.kontrolleHtml(serie);
  assert.match(h, /Einsatz Abwasser \(Grösse nach Aussendurchmesser\)/);
  assert.match(h, /<th[^>]*>Beschriftung<\/th>/);
  assert.match(h, /DN Hersteller/);
  assert.match(h, /<td>d 110<\/td>/, 'Grösse als «d 110»');
  assert.match(h, /<td class="fe-beschriftung">PE 110<\/td><td class="fe-zahl">100<\/td>/);
  assert.match(h, /<td class="fe-beschriftung">PE 32<\/td><td class="fe-zahl">–<\/td>/, 'd 32 ohne DN');
  assert.ok(!h.includes('Probe'), 'Abwasser-Serien haben keine Probe');
});

test('Editor: die Seite zeigt das Formular mit Block frei und Einsatz; freie Serie fragt die Kontrolle nicht von selbst', () => {
  const html = E.leitungTab.zeichne({ def: kanalDef, familien: () => familien, klassen: () => klassen, serie: () => null, kontrolle: () => ({ lauf: { zustand: 'bereit', sekunden: 0 }, veraltet: false }) });
  assert.match(html, /data-pfad="frei\.vorlage" data-ftyp="auswahl"/);
  assert.match(html, /data-pfad="frei\.grenzen\.b\.min" data-ftyp="masse"/);
  assert.match(html, /data-pfad="frei\.schritt" data-ftyp="ganzzahl"/);
  assert.match(html, /data-pfad="formteile\.bogen\.vorlage"/);
  assert.match(html, /data-aktion="kontrolle-laden"/);
  const rohr = E.leitungTab.zeichne({ def: abwasserDef, familien: () => familien, klassen: () => klassen, serie: () => null });
  assert.match(rohr, /data-pfad="einsatz" data-ftyp="auswahl"/);
  assert.match(rohr, /<option value="abwasser" selected>Abwasser/);
  assert.match(rohr, /data-pfad="groessenart"/);
  assert.match(rohr, /wird geladen/, 'Serie ohne freie Grösse: wie bisher');
  assert.ok(!rohr.includes('frei.vorlage'));
  // Knöpfe der Kontrolle gehen an den Editor
  const aufrufe = [];
  const ctx = { kontrolleLaden: () => aufrufe.push('laden'), kontrolleAbbrechen: () => aufrufe.push('abbrechen') };
  E.leitungTab.klick({ dataset: { aktion: 'kontrolle-laden' } }, ctx).eigen();
  E.leitungTab.klick({ dataset: { aktion: 'kontrolle-abbrechen' } }, ctx).eigen();
  assert.deepEqual(aufrufe, ['laden', 'abbrechen']);
  assert.equal(E.leitungTab.klick({ dataset: { aktion: 'x' } }, ctx), null);
  // deaktivierte Auswahl trägt disabled
  assert.match(E.feldHtml({ pfad: 'formteile.reinigungsoeffnung.vorlage', label: 'x', typ: 'auswahl', optionen: [], wert: '', deaktiviert: true }), /<select[^>]* disabled/);
});

// ================================================================================================ Verdrahtung (Lesetests)

test('Verdrahtung: der Editor holt die Abfrage leitungsserie einer freien Serie nur auf Knopfdruck, abbrechbar und mit Laufzeit', () => {
  const ed = lies('../familien/editor.js');
  assert.match(ed, /if \(f\.definition\?\.leitung\?\.frei\) \{/, 'freie Serie: kein automatisches Laden');
  assert.match(ed, /else \{ serieDaten = await serieLaden\(f\)/, 'alle anderen Serien wie bisher');
  assert.match(ed, /abfrage\('leitungsserie', \{ familie: kz \}, \{ signal: ac\.signal \}\)/, 'abbrechbar');
  assert.equal((ed.match(/abfrage\('leitungsserie'/g) ?? []).length, 2, 'eine Stelle für das automatische Laden anderer Serien, eine auf Knopfdruck');
  assert.match(ed, /new AbortController\(\)/);
  assert.match(ed, /setInterval\(/); assert.match(ed, /fe-kontrolle-zeit/);
  assert.match(ed, /kontrolleAbbruch\?\.abort\(\)/, 'beim Schliessen des Editors wird abgebrochen');
  const api = lies('../api.js');
  assert.match(api, /export async function senden\(pfad, koerper = \{\}, \{ signal \} = \{\}\)/);
  assert.match(api, /export function abfrage\(name, parameter = \{\}, opt = \{\}\)/);
});

test('Verdrahtung: Palette, Optionsleiste und Werkzeug lesen freie Serien leicht (nie leitungsserie ungefragt)', () => {
  const daten = lies('./leitung-daten.js');
  assert.match(daten, /if \(f\?\.definition\?\.leitung\?\.frei\) return serieLeichtAus\(f\)/);
  assert.equal((daten.match(/abfrage\('leitungsserie'/g) ?? []).length, 1, 'nur der Zwischenspeicher fragt, und nur für Serien ohne freie Grösse');
  const eig = lies('../oberflaeche/eigenschaften.js');
  assert.match(eig, /serie = await serieLaden\(bezug, f\)/);
  assert.match(eig, /\['lt:serie', 'lt:groesse', 'lt:raster', 'lt:b', 'lt:h', 'lt:d'\]\.includes\(ziel\)/);
  const bearb = lies('./leitung-bearbeiten.js');
  assert.match(bearb, /L\.neueBefundeTexte\(r\)/, 'Befunde P29/P30 nach einer Änderung der Grösse');
  const werkzeug = lies('./leitung.js');
  assert.match(werkzeug, /serie: D\.serieDef\(ctx\.opt\.serie\)/);
  assert.match(werkzeug, /L\.bandBreite\(/);
});

// ================================================================================================ gegen den echten Kern (nur mit HLKS_TEST_SERVER)

const SERVER = process.env.HLKS_TEST_SERVER;
const kern = async (art, name, body, trocken = false) => {
  const r = await fetch(`${SERVER}/api/${art}/${name}${art === 'befehl' ? `?trockenlauf=${trocken}` : ''}`, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body ?? {}) });
  return r.json();
};
const famDir = path.resolve(hier, '../../../../../tests/daten/familien').replace(/\\/g, '/');

test('Kern: nimmt die Parameter der Oberfläche an, die leichte Serie stimmt mit leitungsserie überein, Grenzen und Schritt werden mit Vorschlag abgelehnt', { skip: !SERVER && 'ohne HLKS_TEST_SERVER' }, async () => {
  if (/:(5013|5098|5130|5131|5149|515[0-5])\b/.test(SERVER)) throw new Error('Dieser Test läuft nie gegen die Server der anderen Aufträge.');
  for (const n of ['test_kanalbogen_frei', 'test_kanal_t_frei', 'test_kanalreduktion_frei', 'test_kanalfrei', 'test_abwasser_d_bogen', 'test_abwasser_d_t', 'test_abwasser_d_reduktion', 'test_abwasser_d_serie']) {
    const r = await kern('befehl', 'familie_laden', { datei: `${famDir}/${n}.familie.json` });
    if (!r.angenommen && !/gibt es schon|bereits/i.test(r.fehler ?? '')) console.log('   familie_laden', n, r.fehler);
  }
  const fam = (await kern('abfrage', 'familien')).familien;
  const kanal = fam.find((f) => f.id === 'fam-test-kanalfrei'), abw = fam.find((f) => f.id === 'fam-test-abwasser-d');
  assert.ok(kanal && abw, 'Testfamilien im Projekt');
  // leichte Serie = Kern
  const leicht = F.serieLeichtAus(await kern('abfrage', 'familie', { familie: kanal.kennzeichen }));
  const voll = await kern('abfrage', 'leitungsserie', { familie: kanal.kennzeichen });
  assert.equal(leicht.frei.text, voll.frei.text, 'Text von Grenzen und Schritt');
  assert.deepEqual(leicht.groessen.map((g) => [g.typ, g.b, g.h]), voll.groessen.map((g) => [g.typ, g.b, g.h]));
  assert.equal(leicht.frei.vorlage, voll.frei.vorlage);
  assert.deepEqual(leicht.frei.grenzen, voll.frei.grenzen);
  assert.deepEqual(L.erlaubteWinkel(leicht), L.erlaubteWinkel(voll));
  assert.equal(voll.probe.frei, true, 'Probe mit freier Grösse');
  const leichtA = F.serieLeichtAus(await kern('abfrage', 'familie', { familie: abw.kennzeichen }));
  const vollA = await kern('abfrage', 'leitungsserie', { familie: abw.kennzeichen });
  assert.equal(leichtA.einsatz, vollA.einsatz); assert.equal(leichtA.nachAussendurchmesser, vollA.nachAussendurchmesser);
  assert.deepEqual(F.groessenOptionen(leichtA).map(([, t]) => t), vollA.groessen.map((g) => `d ${g.d}`), '«d 110» wie in der Abfrage des Kerns');
  assert.equal(vollA.groessen.find((g) => g.d === 110).beschriftung, 'PE 110');
  // Systeme für den Trockenlauf
  for (const [kz, typ] of [['T-ZU', 'Zuluft'], ['T-HZ', 'Heizung Vorlauf']]) await kern('befehl', 'element_anlegen', { klasse: 'System', kennzeichen: kz, teilVon: 'A-SAN', eigenschaften: { systemtyp: typ } });
  const punkte = [[14000, 3000, 300], [16000, 3000, 300]];
  // freie Grösse: { b, h } angenommen; Rastergrösse und freie Grösse im Trockenlauf
  const p = L.leitungParameter({ opt: { serie: kanal.kennzeichen, freiB: 430, freiH: 270, hoehe: 300, system: 'T-ZU' }, punkte, serie: leicht }).parameter;
  const r1 = await kern('befehl', 'leitung_zeichnen', { ...p, geschoss: 'EG' }, true);
  assert.equal(r1.angenommen, true, r1.fehler);
  const v = L.vorschauAus(r1);
  assert.equal(v.geraden[0].frei, true, 'der Kern meldet die freie Grösse; die Vorschau zeigt die Marke');
  assert.match(v.geraden[0].groesse, /430 × 270/);
  const r2 = await kern('befehl', 'leitung_zeichnen', { ...L.leitungParameter({ opt: { serie: kanal.kennzeichen, freiB: 400, freiH: 200, hoehe: 300, system: 'T-ZU' }, punkte, serie: leicht }).parameter, geschoss: 'EG' }, true);
  assert.equal(r2.angenommen, true, r2.fehler);
  assert.equal(L.vorschauAus(r2).geraden[0].frei ?? false, false, 'eine Rastergrösse bleibt ein normaler Typ');
  // Grenzen und Schritt: Meldung mit Vorschlag
  const aus = await kern('befehl', 'leitung_zeichnen', { ...L.leitungParameter({ opt: { serie: kanal.kennzeichen, freiB: 3000, freiH: 270, hoehe: 300, system: 'T-ZU' }, punkte, serie: leicht }).parameter, geschoss: 'EG' }, true);
  assert.equal(aus.angenommen, false);
  const aA = L.ablehnung(aus);
  assert.match(aA.text, /ausserhalb der Grenzen/); assert.match(aA.vorschlag, /^2500 × 270/);
  const schritt = await kern('befehl', 'leitung_zeichnen', { ...L.leitungParameter({ opt: { serie: kanal.kennzeichen, freiB: 430.5, freiH: 270, hoehe: 300, system: 'T-ZU' }, punkte, serie: leicht }).parameter, geschoss: 'EG' }, true);
  assert.equal(schritt.angenommen, false);
  assert.match(L.ablehnung(schritt).text, /nicht im Schritt/); assert.match(L.ablehnung(schritt).vorschlag, /^431 × 270/);
  // Abwasser-Serie: Typ «d 110»; mit Heizung als System kommt P30 (fehler) in den neuen Befunden
  const pa = L.leitungParameter({ opt: { serie: abw.kennzeichen, groesse: 'd 110', hoehe: 300, system: 'T-HZ' }, punkte: [[14000, 8000, 300], [16000, 8000, 300]], serie: leichtA }).parameter;
  const ra = await kern('befehl', 'leitung_zeichnen', { ...pa, geschoss: 'EG' }, true);
  assert.equal(ra.angenommen, true, ra.fehler);
  assert.equal(L.vorschauAus(ra).geraden[0].groesse, 'd 110');
  assert.ok(L.neueBefundeTexte(ra).some((t) => /^P30 /.test(t)), 'Befund P30: ' + JSON.stringify(ra.neueBefunde));
  const d32 = await kern('befehl', 'leitung_zeichnen', { ...L.leitungParameter({ opt: { serie: abw.kennzeichen, groesse: 'd 32', hoehe: 300, system: 'T-HZ' }, punkte, serie: leichtA }).parameter, geschoss: 'EG' }, true);
  assert.equal(d32.angenommen, true, 'd 32 ohne DN ist zeichenbar: ' + d32.fehler);
  // Editor: die ops der Oberfläche nimmt der Kern an (Trockenlauf)
  const ops = (def, pfad, typ, text) => T.leitungAenderung(def, T.leitungsArt(def.kategorie, klassen), pfad, typ, text).ops;
  const def = (await kern('abfrage', 'familie', { familie: kanal.kennzeichen })).definition;
  for (const [pfad, typ, text] of [['frei.grenzen.b.min', 'masse', '150'], ['frei.schritt', 'ganzzahl', '5'], ['formteile.bogen.vorlage', 'auswahl', '400x200']]) {
    const t = await kern('befehl', 'familie_aendern', { familie: kanal.kennzeichen, ops: ops(def, pfad, typ, text) }, true);
    assert.equal(t.angenommen, true, `${pfad}: ${t.fehler}`);
  }
  const abDef = (await kern('abfrage', 'familie', { familie: abw.kennzeichen })).definition;
  const ohneEinsatz = await kern('befehl', 'familie_aendern', { familie: abw.kennzeichen, ops: ops(abDef, 'einsatz', 'auswahl', '') }, true);
  assert.equal(ohneEinsatz.angenommen, false, 'Grösse d ohne Einsatz Abwasser lehnt der Kern ab: ' + (ohneEinsatz.fehler ?? ''));
});
