// Tests der Texte und Parameter für das Serien-PDF (Plansätze, entwurf/serien-pdf.md) ohne Browser: Zeilen aus der Antwort des Kerns, Verschieben,
// Zusammenführen nach dem Nachladen, Parameter für plansatz_pdf, Gruppierung für den Projektbrowser, Texte, Verdrahtung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/serienpdf.test.mjs
// Die Beispieldaten haben die Form der Antwort der Abfrage plansaetze (Kern: Plaene.Auskunft); Namen und Nummern sind fiktiv.

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as T from './serienpdf-text.js';

const ordner = (n) => new URL(n, import.meta.url);
const lies = (n) => readFileSync(ordner(n), 'utf8');

const blatt = (pb, nummer, titel, format, gewerk = 'sanitaer', herkunft = 'vorschlag', dabei = true) => ({
  planblatt: pb, nummer, nummerKurz: nummer.replace(/^1-0768[ -]/, ''), titel, format, ausrichtung: 'quer', gewerk, gewerkName: { sanitaer: 'Sanitär', heizung: 'Heizung', lueftung: 'Lüftung' }[gewerk],
  gewerkHerkunft: herkunft, gewerkQuelle: herkunft === 'vorschlag' ? 'Fachbuchstabe S der Plannummer' : 'am Planblatt gesetzt', dabei, ansichten: 1, einzelDatei: `${nummer}.pdf`,
});
const antwort = () => ({
  gewerke: [{ gewerk: 'heizung', name: 'Heizung' }, { gewerk: 'lueftung', name: 'Lüftung' }, { gewerk: 'sanitaer', name: 'Sanitär' }],
  plansaetze: [
    { plansatz: null, schluessel: 'auto:heizung', name: 'Plansatz Heizung', gewerk: 'heizung', gewerkName: 'Heizung', automatisch: true, angelegt: false, anzahl: 1, dabei: 1,
      blaetter: [blatt('PB-2', '1-0768 H01', 'Heizung 1.UG', 'A1', 'heizung')], ausgabe: {}, vorschlag: { anlass: '', ordner: 'W:\\x\\Heizung', sammelDatei: 'a.pdf' }, hinweise: [] },
    { plansatz: 'PS-1', schluessel: 'PS-1', name: 'Plansatz Sanitär', gewerk: 'sanitaer', gewerkName: 'Sanitär', automatisch: true, angelegt: true, anzahl: 3, dabei: 2,
      blaetter: [blatt('PB-4', '1-0768 S01', 'Sanitär 1.UG', 'A1'), blatt('PB-1', '1-0768 S02', 'Sanitär 1.OG', 'A3', 'sanitaer', 'gesetzt', false), blatt('PB-6', '1-0768 S03', 'Dach', 'A3')],
      ausgabe: { anlass: 'Revisionspläne', einzeln: true }, vorschlag: { anlass: 'Revisionspläne', ordner: 'W:\\x\\Sanitär', sammelDatei: 'b.pdf' }, hinweise: [] },
    { plansatz: 'PS-2', schluessel: 'PS-2', name: 'Für den Bauherrn', gewerk: null, gewerkName: null, automatisch: false, angelegt: true, anzahl: 1, dabei: 1,
      blaetter: [blatt('PB-2', '1-0768 H01', 'Heizung 1.UG', 'A1', 'heizung')], weitere: [{ planblatt: 'PB-4', nummer: '1-0768 S01', titel: 'Sanitär 1.UG', format: 'A1' }],
      ausgabe: { ordner: 'D:\\frei' }, vorschlag: { anlass: '', ordner: 'W:\\x\\Für den Bauherrn', sammelDatei: 'c.pdf' }, hinweise: [] },
  ],
});

test('Plansätze: automatische und eigene getrennt, Zeile mit Zahl und «angeboten»', () => {
  const l = T.satzListe(antwort());
  assert.deepEqual(l.automatische.map((s) => s.schluessel), ['auto:heizung', 'PS-1']);
  assert.deepEqual(l.eigene.map((s) => s.schluessel), ['PS-2']);
  assert.equal(T.satzZeile(l.automatische[0]).angeboten, true);
  assert.equal(T.satzZeile(l.automatische[1]).angeboten, false);
  assert.equal(T.satzZeile(l.automatische[1]).zahlText, '2 von 3');
  assert.equal(T.satzZeile(l.automatische[0]).zahlText, '1');
  assert.deepEqual(T.satzListe(null), { automatische: [], eigene: [] });
});

test('findeSatz: Schlüssel, Kennzeichen, auto:<gewerk> auch nach dem Anlegen', () => {
  const a = antwort();
  assert.equal(T.findeSatz(a, 'PS-1').name, 'Plansatz Sanitär');
  assert.equal(T.findeSatz(a, 'auto:sanitaer').schluessel, 'PS-1');   // angebotener Satz heisst jetzt PS-1
  assert.equal(T.findeSatz(a, 'auto:heizung').schluessel, 'auto:heizung');
  assert.equal(T.findeSatz(a, 'x', 'heizung').schluessel, 'auto:heizung');
  assert.equal(T.findeSatz(a, 'gibtsnicht'), null);
  assert.equal(T.findeSatz(a, ''), null);
});

test('Zeilen eines Satzes: Kopie in der Reihenfolge des Kerns', () => {
  const s = antwort().plansaetze[1];
  const z = T.zeilen(s);
  assert.deepEqual(z.map((x) => x.planblatt), ['PB-4', 'PB-1', 'PB-6']);
  assert.deepEqual(z.map((x) => x.dabei), [true, false, true]);
  z[0].dabei = false;
  assert.equal(s.blaetter[0].dabei, true, 'keine Kopie auf dem Original');
  assert.equal(T.zaehlText(z), '1 von 3 Plänen gewählt');
  assert.equal(T.zaehlText([z[0]]), '0 von 1 Plan gewählt');
});

test('Verschieben: ziehen, ↑↓, Grenzen', () => {
  const l = ['a', 'b', 'c', 'd'].map((planblatt) => ({ planblatt }));
  const namen = (x) => x.map((z) => z.planblatt).join('');
  assert.equal(namen(T.schieben(l, 0, 2)), 'bcad');
  assert.equal(namen(T.schieben(l, 3, 0)), 'dabc');
  assert.equal(namen(T.hoch(l, 2)), 'acbd');
  assert.equal(namen(T.runter(l, 0)), 'bacd');
  assert.equal(T.hoch(l, 0), l, 'oben: unverändert');
  assert.equal(T.runter(l, 3), l, 'unten: unverändert');
  assert.equal(T.schieben(l, 1, 1), l);
  assert.equal(T.schieben(l, -1, 2), l);
  assert.equal(namen(l), 'abcd', 'das Original bleibt');
});

test('Zusammenführen: Reihenfolge und Häkchen des Menschen bleiben, Neues kommt an seine Stelle, Entferntes entfällt', () => {
  const zeile = (pb, dabei = true) => ({ planblatt: pb, dabei, titel: pb });
  const alt = [zeile('C', false), zeile('A'), zeile('B')];       // vom Menschen umgestellt, C abgewählt
  const neu = [zeile('A'), zeile('B'), zeile('C'), zeile('D')];   // Kern: D neu
  const m = T.zusammenfuehren(alt, neu);
  assert.deepEqual(m.map((z) => z.planblatt), ['C', 'A', 'B', 'D']);
  assert.deepEqual(m.map((z) => z.dabei), [false, true, true, true]);
  // ein Blatt weg (anderes Gewerk), eines dazwischen neu
  const m2 = T.zusammenfuehren(alt, [zeile('A'), zeile('X'), zeile('C')]);
  assert.deepEqual(m2.map((z) => z.planblatt), ['X', 'C', 'A']);   // X steht im Kern vor C: es kommt vor das erste behaltene Blatt, das im Kern danach steht
  assert.deepEqual(T.zusammenfuehren([], neu).map((z) => z.planblatt), ['A', 'B', 'C', 'D']);
});

test('Parameter von plansatz_pdf: Reihenfolge, Abgewählte, Ordner nur von Hand', () => {
  const s = antwort().plansaetze[1];
  const z = T.zeilen(s);
  const f = T.formularAus(s);
  assert.deepEqual(f, { anlass: 'Revisionspläne', ordner: 'W:\\x\\Sanitär', ordnerVonHand: false, einzeln: true, stempelAn: false, stempel: T.stempelVorschlag() });   // Modul 7 Teil A: Stempel aus, Vorschlag «Revisionsplan <Datum>»
  const p = T.exportParameter(s, z, f);
  assert.deepEqual(p, { plansatz: 'PS-1', blaetter: ['PB-4', 'PB-1', 'PB-6'], abgewaehlt: ['PB-1'], anlass: 'Revisionspläne', ordner: '', einzeln: true });
  const von = T.exportParameter(s, T.hoch(z, 2), { ...f, anlass: '  Eingabe ', ordner: ' D:\\Aus ', ordnerVonHand: true, einzeln: false }, { pruefen: true });
  assert.deepEqual(von, { plansatz: 'PS-1', blaetter: ['PB-4', 'PB-6', 'PB-1'], abgewaehlt: ['PB-1'], anlass: 'Eingabe', ordner: 'D:\\Aus', einzeln: false, pruefen: true });
  // gespeicherter eigener Ordner kommt als «von Hand» wieder
  const hand = T.formularAus(antwort().plansaetze[2]);
  assert.equal(hand.ordnerVonHand, true);
  assert.equal(hand.ordner, 'D:\\frei');
  assert.deepEqual(T.formularAus(null), { anlass: '', ordner: '', ordnerVonHand: false, einzeln: false, stempelAn: false, stempel: T.stempelVorschlag() });
});

test('Prüfung vor dem Export', () => {
  const z = T.zeilen(antwort().plansaetze[1]);
  assert.equal(T.exportPruefen(z, { ordnerVonHand: false }), true);
  assert.match(T.exportPruefen(z.map((x) => ({ ...x, dabei: false })), { ordnerVonHand: false }), /Mindestens ein Plan/);
  assert.match(T.exportPruefen(z, { ordnerVonHand: true, ordner: '  ' }), /Ordner ist leer/);
  assert.equal(T.exportPruefen(z, { ordnerVonHand: true, ordner: 'D:\\x' }), true);
});

test('Umfang, Titel, Gewerk-Tooltip', () => {
  const s = antwort().plansaetze[1];
  const z = T.zeilen(s);
  assert.equal(T.umfangText(z, false), '1 Sammel-PDF mit 2 Seiten');
  assert.equal(T.umfangText(z, true), '1 Sammel-PDF mit 2 Seiten und 2 Einzel-PDFs');
  assert.equal(T.umfangText([z[0]], true), '1 Sammel-PDF mit 1 Seite und 1 Einzel-PDF');
  assert.equal(T.umfangText([], false), 'Kein Plan gewählt.');
  assert.equal(T.satzTitel(s), 'Plansatz Sanitär · automatisch, Gewerk Sanitär');
  assert.match(T.satzTitel(antwort().plansaetze[0]), /noch nicht angelegt/);
  assert.equal(T.satzTitel(antwort().plansaetze[2]), 'Für den Bauherrn · von Hand zusammengestellt');
  assert.match(T.gewerkTip(z[0]), /Vorschlag \(Fachbuchstabe S der Plannummer\).*bestätigen/);
  assert.match(T.gewerkTip(z[1]), /von Hand gesetzt/);
});

test('Texte: Grösse, Dauer, Ergebnis, Dateiliste maskiert, Rückfrage beim Überschreiben', () => {
  assert.equal(T.groesseText(512), '512 B');
  assert.equal(T.groesseText(2048), '2,0 KB');
  assert.equal(T.groesseText(215000), '210 KB');
  assert.equal(T.groesseText(3.5 * 1024 * 1024), '3,5 MB');
  assert.equal(T.groesseText(null), '');
  assert.equal(T.dauerText(240), '240 ms');
  assert.equal(T.dauerText(1500), '1,5 s');
  const a = { dateien: [{ art: 'sammel', name: 'S<1>.pdf', datei: 'D:\\x\\S<1>.pdf', seiten: 3, groesseBytes: 215000 }, { art: 'einzeln', name: 'E.pdf', datei: 'D:\\x\\E.pdf', seiten: 1, groesseBytes: 100 }],
    seiten: 3, groesseBytes: 215100, dauerMs: 850 };
  assert.equal(T.ergebnisText(a), '2 Dateien geschrieben · 3 Seiten im Sammel-PDF · 210 KB · 850 ms');
  const h = T.dateiListeHtml(a);
  assert.match(h, /Sammel-PDF/);
  assert.match(h, /Einzel-PDF/);
  assert.match(h, /S&lt;1&gt;\.pdf/);
  assert.doesNotMatch(h, /S<1>/);
  assert.match(h, /3 Seiten/);
  assert.match(h, /1 Seite</);
  assert.match(T.ueberschreibenText({ vorhanden: ['D:\\x\\a.pdf'] }), /Diese Datei gibt es im Ordner schon:\n\na\.pdf\n\nÜberschreiben\?/);
  assert.match(T.ueberschreibenText({ vorhanden: Array.from({ length: 8 }, (_, i) => `D:\\x\\${i}.pdf`) }), /Diese 8 Dateien geben es .*… und 2 weitere/s);
  assert.equal(T.fehlerText({ message: 'M', vorschlag: 'V' }), 'M – V');
  assert.equal(T.fehlerText(new Error('nur')), 'nur');
});

test('Projektbrowser: Pläne nach Gewerk gruppiert, Auskunft zum Gewerk eines Blatts', () => {
  const g = T.gewerkGruppen(antwort());
  assert.deepEqual(g.map((x) => x.gewerk), ['heizung', 'sanitaer']);   // nur die automatischen Sätze: jedes Blatt in genau einer Gruppe
  assert.deepEqual(g[1].blaetter.map((b) => b.planblatt), ['PB-4', 'PB-1', 'PB-6']);
  assert.equal(g[1].name, 'Sanitär');
  assert.equal(g[1].schluessel, 'PS-1');
  assert.equal(T.blattGewerk(antwort(), 'PB-1').gewerkHerkunft, 'gesetzt');
  assert.equal(T.blattGewerk(antwort(), 'PB-4').gewerkHerkunft, 'vorschlag');
  assert.equal(T.blattGewerk(antwort(), 'PB-99'), null);
  assert.deepEqual(T.gewerkGruppen(null), []);
});

test('Nachladen des Browsers nur bei Änderungen an Plänen, Plansätzen, Ansichten', () => {
  const klasse = (id) => ({ a: 'Planblatt', b: 'Wand', c: 'Ansichtsfenster' }[id]);
  assert.equal(T.betrifftPlaene({ alles: true }, klasse), true);
  assert.equal(T.betrifftPlaene({ ids: ['b'] }, klasse), false);
  assert.equal(T.betrifftPlaene({ ids: ['b', 'a'] }, klasse), true);
  assert.equal(T.betrifftPlaene({ ids: ['c'] }, klasse), true);
  assert.equal(T.betrifftPlaene({ ids: ['b'], geloescht: ['z'] }, klasse), true);
  assert.equal(T.betrifftPlaene({ ids: [] }, klasse), false);
});

test('Verdrahtung: Aktionen, Symbol, Stylesheet, Projektbrowser, Dialog über die Kern-Tür', () => {
  const aktionen = lies('./aktionen.js');
  assert.match(aktionen, /import '\.\/aktionen-serienpdf\.js'/);
  const a = lies('./aktionen-serienpdf.js');
  assert.match(a, /id: 'plan\.serienpdf', titel: 'Serien-PDF …'.*register: 'ansicht', gruppe: 'Planblätter'/);
  assert.match(a, /id: 'plan\.gewerk'/);
  assert.match(a, /id: 'plan\.gewerk-bestaetigen'/);
  assert.match(a, /planblatt_gewerk_setzen/);
  assert.match(lies('./symbole.js'), /serienpdf:/);
  assert.match(lies('../../index.html'), /css\/serienpdf\.css/);
  const pb = lies('./projektbrowser.js');   // G1: die Äste stehen in projektbrowser-aeste.js, Daten (ctx.plansaetze) liefert projektbrowser.js
  assert.match(lies('./projektbrowser-aeste.js'), /gewerkGruppen\(ctx\.plansaetze\)/);
  assert.match(pb, /plansaetze: plansaetzeDaten\(\)/);
  assert.match(pb, /eintrag\('Serien-PDF …', 'plan\.serienpdf'/);
  assert.match(pb, /plaeneAstZeile = kontextArt === 'plaene'/);
  const d = lies('./serienpdf-dialog.js');
  for (const name of ['plansaetze', 'plansatz_pdf', 'plansatz_setzen', 'planblatt_gewerk_setzen']) assert.ok(d.includes(`'${name}'`), name);
  assert.match(d, /pruefen: true/);
  assert.match(d, /ueberschreiben/);
  assert.match(d, /\/api\/ordner\/zeigen/);
  // keine Fachlogik im Dialog: Gewerk-Buchstaben, Plannummer-Muster, Ordnerbau stehen nicht dort
  for (const f of ['serienpdf-dialog.js', 'serienpdf-text.js', 'serienpdf-daten.js']) {
    const q = lies('./' + f);
    assert.doesNotMatch(q, /d_Ausgang|c_Pläne und Modelle|Fachbuchstabe\s*=|\[HLSK\]|\\d\{4\}/, f);
  }
});
