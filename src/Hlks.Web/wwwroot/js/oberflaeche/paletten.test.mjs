// Tests der Palettenanordnung (Auftrag W1) ohne Browser: Anordnung als Daten (Vorgabe, andocken, lösen, Reiter, ein- und ausblenden, zurücksetzen, merken und laden,
// ungültige gemerkte Werte), Andockziele beim Ziehen und Verdrahtung der Verwaltung.
// Aufruf:  node --test src/Hlks.Web/wwwroot/js/oberflaeche/paletten.test.mjs

import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import * as D from './paletten-daten.js';
import { zonenBerechnen, zielAn, schwebendLage } from './paletten-ziele.js';

const lies = (n) => readFileSync(new URL(n, import.meta.url), 'utf8');

// Die drei Paletten von heute: Eigenschaften und Projektbrowser links, Koordination unten (nur auf Verlangen, flüchtig)
const REG = [
  { id: 'eigenschaften', ort: 'links' },
  { id: 'projektbrowser', ort: 'links' },
  { id: 'koordination', ort: 'unten', vorgabeSichtbar: false, fluechtig: true },
];
const ids = (l, b) => l.bereiche[b].gruppen.map((g) => g.paletten.join('+'));
const alle = (l) => [...D.sichtbareIds(l), ...l.versteckt].sort();
const GEOM = { x: 400, y: 200, b: 300, h: 360 };

// ------------------------------------------------------------------------------------------------ Vorgabe

test('Vorgabe = heutige Anordnung: Eigenschaften oben, Projektbrowser unten, links; Koordination versteckt', () => {
  const l = D.vorgabe(REG);
  assert.deepEqual(ids(l, 'links'), ['eigenschaften', 'projektbrowser']);
  assert.deepEqual(ids(l, 'rechts'), []);
  assert.deepEqual(ids(l, 'unten'), []);
  assert.deepEqual(l.schwebend, []);
  assert.deepEqual(l.versteckt, ['koordination']);
  assert.equal(l.version, 1);
  assert.equal(l.bereiche.links.groesse, null);
  const [a, b] = l.bereiche.links.gruppen;
  assert.equal(a.anteil, b.anteil, 'beide gleich hoch wie heute (je die Hälfte)');
  assert.deepEqual(alle(l), ['eigenschaften', 'koordination', 'projektbrowser']);
});

test('Vorgabe: jede Palette steht genau einmal da; Orte, Sichtbarkeit und Anteil kommen aus der Anmeldung', () => {
  const reg = [{ id: 'a', ort: 'rechts', anteil: 2 }, { id: 'b', ort: 'unten' }, { id: 'c', ort: 'ungueltig' }, { id: 'd', vorgabeSichtbar: false }];
  const l = D.vorgabe(reg);
  assert.deepEqual(ids(l, 'rechts'), ['a']);
  assert.equal(l.bereiche.rechts.gruppen[0].anteil, 2);
  assert.deepEqual(ids(l, 'unten'), ['b']);
  assert.deepEqual(ids(l, 'links'), ['c'], 'ein unbekannter Ort fällt auf links zurück');
  assert.deepEqual(l.versteckt, ['d']);
});

test('ort und sichtbar', () => {
  const l = D.vorgabe(REG);
  assert.deepEqual(D.ort(l, 'projektbrowser'), { art: 'bereich', bereich: 'links', gruppe: 1 });
  assert.deepEqual(D.ort(l, 'koordination'), { art: 'versteckt' });
  assert.equal(D.ort(l, 'unbekannt'), null);
  assert.ok(D.sichtbar(l, 'eigenschaften'));
  assert.ok(!D.sichtbar(l, 'koordination'));
  assert.ok(!D.sichtbar(l, 'unbekannt'));
});

// ------------------------------------------------------------------------------------------------ Andocken

test('Projektbrowser nach rechts andocken: eigene Gruppe im rechten Bereich, links bleibt nur die Eigenschaften', () => {
  const v = D.vorgabe(REG);
  const l = D.andocken(v, ['projektbrowser'], { bereich: 'rechts', index: 0 }, REG);
  assert.deepEqual(ids(l, 'links'), ['eigenschaften']);
  assert.deepEqual(ids(l, 'rechts'), ['projektbrowser']);
  assert.deepEqual(ids(v, 'links'), ['eigenschaften', 'projektbrowser'], 'die alte Anordnung bleibt unverändert');
  assert.deepEqual(alle(l), alle(v));
});

test('andocken davor/danach: untereinander an der gewählten Stelle, die Höhe der Zielgruppe wird geteilt', () => {
  const v = D.vorgabe([...REG, { id: 'ki', ort: 'rechts' }]);
  const davor = D.andocken(v, ['ki'], { palette: 'eigenschaften', lage: 'davor' }, REG.concat([{ id: 'ki' }]));
  assert.deepEqual(ids(davor, 'links'), ['ki', 'eigenschaften', 'projektbrowser']);
  const [k, e, p] = davor.bereiche.links.gruppen;
  assert.equal(k.anteil, e.anteil, 'die geteilte Gruppe und die neue sind gleich gross');
  assert.equal(k.anteil + e.anteil, v.bereiche.links.gruppen[0].anteil, 'zusammen so gross wie die Zielgruppe vorher');
  assert.equal(p.anteil, v.bereiche.links.gruppen[1].anteil, 'die übrigen bleiben');
  const danach = D.andocken(v, ['ki'], { palette: 'eigenschaften', lage: 'danach' });
  assert.deepEqual(ids(danach, 'links'), ['eigenschaften', 'ki', 'projektbrowser']);
});

test('andocken an eine Stelle (bereich, index): vorn, in der Mitte, am Ende; ein zu grosser Index heisst Ende', () => {
  let l = D.vorgabe(REG);
  l = D.andocken(l, ['koordination'], { bereich: 'links', index: 1 });
  assert.deepEqual(ids(l, 'links'), ['eigenschaften', 'koordination', 'projektbrowser']);
  l = D.andocken(l, ['eigenschaften'], { bereich: 'links', index: 99 });
  assert.deepEqual(ids(l, 'links'), ['koordination', 'projektbrowser', 'eigenschaften']);
  l = D.andocken(l, ['eigenschaften'], { bereich: 'links', index: 0 });
  assert.deepEqual(ids(l, 'links'), ['eigenschaften', 'koordination', 'projektbrowser']);
});

test('Umordnen im selben Bereich: der Index meint die Stelle vor dem Verschieben', () => {
  let l = D.vorgabe([...REG, { id: 'drei', ort: 'links' }]);
  assert.deepEqual(ids(l, 'links'), ['eigenschaften', 'projektbrowser', 'drei']);
  l = D.andocken(l, ['eigenschaften'], { bereich: 'links', index: 3 });   // ans Ende
  assert.deepEqual(ids(l, 'links'), ['projektbrowser', 'drei', 'eigenschaften']);
  l = D.andocken(l, ['eigenschaften'], { bereich: 'links', index: 1 });   // vor «drei»
  assert.deepEqual(ids(l, 'links'), ['projektbrowser', 'eigenschaften', 'drei']);
});

// ------------------------------------------------------------------------------------------------ Reiter

test('Eigenschaften und Projektbrowser als Reiter zusammenlegen: eine Gruppe, die abgelegte Palette ist aktiv', () => {
  const v = D.vorgabe(REG);
  const l = D.andocken(v, ['eigenschaften'], { palette: 'projektbrowser', lage: 'reiter' });
  assert.deepEqual(ids(l, 'links'), ['projektbrowser+eigenschaften']);
  assert.equal(l.bereiche.links.gruppen[0].aktiv, 'eigenschaften');
  assert.equal(D.sichtbareIds(l).length, 2);
});

test('Reiter wechseln und wieder aus der Gruppe lösen', () => {
  let l = D.andocken(D.vorgabe(REG), ['eigenschaften'], { palette: 'projektbrowser', lage: 'reiter' });
  l = D.aktivSetzen(l, 'projektbrowser');
  assert.equal(l.bereiche.links.gruppen[0].aktiv, 'projektbrowser');
  // einen Reiter herausziehen und rechts andocken
  const r = D.andocken(l, ['projektbrowser'], { bereich: 'rechts', index: 0 });
  assert.deepEqual(ids(r, 'links'), ['eigenschaften']);
  assert.equal(r.bereiche.links.gruppen[0].aktiv, 'eigenschaften', 'die Gruppe nimmt eine vorhandene Palette als aktive');
  assert.deepEqual(ids(r, 'rechts'), ['projektbrowser']);
  // oder schweben lassen
  const s = D.loesen(l, ['eigenschaften'], GEOM);
  assert.deepEqual(ids(s, 'links'), ['projektbrowser']);
  assert.equal(s.schwebend.length, 1);
});

test('Ganze Reitergruppe ziehen: alle Reiter wandern zusammen', () => {
  const l = D.andocken(D.vorgabe(REG), ['eigenschaften'], { palette: 'projektbrowser', lage: 'reiter' });
  const r = D.andocken(l, ['projektbrowser', 'eigenschaften'], { bereich: 'rechts', index: 0 });
  assert.deepEqual(ids(r, 'links'), []);
  assert.deepEqual(ids(r, 'rechts'), ['projektbrowser+eigenschaften']);
});

test('Reiter an eine schwebende Gruppe', () => {
  let l = D.loesen(D.vorgabe(REG), ['eigenschaften'], GEOM);
  l = D.andocken(l, ['projektbrowser'], { palette: 'eigenschaften', lage: 'reiter' });
  assert.deepEqual(ids(l, 'links'), []);
  assert.equal(l.schwebend.length, 1);
  assert.deepEqual(l.schwebend[0].paletten, ['eigenschaften', 'projektbrowser']);
  assert.equal(l.schwebend[0].aktiv, 'projektbrowser');
});

// ------------------------------------------------------------------------------------------------ Lösen (schweben)

test('Eigenschaften schweben lassen: eigene Gruppe mit Lage und Grösse, links bleibt der Projektbrowser', () => {
  const l = D.loesen(D.vorgabe(REG), ['eigenschaften'], { x: 400.4, y: 200.6, b: 300, h: 360 }, REG);
  assert.deepEqual(ids(l, 'links'), ['projektbrowser']);
  assert.deepEqual(l.schwebend, [{ paletten: ['eigenschaften'], aktiv: 'eigenschaften', anteil: 1, x: 400, y: 201, b: 300, h: 360 }]);
  assert.ok(D.sichtbar(l, 'eigenschaften'));
  assert.deepEqual(D.ort(l, 'eigenschaften'), { art: 'schwebend', gruppe: 0 });
});

test('Schwebende Palette: Lage und Grösse ändern, nach vorn holen, wieder andocken', () => {
  let l = D.loesen(D.vorgabe(REG), ['eigenschaften'], GEOM);
  l = D.loesen(l, ['projektbrowser'], { x: 10, y: 10, b: 200, h: 200 });
  assert.deepEqual(l.schwebend.map((g) => g.paletten[0]), ['eigenschaften', 'projektbrowser']);
  l = D.nachVorne(l, 'eigenschaften');
  assert.deepEqual(l.schwebend.map((g) => g.paletten[0]), ['projektbrowser', 'eigenschaften'], 'zuletzt = oben');
  l = D.schwebendSetzen(l, 'eigenschaften', { x: 50, y: 60, b: 320, h: 400 });
  assert.deepEqual(l.schwebend[1], { paletten: ['eigenschaften'], aktiv: 'eigenschaften', anteil: 1, x: 50, y: 60, b: 320, h: 400 });
  l = D.andocken(l, ['eigenschaften'], { bereich: 'rechts', index: 0 });
  assert.deepEqual(ids(l, 'rechts'), ['eigenschaften']);
  assert.equal(l.schwebend.length, 1);
});

test('schwebendSetzen und loesen verwerfen ungültige Geometrie, ohne etwas zu ändern', () => {
  const v = D.vorgabe(REG);
  assert.equal(D.loesen(v, ['eigenschaften'], { x: NaN, y: 0, b: 100, h: 100 }), v);
  assert.equal(D.loesen(v, ['eigenschaften'], null), v);
  const s = D.loesen(v, ['eigenschaften'], GEOM);
  assert.equal(D.schwebendSetzen(s, 'eigenschaften', { x: 'a' }), s);
  assert.equal(D.schwebendSetzen(v, 'eigenschaften', GEOM), v, 'angedockte Paletten haben keine Schwebelage');
  assert.equal(D.loesen(v, ['gibtsnicht'], GEOM), v);
});

test('Sehr kleine Geometrie wird auf die Mindestgrösse gebracht', () => {
  const l = D.loesen(D.vorgabe(REG), ['eigenschaften'], { x: 5, y: 5, b: 3, h: 4 });
  assert.equal(l.schwebend[0].b, D.MIN_FENSTER);
  assert.equal(l.schwebend[0].h, D.MIN_FENSTER);
});

// ------------------------------------------------------------------------------------------------ Ein- und Ausblenden

test('verbergen und zeigen: die Palette kommt an den gemerkten Ort zurück', () => {
  let l = D.andocken(D.vorgabe(REG), ['projektbrowser'], { bereich: 'rechts', index: 0 });
  l = D.verbergen(l, 'projektbrowser');
  assert.ok(!D.sichtbar(l, 'projektbrowser'));
  assert.deepEqual(l.versteckt.sort(), ['koordination', 'projektbrowser']);
  assert.deepEqual(l.zuletzt.projektbrowser, { bereich: 'rechts' });
  l = D.zeigen(l, 'projektbrowser', REG);
  assert.deepEqual(ids(l, 'rechts'), ['projektbrowser']);
  assert.ok(!l.versteckt.includes('projektbrowser'));
});

test('verbergen und zeigen einer schwebenden Palette: gleiche Lage und Grösse', () => {
  let l = D.loesen(D.vorgabe(REG), ['eigenschaften'], GEOM);
  l = D.verbergen(l, 'eigenschaften');
  assert.equal(l.schwebend.length, 0);
  l = D.zeigen(l, 'eigenschaften', REG);
  assert.deepEqual(l.schwebend.map((g) => ({ x: g.x, y: g.y, b: g.b, h: g.h })), [GEOM]);
});

test('zeigen ohne gemerkten Ort: Vorgabeort der Anmeldung; schon sichtbar: nur aktiv machen', () => {
  let l = D.zeigen(D.vorgabe(REG), 'koordination', REG);
  assert.deepEqual(ids(l, 'unten'), ['koordination']);
  l = D.andocken(l, ['koordination'], { palette: 'projektbrowser', lage: 'reiter' });
  l = D.aktivSetzen(l, 'projektbrowser');
  const l2 = D.zeigen(l, 'koordination', REG);
  assert.equal(l2.bereiche.links.gruppen[1]?.aktiv ?? l2.bereiche.links.gruppen[0].aktiv, 'koordination', 'eine verdeckte Reiter-Palette wird nach vorn geholt');
  assert.equal(D.zeigen(D.vorgabe(REG), 'unbekannt', REG).versteckt.length, 1, 'unbekannte Paletten ändern nichts');
});

test('verbergen: das letzte Mitglied einer Gruppe lässt keine leere Gruppe zurück', () => {
  let l = D.verbergen(D.vorgabe(REG), 'projektbrowser');
  l = D.verbergen(l, 'eigenschaften');
  assert.deepEqual(ids(l, 'links'), []);
  assert.deepEqual(alle(l), ['eigenschaften', 'koordination', 'projektbrowser']);
});

// ------------------------------------------------------------------------------------------------ Zurücksetzen

test('Paletten zurücksetzen stellt die Vorgabe wieder her', () => {
  let l = D.andocken(D.vorgabe(REG), ['projektbrowser'], { bereich: 'rechts', index: 0 });
  l = D.loesen(l, ['eigenschaften'], GEOM);
  l = D.groesseSetzen(l, 'rechts', 420);
  l = D.verbergen(l, 'projektbrowser');
  assert.deepEqual(D.zuruecksetzen(l, REG), D.vorgabe(REG));
});

test('Paletten zurücksetzen: eine geöffnete flüchtige Palette bleibt offen (an ihrem Vorgabeort), eine geschlossene bleibt zu', () => {
  let l = D.zeigen(D.vorgabe(REG), 'koordination', REG);
  l = D.andocken(l, ['koordination'], { bereich: 'rechts', index: 0 });
  const r = D.zuruecksetzen(l, REG);
  assert.deepEqual(ids(r, 'unten'), ['koordination']);
  assert.deepEqual(ids(r, 'links'), ['eigenschaften', 'projektbrowser']);
  const zu = D.zuruecksetzen(D.vorgabe(REG), REG);
  assert.ok(!D.sichtbar(zu, 'koordination'));
});

// ------------------------------------------------------------------------------------------------ Grössen

test('Breite und Höhe der Bereiche: gerundet, begrenzt, null = Vorgabe', () => {
  let l = D.vorgabe(REG);
  assert.equal(D.groesseSetzen(l, 'rechts', 333.4).bereiche.rechts.groesse, 333);
  assert.equal(D.groesseSetzen(l, 'links', 5).bereiche.links.groesse, D.MIN_GROESSE);
  assert.equal(D.groesseSetzen(l, 'links', 1e9).bereiche.links.groesse, D.MAX_GROESSE);
  assert.equal(D.groesseSetzen(l, 'links', NaN), l);
  assert.equal(D.groesseSetzen(l, 'oben', 300), l);
  l = D.groesseSetzen(l, 'unten', 250);
  assert.equal(D.groesseSetzen(l, 'unten', null).bereiche.unten.groesse, null);
});

test('Trenner zwischen zwei Gruppen verschieben: die Summe der beiden bleibt, die übrigen bleiben', () => {
  const v = D.andocken(D.vorgabe(REG), ['koordination'], { bereich: 'links', index: 2 });
  const l = D.trennerSetzen(v, 'links', 0, 0.25);
  const [a, b, c] = l.bereiche.links.gruppen;
  const [a0, b0, c0] = v.bereiche.links.gruppen;
  assert.ok(Math.abs(a.anteil / (a.anteil + b.anteil) - 0.25) < 1e-9);
  assert.ok(Math.abs(a.anteil + b.anteil - (a0.anteil + b0.anteil)) < 1e-9);
  assert.equal(c.anteil, c0.anteil);
  const rand = D.trennerSetzen(v, 'links', 0, 0.0);
  assert.ok(Math.abs(rand.bereiche.links.gruppen[0].anteil / (a0.anteil + b0.anteil) - 0.1) < 1e-9, 'nie ganz zu: mindestens ein Zehntel');
  assert.equal(D.trennerSetzen(v, 'links', 5, 0.5), v);
  assert.equal(D.trennerSetzen(v, 'links', 0, NaN), v);
});

// ------------------------------------------------------------------------------------------------ Merken und Laden

test('Merken und Laden: die Anordnung übersteht JSON unverändert', () => {
  let l = D.andocken(D.vorgabe(REG), ['projektbrowser'], { bereich: 'rechts', index: 0 });
  l = D.loesen(l, ['eigenschaften'], GEOM);
  l = D.zeigen(l, 'koordination', REG);
  l = D.groesseSetzen(l, 'rechts', 320);
  l = D.verbergen(l, 'koordination');
  const geladen = D.normalisieren(JSON.parse(JSON.stringify(l)), REG);
  assert.deepEqual(geladen, l);
});

test('Laden: nichts, kein Objekt oder falsche Version ergibt die Vorgabe', () => {
  for (const roh of [undefined, null, 'x', 5, [], {}, { version: 2 }, { version: 'eins' }]) {
    assert.deepEqual(D.normalisieren(roh, REG), D.vorgabe(REG), JSON.stringify(roh));
  }
});

test('Laden: ungültige gemerkte Werte werden verworfen, der Rest bleibt', () => {
  const roh = {
    version: 1,
    bereiche: {
      links: { groesse: 'breit', gruppen: [
        { paletten: ['eigenschaften', 'gibtsnicht', 5, null], aktiv: 'gibtsnicht', anteil: -3 },
        { paletten: [], aktiv: 'x' },
        'kaputt',
        null,
      ] },
      rechts: { groesse: 50, gruppen: [{ paletten: ['projektbrowser', 'eigenschaften'], aktiv: 'projektbrowser', anteil: 'viel' }] },
      unten: 'nichts',
      oben: { gruppen: [{ paletten: ['koordination'] }] },
    },
    schwebend: [{ paletten: ['koordination'], aktiv: 'koordination', x: 'a', y: 1, b: 2, h: 3 }, 'x', { paletten: 'abc' }],
    versteckt: ['eigenschaften', 'unbekannt', 7],
    zuletzt: { koordination: { bereich: 'mitte' }, projektbrowser: { schwebend: { x: 1 } }, eigenschaften: 'x', fremd: { bereich: 'links' } },
  };
  const l = D.normalisieren(roh, REG);
  // jede angemeldete Palette steht genau einmal da
  assert.deepEqual(alle(l), ['eigenschaften', 'koordination', 'projektbrowser']);
  assert.equal(D.sichtbareIds(l).length + l.versteckt.length, 3);
  assert.deepEqual(ids(l, 'links'), ['eigenschaften']);
  assert.equal(l.bereiche.links.groesse, null, 'ungültige Breite -> Vorgabe');
  assert.equal(l.bereiche.links.gruppen[0].aktiv, 'eigenschaften');
  assert.equal(l.bereiche.links.gruppen[0].anteil, 1, 'negativer Anteil -> 1');
  assert.deepEqual(ids(l, 'rechts'), ['projektbrowser'], 'eine doppelt genannte Palette bleibt am ersten Ort');
  assert.equal(l.bereiche.rechts.groesse, null, 'zu kleine Breite -> Vorgabe');
  assert.equal(l.bereiche.rechts.gruppen[0].anteil, 1);
  assert.deepEqual(l.schwebend, [], 'ohne gültige Lage schwebt nichts');
  assert.ok(D.sichtbar(l, 'koordination') || l.versteckt.includes('koordination'));
  assert.deepEqual(l.zuletzt, {}, 'ungültige Merkwerte fallen weg');
  assert.deepEqual(JSON.parse(JSON.stringify(l)), l);
});

test('Laden: eine verschwundene Palette fällt weg, eine neu angemeldete kommt an ihren Vorgabeort', () => {
  const alt = D.vorgabe([{ id: 'eigenschaften', ort: 'links' }, { id: 'weg', ort: 'links' }]);
  const l = D.normalisieren(alt, [...REG]);
  assert.ok(!alle(l).includes('weg'));
  assert.deepEqual(ids(l, 'links'), ['eigenschaften', 'projektbrowser']);
  assert.deepEqual(l.versteckt, ['koordination']);
  const mitKi = D.normalisieren(l, [...REG, { id: 'ki', ort: 'rechts' }]);
  assert.deepEqual(ids(mitKi, 'rechts'), ['ki']);
});

test('Laden: flüchtige Paletten werden nicht sichtbar wiederhergestellt, behalten aber ihren Ort', () => {
  let l = D.zeigen(D.vorgabe(REG), 'koordination', REG);
  l = D.andocken(l, ['koordination'], { bereich: 'rechts', index: 0 });
  const geladen = D.normalisieren(JSON.parse(JSON.stringify(l)), REG, { fluechtigAusblenden: true });
  assert.ok(!D.sichtbar(geladen, 'koordination'));
  assert.deepEqual(geladen.zuletzt.koordination, { bereich: 'rechts' });
  const wieder = D.zeigen(geladen, 'koordination', REG);
  assert.deepEqual(ids(wieder, 'rechts'), ['koordination']);
});

test('Laden: unvernünftige Grössen schwebender Paletten werden auf das Mindestmass gebracht, Zahlen gerundet', () => {
  const roh = { version: 1, bereiche: {}, schwebend: [{ paletten: ['eigenschaften'], aktiv: 'eigenschaften', x: 10.7, y: -20.2, b: 5, h: 1000.4 }], versteckt: ['koordination'] };
  const l = D.normalisieren(roh, REG);
  assert.deepEqual(l.schwebend[0], { paletten: ['eigenschaften'], aktiv: 'eigenschaften', anteil: 1, x: 11, y: -20, b: D.MIN_FENSTER, h: 1000 });
  assert.deepEqual(ids(l, 'links'), ['projektbrowser']);
});

test('Ungültige Wünsche geben die Anordnung selbst zurück (nichts wird verändert)', () => {
  const l = D.andocken(D.vorgabe(REG), ['projektbrowser'], { bereich: 'rechts', index: 0 });
  const reg = REG;
  assert.equal(D.andocken(l, ['gibtsnicht'], { bereich: 'links', index: 0 }, reg), l);
  assert.equal(D.andocken(l, [], { bereich: 'links', index: 0 }), l);
  assert.equal(D.andocken(l, ['eigenschaften'], null), l);
  assert.equal(D.andocken(l, ['eigenschaften'], { bereich: 'oben', index: 0 }), l);
  assert.equal(D.andocken(l, ['eigenschaften'], { palette: 'gibtsnicht', lage: 'reiter' }), l);
  assert.equal(D.andocken(l, ['eigenschaften'], { palette: 'projektbrowser', lage: 'quer' }), l);
  assert.equal(D.andocken(l, ['eigenschaften'], { palette: 'eigenschaften', lage: 'davor' }), l, 'auf die eigene Gruppe');
  assert.equal(D.andocken(l, ['eigenschaften'], { palette: 'eigenschaften', lage: 'reiter' }), l);
  const s = D.loesen(l, ['eigenschaften'], GEOM);
  assert.equal(D.andocken(s, ['projektbrowser'], { palette: 'eigenschaften', lage: 'davor' }), s, 'davor/danach gibt es an schwebenden Gruppen nicht');
  assert.equal(D.verbergen(l, 'koordination'), l, 'ein schon verstecktes bleibt');
  assert.equal(D.aktivSetzen(l, 'gibtsnicht'), l);
});

test('Eine Palette aus ihrer Reitergruppe auf die eigene Gruppe davor/danach lösen teilt die Gruppe', () => {
  const l = D.andocken(D.vorgabe(REG), ['eigenschaften'], { palette: 'projektbrowser', lage: 'reiter' });
  const geteilt = D.andocken(l, ['eigenschaften'], { palette: 'projektbrowser', lage: 'davor' });
  assert.deepEqual(ids(geteilt, 'links'), ['eigenschaften', 'projektbrowser']);
});

// ------------------------------------------------------------------------------------------------ Andockziele beim Ziehen

const G = (palette, rect, extra = {}) => ({ palette, ids: [palette], schwebend: false, bereich: 'links', richtung: 'spalte', rect, ...extra });

test('Zonen: jede angedockte Gruppe hat davor, danach und Reiter; die Zone unter dem Zeiger entscheidet', () => {
  const gruppen = [G('eigenschaften', { x: 0, y: 0, b: 280, h: 400 }), G('projektbrowser', { x: 0, y: 404, b: 280, h: 400 })];
  const zonen = zonenBerechnen({ gruppen, plaetze: {}, ziehend: [] });
  assert.equal(zonen.length, 6);
  assert.deepEqual(zielAn(zonen, 100, 10).ziel, { palette: 'eigenschaften', lage: 'davor' });
  assert.deepEqual(zielAn(zonen, 100, 200).ziel, { palette: 'eigenschaften', lage: 'reiter' });
  assert.deepEqual(zielAn(zonen, 100, 395).ziel, { palette: 'eigenschaften', lage: 'danach' });
  assert.deepEqual(zielAn(zonen, 100, 700).ziel, { palette: 'projektbrowser', lage: 'reiter' });
  assert.equal(zielAn(zonen, 900, 200), null, 'ausserhalb jeder Zone: die Palette wird schwebend');
  assert.equal(zielAn(zonen, 100, 402), null, 'der Trenner zwischen den Gruppen gehört zu keiner Zone');
});

test('Zonen: die gezogene Palette hat keine Zonen an der eigenen Gruppe; ihre Gruppenkameraden schon', () => {
  const eigen = { palette: 'a', ids: ['a', 'b'], schwebend: false, bereich: 'links', richtung: 'spalte', rect: { x: 0, y: 0, b: 280, h: 400 } };
  assert.equal(zonenBerechnen({ gruppen: [eigen], ziehend: ['a', 'b'] }).length, 0);
  assert.equal(zonenBerechnen({ gruppen: [eigen], ziehend: ['a'] }).length, 3, 'nur ein Reiter gezogen: die Gruppe bleibt Ziel (zum Teilen)');
});

test('Zonen: in einem Bereich unten stehen die Gruppen nebeneinander (links davor, rechts danach)', () => {
  const g = G('koordination', { x: 0, y: 700, b: 1000, h: 200 }, { bereich: 'unten', richtung: 'zeile' });
  const zonen = zonenBerechnen({ gruppen: [g] });
  assert.deepEqual(zielAn(zonen, 5, 800).ziel, { palette: 'koordination', lage: 'davor' });
  assert.deepEqual(zielAn(zonen, 995, 800).ziel, { palette: 'koordination', lage: 'danach' });
  assert.deepEqual(zielAn(zonen, 500, 800).ziel, { palette: 'koordination', lage: 'reiter' });
});

test('Zonen: schwebende Gruppen nehmen nur Reiter an und liegen vor den angedockten (Vorrang)', () => {
  const gruppen = [G('a', { x: 0, y: 0, b: 280, h: 400 }), G('f', { x: 100, y: 100, b: 200, h: 200 }, { schwebend: true, bereich: null })];
  const zonen = zonenBerechnen({ gruppen });
  assert.deepEqual(zielAn(zonen, 150, 150).ziel, { palette: 'f', lage: 'reiter' }, 'über der angedockten Gruppe gewinnt die schwebende');
  assert.deepEqual(zielAn(zonen, 20, 20).ziel, { palette: 'a', lage: 'davor' });
  const oben = zonenBerechnen({ gruppen: [G('f1', { x: 0, y: 0, b: 100, h: 100 }, { schwebend: true }), G('f2', { x: 0, y: 0, b: 100, h: 100 }, { schwebend: true })] });
  assert.equal(zielAn(oben, 50, 50).ziel.palette, 'f2', 'von mehreren schwebenden liegt die letzte oben');
});

test('Zonen: ein leerer Bereich bietet eine Fläche zum Andocken an', () => {
  const plaetze = { rechts: { x: 1450, y: 100, b: 150, h: 800 }, unten: { x: 280, y: 780, b: 1000, h: 120 } };
  const zonen = zonenBerechnen({ gruppen: [], plaetze });
  assert.deepEqual(zielAn(zonen, 1500, 400).ziel, { bereich: 'rechts', index: 0 });
  assert.deepEqual(zielAn(zonen, 600, 850).ziel, { bereich: 'unten', index: 0 });
  assert.equal(zielAn(zonen, 600, 400), null);
  assert.ok(zonen.every((z) => z.text.length > 0));
});

test('Zonen: sehr kleine Gruppen behalten eine Mitte', () => {
  const zonen = zonenBerechnen({ gruppen: [G('a', { x: 0, y: 0, b: 100, h: 30 })] });
  for (const z of zonen) assert.ok(z.rect.h >= 1 && z.rect.b >= 1, JSON.stringify(z));
  assert.equal(zielAn(zonen, 50, 15).ziel.lage, 'reiter');
});

test('Zonen und Daten passen zusammen: jedes Ziel einer Zone ist für andocken gültig', () => {
  const l = D.vorgabe(REG);
  const gruppen = l.bereiche.links.gruppen.map((g, i) => G(g.aktiv, { x: 0, y: i * 404, b: 280, h: 400 }));
  const zonen = zonenBerechnen({ gruppen, plaetze: { rechts: { x: 900, y: 0, b: 150, h: 800 } }, ziehend: ['koordination'] });
  for (const z of zonen) {
    const r = D.andocken(l, ['koordination'], z.ziel, REG);
    assert.notEqual(r, l, JSON.stringify(z.ziel));
    assert.deepEqual(alle(r), alle(l));
  }
});

test('schwebendLage: der Zeiger bleibt auf dem Kopf, das Fenster ganz im sichtbaren Bereich', () => {
  const sicht = { x: 0, y: 0, b: 1600, h: 900 };
  assert.deepEqual(schwebendLage({ x: 800, y: 450, greifX: 40, greifY: 9, b: 300, h: 360, sicht }), { x: 760, y: 441, b: 300, h: 360 });
  assert.deepEqual(schwebendLage({ x: 1590, y: 890, greifX: 40, greifY: 9, b: 300, h: 360, sicht }), { x: 1300, y: 540, b: 300, h: 360 });
  assert.deepEqual(schwebendLage({ x: -50, y: -50, greifX: 0, greifY: 0, b: 300, h: 360, sicht }), { x: 0, y: 0, b: 300, h: 360 });
  assert.deepEqual(schwebendLage({ x: 100, y: 100, b: 3000, h: 2000, sicht }).b, 1600);
  // ein Griffpunkt weit rechts wird in die Fensterbreite zurückgeholt
  assert.equal(schwebendLage({ x: 700, y: 100, greifX: 900, greifY: 5, b: 300, h: 300, sicht }).x, 700 - 276, 'der Griffpunkt bleibt 24 px vor dem rechten Rand des Fensters');
});

// ------------------------------------------------------------------------------------------------ Verdrahtung

test('Verdrahtung: die drei Paletten melden sich bei der Verwaltung an, kein Modul baut sein eigenes Palettenfenster', () => {
  assert.match(lies('./eigenschaften.js'), /anmelden\(\{ id: 'eigenschaften'[^)]*ort: 'links'[^)]*bauen: einrichten \}\)/);
  assert.match(lies('./projektbrowser.js'), /anmelden\(\{ id: 'projektbrowser'[^)]*ort: 'links'[^)]*bauen: einrichten \}\)/);
  const k = lies('./koordination-panel.js');
  assert.match(k, /anmelden\(\{\s*id: 'koordination'[^}]*ort: 'unten'[^}]*vorgabeSichtbar: false[^}]*fluechtig: true/);
  assert.ok(!/ansichtsbereich'\)\.appendChild|bereich\.appendChild\(panel\)/.test(k), 'die Koordination hängt sich nicht mehr selbst an die Ansichtsfläche');
  assert.ok(!/getElementById\('seitenleiste'\)/.test(lies('./aktionen-bearbeiten.js')), 'kein Sonderweg für die Eigenschaften mehr');
  assert.match(lies('../app.js'), /paletten\.einrichten\(\)/);
});

test('Verdrahtung: Register Ansicht › Paletten: ein Knopf in der Gruppe «Fenster», die Liste entsteht beim Öffnen aus den angemeldeten Paletten, dazu «Paletten zurücksetzen»', () => {
  const q = lies('./aktionen-paletten.js');
  assert.match(q, /id: 'ansicht\.paletten'[^}]*register: 'ansicht', gruppe: 'Fenster'/);
  assert.match(q, /liste\(\)\.map\(/, 'je angemeldeter Palette ein Eintrag');
  assert.match(q, /umschalten\(d\.id\)/);
  assert.match(q, /titel: 'Paletten zurücksetzen'/);
  assert.match(lies('./aktionen.js'), /aktionen-paletten\.js/);
  assert.ok(!/ansicht: \[[^\]]*'Paletten'/.test(lies('./menueband.js')), 'keine zusätzliche Gruppe im Register Ansicht: es ist schon voll (menueband.browser.mjs, Stufen bei 1920 px)');
});

test('Verdrahtung: Raster mit Andockbereichen links, unten und rechts, Schicht für schwebende Paletten und Andockziele', () => {
  const html = lies('../../index.html');
  for (const id of ['seitenleiste', 'seitenleiste-griff', 'dock-unten', 'dock-unten-griff', 'dock-rechts', 'dock-rechts-griff', 'palettenschicht', 'palettenziele', 'ansichtsbereich', 'ansichtsflaeche']) {
    assert.match(html, new RegExp(`id="${id}"`), id);
  }
  assert.ok(!/id="palette-eigenschaften"/.test(html), 'die Paletten stehen nicht mehr fest im HTML');
  const css = lies('../../css/fensterrahmen.css');
  assert.match(css, /#arbeitsbereich\s*\{[^}]*grid-template-columns:[^;]*--dock-links[^;]*--dock-rechts/);
  assert.match(css, /\.pal-ziel\.aktiv/);
  assert.match(css, /#palettenschicht\s*\{[^}]*position:\s*fixed/);
});

test('Verdrahtung: die Ansichtsfläche bleibt ein eigener Bereich im Raster (W2 füllt ihn)', () => {
  const css = lies('../../css/fensterrahmen.css');
  assert.match(css, /#ansichtsbereich\s*\{\s*grid-column:\s*3;\s*grid-row:\s*1;\s*\}/);
  assert.ok(!/ansichtsflaeche|ansichtsreiter|\.kachel/.test(css), 'fensterrahmen.css fasst Ansichtsreiter und Kacheln nicht an');
});
