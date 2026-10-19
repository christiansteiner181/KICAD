// Tests der Hilfe (F1) ohne Browser: Schlüssel der Sprungziele (identisch mit der Anleitungsseite), Wahl des Kontexts und Bau der Adresse.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/hilfe.test.mjs
// Die Anbindung an Oberfläche, F1 und window.open prüft hilfe.browser.mjs; die Adresse selbst kommt aus kataloge/buero.json (AnleitungApiTests.cs).

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

const H = await import('./hilfe.js');
const BASIS = 'https://claude.ai/artifact/5NEkGw76J5tk5sxFrbfbkq';

test('Schlüssel: die vorgegebenen Werte', () => {
  assert.equal(H.hilfeSchluessel('Serien-PDF …'), 'serien-pdf');
  assert.equal(H.hilfeSchluessel('Planblätter'), 'planblaetter');
  assert.equal(H.hilfeSchluessel('Ändern'), 'aendern');
  assert.equal(H.hilfeSchluessel('Gebäudetechnik'), 'gebaeudetechnik');
  assert.equal(H.hilfeSchluessel('Typenliste «Typname → Material»'), 'typenliste-typname-material');
  assert.equal(H.hilfeSchluessel(''), '');
});

test('Schlüssel: leere und fremde Eingaben', () => {
  assert.equal(H.hilfeSchluessel(null), '');
  assert.equal(H.hilfeSchluessel(undefined), '');
  assert.equal(H.hilfeSchluessel(0), '');                    // wie die Seite: String(0 || '')
  assert.equal(H.hilfeSchluessel('…'), '');
  assert.equal(H.hilfeSchluessel('  Ändern | Waschtisch  '), 'aendern-waschtisch');
  assert.equal(H.hilfeSchluessel('Strasse ß Öl Übung'), 'strasse-ss-oel-uebung');
  assert.equal(H.hilfeSchluessel('Café è à'), 'cafe-e-a');   // Akzente fallen weg (NFD)
  assert.equal(H.hilfeSchluessel('Gewerk …'), 'gewerk');
  assert.equal(H.hilfeSchluessel('Rückgängig (Strg+Z)'), 'rueckgaengig-strg-z');
});

test('Schlüssel: Text in der Anleitungsseite (vorlage.html) ist derselbe wie hier', () => {
  const hier = path.dirname(fileURLToPath(import.meta.url));
  const seite = readFileSync(path.resolve(hier, '../../../../../werkzeuge/anleitung/vorlage.html'), 'utf8');
  const quelle = readFileSync(path.join(hier, 'hilfe.js'), 'utf8');
  const ausdruck = (t, kopf) => {
    const i = t.indexOf(kopf);
    assert.ok(i >= 0, `${kopf} fehlt`);
    const ende = t.indexOf("replace(/^-+|-+$/g, '');", i);
    return t.slice(t.indexOf('t =>', i), ende).replace(/\s+/g, ' ');
  };
  assert.equal(ausdruck(quelle, 'export const hilfeSchluessel'), ausdruck(seite, 'const hilfeSchluessel'));
});

test('Kontext: Dialog geht vor Knopf, Knopf vor keinem', () => {
  const knopf = { register: 'Ansicht', knopf: 'Serien-PDF …' };
  assert.deepEqual(H.hilfeKontext({ dialogTitel: 'Serien-PDF', knopf }), { art: 'dialog', titel: 'Serien-PDF' });
  assert.deepEqual(H.hilfeKontext({ dialogTitel: null, knopf }), { art: 'knopf', register: 'Ansicht', knopf: 'Serien-PDF …' });
  assert.deepEqual(H.hilfeKontext({ knopf }), { art: 'knopf', register: 'Ansicht', knopf: 'Serien-PDF …' });
  assert.deepEqual(H.hilfeKontext({}), { art: 'keiner' });
  assert.deepEqual(H.hilfeKontext(), { art: 'keiner' });
  assert.deepEqual(H.hilfeKontext({ dialogTitel: '  ', knopf: null }), { art: 'keiner' });
  assert.deepEqual(H.hilfeKontext({ knopf: { register: 'Ansicht', knopf: '' } }), { art: 'keiner' });
});

test('Adresse: Knopf im Register, Dialog, ohne Kontext', () => {
  assert.equal(H.hilfeAdresse(BASIS, { art: 'knopf', register: 'Ansicht', knopf: 'Serien-PDF …' }), `${BASIS}#k-ansicht~serien-pdf`);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'knopf', register: 'Ändern | Waschtisch', knopf: 'Aufteilen mit Serie …' }), `${BASIS}#k-aendern-waschtisch~aufteilen-mit-serie`);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'knopf', register: 'Gebäudetechnik', knopf: 'Rohr' }), `${BASIS}#k-gebaeudetechnik~rohr`);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'dialog', titel: 'Typenliste «Typname → Material»' }), `${BASIS}#s-typenliste-typname-material`);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'dialog', titel: 'Serien-PDF' }), `${BASIS}#s-serien-pdf`);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'keiner' }), BASIS);
  assert.equal(H.hilfeAdresse(BASIS), BASIS);
});

test('Adresse: ohne Register nur #k-<knopf>; ohne Schlüssel bleibt die Basisadresse', () => {
  assert.equal(H.hilfeAdresse(BASIS, { art: 'knopf', register: '', knopf: 'Rückgängig' }), `${BASIS}#k-rueckgaengig`);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'knopf', register: 'Ansicht', knopf: '…' }), BASIS);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'dialog', titel: '…' }), BASIS);
  assert.equal(H.hilfeAdresse(BASIS, { art: 'unbekannt' }), BASIS);
});

test('Adresse: Fragment der Basisadresse fällt weg, Leerraum wird getrimmt', () => {
  assert.equal(H.hilfeAdresse(`  ${BASIS}#alt `, { art: 'dialog', titel: 'Gewerk des Plans' }), `${BASIS}#s-gewerk-des-plans`);
  assert.equal(H.hilfeAdresse(`${BASIS}#alt`, { art: 'keiner' }), BASIS);
});

test('Adresse: fehlt die Basisadresse, ist das Ergebnis null (Meldung «nicht eingerichtet»)', () => {
  for (const b of [null, undefined, '', '   ', '#nur-fragment'])
    for (const k of [{ art: 'keiner' }, { art: 'knopf', register: 'Ansicht', knopf: 'Serien-PDF …' }, { art: 'dialog', titel: 'Serien-PDF' }])
      assert.equal(H.hilfeAdresse(b, k), null, `${b} / ${k.art}`);
});

test('Texte: Tooltip, letzte Zeile, Meldungen, Fenstername', () => {
  assert.equal(H.HILFE_TOOLTIP, 'Hilfe – Bedienungsanleitung (F1)');
  assert.equal(H.HILFE_ZEILE, 'F1: Hilfe zu diesem Befehl');
  assert.equal(H.HILFE_NICHT_EINGERICHTET, 'Die Bedienungsanleitung ist noch nicht eingerichtet.');
  assert.equal(H.hilfeBlockiert(BASIS), `Das Hilfefenster wurde blockiert – Adresse: ${BASIS}`);
  assert.equal(H.HILFE_FENSTER, 'kicad-hilfe');
});
