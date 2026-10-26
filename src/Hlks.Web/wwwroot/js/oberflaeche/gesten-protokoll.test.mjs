// Abbruchprotokoll der Gesten (Modul G): nur erlaubte Felder, nur Abbrüche, Sendefehler bleiben still.
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { abbruchEintrag, gestenProtokollEinrichten } from './gesten-protokoll.js';

test('abbruchEintrag: nur geste, dauerMs, grund und Zahlen in werte; andere Ereignisse nicht', () => {
  const e = { art: 'abbruch', geste: 'drehen', dauerMs: 1400, grund: 'hand verloren', werte: { faust: 1.1, luecke: 400, punkte: [[0, 0]], text: 'x' }, punkte: [[1, 2]], bild: 'data:' };
  assert.deepEqual(abbruchEintrag(e), { geste: 'drehen', dauerMs: 1400, grund: 'hand verloren', werte: { faust: 1.1, luecke: 400 } });
  assert.equal(abbruchEintrag({ art: 'schritt', geste: 'G2' }), null);
  assert.equal(abbruchEintrag({ art: 'punkte', punkte: [] }), null);
});

test('gestenProtokollEinrichten: sendet je Abbruch einmal, Fehler beim Senden bleiben still', async () => {
  const hoerer = new Set();
  const steuerung = { abonnieren: (fn) => { hoerer.add(fn); return () => hoerer.delete(fn); } };
  const gesendet = [];
  const ab = gestenProtokollEinrichten(steuerung, async (k) => { gesendet.push(k); throw new Error('Server weg'); });
  for (const fn of hoerer) { fn({ art: 'zustand', zustand: 'hand' }); fn({ art: 'abbruch', geste: 'rollen', dauerMs: 900, grund: 'hand öffnet sich', werte: {} }); }
  await new Promise((r) => setTimeout(r, 10));
  assert.equal(gesendet.length, 1);
  assert.equal(gesendet[0].grund, 'hand öffnet sich');
  ab();
  assert.equal(hoerer.size, 0);
});
