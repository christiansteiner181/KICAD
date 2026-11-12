// Lagen der Optionsleiste folgen dem Modell (Gesamtprüfung 13.11.2026): anderes Projekt = leer, Änderung an Geschoss/Decke = neu geholt.
// Werte fiktiv. Lauf: node --test js/werkzeuge/lagen-daten.test.mjs (aus wwwroot)

import { test } from 'node:test';
import assert from 'node:assert/strict';

let ukd = 2700, anfragen = 0;
globalThis.fetch = async () => {
  anfragen++;
  const wert = ukd;
  return { ok: true, headers: { get: () => 'application/json' },
    json: async () => ({ geschosse: [{ geschoss: 'EG', ukRohdecke: wert, lagen: [{ nr: 1, abstand: 120, hoehe: wert - 120 }] }] }) };
};

const { melden } = await import('../zustand.js');
const { lagenLaden, lagenFuer, ukdFuer } = await import('./lagen-daten.js');

test('Lagen: anderes Projekt leert den Stand, Änderungen am Modell holen ihn neu', async () => {
  await lagenLaden();
  assert.equal(ukdFuer('EG'), 2700);
  assert.equal(lagenFuer('EG')[0].hoehe, 2580);
  assert.equal(anfragen, 1);

  // Rohr geändert: die Lagen bleiben, keine neue Abfrage
  melden('modell', { ids: ['gibt-es-nicht'], geloescht: [] });
  assert.equal(ukdFuer('EG'), 2700);
  assert.equal(anfragen, 1);

  // etwas gelöscht (z. B. eine Decke): alter Stand gilt, bis der neue da ist
  ukd = 2650;
  melden('modell', { ids: [], geloescht: ['decke-1'] });
  assert.equal(ukdFuer('EG'), 2700);
  await lagenLaden();
  assert.equal(ukdFuer('EG'), 2650);
  assert.equal(anfragen, 2);

  // anderes Projekt geladen: sofort leer (nicht die Lagen des letzten Projekts), dann die neuen
  ukd = 3000;
  melden('modell', { alles: true });
  assert.equal(ukdFuer('EG'), null);
  assert.deepEqual(lagenFuer('EG'), []);
  await lagenLaden();
  assert.equal(ukdFuer('EG'), 3000);
});
