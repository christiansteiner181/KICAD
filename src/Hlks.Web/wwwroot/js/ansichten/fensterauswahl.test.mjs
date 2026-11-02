// Tests des Auswahlfensters (fensterauswahl.js). Aufruf: node --test src/Hlks.Web/wwwroot/js/ansichten/fensterauswahl.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { gewaehlt, modus } from './fensterauswahl.js';

const K = (id, l, o, r, u, mehr = {}) => ({ id, kasten: { l, o, r, u }, ...mehr });
const kandidaten = [
  K('ganz', 20, 20, 40, 40),
  K('halb', 80, 20, 120, 40),
  K('raum', 0, 0, 200, 200, { raum: true }),
  // schräge Leitung von (150,0) nach (200,50): ihr Kasten streift das Fenster (0…160, 0…100) bei x 150…160, die Achse liegt bei y = x − 150 → schneidet
  K('schraeg', 150, 0, 200, 50, { linien: [[{ x: 150, y: 0 }, { x: 200, y: 50 }]] }),
  // schräge Leitung von (160,100) nach (200,60): Kasten 160…200 × 60…100 berührt das Fenster nur in der Ecke (160,100), Achse ebenso → gewählt;
  // Leitung von (165,95) nach (200,60): Kasten ab x 165 → nicht berührt
  K('aussen', 165, 60, 200, 95, { linien: [[{ x: 165, y: 95 }, { x: 200, y: 60 }]] }),
];

test('Fenster (links → rechts): nur was ganz drin liegt, Räume nur ganz drin', () => {
  assert.equal(modus(0, 160), 'fenster');
  assert.deepEqual(gewaehlt(kandidaten, { x: 0, y: 0 }, { x: 100, y: 100 }), ['ganz']);
  assert.deepEqual(gewaehlt(kandidaten, { x: -1, y: -1 }, { x: 201, y: 201 }), ['ganz', 'halb', 'raum', 'schraeg', 'aussen']);
});

test('Kreuzen (rechts → links): alles Berührte, linienförmige nach ihrer Achse, ohne Räume', () => {
  assert.equal(modus(160, 0), 'kreuzen');
  assert.deepEqual(gewaehlt(kandidaten, { x: 160, y: 100 }, { x: 0, y: 0 }), ['ganz', 'halb', 'schraeg']);
  // Achse verfehlt, obwohl der Kasten überlappt: Leitung (100,0)–(200,100), Fenster 160…200 × 0…30 liegt über der Achse
  const diag = [K('diag', 100, 0, 200, 100, { linien: [[{ x: 100, y: 0 }, { x: 200, y: 100 }]] })];
  assert.deepEqual(gewaehlt(diag, { x: 200, y: 0 }, { x: 160, y: 30 }), []);
  assert.deepEqual(gewaehlt(diag, { x: 200, y: 50 }, { x: 160, y: 80 }), ['diag']);
});
