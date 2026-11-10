// Tests zu js/zeichnung/befehle.js: getippte Punkte der Befehlszeile, Ortho, Kürzel und der Ablauf eines Befehls über «io».
import test from 'node:test';
import assert from 'node:assert/strict';
import { punktLesen, ortho, KUERZEL, BEFEHLE } from './befehle.js';

const nah = (a, b) => assert.ok(Math.abs(a[0] - b[0]) < 1e-9 && Math.abs(a[1] - b[1]) < 1e-9, `${a} ≠ ${b}`);

test('punktLesen: absolut, relativ, polar und Abstand in Zeigerrichtung', () => {
  nah(punktLesen('1200,500', [10, 10], [0, 0]), [1200, 500]);
  nah(punktLesen('1200.5;-3', null, null), [1200.5, -3]);
  nah(punktLesen('@100,50', [10, 10], null), [110, 60]);
  nah(punktLesen('@500<90', [10, 10], null), [10, 510]);
  nah(punktLesen('500<180', [10, 10], null), [-490, 10]);
  nah(punktLesen('250', [0, 0], [3, 4]), [150, 200]);          // 250 in Richtung (3, 4)
  assert.equal(punktLesen('@100,50', null, null), null);        // relativ ohne letzten Punkt
  assert.equal(punktLesen('s', [0, 0], [1, 1]), null);          // ein Wort ist kein Punkt
  assert.equal(punktLesen('12,', [0, 0], [1, 1]), null);
});

test('ortho: die grössere Abweichung gewinnt', () => {
  assert.deepEqual(ortho([10, 3], [0, 0]), [10, 0]);
  assert.deepEqual(ortho([2, -9], [0, 0]), [0, -9]);
  assert.deepEqual(ortho([5, 5], null), [5, 5]);
});

test('Kürzel: deutsch und englisch führen zum selben Befehl; jeder Zeichen- und Änderungsbefehl ist erreichbar', () => {
  assert.equal(KUERZEL.l, 'linie'); assert.equal(KUERZEL.pl, 'polylinie'); assert.equal(KUERZEL.re, 'rechteck');
  assert.equal(KUERZEL.k, KUERZEL.c); assert.equal(KUERZEL.b, KUERZEL.a);
  assert.equal(KUERZEL.s, KUERZEL.m); assert.equal(KUERZEL.ko, KUERZEL.co); assert.equal(KUERZEL.dr, KUERZEL.ro);
  assert.equal(KUERZEL['lö'], KUERZEL.e); assert.equal(KUERZEL.z, KUERZEL.u);
  assert.equal(KUERZEL.su, KUERZEL.tr); assert.equal(KUERZEL.de, KUERZEL.ex); assert.equal(KUERZEL.ar, KUERZEL.f); assert.equal(KUERZEL.sp, KUERZEL.mi); assert.equal(KUERZEL.ve, KUERZEL.o);
  for (const name of Object.keys(BEFEHLE)) assert.ok(Object.values(KUERZEL).includes(name), name);
});

test('Befehl Linie: jede Strecke wird einzeln gezeichnet, Eingabetaste beendet', async () => {
  const eingaben = [[0, 0], [100, 0], [100, 50], null];
  const gezeichnet = [];
  await BEFEHLE.linie({ punkt: async () => eingaben.shift(), zeichnen: async o => gezeichnet.push(...o) });
  assert.deepEqual(gezeichnet, [{ art: 'linie', von: [0, 0], bis: [100, 0] }, { art: 'linie', von: [100, 0], bis: [100, 50] }]);
});

test('Befehl Polylinie: «s» schliesst; Befehl Schieben: Versatz aus Basis- und Zielpunkt', async () => {
  const eingaben = [[0, 0], [10, 0], [10, 10], 's'];
  let pl;
  await BEFEHLE.polylinie({ punkt: async () => eingaben.shift(), zeichnen: async o => { pl = o[0]; } });
  assert.deepEqual(pl, { art: 'polylinie', punkte: [[0, 0], [10, 0], [10, 10]], geschlossen: true });

  const punkte = [[5, 5], [25, 0]];
  let aufruf;
  await BEFEHLE.schieben({ auswahl: async () => ['A1', 'B2'], boxen: () => [], punkt: async () => punkte.shift(), aendern: async (...a) => { aufruf = a; } });
  assert.deepEqual(aufruf, [['A1', 'B2'], 'verschieben', { dx: 20, dy: -5 }]);
});

test('Befehl Stutzen: klickt Objekt für Objekt, ein Fehlschlag beendet nicht; Befehl Abrunden: Radius, zwei Linien mit Klickpunkten', async () => {
  const klicks = [{ kennung: 'A', punkt: [5, 0] }, { kennung: 'B', punkt: [9, 9] }, null];
  const aufrufe = [];
  await BEFEHLE.stutzen({ objekt: async () => klicks.shift(), versuchen: async f => { try { await f(); } catch { /* gemeldet */ } },
    aendern: async (...a) => { aufrufe.push(a); if (a[0][0] === 'A') throw new Error('kein Schnitt'); } });
  assert.deepEqual(aufrufe, [[['A'], 'stutzen', { punkt: [5, 0] }], [['B'], 'stutzen', { punkt: [9, 9] }]]);

  const linien = [{ kennung: 'L1', punkt: [50, 0] }, { kennung: 'L2', punkt: [0, 50] }];
  let rund;
  await BEFEHLE.abrunden({ merken: {}, zahl: async () => 10, objekt: async () => linien.shift(), aendern: async (...a) => { rund = a; } });
  assert.deepEqual(rund, [['L1', 'L2'], 'abrunden', { radius: 10, punkte: [[50, 0], [0, 50]] }]);
});

test('Bemassung: waagrecht, wenn die Masslinie über/unter den Punkten liegt, sonst senkrecht', async () => {
  const lauf = async ort => { const p = [[0, 0], [100, 20], ort]; let o; await BEFEHLE.bemassung({ punkt: async () => p.shift(), zeichnen: async z => { o = z[0]; } }); return o.richtung; };
  assert.equal(await lauf([50, -40]), 'waagrecht');
  assert.equal(await lauf([150, 10]), 'senkrecht');
});

test('Kettenmass: jedes weitere Mass beginnt am letzten Punkt und liegt auf derselben Masslinie; Strecken gibt Fenster und Versatz weiter', async () => {
  const p = [[0, 0], [800, 0], [400, 500], [2000, 0], null];
  const masse = [];
  await BEFEHLE.bemkette({ punkt: async () => p.shift(), zeichnen: async o => masse.push(o[0]) });
  assert.deepEqual(masse.map(m => [m.von, m.bis, m.ort, m.richtung]), [[[0, 0], [800, 0], [400, 500], 'waagrecht'], [[800, 0], [2000, 0], [400, 500], 'waagrecht']]);

  const q = [[90, -10], [110, 60], [0, 0], [40, 0]];
  let aufruf;
  await BEFEHLE.strecken({ punkt: async () => q.shift(), imFenster: async () => ['R1'], aendern: async (...a) => { aufruf = a; } });
  assert.deepEqual(aufruf, [['R1'], 'strecken', { fenster: [[90, -10], [110, 60]], dx: 40, dy: 0 }]);
});
