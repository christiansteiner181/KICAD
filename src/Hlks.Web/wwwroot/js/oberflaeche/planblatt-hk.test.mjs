// Tests der reinen Bausteine «Planblatt wie H+K» (Modul 7 Teil A) ohne Browser: Revisionstabelle, Wert der Eigenschaft, Plannummern-Vorschlag im Dialog,
// Stempel im Serien-PDF, Verdrahtung der Aktionen. Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/planblatt-hk.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as P from './planblatt-hk.js';
import * as S from './serienpdf-text.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');

test('revisionen: liest die Eigenschaft, ältere Einträge mit text statt beschreibung, Unsinn wird ignoriert', () => {
  assert.deepEqual(P.revisionen(undefined), []);
  assert.deepEqual(P.revisionen('x'), []);
  assert.deepEqual(P.revisionen([{ index: 'A', datum: '20.05.2025', beschreibung: 'Anpassungen' }, { index: 'B', datum: '11.06.2025', text: 'ergänzt' }, null, 3]),
    [{ index: 'A', datum: '20.05.2025', beschreibung: 'Anpassungen' }, { index: 'B', datum: '11.06.2025', beschreibung: 'ergänzt' }]);
});

test('revisionFeld: Tabelle Index/Datum/Beschreibung, ein Eingabefeld je Zelle, Knopf «+ Revision hinzufügen», Ziel eig:revisionen', () => {
  const h = P.revisionFeld([{ index: 'A', datum: '20.05.2025', beschreibung: 'a <b> & c' }]);
  assert.match(h, /data-ziel="eig:revisionen" data-typ="revisionen"/);
  assert.match(h, /<th[^>]*>Index<\/th><th>Datum<\/th><th>Beschreibung<\/th>/);
  assert.equal((h.match(/class="rev-i"/g) ?? []).length, 1);
  assert.match(h, /value="a &lt;b&gt; &amp; c"/, 'Text wird maskiert');
  assert.match(h, /class="ep-rev-neu"[^>]*>\+ Revision hinzufügen</);
  assert.match(h, /class="ep-rev-weg"/);
  assert.match(h, /Letzte Revision: A/);
  assert.match(P.revisionFeld([]), /Noch keine Revision/);
  assert.equal((P.revisionFeld([]).match(/<tr>/g) ?? []).length, 1, 'nur die Kopfzeile');
});

test('revisionenWert: leere Zeilen entfallen, Text wird getrimmt, keine Zeile → null (Eigenschaft entfernen)', () => {
  assert.equal(P.revisionenWert([]), null);
  assert.equal(P.revisionenWert([{ index: ' ', datum: '', beschreibung: '' }]), null);
  assert.deepEqual(P.revisionenWert([{ index: ' A ', datum: '20.05.2025 ', beschreibung: ' x ' }, { index: '', datum: '', beschreibung: '' }, { index: 'B', datum: '', beschreibung: 'y' }]),
    [{ index: 'A', datum: '20.05.2025', beschreibung: 'x' }, { index: 'B', datum: '', beschreibung: 'y' }]);
});

test('zeilenAusTabelle: liest die Felder der Zeilen (DOM-Attrappe)', () => {
  const feld = { querySelectorAll: () => [{ querySelector: (s) => ({ '.rev-i': { value: 'A' }, '.rev-d': { value: '1.1.2026' }, '.rev-b': { value: 'x' } })[s] }] };
  assert.deepEqual(P.zeilenAusTabelle(feld), [{ index: 'A', datum: '1.1.2026', beschreibung: 'x' }]);
});

test('vorschlagParameter: leere Angaben entfallen, Geschossplan ist die Vorgabe', () => {
  assert.deepEqual(P.vorschlagParameter({ gewerk: '', geschoss: '', art: 'geschoss' }), {});
  assert.deepEqual(P.vorschlagParameter({ gewerk: 'heizung', geschoss: 'EG', art: 'geschoss' }), { gewerk: 'heizung', geschoss: 'EG' });
  assert.deepEqual(P.vorschlagParameter({ gewerk: 'heizung', geschoss: 'EG', art: 'fbh' }), { gewerk: 'heizung', geschoss: 'EG', art: 'fbh' });
});

test('vorschlagText: Begründung und Hinweise des Kerns, wörtlich', () => {
  assert.equal(P.vorschlagText(null), '');
  assert.equal(P.vorschlagText({ begruendung: 'Heizung EG: Geschoss 3 von 6', hinweise: ['H03 ist schon vergeben (PB-1).'] }), 'Heizung EG: Geschoss 3 von 6 H03 ist schon vergeben (PB-1).');
  assert.equal(P.vorschlagText({ begruendung: 'Das Gewerk fehlt.' }), 'Das Gewerk fehlt.');
});

test('ART_OPTIONEN: die Arten der Abfrage plannummer_vorschlag, Geschossplan zuerst', () => {
  assert.deepEqual(P.ART_OPTIONEN.map(([v]) => v), ['geschoss', 'fbh', 'steigzonen', 'grundleitungen', 'einlagen', 'kanalisation']);
});

test('Serien-PDF: Stempel nur mit Häkchen im Parameter, Vorschlag «Revisionsplan TT.MM.JJJJ»', () => {
  assert.equal(S.stempelVorschlag(new Date(2026, 0, 21)), 'Revisionsplan 21.01.2026');
  assert.equal(S.stempelVorschlag(new Date(2026, 9, 5)), 'Revisionsplan 05.10.2026');
  const satz = { schluessel: 'auto:heizung' };
  const liste = [{ planblatt: 'PB-1', dabei: true }];
  assert.equal('stempel' in S.exportParameter(satz, liste, { anlass: '', einzeln: false }), false, 'ohne Häkchen kein Parameter');
  assert.equal(S.exportParameter(satz, liste, { anlass: '', einzeln: false, stempelAn: true, stempel: ' Revisionsplan 21.01.2026 ' }).stempel, 'Revisionsplan 21.01.2026');
  assert.match(S.exportPruefen(liste, { stempelAn: true, stempel: ' ' }), /Revisionsstempel/);
  assert.equal(S.exportPruefen(liste, { stempelAn: true, stempel: 'x' }), true);
  const f = S.formularAus({ ausgabe: {}, vorschlag: {} });
  assert.equal(f.stempelAn, false);
  assert.match(f.stempel, /^Revisionsplan \d\d\.\d\d\.\d{4}$/);
});

test('Verdrahtung: Aktion plan.revision im Register Ansicht, Kontextmenü, Eigenschaften, Serien-PDF-Dialog, Dialog «Planblatt anlegen»', () => {
  const plaene = lies('aktionen-plaene.js');
  assert.match(plaene, /id: 'plan\.revision'[^}]*register: 'ansicht', gruppe: 'Planblätter'/);
  assert.match(plaene, /sende\('revision_hinzufuegen'/);
  assert.match(plaene, /abfrage\('plannummer_vorschlag'/);
  assert.match(plaene, /PLANARTEN = new Set\(\['grundriss', 'schnitt', 'ansicht', 'schema', 'liste'\]\)/);
  assert.match(lies('projektbrowser.js'), /eintrag\('Revision …', 'plan\.revision', p\)/);
  const eig = lies('eigenschaften.js');
  assert.match(eig, /el\.klasse === 'Planblatt' && d\.name === 'revisionen'/);
  assert.match(eig, /ausfuehren\('plan\.revision'/);
  assert.match(eig, /case 'revisionen': return revisionenWert/);
  const dlg = lies('serienpdf-dialog.js');
  assert.match(dlg, /sende\('plansatz_revision_hinzufuegen'/);
  assert.match(dlg, /sp-stempel-an/);
});

test('Keine Logik in der Oberfläche: kein Buchstabe wird hier hochgezählt, kein Datum gesetzt, keine Plannummer gebildet', () => {
  const plaene = lies('aktionen-plaene.js'), pal = lies('planblatt-hk.js');
  for (const t of [plaene, pal]) {
    assert.doesNotMatch(t, /String\.fromCharCode|charCodeAt/, 'Index kommt vom Kern');
    assert.doesNotMatch(t, /new Date\(/, 'Datum der Revision kommt vom Kern');
  }
  assert.doesNotMatch(plaene, /padStart\(2/, 'Plannummer kommt vom Kern');
});
