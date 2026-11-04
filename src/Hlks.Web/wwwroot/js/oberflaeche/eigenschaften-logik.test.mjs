// Tests zu eigenschaften-logik.js (Gliederung der Eigenschaftenpalette, 04.11.2026).
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { eigenschaftTitel, eigenschaftsGruppe } from './eigenschaften-logik.js';

test('Titel: feste Begriffe, Binnenmajuskel als Wörter, Umlaute in Wortstämmen', () => {
  assert.equal(eigenschaftTitel('verlAnfang'), 'Verlängerung Anfang');
  assert.equal(eigenschaftTitel('lichteHoehe'), 'Lichte Höhe');
  assert.equal(eigenschaftTitel('daemmdicke'), 'Dämmdicke');
  assert.equal(eigenschaftTitel('material'), 'Material');
  assert.equal(eigenschaftTitel('volumenstromMin'), 'Volumenstrom min.');
  assert.equal(eigenschaftTitel('quelle'), 'Quelle');            // kein falsches ü
});

test('Gruppen: Längen und Winkel = Abmessungen, andere Einheiten = Kennwerte, ohne Einheit = Eigenschaften', () => {
  assert.equal(eigenschaftsGruppe({ name: 'verlAnfang', typ: 'zahl', einheit: 'mm' }), 'Abmessungen');
  assert.equal(eigenschaftsGruppe({ name: 'leistung', typ: 'zahl', einheit: 'W' }), 'Kennwerte');
  assert.equal(eigenschaftsGruppe({ name: 'uWert', typ: 'zahl', einheit: 'W/(m²·K)' }), 'Kennwerte');
  assert.equal(eigenschaftsGruppe({ name: 'material', typ: 'auswahl' }), 'Eigenschaften');
  assert.equal(eigenschaftsGruppe({ name: 'personen', typ: 'zahl' }), 'Eigenschaften');
});
